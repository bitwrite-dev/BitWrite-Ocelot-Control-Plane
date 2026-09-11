using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace BitWrite.OcelotControl.Application.Events;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public DomainEventDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : DomainEvent
    {
        var handlers = _serviceProvider.GetServices<IDomainEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(domainEvent, cancellationToken);
        }
    }

    public async Task DispatchAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchDomainEventAsync(domainEvent, cancellationToken);
        }
    }

    private async Task DispatchDomainEventAsync(DomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var dispatcherType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handlers = _serviceProvider.GetServices(dispatcherType);

        foreach (var handler in handlers)
        {
            var handleMethod = dispatcherType.GetMethod("HandleAsync");
            if (handleMethod != null)
            {
                await (Task)handleMethod.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
            }
        }
    }
}
