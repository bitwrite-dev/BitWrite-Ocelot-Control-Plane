using FluentAssertions;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.Exceptions;

// Disambiguates from System.Net.Http.HttpMethod, which this test project also sees.
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// Guards the rehydration factories against regressing into Create-on-read.
///
/// Create mints a new id and stamps UtcNow, so using it when loading from a
/// repository produced a different id on every read. That was observed live:
/// reading one service three times returned three ids, and creating a route
/// failed with "Service not found" because the id it referenced had changed.
/// </summary>
public class ReconstituteTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void ServiceReconstitute_ShouldPreserveId()
    {
        var id = ServiceId.New();

        var service = Service.Reconstitute(id, "user-service", "desc", Created, Updated);

        service.Id.Should().Be(id);
    }

    [Fact]
    public void ServiceReconstitute_ShouldBeStableAcrossRepeatedReads()
    {
        var id = ServiceId.New();

        var first = Service.Reconstitute(id, "user-service", "desc", Created, Updated);
        var second = Service.Reconstitute(id, "user-service", "desc", Created, Updated);

        // The original defect: Create() made these differ.
        first.Id.Should().Be(second.Id);
    }

    [Fact]
    public void ServiceReconstitute_ShouldNotSwapNameAndDescription()
    {
        // Service.Create's first parameter is `name`. Passing the id there put a
        // UUID in Name and the real name in Description.
        var service = Service.Reconstitute(ServiceId.New(), "user-service", "the description", Created, Updated);

        service.Name.Should().Be("user-service");
        service.Description.Should().Be("the description");
    }

    [Fact]
    public void ServiceReconstitute_ShouldPreserveTimestamps()
    {
        var service = Service.Reconstitute(ServiceId.New(), "s", null, Created, Updated);

        service.CreatedAt.Should().Be(Created);
        service.UpdatedAt.Should().Be(Updated);
    }

    [Fact]
    public void ServiceReconstitute_ShouldPreserveEndpoints()
    {
        // ServiceEndpoint has internal setters, so endpoints are built through
        // the public API and then handed to Reconstitute.
        var seed = Service.Create("s");
        seed.AddHost("localhost", 5001, weight: 2);
        seed.AddHost("localhost", 5002);

        var service = Service.Reconstitute(
            ServiceId.New(), "s", null, Created, Updated, seed.Endpoints);

        service.Endpoints.Should().HaveCount(2);
        service.Endpoints[0].Port.Should().Be(5001);
        service.Endpoints[0].Weight.Should().Be(2);
        service.Endpoints[1].Port.Should().Be(5002);
    }

    [Fact]
    public void ServiceReconstitute_ShouldRaiseNoDomainEvents()
    {
        var service = Service.Reconstitute(ServiceId.New(), "s", null, Created, Updated);

        // The events Create raises already happened; re-raising them on every
        // read would republish on each poll.
        service.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ServiceReconstitute_ShouldStillRejectAnEmptyName()
    {
        var act = () => Service.Reconstitute(ServiceId.New(), "   ", null, Created, Updated);

        act.Should().Throw<DomainException>();
    }

    private static Route SampleRoute(RouteId id) => Route.Reconstitute(
        id,
        HttpMethod.Parse("GET"),
        UpstreamPath.From("/api/users"),
        ServiceId.New(),
        new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
        isEnabled: true,
        Created,
        Updated,
        key: "user-route",
        host: "example.com");

    [Fact]
    public void RouteReconstitute_ShouldPreserveId()
    {
        var id = RouteId.New();

        var route = SampleRoute(id);

        route.Id.Should().Be(id);
    }

    [Fact]
    public void RouteReconstitute_ShouldPreserveDisabledState()
    {
        // Create() always yields IsEnabled = true, so reloading undid a disable.
        var route = Route.Reconstitute(
            RouteId.New(),
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "h", 1) },
            isEnabled: false,
            Created,
            Updated);

        route.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RouteReconstitute_ShouldPreserveTimestamps()
    {
        var route = SampleRoute(RouteId.New());

        route.CreatedAt.Should().Be(Created);
        route.UpdatedAt.Should().Be(Updated);
    }

    [Fact]
    public void RouteReconstitute_ShouldKeepKeyAndHostInTheirOwnFields()
    {
        // The old read path passed routeKey.Host into the `key` parameter slot.
        var route = SampleRoute(RouteId.New());

        route.Key.Should().Be("user-route");
        route.Host.Should().Be("example.com");
    }

    [Fact]
    public void RouteReconstitute_ShouldNotDuplicateDownstreamTargets()
    {
        var route = SampleRoute(RouteId.New());

        // The old path called Create (which adds them) and then added them again.
        route.DownstreamTargets.Should().HaveCount(1);
    }

    [Fact]
    public void RouteReconstitute_ShouldRaiseNoDomainEvents()
    {
        SampleRoute(RouteId.New()).DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RouteReconstitute_ShouldRejectAnEmptyTargetList()
    {
        var act = () => Route.Reconstitute(
            RouteId.New(),
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget>(),
            true, Created, Updated);

        act.Should().Throw<DomainException>();
    }
}
