using Moq;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Enough Redis to watch what a repository does to it.
/// </summary>
/// <remarks>
/// A repository under test reads back what it wrote — a delete reads the row first,
/// a list reads the index — so recording only the keys passed to the mocked calls
/// would miss the half of the behaviour that goes wrong quietly. This keeps the rows,
/// the index sets and the sorted sets in dictionaries, so a test can assert on what a
/// second environment's repository can see.
/// <para>
/// Only the operations the repositories actually call are implemented. An
/// unimplemented call returns the mock's default rather than throwing, so a repository
/// that grows a new call fails its own assertion instead of this one.
/// </para>
/// </remarks>
internal sealed class RecordingRedis
{
    private readonly Dictionary<string, HashEntry[]> _hashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSetEntry[]> _sortedSets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _sets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public RecordingRedis()
    {
        Database = new Mock<IDatabase>(MockBehavior.Loose);

        Database.Setup(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, entries, _) =>
            {
                _hashes[key.ToString()!] = entries;
                return Task.CompletedTask;
            });

        Database.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) => Task.FromResult(
                _hashes.TryGetValue(key.ToString()!, out var entries) ? entries : []));

        Database.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
            {
                _hashes.Remove(key.ToString()!);
                _sortedSets.Remove(key.ToString()!);
                _sets.Remove(key.ToString()!);
                _strings.Remove(key.ToString()!);
                return Task.FromResult(true);
            });

        Database.Setup(d => d.SetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue[], CommandFlags>((key, values, _) =>
            {
                if (!_sets.TryGetValue(key.ToString()!, out var members))
                    _sets[key.ToString()!] = members = new HashSet<string>(StringComparer.Ordinal);

                foreach (var value in values)
                    members.Add(value.ToString()!);

                return Task.FromResult((long)members.Count);
            });

        Database.Setup(d => d.SetRemoveAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, CommandFlags>((key, value, _) =>
            {
                return Task.FromResult(
                    _sets.TryGetValue(key.ToString()!, out var members) && members.Remove(value.ToString()!));
            });

        Database.Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) => Task.FromResult(
                _sets.TryGetValue(key.ToString()!, out var members)
                    ? members.Select(member => (RedisValue)member).ToArray()
                    : Array.Empty<RedisValue>()));

        Database.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((key, value, _, _, _, _) =>
            {
                _strings[key.ToString()!] = value.ToString()!;
                return Task.FromResult(true);
            });

        Database.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) => Task.FromResult<RedisValue>(
                _strings.TryGetValue(key.ToString()!, out var value) ? value : RedisValue.Null));

        Database.Setup(d => d.SortedSetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, double, SortedSetWhen, CommandFlags>((key, value, score, _, _) =>
            {
                if (!_sortedSets.TryGetValue(key.ToString()!, out var entries))
                    _sortedSets[key.ToString()!] = entries = [];

                var existing = Array.FindIndex(entries, entry => entry.Element == value);
                var entry = new SortedSetEntry(value, score);

                if (existing >= 0)
                    entries[existing] = entry;
                else
                    entries = [.. entries, entry];

                _sortedSets[key.ToString()!] = [.. entries.OrderBy(e => e.Score)];
                return Task.FromResult(true);
            });

        Database.Setup(d => d.SortedSetRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(),
                It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, double, double, Exclude, Order, long, long, CommandFlags>(
                (key, _, _, _, _, skip, take, _) =>
                {
                    var entries = _sortedSets.TryGetValue(key.ToString()!, out var stored)
                        ? stored.OrderBy(entry => entry.Score).Skip((int)skip)
                        : Enumerable.Empty<SortedSetEntry>();

                    if (take > 0)
                        entries = entries.Take((int)take);

                    return Task.FromResult(entries.Select(entry => entry.Element).ToArray());
                });

        Multiplexer = new Mock<IConnectionMultiplexer>();
        Multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(Database.Object);
    }

    public Mock<IDatabase> Database { get; }

    public Mock<IConnectionMultiplexer> Multiplexer { get; }

    /// <summary>Every key this Redis has been asked about, for a shape assertion.</summary>
    public IEnumerable<string> Keys => _hashes.Keys
        .Concat(_sortedSets.Keys)
        .Concat(_sets.Keys)
        .Concat(_strings.Keys);

    public bool HasKey(string key) =>
        _hashes.ContainsKey(key) ||
        _sortedSets.ContainsKey(key) ||
        _sets.ContainsKey(key) ||
        _strings.ContainsKey(key);

    public string? StringAt(string key) =>
        _strings.TryGetValue(key, out var value) ? value : null;
}
