using System.Text.Json;
using BitWrite.OcelotControl.Api.DTOs;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Controllers;

/// <summary>
/// A service endpoint, as the API reports it.
/// </summary>
/// <remarks>
/// It used to borrow the route's endpoint shape and answer with the invented
/// constants "http" and "/" — a <c>ServiceEndpoint</c> has host, port, weight and
/// isActive, so a caller could not tell a stored value from a placeholder, and
/// weight could neither be read nor set. The request shape now matches the stored
/// shape, so there is nothing to be silently dropped.
/// </remarks>
public class ServicesEndpointContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value, WireOptions);

    [Fact]
    public void TheRequestCarriesOnlyWhatTheDomainStores()
    {
        var json = ToJson(new ServiceEndpointRequest("localhost", 5001, 5));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // scheme and path are not here at all: accepting them meant a caller could
        // type one, see it echoed back as the constant, and have no way to tell it
        // was never kept.
        root.TryGetProperty("scheme", out _).Should().BeFalse();
        root.TryGetProperty("path", out _).Should().BeFalse();
        root.GetProperty("weight").GetInt32().Should().Be(5);
    }

    [Fact]
    public void TheResponseReportsTheWeightItStored()
    {
        var json = ToJson(new ServiceEndpointResponse("localhost", 5001, 7, true));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("weight").GetInt32().Should().Be(7);
        root.GetProperty("isActive").GetBoolean().Should().BeTrue();
        root.TryGetProperty("scheme", out _).Should().BeFalse();
        root.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public void TheResponseIsNotTheRoutesDownstreamTarget()
    {
        // The two shapes were the same record, so the service response was shaped
        // like a route's and filled with constants. A service endpoint is a
        // different thing and now says so.
        var serviceEndpoint = ToJson(new ServiceEndpointResponse("localhost", 5001, 1, true));
        var routeTarget = ToJson(new DownstreamTargetResponse("localhost", 5001, "http", "/"));

        serviceEndpoint.Should().NotBe(routeTarget);
    }
}
