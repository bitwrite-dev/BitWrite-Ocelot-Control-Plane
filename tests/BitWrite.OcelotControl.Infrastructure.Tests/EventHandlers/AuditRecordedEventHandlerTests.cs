using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Infrastructure.EventHandlers;
using FluentAssertions;
using Moq;
using Xunit;

using AuditLogAggregate = BitWrite.OcelotControl.Domain.Aggregates.AuditLog.AuditLog;

namespace BitWrite.OcelotControl.Infrastructure.Tests.EventHandlers;

/// <summary>
/// The subscriber that turned an audit event into a stored entry.
/// </summary>
/// <remarks>
/// Nothing handled <c>AuditRecorded</c> until this, so every command dispatched it
/// into the void and the log answered <c>totalCount: 0</c> forever. #469 and #510 were
/// both the same shape of bug — data computed or accepted, then never stored — and
/// both survived because nothing tested the seam between the two ends.
/// </remarks>
public class AuditRecordedEventHandlerTests
{
    private static AuditRecorded Event(
        string actor = "alex",
        string action = "CreateService",
        string resourceType = "Service",
        string resourceId = "svc-1",
        string result = "Success") =>
        new(actor, action, resourceType, resourceId, result);

    [Fact]
    public async Task WritesTheEntryTheEventDescribes()
    {
        var repository = new Mock<IAuditLogRepository>();
        var handler = new AuditRecordedEventHandler(repository.Object);
        AuditLogAggregate? written = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<AuditLogAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogAggregate, CancellationToken>((entry, _) => written = entry)
            .Returns(Task.CompletedTask);

        await handler.HandleAsync(Event());

        written.Should().NotBeNull();
        written!.Actor.Should().Be("alex");
        written.Action.Should().Be("CreateService");
        written.ResourceType.Should().Be("Service");
        written.ResourceId.Should().Be("svc-1");
        written.Result.Should().Be("Success");
    }

    [Fact]
    public async Task WritesNothingButTheEntry()
    {
        var repository = new Mock<IAuditLogRepository>();
        repository
            .Setup(r => r.AddAsync(It.IsAny<AuditLogAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await new AuditRecordedEventHandler(repository.Object).HandleAsync(Event());

        // One event, one record. Writing twice would double every count on the page.
        repository.Verify(
            r => r.AddAsync(It.IsAny<AuditLogAggregate>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RefusesAnEntryWithNoActor()
    {
        // The domain refuses it, and the refusal has to travel: an audit trail that
        // accepted an unattributed action is not an audit trail.
        var repository = new Mock<IAuditLogRepository>();
        var handler = new AuditRecordedEventHandler(repository.Object);

        var handle = async () => await handler.HandleAsync(Event(actor: "  "));

        await handle.Should().ThrowAsync<BitWrite.OcelotControl.Domain.Exceptions.DomainException>()
            .Where(exception => exception.ErrorCode == "INVALID_AUDIT_ACTOR");
    }
}