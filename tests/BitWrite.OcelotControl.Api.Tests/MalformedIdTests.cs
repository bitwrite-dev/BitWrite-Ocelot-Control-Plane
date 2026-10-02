using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests;

/// <summary>
/// A malformed id in a URL, across every controller that takes one.
/// </summary>
/// <remarks>
/// Each controller parsed the GUID itself before handing it to its value object, so
/// <c>/api/v1/routes/-1</c> threw <see cref="FormatException"/>. That matches no case
/// in either error path, so a bad URL answered 500 with the message replaced — an
/// operator was told the server was broken and invited to retry something that could
/// never succeed.
/// <para>
/// This walks every route that takes an id, because the 25 call sites were a
/// mechanical fix and a mechanical fix is exactly the kind that leaves one behind.
/// </para>
/// </remarks>
public class MalformedIdTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MalformedIdTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public static TheoryData<string, string> MalformedIds() => new()
    {
        { "/api/v1/routes/-1", "Routes" },
        { "/api/v1/routes/not-a-guid", "Routes" },
        { "/api/v1/routes/00000000-0000-0000-0000-00000000000", "Routes" },
        { "/api/v1/services/-1", "Services" },
        { "/api/v1/services/nope", "Services" },
        { "/api/v1/gateways/-1", "Gateways" },
        { "/api/v1/gateways/not-a-guid", "Gateways" },
        { "/api/v1/licenses/-1", "Licenses" },
        { "/api/v1/licenses/nope", "Licenses" },
        { "/api/v1/runtime/gateways/-1", "Runtime" },
        { "/api/v1/runtime/gateways/not-a-guid", "Runtime" },
    };

    [Theory]
    [MemberData(nameof(MalformedIds))]
    public async Task AnswersBadRequestRatherThanAServerFault(string path, string controller)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            $"{controller} answered {(int)response.StatusCode} for {path}, which reads as a " +
            "broken server rather than a mistyped URL");
    }

    [Theory]
    [MemberData(nameof(MalformedIds))]
    public async Task ExplainsWhatWasWrongWithIt(string path, string controller)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        // Not "An internal server error occurred" — the value objects name what was
        // expected, which is the whole point of routing the parse through them.
        json.GetProperty("error").GetString().Should().NotBe(
            "An internal server error occurred",
            $"{controller} discarded the reason for {path}");
        json.GetProperty("error").GetString().Should().ContainAny("Invalid", "empty");
    }
}