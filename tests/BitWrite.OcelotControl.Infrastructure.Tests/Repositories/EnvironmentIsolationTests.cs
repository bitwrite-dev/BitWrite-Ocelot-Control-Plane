using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// The point of the environment in a Redis key: the same id in two environments must
/// be two different rows, and neither may be able to delete the other's.
/// </summary>
/// <remarks>
/// Without the environment segment, writing a route in development and then in
/// production overwrote the first, and a read returned whichever was written last.
/// That is silent — the list is never empty, it is just wrong — so it is worth a test
/// that fails loudly if the segment ever goes missing.
/// <para>
/// The index sets are asserted as well as the rows, and that is the part that goes
/// wrong the second way: rows scoped but a shared index means a delete in development
/// removes a production route from the index and leaves its row unreachable, and a list
/// reads the shared index and answers with a short list that looks like the truth.
/// </para>
/// </remarks>
public class EnvironmentIsolationTests
{
    private readonly RecordingRedis _redis = new();

    private RedisRouteRepository Routes(string environment) =>
        new(_redis.Multiplexer.Object, TestEnvironment.Context(environment));

    private RedisServiceRepository Services(string environment) =>
        new(_redis.Multiplexer.Object, TestEnvironment.Context(environment));

    private RedisSnapshotRepository Snapshots(string environment) =>
        new(_redis.Multiplexer.Object, TestEnvironment.Context(environment));

    private static Route ARoute() =>
        Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/v1/cheques/{id}"),
            ServiceId.From(Guid.NewGuid()),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 9000, "/") });

    private static Service AService()
    {
        var service = Service.Create("cheques", "The cheques service");
        service.AddHost("localhost", 9000);

        return service;
    }

    private static Snapshot AVersionedSnapshot(int version) =>
        Snapshot.Reconstitute(
            """{"global":{"baseUrl":"http://gw:5000"},"routes":[]}""",
            ConfigurationHash.FromString("fcc16f2f1dd6e0e2ff0361df126677c59d567bf85c87fe619fb1c1db0ee3efc1"),
            SnapshotVersion.From(version),
            SnapshotStatus.Published,
            "admin",
            DateTimeOffset.Parse("2026-01-02T03:04:05+00:00"),
            DateTimeOffset.Parse("2026-01-02T08:04:05+00:00"));

    [Theory]
    [InlineData("development", "ocelot:route:development:")]
    [InlineData("production", "ocelot:route:production:")]
    [InlineData("staging", "ocelot:route:staging:")]
    public async Task A_route_is_written_under_the_selected_environment(string environment, string prefix)
    {
        var route = ARoute();

        await Routes(environment).AddAsync(route);

        _redis.Keys.Should().Contain(key => key.StartsWith(prefix));
    }

    [Fact]
    public async Task The_same_route_in_two_environments_is_two_rows()
    {
        var route = ARoute();

        await Routes("development").AddAsync(route);
        await Routes("production").AddAsync(route);

        _redis.Keys.Should().Contain(key => key == $"ocelot:route:development:{route.Id.Value}");
        _redis.Keys.Should().Contain(key => key == $"ocelot:route:production:{route.Id.Value}");
    }

    [Fact]
    public async Task The_route_indexes_are_per_environment()
    {
        await Routes("development").AddAsync(ARoute());

        _redis.Keys.Should().Contain("ocelot:index:routes:development");
        _redis.Keys.Should().NotContain(key => key.StartsWith("ocelot:index:routes:production"));
    }

    [Fact]
    public async Task The_service_route_and_signature_indexes_are_per_environment()
    {
        // Shared, these are the indexes that decide which route ids exist, and a
        // signature written in development would overwrite the production route's.
        var route = ARoute();

        await Routes("development").AddAsync(route);

        _redis.Keys.Should().Contain(key => key.StartsWith($"ocelot:index:service:development:{route.ServiceId.Value}"));
        _redis.Keys.Should().Contain(key => key.StartsWith("ocelot:index:route-signature:development:"));
    }

    [Fact]
    public async Task One_environments_list_does_not_answer_with_the_other_environments_rows()
    {
        await Routes("production").AddAsync(ARoute());

        var inDevelopment = await Routes("development").GetAllAsync();

        // Empty rather than short: a production route id absent from development is
        // genuinely not there, and reporting it as nothing at all is the honest answer.
        inDevelopment.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_route_in_one_environment_leaves_the_other_environments_row()
    {
        var route = ARoute();
        await Routes("development").AddAsync(route);
        await Routes("production").AddAsync(route);

        await Routes("development").DeleteAsync(route.Id);

        _redis.HasKey($"ocelot:route:production:{route.Id.Value}").Should().BeTrue(
            "a delete addresses the environment it was asked about");
        _redis.HasKey($"ocelot:route:development:{route.Id.Value}").Should().BeFalse();

        // And the other environment can still list it, which is the part a row-only
        // assertion would miss.
        (await Routes("production").GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task The_same_service_in_two_environments_is_two_rows()
    {
        var service = AService();

        await Services("development").AddAsync(service);
        await Services("production").AddAsync(service);

        _redis.Keys.Should().Contain(key => key == $"ocelot:service:development:{service.Id.Value}");
        _redis.Keys.Should().Contain(key => key == $"ocelot:service:production:{service.Id.Value}");
        _redis.Keys.Should().Contain("ocelot:index:services:development");
        _redis.Keys.Should().Contain("ocelot:index:services:production");
    }

    [Fact]
    public async Task One_environments_services_do_not_list_in_the_other()
    {
        await Services("production").AddAsync(AService());

        (await Services("development").GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_snapshot_version_in_two_environments_is_two_rows()
    {
        // Version numbers are per environment too: both environments seal their first
        // snapshot as version 1, and one overwriting the other would mean publishing in
        // development changed what production serves.
        var snapshot = AVersionedSnapshot(1);

        await Snapshots("development").AddAsync(snapshot);
        await Snapshots("production").AddAsync(AVersionedSnapshot(1));

        _redis.Keys.Should().Contain(key => key == "ocelot:snapshot:development:1");
        _redis.Keys.Should().Contain(key => key == "ocelot:snapshot:production:1");
        _redis.Keys.Should().Contain("ocelot:index:snapshots:development");
        _redis.Keys.Should().Contain("ocelot:index:snapshots:production");
    }

    [Fact]
    public async Task One_environments_snapshots_do_not_list_in_the_other()
    {
        await Snapshots("development").AddAsync(AVersionedSnapshot(1));

        (await Snapshots("production").GetAllAsync()).Should().BeEmpty();
        (await Snapshots("production").GetLatestAsync()).Should().BeNull();
        (await Snapshots("development").GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public void The_environment_name_is_normalised_so_case_cannot_split_a_key()
    {
        EnvironmentName.From("Production").Value.Should().Be("production");
        EnvironmentName.From("Production").Should().Be(EnvironmentName.From("  production "),
            "\"Production\" and \"production\" are one environment, not two");
    }

    [Fact]
    public void An_environment_name_that_is_not_a_key_segment_is_refused()
    {
        var act = () => EnvironmentName.From("prod:eu");

        act.Should().Throw<Domain.Exceptions.DomainException>()
            .Which.ErrorCode.Should().Be("INVALID_ENVIRONMENT_NAME");
    }
}
