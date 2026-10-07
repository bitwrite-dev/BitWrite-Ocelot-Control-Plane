using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Exceptions;
using EnvironmentName = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.EnvironmentName;

namespace BitWrite.OcelotControl.Api.Middleware;

/// <summary>
/// Picks where a scope's environment comes from: the request, or the configuration.
/// </summary>
/// <remarks>
/// Repositories resolve the environment through <see cref="IEnvironmentContext"/>,
/// and they are resolved from two kinds of scope. A request scope reads the
/// environment the caller selected; a background scope — the runtime adapter that
/// applies a published snapshot, for instance — has no request to read a selection
/// from, so it uses the one this installation is configured with.
///
/// The distinction is the presence of an <see cref="HttpContext"/>, not a second
/// registration: one service, resolved per scope, so a repository cannot be handed
/// an environment from the wrong source.
/// </remarks>
public static class EnvironmentContextRegistration
{
    /// <summary>Configuration key naming the environment background work operates on.</summary>
    public const string ConfigurationKey = "Environment";

    /// <summary>Builds the context for the scope being resolved.</summary>
    public static IEnvironmentContext From(IServiceProvider services)
    {
        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;

        return http is not null
            ? new RequestEnvironmentContext(http)
            : new FixedEnvironmentContext(Configured(services.GetRequiredService<IConfiguration>()));
    }

    /// <summary>
    /// The environment this installation is configured to operate on.
    /// </summary>
    /// <remarks>
    /// Deliberately not defaulted from <c>ASPNETCORE_ENVIRONMENT</c>. That names
    /// how the process is deployed, not which environment's configuration it is
    /// responsible for, and on a host that sets it to "Production" by omission the
    /// background worker would then read production keys on a development machine.
    /// </remarks>
    public static EnvironmentName Configured(IConfiguration configuration)
    {
        var configured = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
            throw new DomainException(
                $"There is no request to read an environment from and no '{ConfigurationKey}' configured, " +
                "so background work has no environment to use. Set it to the environment this installation serves.",
                "ENVIRONMENT_NOT_SELECTED");

        return EnvironmentName.From(configured);
    }
}