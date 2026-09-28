using System.Reflection;
using System.Text.Json;
using BitWrite.OcelotControl.Api.DTOs;
using BitWrite.OcelotControl.Domain.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Controllers;

/// <summary>
/// The snapshot list contract.
///
/// The dashboard's snapshot table is typed against these names, so a rename here
/// would not fail the build — it would fail silently in the browser, as a column
/// of dashes. These tests pin the wire names to the count of the page that
/// consumes them.
/// </summary>
public class SnapshotResponseContractTests
{
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web);

    private static string ToJson<T>(T response) => JsonSerializer.Serialize(response, WireOptions);

    [Fact]
    public void TheListCarriesEverythingTheTableShows()
    {
        var response = new SnapshotResponse(
            4,
            "abc123",
            "{}",
            "Active",
            "operator",
            DateTimeOffset.UtcNow,
            null,
            null,
            2,
            1,
            new[] { "RateLimiting 2.1.0" },
            new[] { new SnapshotValidationResultResponse("RoutesResolve", true, null) });

        var json = ToJson(response);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Named explicitly: a default serializer that omits nulls would drop
        // publishedAt and archivedAt, and the table has columns for both.
        foreach (var name in new[]
                 {
                     "version", "hash", "content", "status", "createdBy", "createdAt",
                     "publishedAt", "archivedAt", "routeCount", "serviceCount",
                     "pluginVersions", "validationResults",
                 })
        {
            root.TryGetProperty(name, out _).Should().BeTrue($"the dashboard reads '{name}'");
        }

        root.GetProperty("routeCount").GetInt32().Should().Be(2);
        root.GetProperty("serviceCount").GetInt32().Should().Be(1);
        root.GetProperty("pluginVersions").EnumerateArray().Should().HaveCount(1);
        root.GetProperty("validationResults")[0].GetProperty("rule").GetString()
            .Should().Be("RoutesResolve");
        root.GetProperty("validationResults")[0].GetProperty("isValid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void AValidationResultKeepsItsMessageSoAFailureCanBeExplained()
    {
        var response = new SnapshotResponse(
            4, "abc", "{}", "Active", "operator", DateTimeOffset.UtcNow, null, null, 0, 0,
            Array.Empty<string>(),
            new[] { new SnapshotValidationResultResponse("ServicesResolve", false, "service unknown") });

        using var document = JsonDocument.Parse(ToJson(response));
        var result = document.RootElement.GetProperty("validationResults")[0];

        result.GetProperty("isValid").GetBoolean().Should().BeFalse();
        result.GetProperty("message").GetString().Should().Be("service unknown");
    }

    [Fact]
    public void TheCompositionIsReadOutOfTheContentRatherThanStoredSeparately()
    {
        // The numbers describe the file a gateway runs. A second stored copy
        // could disagree with it, and the file is the thing that is deployed.
        const string content = """
        {
          "Routes": [ {}, {} ],
          "Services": [ {} ],
          "PluginConfigurations": [ { "Name": "RateLimiting", "Version": "2.1.0" } ]
        }
        """;

        var response = new SnapshotResponse(
            1, "abc", content, "Active", "operator", DateTimeOffset.UtcNow, null, null,
            SnapshotComposition.RouteCount(content) ?? 0,
            SnapshotComposition.ServiceCount(content) ?? 0,
            SnapshotComposition.PluginVersions(content),
            Array.Empty<SnapshotValidationResultResponse>());

        response.RouteCount.Should().Be(2);
        response.ServiceCount.Should().Be(1);
        response.PluginVersions.Should().Equal("RateLimiting 2.1.0");
    }

    [Fact]
    public void AContentThatCannotBeReadReportsUnknownRatherThanClaimingZero()
    {
        // Reporting, not correctness: one unreadable row must not take the whole
        // list down. But "no routes" and "cannot tell how many routes" are
        // different facts, so the unknown is passed through instead of being
        // rounded to a zero that would read as a fact.
        const string unreadable = "not json";

        SnapshotComposition.RouteCount(unreadable).Should().BeNull();
        SnapshotComposition.ServiceCount(unreadable).Should().BeNull();
        SnapshotComposition.PluginVersions(unreadable).Should().BeEmpty();
    }

    [Fact]
    public void AnEmptySectionIsARealZeroAndStaysOne()
    {
        // The document is readable and declares no routes. That is an answer.
        SnapshotComposition.RouteCount("""{ "Routes": [] }""").Should().Be(0);
    }

    [Fact]
    public void ThePreviewCarriesEverythingTheCreatePageNeedsBeforeItSealsAnything()
    {
        var response = new PreviewSnapshotResponse(
            "{\"Routes\":[]}",
            "abc123",
            2,
            1,
            new[] { "RateLimiting 2.1.0" },
            new[] { new SnapshotValidationResultResponse("RouteConflicts", true, null) },
            true,
            "20.0.0",
            105);

        var json = ToJson(response);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // The page shows each of these before the operator commits, so a missing
        // one is a field that silently cannot be displayed.
        foreach (var name in new[]
                 {
                     "content", "hash", "routeCount", "serviceCount", "pluginVersions",
                     "validationResults", "isValid", "ocelotVersion", "nextVersion",
                 })
        {
            root.TryGetProperty(name, out _).Should().BeTrue($"the create page reads '{name}'");
        }

        root.GetProperty("nextVersion").GetInt32().Should().Be(105);
    }

    [Fact]
    public void APreviewWithNothingToHashSaysSoRatherThanReportingAnEmptyHash()
    {
        // An empty string would read as a real hash that happens to be blank.
        var json = ToJson(new PreviewSnapshotResponse(
            null, null, 0, 0, Array.Empty<string>(),
            Array.Empty<SnapshotValidationResultResponse>(), false, "20.0.0", 1));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("hash").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void TheSnapshotEndpointsKeepTheirDocumentedPaths()
    {
        var controller = typeof(SnapshotResponse).Assembly
            .GetTypes()
            .Single(type => type.Name == "SnapshotsController");

        var paths = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(method => method
                .GetCustomAttributes<HttpGetAttribute>()
                .Select(get => get.Template)
                .Concat(method.GetCustomAttributes<HttpPostAttribute>().Select(post => post.Template)))
            .ToList();

        // The page links to exactly these; a moved endpoint would leave the
        // buttons failing with a 404 and nothing to explain it.
        paths.Should().Contain(new[]
        {
            null,
            "preview",
            "{version}",
            "{version}/compare",
            "{version}/clone",
            "{version}/export",
            "{version}/publish",
            "{version}/rollback",
            "{version}/deployment",
        });
    }
}
