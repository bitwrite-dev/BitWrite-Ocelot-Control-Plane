using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BitWrite.OcelotControl.Infrastructure.Outbox;

public class OutboxPublisher : BackgroundService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public OutboxPublisher(IOutboxRepository outboxRepository, ILogger<OutboxPublisher> logger)
    {
        _outboxRepository = outboxRepository;
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
        var pendingMessages = await _outboxRepository.GetPendingMessagesAsync(batchSize: 100, cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                // TODO: Publish to Redis Pub/Sub or Kafka
                _logger.LogInformation("Publishing message: {EventType} with ID: {MessageId}", message.EventType, message.Id);

                // For now, just mark as processed
                await _outboxRepository.MarkAsProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message: {MessageId}", message.Id);
                await _outboxRepository.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }
    }
}
