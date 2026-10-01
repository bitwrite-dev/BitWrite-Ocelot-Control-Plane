using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using FluentAssertions;
using Xunit;

using SnapshotAggregate = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using ConfigurationHashValue = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.ConfigurationHash;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// A snapshot's validation results, and whether they survive being stored.
/// </summary>
/// <remarks>
/// <c>ValidationResults</c> existed on the aggregate, <c>Create</c> already accepted
/// them, and nothing ever passed any — so the snapshots page showed "not validated"
/// for every snapshot, permanently. The API contract test asserted the field was
/// present and the page test asserted the verdict for a populated list; nothing
/// asserted that anything populated it.
///
/// These tests are that assertion, at both ends: computed on the way in, and
/// restored on the way out.
/// </remarks>
public class SnapshotValidationResultsTests
{
    private static readonly IReadOnlyList<ValidationResult> Results =
    [
        new() { Rule = "RouteConflicts", IsValid = true, Message = null },
        new() { Rule = "References", IsValid = false, Message = "Unknown service s-1" },
    ];

    /// <summary>
    /// A hash of the shape the value object accepts — a 64-character hex string,
    /// which is what a real snapshot carries.
    /// </summary>
    private static ConfigurationHashValue Hash() =>
        ConfigurationHashValue.FromString(new string('a', 64));

    [Fact]
    public void CarriesTheRulesItWasCreatedWith()
    {
        var snapshot = SnapshotAggregate.Create(
            "{}", Hash(), SnapshotVersion.From(4), "operator", validationResults: Results);

        snapshot.ValidationResults.Should().HaveCount(2);
        snapshot.ValidationResults.Should().BeEquivalentTo(Results);
    }

    [Fact]
    public void ReportsTheFailuresAmongThem()
    {
        var snapshot = SnapshotAggregate.Create(
            "{}", Hash(), SnapshotVersion.From(4), "operator", validationResults: Results);

        snapshot.GetValidationErrors().Should().ContainSingle();
        snapshot.GetValidationErrors()[0].Rule.Should().Be("References");
    }

    [Fact]
    public void IsNotValidWhenARuleFailed()
    {
        var snapshot = SnapshotAggregate.Create(
            "{}", Hash(), SnapshotVersion.From(4), "operator", validationResults: Results);

        snapshot.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RestoresThemAfterBeingStored()
    {
        // The half that was missing: `Reconstitute` rebuilt the snapshot from its
        // document and had no way to carry the results, so they were dropped on
        // every read no matter how many times they were computed.
        var restored = SnapshotAggregate.Reconstitute(
            "{}", Hash(), SnapshotVersion.From(4), SnapshotStatus.Ready,
            "operator", DateTimeOffset.UtcNow, validationResults: Results);

        restored.ValidationResults.Should().HaveCount(2);
        restored.ValidationResults[1].Message.Should().Be("Unknown service s-1");
        restored.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RestoresInOrderBecauseTheSequenceIsTheReport()
    {
        var restored = SnapshotAggregate.Reconstitute(
            "{}", Hash(), SnapshotVersion.From(4), SnapshotStatus.Ready,
            "operator", DateTimeOffset.UtcNow, validationResults: Results);

        // The first failure is the one an operator acts on; reordering would
        // change which problem they are shown first.
        restored.ValidationResults.Select(r => r.Rule)
            .Should().ContainInOrder("RouteConflicts", "References");
    }

    [Fact]
    public void RestoresFromADocumentWrittenBeforeTheFieldExisted()
    {
        // Snapshots stored before this change have no results. Reading one has to
        // keep working, and it is reported as unchecked rather than as checked.
        var restored = SnapshotAggregate.Reconstitute(
            "{}", Hash(), SnapshotVersion.From(2), SnapshotStatus.Archived,
            "operator", DateTimeOffset.UtcNow);

        restored.ValidationResults.Should().BeEmpty();
    }

    [Fact]
    public void ReadsAnAbsentResultListAsUncheckedRatherThanAsChecked()
    {
        // An empty list means no rule ran. `IsValid` is `All(r => r.IsValid)` over
        // nothing, which is vacuously true — so the aggregate said "valid" about a
        // snapshot while the page, given the same empty list, said "not validated".
        // They were describing the same snapshot and disagreeing.
        var restored = SnapshotAggregate.Reconstitute(
            "{}", Hash(), SnapshotVersion.From(2), SnapshotStatus.Archived,
            "operator", DateTimeOffset.UtcNow);

        restored.ValidationResults.Should().BeEmpty(
            "so nothing can claim these rules passed");
    }
}