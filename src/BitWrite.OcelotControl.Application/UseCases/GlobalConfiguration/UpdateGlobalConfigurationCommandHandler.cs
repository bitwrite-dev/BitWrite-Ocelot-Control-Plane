using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;

public class UpdateGlobalConfigurationCommandHandler
{
    private readonly IGlobalConfigurationRepository _globalConfigRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateGlobalConfigurationCommandHandler(
        IGlobalConfigurationRepository globalConfigRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _globalConfigRepository = globalConfigRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<GlobalConfigurationResponse> HandleAsync(UpdateGlobalConfigurationCommand command, CancellationToken cancellationToken = default)
    {
        var config = await _globalConfigRepository.GetAsync(cancellationToken);

        // Update fields if provided
        if (command.BaseUrl != null)
            config.SetBaseUrl(command.BaseUrl);

        if (command.RequestIdKey != null)
            config.SetRequestIdKey(command.RequestIdKey);

        if (command.DownstreamScheme != null)
            config.SetDownstreamScheme(command.DownstreamScheme);

        if (command.Timeout != null)
            config.SetTimeout(command.Timeout);

        if (command.RateLimit != null)
            config.SetRateLimit(command.RateLimit);

        if (command.QoS != null)
            config.SetQoS(command.QoS);

        if (command.HttpHandler != null)
            config.SetHttpHandler(command.HttpHandler);

        if (command.ServiceDiscovery != null)
            config.SetServiceDiscovery(command.ServiceDiscovery);

        // Persist
        await _globalConfigRepository.UpdateAsync(config, cancellationToken);

        // Dispatch domain events from aggregate
        foreach (var domainEvent in config.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        config.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "UpdateGlobalConfiguration",
            "GlobalConfiguration",
            config.Id.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(config);
    }

    private static GlobalConfigurationResponse MapToResponse(DomainGlobalConfig config)
    {
        return new GlobalConfigurationResponse(
            config.Id.ToString(),
            config.BaseUrl,
            config.RequestIdKey,
            config.DownstreamScheme,
            config.Timeout,
            config.RateLimit,
            config.QoS,
            config.HttpHandler,
            config.ServiceDiscovery,
            config.UpdatedAt
        );
    }
}