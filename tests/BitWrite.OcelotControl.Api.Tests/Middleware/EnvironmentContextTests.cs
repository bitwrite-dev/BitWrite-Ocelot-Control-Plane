using BitWrite.OcelotControl.Api.Middleware;
using BitWrite.OcelotControl.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Middleware;

/// <summary>
/// Where a scope's environment comes from: the request, or the configuration.
/// </summary>
/// <remarks>
/// Every repository builds its keys from this, so the two cases are the whole
/// contract: a request names the environment it operates on, and background work —
/// which has no request to ask — uses the one the installation is configured with.
/// The failure each guards against is silent in both directions, so each is asserted
/// here rather than left to whichever test happens to touch it.
/// </remarks>
public class EnvironmentContextTests
{
    private const string Configured = "production";

    [Fact]
    public void ARequestIsTheEnvironmentItNames()
    {
        var context = new RequestEnvironmentContext(HttpRequest(("X-Environment", "development")));

        context.Current.Value.Should().Be("development");
    }

    [Fact]
    public void TheNameIsNormalisedSoOneEnvironmentCannotBecomeTwo()
    {
        var context = new RequestEnvironmentContext(HttpRequest(("X-Environment", "  Production ")));

        context.Current.Value.Should().Be("production");
    }

    [Fact]
    public void ARequestThatNamesNoEnvironmentIsRejected()
    {
        // Answering with a configured default would show one environment's rows under
        // another environment's name, which reads as a populated list rather than as a
        // mistake — the exact failure environment isolation exists to prevent.
        var context = new RequestEnvironmentContext(HttpRequest());

        var act = () => _ = context.Current;

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("ENVIRONMENT_NOT_SELECTED");
    }

    [Fact]
    public void ARequestThatNamesABlankEnvironmentIsRejected()
    {
        var context = new RequestEnvironmentContext(HttpRequest(("X-Environment", "   ")));

        var act = () => _ = context.Current;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ARequestThatNamesSomethingUnusableAsAKeyIsRejected()
    {
        var context = new RequestEnvironmentContext(HttpRequest(("X-Environment", "prod:eu")));

        var act = () => _ = context.Current;

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("INVALID_ENVIRONMENT_NAME");
    }

    [Fact]
    public void BackgroundWorkUsesTheConfiguredEnvironment()
    {
        // No request to read a selection from, so the configured name is the only one
        // it can use. It is also what an operator sets to say which environment this
        // installation serves.
        var configured = EnvironmentContextRegistration.Configured(Configuration(("Environment", "Production")));

        configured.Value.Should().Be("production");
    }

    [Fact]
    public void BackgroundWorkIsToldWhenNoEnvironmentIsConfigured()
    {
        // Said once, by whoever asks, rather than by defaulting: a default here would
        // point a production deployment at development keys without anyone deciding it.
        var act = () => EnvironmentContextRegistration.Configured(Configuration());

        act.Should().Throw<DomainException>()
            .Which.Message.Should().Contain("background work has no environment");
    }

    [Fact]
    public void ARequestIsNotAnsweredFromTheConfiguredEnvironment()
    {
        // Stated as its own test because it is the rule that is easiest to reintroduce
        // by accident: adding the configured name as a fallback looks harmless and turns
        // every client that forgot the header into a client reading the wrong data.
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(Configuration(("Environment", Configured)))
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = HttpRequest() })
            .BuildServiceProvider();

        var context = EnvironmentContextRegistration.From(services);

        var act = () => _ = context.Current;

        act.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("ENVIRONMENT_NOT_SELECTED");
    }

    [Fact]
    public void ARequestTheRequestNamedWins()
    {
        // The request names an environment on every call and configuration is only for
        // background work, so "switch environment" stays a header change.
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(Configuration(("Environment", Configured)))
            .AddSingleton<IHttpContextAccessor>(
                new HttpContextAccessor { HttpContext = HttpRequest(("X-Environment", "staging")) })
            .BuildServiceProvider();

        EnvironmentContextRegistration.From(services).Current.Value.Should().Be("staging");
    }

    private static HttpContext HttpRequest(params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();

        foreach (var (name, value) in headers)
            context.Request.Headers[name] = value;

        return context;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(setting =>
                new KeyValuePair<string, string?>(setting.Key, setting.Value)))
            .Build();
}
