using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using BitWrite.OcelotControl.Infrastructure.Repositories;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

public class RedisSnapshotRepositoryTests
{
    private const string Content = "{\"globalConfiguration\":{},\"routes\":[]}";
    private const string Hash = "d563657ba64f0047febc03b2b4162f831a9ceb5fdac714e5e5c947a2dff6a063";
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset PublishedAt = new(2026, 1, 2, 8, 4, 5, TimeSpan.Zero);

    private readonly Mock<IDatabase> _mockDatabase = new();
    private readonly RedisSnapshotRepository _repository;
    private RedisValue _writtenValue = default;
    private bool _hashWriteCalled;

    public RedisSnapshotRepositoryTests()
    {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
        _repository = new RedisSnapshotRepository(mockMultiplexer.Object);
    }

    [Fact]
    public async Task AddAsync_ShouldStoreSnapshotAsJsonString_NotAsHash()
    {
        SetupWrites();

        await _repository.AddAsync(BuildSnapshot());

        _hashWriteCalled.Should().BeFalse("snapshots are stored as a JSON string, not a Redis hash");

        var json = _writtenValue.ToString();
        json.Should().Contain("\"version\":1");
        json.Should().Contain("\"status\":\"Published\"");
        json.Should().Contain($"\"hash\":\"{Hash}\"");
        json.Should().Contain("\"createdBy\":\"admin\"");
        json.Should().Contain($"\"content\":{System.Text.Json.JsonSerializer.Serialize(Content)}");
        json.Should().Contain("\"createdAt\":\"2026-01-02T03:04:05+00:00\"");
        json.Should().Contain("\"publishedAt\":\"2026-01-02T08:04:05+00:00\"");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenKeyMissing()
    {
        _mockDatabase.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _repository.GetAsync(SnapshotVersion.From(1));

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldRoundTripJsonString_PreservingStatusAndTimestamps()
    {
        SetupWrites();
        await _repository.AddAsync(BuildSnapshot());

        var result = await _repository.GetAsync(SnapshotVersion.From(1));

        result.Should().NotBeNull();
        result!.Version.Should().Be(SnapshotVersion.From(1));
        result.Hash.Value.Should().Be(Hash);
        result.Content.Should().Be(Content);
        result.Status.Should().Be(SnapshotStatus.Published);
        result.CreatedBy.Should().Be("admin");
        result.CreatedAt.Should().Be(CreatedAt);
        result.PublishedAt.Should().Be(PublishedAt);
        result.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_ShouldIndexTheVersionInTheSnapshotsIndex()
    {
        SetupWrites();

        await _repository.AddAsync(BuildSnapshot());

        _mockDatabase.Verify(d => d.SortedSetAddAsync(
            RedisKeyHelper.IndexSnapshots, It.IsAny<RedisValue>(), It.IsAny<double>(),
            It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private void SetupWrites()
    {
        _mockDatabase.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>((_, v, _, _, _, _) => _writtenValue = v)
            .ReturnsAsync(true);

        _mockDatabase.Setup(d => d.SortedSetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>(),
                It.IsAny<SortedSetWhen>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _mockDatabase.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Callback(() => _hashWriteCalled = true)
            .Returns(Task.CompletedTask);

        _mockDatabase.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() => _writtenValue);
    }

    private static Snapshot BuildSnapshot() =>
        Snapshot.Reconstitute(
            Content,
            ConfigurationHash.FromString(Hash),
            SnapshotVersion.From(1),
            SnapshotStatus.Published,
            "admin",
            CreatedAt,
            PublishedAt);
}
