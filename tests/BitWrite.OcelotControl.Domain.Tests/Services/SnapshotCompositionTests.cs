using BitWrite.OcelotControl.Domain.Services;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

/// <summary>
/// Reading a snapshot's composition out of its content.
///
/// The counts are reporting rather than correctness, so a document that cannot be
/// read has to yield nulls and not an exception: failing to load the whole list
/// because one row is malformed is worse than showing a dash.
/// </summary>
public class SnapshotCompositionTests
{
    private const string Document = """
    {
      "Routes": [
        { "DownstreamPathTemplate": "/a", "DownstreamHttpMethod": null },
        { "DownstreamPathTemplate": "/b", "DownstreamHttpMethod": "POST" }
      ],
      "Services": [
        { "DownstreamHostAndPorts": [] }
      ],
      "PluginConfigurations": [
        { "Name": "RateLimiting", "Version": "2.1.0" },
        { "Name": "Tracing", "Version": "1.4.0" }
      ]
    }
    """;

    [Fact]
    public void CountsTheRoutesInTheDocument()
    {
        SnapshotComposition.RouteCount(Document).Should().Be(2);
    }

    [Fact]
    public void CountsTheServicesInTheDocument()
    {
        SnapshotComposition.ServiceCount(Document).Should().Be(1);
    }

    [Fact]
    public void ReportsAnEmptySectionAsZeroRatherThanUnknown()
    {
        // Zero routes is a real answer; null would mean "could not tell".
        SnapshotComposition.RouteCount("""{ "Routes": [], "Services": [] }""").Should().Be(0);
    }

    [Fact]
    public void ListsThePluginVersionsWithTheirNames()
    {
        SnapshotComposition.PluginVersions(Document)
            .Should().BeEquivalentTo(new[] { "RateLimiting 2.1.0", "Tracing 1.4.0" });
    }

    [Fact]
    public void FallsBackToTheVersionWhenAPluginHasNoName()
    {
        SnapshotComposition.PluginVersions("""{ "PluginConfigurations": [ { "Version": "1.0.0" } ] }""")
            .Should().Equal("1.0.0");
    }

    [Fact]
    public void ReportsNoPluginsRatherThanAnUnknownWhenThereAreNone()
    {
        // A deployment without plugins is an empty list, which is different from
        // a document that could not be read.
        SnapshotComposition.PluginVersions("""{ "Routes": [] }""").Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ReportsNothingForAbsentContent(string? content)
    {
        SnapshotComposition.RouteCount(content).Should().BeNull();
        SnapshotComposition.ServiceCount(content).Should().BeNull();
        SnapshotComposition.PluginVersions(content).Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ truncated")]
    public void ReportsNothingForContentThatIsNotJson(string content)
    {
        // Reporting, not correctness: a dash beats taking the list down.
        SnapshotComposition.RouteCount(content).Should().BeNull();
        SnapshotComposition.PluginVersions(content).Should().BeEmpty();
    }

    [Fact]
    public void ReportsNothingForAJsonDocumentThatIsNotAnObject()
    {
        SnapshotComposition.RouteCount("[]").Should().BeNull();
        SnapshotComposition.ServiceCount("\"a string\"").Should().BeNull();
    }

    [Fact]
    public void ReportsNothingWhenTheSectionIsTheWrongKind()
    {
        SnapshotComposition.RouteCount("""{ "Routes": "not an array" }""").Should().BeNull();
    }

    [Fact]
    public void FindsTheSectionsWhateverTheirCasing()
    {
        // The document is written in more than one casing across the code paths
        // that produce it, and a count that vanishes over a capital letter is
        // worse than looking the name up.
        SnapshotComposition.RouteCount("""{ "routes": [ {}, {} ] }""").Should().Be(2);
        SnapshotComposition.ServiceCount("""{ "Services": [ {} ] }""").Should().Be(1);
    }

    [Fact]
    public void AcceptsADocumentWithTrailingCommasAndComments()
    {
        // Content pasted in and hand-edited is a real source of these.
        var messy = """
        {
          // the generated document
          "Routes": [ {}, ],
        }
        """;

        SnapshotComposition.RouteCount(messy).Should().Be(1);
    }

    [Fact]
    public void SkipsAPluginEntryWithNeitherNameNorVersion()
    {
        SnapshotComposition.PluginVersions("""{ "PluginConfigurations": [ { "Enabled": true } ] }""")
            .Should().BeEmpty();
    }
}
