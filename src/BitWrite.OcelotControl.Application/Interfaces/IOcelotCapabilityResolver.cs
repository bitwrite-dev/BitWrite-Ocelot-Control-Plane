using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IOcelotCapabilityResolver
{
    bool IsCapabilitySupported(string capabilityKey, OcelotVersion ocelotVersion);
    IReadOnlyList<CapabilityKey> GetSupportedCapabilities(OcelotVersion ocelotVersion);
    IReadOnlyList<CapabilityKey> ResolveEffectiveCapabilities(
        OcelotVersion ocelotVersion,
        IReadOnlyList<string> enabledPluginCapabilities);
}