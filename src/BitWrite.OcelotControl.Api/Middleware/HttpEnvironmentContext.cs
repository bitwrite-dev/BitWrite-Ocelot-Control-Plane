using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Exceptions;
using EnvironmentName = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.EnvironmentName;

namespace BitWrite.OcelotControl.Api.Middleware;

/// <summary>
/// Reads the selected environment off the request and makes it the one every
/// repository in that request will use.
/// </summary>
/// <remarks>
/// The dashboard's environment selector sends <c>X-Environment</c> on every
/// request. Reading it here, once, is what keeps the selection consistent across
/// a request that touches routes, services and snapshots together — and what makes
/// "switch environment" a header change rather than a parameter on 29 use cases.
///
/// A request that names no environment is rejected rather than served a default.
/// Serving a default would answer a production request with development routes,
/// which looks like an empty list rather than a mistake.
/// </remarks>
public sealed class HttpEnvironmentContext : IEnvironmentContext
{
    public const string HeaderName = "X-Environment";

    private EnvironmentName? _current;
    private readonly IHttpContextAccessor _accessor;
    private readonly IConfiguration _configuration;

    public HttpEnvironmentContext(IHttpContextAccessor accessor, IConfiguration configuration)
    {
        _accessor = accessor;
        _configuration = configuration;
    }

    public EnvironmentName Current => _current ??= Resolve();

    private EnvironmentName Resolve()
    {
        var requested = _accessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(requested))
            requested = _configuration["Environment"];

        if (string.IsNullOrWhiteSpace(requested))
            throw new DomainException(
                $"No environment selected. Send the {HeaderName} header, or configure a default.",
                "ENVIRONMENT_NOT_SELECTED");

        return EnvironmentName.From(requested);
    }
}