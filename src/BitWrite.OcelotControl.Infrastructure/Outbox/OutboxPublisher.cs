using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BitWrite.OcelotControl.Application.Interfaces;

using AppIOutboxRepository = BitWrite.OcelotControl.Application.Interfaces.IOutboxRepository;
using AppOutboxMessage = BitWrite.OcelotControl.Application.Interfaces.OutboxMessage;
using AppIEventSerializer = BitWrite.OcelotControl.Application.Interfaces.IEventSerializer;

namespace BitWrite.OcelotControl.Infrastructure.Outbox;

public class OutboxPublisher : BackgroundService
{
    private readonly AppIOutboxRepository _outboxRepository;
    private readonly IRedisPublisher _redisPublisher;
    private readonly AppIEventSerializer _eventSerializer;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);
    private const int MaxRetries = 5;

    public OutboxPublisher(
        AppIOutboxRepository outboxRepository,
        IRedisPublisher redisPublisher,
        AppIEventSerializer eventSerializer,
        ILogger<OutboxPublisher> logger)
    {
        _outboxRepository = outboxRepository;
        _redisPublisher = redisPublisher;
        _eventSerializer = eventSerializer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Publisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages.");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("Outbox Publisher stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var pendingMessages = await _outboxRepository.GetPendingAsync(batchSize: 100, cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                var channel = GetChannelForEvent(message.EventType);
                
                _logger.LogInformation("Publishing message: {EventType} with ID: {MessageId} to channel: {Channel}", 
                    message.EventType, message.Id, channel);

                await _redisPublisher.PublishAsync(channel, message.Payload, cancellationToken);

                await _outboxRepository.MarkProcessedAsync(message.Id, cancellationToken);
                
                _logger.LogInformation("Successfully published message: {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message: {MessageId}", message.Id);
                await HandlePublishFailureAsync(message, ex, cancellationToken);
            }
        }
    }

    private string GetChannelForEvent(string eventType)
    {
        var aggregate = ExtractAggregateName(eventType);
        return $"events.{aggregate.ToLowerInvariant()}";
    }

    private string ExtractAggregateName(string eventType)
    {
        var knownAggregates = new[]
        {
            "Gateway", "Route", "Service", "Snapshot", 
            "Publication", "GlobalConfiguration", "Plugin", 
            "RuntimeInstance", "License", "Audit"
        };

        foreach (var aggregate in knownAggregates)
        {
            if (eventType.StartsWith(aggregate, StringComparison.OrdinalIgnoreCase))
            {
                return aggregate;
            }
        }

        for (int i = 1; i < eventType.Length; i++)
        {
            if (char.IsUpper(eventType[i]) && i > 1)
            {
                return eventType.Substring(0, i);
            }
        }

        return "unknown";
    }

    private async Task HandlePublishFailureAsync(AppOutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var newRetryCount = message.RetryCount + 1;

        if (newRetryCount >= MaxRetries)
        {
            _logger.LogError("Message {MessageId} exceeded max retries ({MaxRetries}). Moving to dead letter.", 
                message.Id, MaxRetries);
            
            await _outboxRepository.MarkFailedAsync(message.Id, 
                $"Max retries exceeded: {exception.Message}", cancellationToken);
            
            await _outboxRepository.AddToDeadLetterAsync(message.Id, exception.Message, cancellationToken);
        }
        else
        {
            var delay = CalculateBackoffDelay(newRetryCount);
            _logger.LogWarning("Publish failed for message {MessageId}. Retry {RetryCount}/{MaxRetries} in {Delay}ms", 
                message.Id, newRetryCount, MaxRetries, delay.TotalMilliseconds);
            
            await _outboxRepository.MarkFailedAsync(message.Id, 
                $"Retry {newRetryCount}/{MaxRetries}: {exception.Message}", cancellationToken);
            
            await Task.Delay(delay, cancellationToken);
        }
    }

    private TimeSpan CalculateBackoffDelay(int retryCount)
    {
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1));
        return baseDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : baseDelay;
    }
}