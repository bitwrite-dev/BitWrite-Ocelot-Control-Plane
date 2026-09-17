using BitWrite.OcelotControl.Infrastructure.Outbox;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Outbox;

public class RedisOutboxRepository : IOutboxRepository
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private const string PendingKey = "outbox:pending";
    private const string ProcessingKey = "outbox:processing";
    private const string DeadLetterKey = "outbox:deadletter";
    private const string MessagePrefix = "outbox:message:";
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisOutboxRepository(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var messageKey = MessagePrefix + message.Id;
        var json = JsonSerializer.Serialize(message, _jsonOptions);

        var transaction = _database.CreateTransaction();
        transaction.StringSetAsync(messageKey, json);
        transaction.SortedSetAddAsync(PendingKey, message.Id.ToString(), ToUnixTimestamp(message.CreatedAt));
        await transaction.ExecuteAsync();
    }

    public async Task<List<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var now = ToUnixTimestamp(DateTime.UtcNow);
        var messageIds = await _database.SortedSetRangeByScoreAsync(PendingKey, 0, now, Exclude.None, Order.Ascending, 0, batchSize);

        var messages = new List<OutboxMessage>();
        foreach (var id in messageIds)
        {
            var messageKey = MessagePrefix + id;
            var json = await _database.StringGetAsync(messageKey);
            if (!json.IsNullOrEmpty)
            {
                var message = JsonSerializer.Deserialize<OutboxMessage>(json!, _jsonOptions);
                if (message != null)
                {
                    messages.Add(message);
                }
            }
        }

        return messages;
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var messageKey = MessagePrefix + messageId;
        var transaction = _database.CreateTransaction();
        transaction.SortedSetRemoveAsync(PendingKey, messageId.ToString());
        transaction.KeyDeleteAsync(messageKey);
        await transaction.ExecuteAsync();
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var messageKey = MessagePrefix + messageId;
        var json = await _database.StringGetAsync(messageKey);
        if (!json.IsNullOrEmpty)
        {
            var message = JsonSerializer.Deserialize<OutboxMessage>(json!, _jsonOptions);
            if (message != null)
            {
                message.Error = error;
                message.RetryCount++;
                var updatedJson = JsonSerializer.Serialize(message, _jsonOptions);
                await _database.StringSetAsync(messageKey, updatedJson);
            }
        }
    }

    public async Task AddToDeadLetterAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var messageKey = MessagePrefix + messageId;
        var json = await _database.StringGetAsync(messageKey);
        if (!json.IsNullOrEmpty)
        {
            var message = JsonSerializer.Deserialize<OutboxMessage>(json!, _jsonOptions);
            if (message != null)
            {
                message.Error = error;
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;

                var transaction = _database.CreateTransaction();
                transaction.SortedSetRemoveAsync(PendingKey, messageId.ToString());
                transaction.StringSetAsync(messageKey, JsonSerializer.Serialize(message, _jsonOptions));
                transaction.SortedSetAddAsync(DeadLetterKey, messageId.ToString(), ToUnixTimestamp(DateTime.UtcNow));
                await transaction.ExecuteAsync();
            }
        }
    }

    private static double ToUnixTimestamp(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime).ToUnixTimeSeconds();
    }
}