using BitWrite.OcelotControl.Infrastructure.Redis;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Redis;

public class RedisSerializerTests
{
    [Fact]
    public void Serialize_ShouldSerializeObjectToJson()
    {
        var obj = new TestObject { Name = "Test", Value = 42 };
        var json = RedisSerializer.Serialize(obj);
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Test");
        json.Should().Contain("42");
    }

    [Fact]
    public void Deserialize_ShouldDeserializeJsonToObject()
    {
        var json = """{"name":"Test","value":42}""";
        var obj = RedisSerializer.Deserialize<TestObject>(json);
        obj.Should().NotBeNull();
        obj!.Name.Should().Be("Test");
        obj.Value.Should().Be(42);
    }

    [Fact]
    public void ToHashEntries_ShouldConvertObjectToHashEntries()
    {
        var obj = new TestObject { Name = "Test", Value = 42 };
        var entries = RedisSerializer.ToHashEntries(obj);
        entries.Should().NotBeNullOrEmpty();
        entries.Should().HaveCount(2);
    }

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}