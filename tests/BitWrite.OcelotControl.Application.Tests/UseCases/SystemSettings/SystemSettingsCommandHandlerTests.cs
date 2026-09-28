using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.SystemSettings;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Xunit;
using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;
using SettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.SystemSettings;

/// <summary>
/// First-run setup, and the rule that the Ocelot version is chosen exactly once.
/// </summary>
public class SystemSettingsCommandHandlerTests
{
    private readonly Mock<ISystemSettingsRepository> _repository = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly SystemSettingsCommandHandler _handler;

    public SystemSettingsCommandHandlerTests()
    {
        _handler = new SystemSettingsCommandHandler(_repository.Object, _dispatcher.Object);
    }

    private void StoredSettings(SettingsAggregate settings) =>
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(settings);

    private void NeverStored() =>
        _repository.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(SettingsAggregate.Create());

    [Fact]
    public async Task ReadingBeforeAnyChoiceReportsThatSetupIsOutstanding()
    {
        // What a first-run screen needs in order to know whether to render itself.
        NeverStored();

        var response = await _handler.HandleAsync(new GetSystemSettingsQuery());

        response.IsInitialised.Should().BeFalse();
        response.OcelotVersion.Should().BeNull();
        response.OcelotVersionSelectedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReadingOffersTheVersionsThatCanBeEmitted()
    {
        NeverStored();

        var response = await _handler.HandleAsync(new GetSystemSettingsQuery());

        // 18 only. 19 and 20 would promise a shape that has not been established.
        response.AvailableOcelotVersions.Should().Equal("18.0.0");
    }

    [Fact]
    public async Task FirstRunChoosesTheVersion()
    {
        NeverStored();

        var response = await _handler.HandleAsync(
            new CompleteFirstRunCommand("18.0.0", InitiatedBy: "admin"));

        response.IsInitialised.Should().BeTrue();
        response.OcelotVersion.Should().Be("18.0.0");
        response.OcelotVersionSelectedBy.Should().Be("admin");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<SettingsAggregate>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task FirstRunAlsoAppliesTheSettingsChosenWithIt()
    {
        NeverStored();

        var response = await _handler.HandleAsync(new CompleteFirstRunCommand(
            "18.0.0",
            PollIntervalSeconds: 60,
            AuditLogRetentionDays: 30,
            SnapshotRetentionCount: 5,
            InitiatedBy: "admin"));

        response.PollIntervalSeconds.Should().Be(60);
        response.AuditLogRetentionDays.Should().Be(30);
        response.SnapshotRetentionCount.Should().Be(5);
    }

    [Fact]
    public async Task FirstRunDispatchesTheEventThatEndsSetup()
    {
        NeverStored();

        await _handler.HandleAsync(new CompleteFirstRunCommand("18.0.0", InitiatedBy: "admin"));

        _dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<IEnumerable<DomainEvent>>(
                    events => events.Any(e => e is SystemSettingsInitialised)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ASecondVersionIsRefused()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");
        StoredSettings(settings);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new CompleteFirstRunCommand("20.0.0", InitiatedBy: "someone")));

        ex.ErrorCode.Should().Be("OCELOT_VERSION_ALREADY_CHOSEN");
    }

    [Fact]
    public async Task ResubmittingTheSameVersionIsAccepted()
    {
        // Two operators racing through first-run is a real scenario. Telling the
        // second one "already chosen, cannot be changed" would leave them unable
        // to tell whether their own submission was the one that landed.
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");
        StoredSettings(settings);

        var response = await _handler.HandleAsync(
            new CompleteFirstRunCommand("18.0.0", InitiatedBy: "other"));

        response.OcelotVersion.Should().Be("18.0.0");
        response.OcelotVersionSelectedBy.Should().Be("admin");
    }

    [Theory]
    [InlineData("19.0")]
    [InlineData("20.0")]
    [InlineData("23.3")]
    public async Task AVersionWithNoEstablishedShapesIsRefused(string version)
    {
        NeverStored();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new CompleteFirstRunCommand(version)));

        ex.ErrorCode.Should().Be("OCELOT_VERSION_NOT_EMITTABLE");
        // Nothing was written, so setup is still outstanding and the operator can
        // pick again.
        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<SettingsAggregate>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyVersionIsRefused(string version)
    {
        NeverStored();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new CompleteFirstRunCommand(version)));

        ex.ErrorCode.Should().Be("MISSING_OCELOT_VERSION");
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("18")]
    [InlineData("18.0.0.1")]
    public async Task AMalformedVersionIsARejectionRatherThanAServerError(string version)
    {
        // Parse throws a FormatException on a non-numeric part, which would
        // otherwise surface as a 500.
        NeverStored();

        await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new CompleteFirstRunCommand(version)));
    }

    [Fact]
    public async Task OperationalSettingsCanChangeAfterwards()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");
        StoredSettings(settings);

        var response = await _handler.HandleAsync(
            new UpdateSystemSettingsCommand(PollIntervalSeconds: 120, InitiatedBy: "admin"));

        response.PollIntervalSeconds.Should().Be(120);
        response.OcelotVersion.Should().Be("18.0.0");
    }

    [Fact]
    public async Task ChangingOperationalSettingsNeverTouchesTheVersion()
    {
        // The version has no update path at all, so the ordinary settings command
        // must not be a way in.
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");
        StoredSettings(settings);

        await _handler.HandleAsync(new UpdateSystemSettingsCommand(AuditLogRetentionDays: 7));

        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        settings.AuditLogRetentionDays.Should().Be(7);
    }

    [Fact]
    public async Task AnInvalidOperationalSettingIsRefusedBeforeAnythingIsWritten()
    {
        NeverStored();

        await Assert.ThrowsAsync<DomainException>(
            () => _handler.HandleAsync(new UpdateSystemSettingsCommand(PollIntervalSeconds: 1)));

        _repository.Verify(
            r => r.UpdateAsync(It.IsAny<SettingsAggregate>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void TheUpdateCommandHasNoVersionParameter()
    {
        // Stated as a fact rather than left to inspection, because adding one would
        // be the easiest way to break the whole decision.
        typeof(UpdateSystemSettingsCommand).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("OcelotVersion");
    }
}
