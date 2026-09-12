using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements IOcelotCapabilityResolver using Domain.OcelotCapabilityResolver.
/// </summary>
public class OcelotCapabilityResolverAdapter : IOcelotCapabilityResolver
{
    private readonly OcelotCapabilityResolver _domainResolver;

    public OcelotCapabilityResolverAdapter(OcelotCapabilityResolver domainResolver)
    {
        _domainResolver = domainResolver;
    }

    public bool IsCapabilitySupported(string capabilityKey, OcelotVersion ocelotVersion)
    {
        return _domainResolver.IsCapabilitySupported(capabilityKey, ocelotVersion);
    }

    public IReadOnlyList<CapabilityKey> GetSupportedCapabilities(OcelotVersion ocelotVersion)
    {
        return _domainResolver.GetSupportedCapabilities(ocelotVersion);
    }

    public IReadOnlyList<CapabilityKey> ResolveEffectiveCapabilities(
        OcelotVersion ocelotVersion,
        IReadOnlyList<string> enabledPluginCapabilities)
    {
        return _domainResolver.ResolveEffectiveCapabilities(ocelotVersion, enabledPluginCapabilities);
    }
}