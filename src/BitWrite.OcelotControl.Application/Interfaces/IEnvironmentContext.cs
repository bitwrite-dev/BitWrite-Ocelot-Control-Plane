using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.Interfaces;

/// <summary>
/// Which environment the current operation applies to.
/// </summary>
/// <remarks>
/// Registered as scoped and set once per request, so that every repository call
/// in that request addresses the same environment without each use case having to
/// carry the name through its command and pass it down.
///
/// This is deliberately not a parameter default. A default such as "development"
/// would compile fine and then quietly address the wrong environment for anyone
/// who forgot it — which is the exact failure environment isolation exists to
/// prevent. Here there is nothing to forget: there is one value, set at the edge.
/// </remarks>
public interface IEnvironmentContext
{
    EnvironmentName Current { get; }
}

/// <summary>
/// Fixed environment, for the gateway and for background work that has no request
/// to read a selection from.
/// </summary>
public sealed class FixedEnvironmentContext : IEnvironmentContext
{
    public FixedEnvironmentContext(EnvironmentName environment)
    {
        Current = environment;
    }

    public EnvironmentName Current { get; }
}