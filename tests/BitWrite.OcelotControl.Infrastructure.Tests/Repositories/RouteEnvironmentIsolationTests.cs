using BitWrite.OcelotControl.Domain.Aggregates.Route;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using BitWrite.OcelotControl.Infrastructure.Tests.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// The point of the environment in a Redis key: the same route id in two
/// environments must be two different rows.
/// </summary>
/// <remarks>
/// Without the environment segment, writing a route in development and then in
/// production overwrote the first, and a read returned whichever was written
/// last. That is silent — the list is never empty, it is just wrong — so it is
/// worth a test that fails loudly if the segment ever goes missing.
/// </remarks>
public class RouteEnvironmentIsolationTests
{
    private readonly Mock<IConnectionMultiplexer> _mux = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly List<string> _writtenKeys = new();

    public RouteEnvironmentIsolationTests()
    {
        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        _db.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, _, _) =>
            {
                _writtenKeys.Add(key.ToString()!);
                return Task.CompletedTask;
            });
        _db.Setup(d => d.SetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue[], CommandFlags>((_, _, _) => Task.FromResult(1L));
        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), When.NotExists, It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (_, _, _, _, _, _) => Task.FromResult(true));
    }

    private RedisRouteRepository RepositoryFor(string environment) =>
        new(_mux.Object, TestEnvironment.Context(environment));

    private static Route ARoute() =>
        Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/v1/cheques/{id}"),
            ServiceId.From(Guid.NewGuid()),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 9000, "/") });

    [Theory]
    [InlineData("development", "ocelot:route:development:")]
    [InlineData("production", "ocelot:route:production:")]
    [InlineData("staging", "ocelot:route:staging:")]
    public async Task Writes_the_key_under_the_selected_environment(string environment, string prefix)
    {
        var route = ARoute();

        await RepositoryFor(environment).AddAsync(route);

        _writtenKeys.Should().ContainSingle()
            .Which.Should().StartWith(prefix);
    }

    [Fact]
    public async Task Same_route_in_two_environments_writes_two_keys()
    {
        var route = ARoute();

        await RepositoryFor("development").AddAsync(route);
        await RepositoryFor("production").AddAsync(route);

        // One row per environment, not one row overwritten by the second write.
        _writtenKeys.Should().HaveCount(2);
        _writtenKeys.Should().OnlyContain(k => k.StartsWith("ocelot:route:"));
        _writtenKeys.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Environment_segment_is_normalised_so_case_cannot_split_a_key()
    {
        await RepositoryFor("Production").AddAsync(ARoute());

        // "Production" and "production" are one environment. If they were not
        // normalised they would be two environments with two sets of routes.
        _writtenKeys.Should().ContainSingle()
            .Which.Should().StartWith("ocelot:route:production:");
    }

    [Fact]
    public void Environment_name_rejects_a_colon_because_it_is_a_key_segment()
    {
        var act = () => TestEnvironment.Of("prod:eu");

        act.Should().Throw<Domain.Exceptions.DomainException>();
    }
}