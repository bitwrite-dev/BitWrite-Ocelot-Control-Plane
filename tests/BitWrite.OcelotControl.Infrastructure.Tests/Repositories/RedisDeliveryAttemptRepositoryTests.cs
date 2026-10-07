using System.Text.Json;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Infrastructure.Redis;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Reading the delivery attempts gateways reported.
/// </summary>
/// <remarks>
/// The runtime appends one JSON document per attempt and nothing read them, so the
/// list grew unnoticed. Two of the entries hold failures that were being diagnosed
/// by hand at the time.
///
/// A document that cannot be read is skipped rather than failing the report: one bad
/// entry should cost its own line, not the metrics for every gateway.
/// </remarks>
public class RedisDeliveryAttemptRepositoryTests
{
    private const string GatewayId = "3ba143db-8ee4-40f0-a428-3bbf941f37d2";

    private readonly Mock<IDatabase> _db = new();

    public RedisDeliveryAttemptRepositoryTests()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        Repository = new RedisDeliveryAttemptRepository(mux.Object);
    }

    private IDeliveryAttemptRepository Repository { get; }

    private void Stored(params string[] documents) =>
        _db.Setup(d => d.ListRangeAsync(
                RedisKeyHelper.GatewayActivationResults, It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(documents.Select(document => (RedisValue)document).ToArray());

    private static string Attempt(
        bool success = true,
        string? version = "2",
        string? gatewayId = GatewayId,
        string? timestamp = "2026-10-01T23:39:08.4389277+00:00",
        string? error = null)
    {
        var parts = new List<string> { $"\"GatewayId\":\"{gatewayId}\"" };

        if (version is not null)
            parts.Add($"\"Version\":\"{version}\"");

        parts.Add($"\"Success\":{success.ToString().ToLowerInvariant()}");

        if (error is not null)
            parts.Add($"\"Error\":{JsonSerializer.Serialize(error)}");

        if (timestamp is not null)
            parts.Add($"\"Timestamp\":\"{timestamp}\"");

        return "{" + string.Join(",", parts) + "}";
    }

    [Fact]
    public async Task ReadsWhatAGatewayReported()
    {
        Stored(Attempt());

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].GatewayId.Should().Be(Guid.Parse(GatewayId));
        attempts[0].SnapshotVersion.Should().Be(2);
        attempts[0].Succeeded.Should().BeTrue();
        attempts[0].AttemptedAt.Should().BeCloseTo(
            new DateTimeOffset(2026, 10, 1, 23, 39, 8, TimeSpan.Zero), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task KeepsTheReasonAFailureCarries()
    {
        Stored(Attempt(success: false, error: "GlobalConfiguration is required"));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].Succeeded.Should().BeFalse();
        attempts[0].Error.Should().Be("GlobalConfiguration is required");
    }

    [Fact]
    public async Task NewestFirst()
    {
        // The list is appended to, so the newest entry is last. Read in order, a
        // report would attribute the wrong "last attempt" to each gateway.
        Stored(
            Attempt(timestamp: "2026-10-01T10:00:00.0000000+00:00"),
            Attempt(timestamp: "2026-10-01T23:00:00.0000000+00:00"));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().HaveCount(2);
        attempts[0].AttemptedAt.Should().BeAfter(attempts[1].AttemptedAt);
    }

    [Fact]
    public async Task ReadsTheVersionWhenItIsANumberRatherThanText()
    {
        Stored(Attempt().Replace("\"Version\":\"2\"", "\"Version\":2"));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].SnapshotVersion.Should().Be(2);
    }

    [Fact]
    public async Task SkipsAnEntryItCannotReadRatherThanFailingTheReport()
    {
        Stored(
            Attempt(success: false, error: "first"),
            "{ this is not json",
            "[]",
            Attempt(success: false, error: "second", timestamp: "2026-10-01T11:00:00.0000000+00:00"));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().HaveCount(2);
        attempts.Select(attempt => attempt.Error).Should().Equal("second", "first");
    }

    [Fact]
    public async Task SkipsAnEntryWhoseGatewayIsNotAGuid()
    {
        Stored(Attempt(gatewayId: "not-a-guid"), Attempt());

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].GatewayId.Should().Be(Guid.Parse(GatewayId));
    }

    [Fact]
    public async Task ReadsAFailureThatCarriesNoError()
    {
        Stored(Attempt(success: false));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].Succeeded.Should().BeFalse();
        attempts[0].Error.Should().BeEmpty();
    }

    [Fact]
    public async Task TreatsAnEntryWithNoTimeAsHavingHappenedAtTheEpoch()
    {
        // Not the current time, which would be a lie: an attempt whose time is
        // unreadable did not just happen. The epoch falls outside any sane range, so
        // it is excluded from the report rather than counted against an open window.
        Stored(Attempt(timestamp: null));

        var attempts = await Repository.GetRecentAsync(10);

        attempts.Should().ContainSingle();
        attempts[0].AttemptedAt.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public async Task ReportsNothingWhenNothingHasBeenReported()
    {
        Stored();

        (await Repository.GetRecentAsync(10)).Should().BeEmpty();
    }
}
