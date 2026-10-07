using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Exceptions;
using EnvironmentName = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.EnvironmentName;

namespace BitWrite.OcelotControl.Api.Middleware;

/// <summary>
/// The environment the current request selected, read off that request.
/// </summary>
/// <remarks>
/// The dashboard sends <c>X-Environment</c> on every request. Reading it here,
/// once, is what keeps the selection consistent across a request that touches
/// routes, services and snapshots together — and what makes "switch environment"
/// a header change rather than a parameter on 29 use cases.
///
/// A request that names no environment is rejected rather than served a
/// configured default. The configured name is the operator's statement about
/// background work (see <see cref="EnvironmentContextRegistration"/>); answering
/// a request with it would show one environment's rows under another environment's
/// name, which reads as a populated list rather than as a mistake.
/// </remarks>
public sealed class RequestEnvironmentContext : IEnvironmentContext
{
    public const string HeaderName = "X-Environment";

    private readonly HttpContext _http;
    private EnvironmentName? _current;

    public RequestEnvironmentContext(HttpContext http)
    {
        _http = http;
    }

    public EnvironmentName Current => _current ??= Resolve();

    private EnvironmentName Resolve()
    {
        var requested = _http.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(requested))
            throw new DomainException(
                $"No environment selected. Send the {HeaderName} header naming one.",
                "ENVIRONMENT_NOT_SELECTED");

        return EnvironmentName.From(requested);
    }
}