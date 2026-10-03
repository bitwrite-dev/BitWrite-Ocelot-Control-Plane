using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Runtime;

/// <summary>
/// Aggregating what gateways reported when they applied a configuration.
/// </summary>
/// <remarks>
/// This measures configuration delivery, not request traffic. Nothing in the system
/// records how many requests a gateway served or how long they took, so there is no
/// request rate or latency to report and none is invented here. What does exist is
/// every attempt a gateway made to apply a snapshot, with its outcome and error —
/// 172 of them sat unread in Redis while this was being written.
/// </remarks>
public class GetDeliveryMetricsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private static DeliveryAttempt Attempt(
        string gateway,
        bool success,
        DateTimeOffset at,
        int version = 2,
        string? error = null) =>
        new(Guid.Parse(gateway), version, success, error ?? "", at);

    private static GetDeliveryMetricsQuery Query(int hours = 24) =>
        new(Now.AddHours(-hours), Now, Limit: 1000);

    [Fact]
    public void CountsWhatSucceededAndWhatDidNot()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-5)),
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", false, Now.AddMinutes(-10), error: "boom"),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", true, Now.AddMinutes(-7)),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.TotalAttempts.Should().Be(3);
        result.Successful.Should().Be(2);
        result.Failed.Should().Be(1);
        result.SuccessRate.Should().BeApproximately(2d / 3, 0.0001);
    }

    [Fact]
    public void ReportsNothingRatherThanAFullRateWhenNothingWasAttempted()
    {
        // No attempts is not a 100% success rate. Reporting 1.0 would tell an
        // operator their delivery is healthy when nothing has been asked of it.
        var result = GetDeliveryMetricsQueryHandler.Aggregate([], Query());

        result.TotalAttempts.Should().Be(0);
        result.SuccessRate.Should().BeNull();
    }

    [Fact]
    public void KeepsEachGatewaysOutcomesToItself()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-5)),
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", false, Now.AddMinutes(-10), error: "boom"),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", true, Now.AddMinutes(-7)),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", true, Now.AddMinutes(-8)),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.Gateways.Should().HaveCount(2);

        var first = result.Gateways.Single(g => g.GatewayId.StartsWith("3ba143db"));
        first.Attempts.Should().Be(2);
        first.Successful.Should().Be(1);
        first.Failed.Should().Be(1);
        first.SuccessRate.Should().BeApproximately(0.5, 0.0001);
        // The failure, kept verbatim: it is the only place the reason survives.
        first.RecentErrors.Should().ContainSingle().Which.Should().Be("boom");

        var second = result.Gateways.Single(g => g.GatewayId.StartsWith("e6cc2784"));
        second.Attempts.Should().Be(2);
        second.Failed.Should().Be(0);
        second.RecentErrors.Should().BeEmpty();
    }

    [Fact]
    public void LeavesOutAttemptsFromOutsideTheRange()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-5)),
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", false, Now.AddHours(-30), error: "last week"),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query(hours: 24));

        result.TotalAttempts.Should().Be(1);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public void ReportsTheLastTimeEachGatewayReportedIn()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-30)),
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-5)),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.Gateways.Single().LastAttemptAt.Should().Be(Now.AddMinutes(-5));
    }

    [Fact]
    public void GroupsIdenticalFailuresSoOneFaultIsNotCountedAsMany()
    {
        // The stored history has the same WRONGTYPE failure repeated per gateway.
        // Counting occurrences says how many times it happened; grouping says what
        // is broken, which is the reason anyone reads this.
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", false, Now.AddMinutes(-5), error: "WRONGTYPE"),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", false, Now.AddMinutes(-6), error: "WRONGTYPE"),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", false, Now.AddMinutes(-7), error: "connection reset"),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.Errors.Should().HaveCount(2);
        result.Errors[0].Message.Should().Be("WRONGTYPE");
        result.Errors[0].Occurrences.Should().Be(2);
        result.Errors[0].LastSeenAt.Should().Be(Now.AddMinutes(-5));
    }

    [Fact]
    public void ListsGatewaysWithTheMostRecentAttemptFirst()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-30)),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", true, Now.AddMinutes(-5)),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.Gateways[0].GatewayId.Should().StartWith("e6cc2784");
    }

    [Fact]
    public void NamesTheVersionsAttemptedSoDriftIsVisible()
    {
        DeliveryAttempt[] attempts =
        [
            Attempt("3ba143db-8ee4-40f0-a428-3bbf941f37d2", true, Now.AddMinutes(-9), version: 2),
            Attempt("e6cc2784-7c3d-42d0-8351-2c642db152db", true, Now.AddMinutes(-5), version: 3),
        ];

        var result = GetDeliveryMetricsQueryHandler.Aggregate(attempts, Query());

        result.SnapshotVersions.Should().Equal(3, 2);
    }

    [Fact]
    public void RefusesARangeThatEndsBeforeItStarts()
    {
        var query = new GetDeliveryMetricsQuery(Now, Now.AddHours(-1), Limit: 100);

        var act = () => GetDeliveryMetricsQueryHandler.Aggregate([], query);

        act.Should().Throw<ArgumentException>();
    }
}