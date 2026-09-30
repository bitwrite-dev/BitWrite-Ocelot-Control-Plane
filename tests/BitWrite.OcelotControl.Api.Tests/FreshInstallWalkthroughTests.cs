using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests;

/// <summary>
/// A fresh install, taken from nothing to able to create a snapshot.
/// </summary>
/// <remarks>
/// This is the sequence that was broken, and it is easy to break again: every
/// step can pass its own test while the walk between them does not. Nothing
/// connected <c>POST /settings/first-run</c> to <c>POST /snapshots/preview</c>,
/// so a fresh install answered 401 at the first step and then refused to build
/// anything at the second, pointing at a setup screen it could not reach.
///
/// It goes through the real HTTP pipeline rather than calling handlers, because the
/// failure was an authorisation attribute — invisible to a handler-level test.
///
/// Settings live in a Redis shared with the developer's own run, so these cannot
/// assume a virgin install: each one completes setup first, or reads the current
/// state, rather than depending on what the test before it left behind. The class
/// is disabled where that sharing would make a result meaningless.
/// </remarks>
[Collection("fresh-install")]
public class FreshInstallWalkthroughTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FreshInstallWalkthroughTests(WebApplicationFactory<Program> factory) =>
        _factory = factory;

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    /// <summary>
    /// Completes setup, so a test starts from a state it arranged itself.
    /// </summary>
    private async Task CompleteSetup(HttpClient client)
    {
        var existing = await ReadJson(client, HttpMethod.Get, "/api/v1/settings");
        if (!existing.GetProperty("isInitialised").GetBoolean())
        {
            await ReadJson(client, HttpMethod.Post, "/api/v1/settings/first-run",
                new { ocelotVersion = "18.0.0", initiatedBy = "test", snapshotRetentionCount = 10 });
        }
    }

    [Fact]
    public async Task AnUnconfiguredInstallCanChooseItsVersionAndThenBuildASnapshot()
    {
        using var client = CreateClient();
        await CompleteSetup(client);

        // 1. Setup completed, and the install says so.
        var chosen = await ReadJson(client, HttpMethod.Get, "/api/v1/settings");
        chosen.GetProperty("isInitialised").GetBoolean().Should().BeTrue();
        chosen.GetProperty("ocelotVersion").GetString().Should().Be("18.0.0");

        // 3. And a snapshot can now be previewed, which is the thing that was
        //    refused with "Complete first-run setup" and a null reference before.
        var preview = await ReadJson(client, HttpMethod.Post, "/api/v1/snapshots/preview");
        preview.GetProperty("ocelotVersion").GetString().Should().Contain("18");
        preview.GetProperty("content").ValueKind.Should().Be(
            JsonValueKind.String,
            "a configured install can resolve an artifact");
    }

    [Fact]
    public async Task SetupRecordsWhoChoseTheVersion()
    {
        using var client = CreateClient();
        await CompleteSetup(client);

        // The choice is permanent, so the audit trail has to be able to say who
        // made it — and with no session there is nothing else to read.
        var chosen = await ReadJson(client, HttpMethod.Get, "/api/v1/settings");

        chosen.GetProperty("ocelotVersionSelectedBy").ValueKind
            .Should().Be(JsonValueKind.String, "a setup call always records someone");
        chosen.GetProperty("ocelotVersionSelectedAt").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task SetupRefusesAVersionTheProductCannotEmit()
    {
        using var client = CreateClient();
        await CompleteSetup(client);

        // Offering 20.0 in the setup screen would promise a configuration the
        // builder refuses to generate: setup would complete and then every
        // snapshot would fail.
        var response = await client.PostAsJsonAsync("/api/v1/settings/first-run",
            new { ocelotVersion = "20.0.0", initiatedBy = "operator" });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task SetupRefusesAVersionItDoesNotRecognise()
    {
        using var client = CreateClient();
        await CompleteSetup(client);

        var response = await client.PostAsJsonAsync("/api/v1/settings/first-run",
            new { ocelotVersion = "17.0.0", initiatedBy = "operator" });

        // It is refused, which is the part that matters here. The status code is
        // 500 rather than the 400 this deserves because GlobalExceptionMiddleware
        // does not map DomainException at all, and discards its message on the way
        // — that is #504, a separate bug, and fixing the mapping inside a setup
        // test would hide it in the one place it is least likely to be noticed.
        response.IsSuccessStatusCode.Should().BeFalse(
            "a version the product cannot emit must not be accepted");
    }

    [Fact]
    public async Task TheSettingsScreenIsToldWhichVersionsItCanOffer()
    {
        using var client = CreateClient();

        // The setup screen offers exactly this list, so it has to come from the
        // product rather than being typed into the page.
        var settings = await ReadJson(client, HttpMethod.Get, "/api/v1/settings");

        var available = settings.GetProperty("availableOcelotVersions")
            .EnumerateArray()
            .Select(version => version.GetString())
            .ToList();

        available.Should().NotBeEmpty();
        available.Should().OnlyContain(version => version == "18.0.0");
    }

    private static async Task<JsonElement> ReadJson(
        HttpClient client,
        HttpMethod method,
        string path,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        var response = await client.SendAsync(request);

        // The response is read on the failure path too, because the body is where
        // the reason lives — a bare status code is what sent this bug looking
        // like a server fault rather than a rejected configuration.
        var json = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(
            $"{method} {path} answered {(int)response.StatusCode}: {json}");

        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
