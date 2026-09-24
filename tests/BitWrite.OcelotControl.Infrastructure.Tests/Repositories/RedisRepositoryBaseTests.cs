using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

public class RedisRepositoryBaseTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IConnectionMultiplexer> _mockMultiplexer;
    private readonly TestRedisRepository _repository;

    public RedisRepositoryBaseTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockMultiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
        _repository = new TestRedisRepository(_mockMultiplexer.Object);
    }

    [Fact]
    public async Task GetHashAsync_ShouldReturnHashEntries()
    {
        var key = "test:key";
        var entries = new[]
        {
            new HashEntry("Field1", "Value1"),
            new HashEntry("Field2", "Value2")
        };
        _mockDatabase.Setup(d => d.HashGetAllAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(entries);

        var result = await _repository.TestGetHashAsync(key);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Name == "Field1" && e.Value == "Value1");
    }

    [Fact]
    public async Task GetHashAsync_ShouldReturnEmpty_WhenKeyNotExists()
    {
        _mockDatabase.Setup(d => d.HashGetAllAsync((RedisKey)"nonexistent:key", It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<HashEntry>());

        var result = await _repository.TestGetHashAsync("nonexistent:key");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SetHashAsync_ShouldSetHashEntries()
    {
        var key = "test:key";
        var entries = new[]
        {
            new HashEntry("Field1", "Value1"),
            new HashEntry("Field2", "Value2")
        };
        _mockDatabase.Setup(d => d.HashSetAsync(key, entries, It.IsAny<CommandFlags>()))
            .Returns(Task.FromResult(true));

        await _repository.TestSetHashAsync(key, entries);

        _mockDatabase.Verify(d => d.HashSetAsync(key, entries, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteKey()
    {
        _mockDatabase.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await _repository.TestKeyDeleteAsync("key:to:delete");

        _mockDatabase.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    private class TestRedisRepository : RedisRepositoryBase
    {
        public TestRedisRepository(IConnectionMultiplexer connectionMultiplexer) : base(connectionMultiplexer) { }

        public Task<HashEntry[]> TestGetHashAsync(string key) => GetHashAsync(key);
        public Task TestSetHashAsync(string key, HashEntry[] entries) => SetHashAsync(key, entries);
        public Task TestKeyDeleteAsync(string key) => DeleteAsync(key);
    }
}