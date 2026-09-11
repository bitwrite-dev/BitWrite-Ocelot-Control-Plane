using BitWrite.OcelotControl.Application.Events;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.Tests.Events;

public class DomainEventDispatcherTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDomainEventDispatcher _dispatcher;

    public DomainEventDispatcherTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddTransient<IDomainEventHandler<GatewayRegistered>, TestGatewayRegisteredHandler>();
        services.AddTransient<IDomainEventHandler<RouteCreated>, TestRouteCreatedHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = _serviceProvider.GetRequiredService<IDomainEventDispatcher>();
    }

    [Fact]
    public async Task DispatchAsync_ShouldCallHandler()
    {
        // Arrange
        var gatewayId = GatewayId.New();
        var domainEvent = new GatewayRegistered(gatewayId, "Test Gateway", "Description");

        // Act
        await _dispatcher.DispatchAsync(domainEvent);

        // Assert
        TestGatewayRegisteredHandler.LastEvent.Should().Be(domainEvent);
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleMultipleEvents()
    {
        // Arrange
        var gatewayId = GatewayId.New();
        var routeId = RouteId.New();
        var serviceId = ServiceId.New();
        var events = new List<DomainEvent>
        {
            new GatewayRegistered(gatewayId, "Test Gateway", "Description"),
            new RouteCreated(routeId, RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/test")), serviceId)
        };

        // Act
        await _dispatcher.DispatchAsync(events);

        // Assert
        TestGatewayRegisteredHandler.LastEvent.Should().NotBeNull();
        TestRouteCreatedHandler.LastEvent.Should().NotBeNull();
    }
}

public class TestGatewayRegisteredHandler : IDomainEventHandler<GatewayRegistered>
{
    public static GatewayRegistered? LastEvent { get; private set; }

    public Task HandleAsync(GatewayRegistered domainEvent, CancellationToken cancellationToken = default)
    {
        LastEvent = domainEvent;
        return Task.CompletedTask;
    }
}

public class TestRouteCreatedHandler : IDomainEventHandler<RouteCreated>
{
    public static RouteCreated? LastEvent { get; private set; }

    public Task HandleAsync(RouteCreated domainEvent, CancellationToken cancellationToken = default)
    {
        LastEvent = domainEvent;
        return Task.CompletedTask;
    }
}
