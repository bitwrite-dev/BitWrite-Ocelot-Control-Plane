using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements ISnapshotIntegrityVerifier using Domain.SnapshotIntegrityVerifier.
/// </summary>
public class SnapshotIntegrityVerifierAdapter : ISnapshotIntegrityVerifier
{
    private readonly SnapshotIntegrityVerifier _domainVerifier;

    public SnapshotIntegrityVerifierAdapter(SnapshotIntegrityVerifier domainVerifier)
    {
        _domainVerifier = domainVerifier;
    }

    public bool VerifyHash(string snapshotContent, ConfigurationHash expectedHash)
    {
        return _domainVerifier.VerifyHash(snapshotContent, expectedHash);
    }

    public ConfigurationHash ComputeHash(string snapshotContent)
    {
        return _domainVerifier.ComputeHash(snapshotContent);
    }

    public bool VerifyImmutability(string snapshotContent, ConfigurationHash originalHash)
    {
        return _domainVerifier.VerifyImmutability(snapshotContent, originalHash);
    }

    public void ValidateIntegrity(string snapshotContent, ConfigurationHash expectedHash, SnapshotVersion version)
    {
        _domainVerifier.ValidateIntegrity(snapshotContent, expectedHash, version);
    }
}