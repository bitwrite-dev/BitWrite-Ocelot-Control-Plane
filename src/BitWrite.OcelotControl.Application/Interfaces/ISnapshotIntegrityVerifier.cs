using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface ISnapshotIntegrityVerifier
{
    bool VerifyHash(string snapshotContent, ConfigurationHash expectedHash);
    ConfigurationHash ComputeHash(string snapshotContent);
    bool VerifyImmutability(string snapshotContent, ConfigurationHash originalHash);
    void ValidateIntegrity(string snapshotContent, ConfigurationHash expectedHash, SnapshotVersion version);
}