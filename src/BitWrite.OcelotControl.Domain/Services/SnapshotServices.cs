using System.Security.Cryptography;
using System.Text;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Verifies snapshot integrity (§1414).
/// Ensures snapshot content is immutable after creation and validates integrity.
/// </summary>
public class SnapshotIntegrityVerifier
{
    private readonly ConfigurationCanonicalizer _canonicalizer;

    public SnapshotIntegrityVerifier(ConfigurationCanonicalizer canonicalizer)
    {
        _canonicalizer = canonicalizer;
    }

    /// <summary>
    /// Verifies that a snapshot's hash matches its content.
    /// </summary>
    /// <param name="snapshotContent">The snapshot content to verify.</param>
    /// <param name="expectedHash">The expected configuration hash.</param>
    /// <returns>True if the hash is valid; otherwise false.</returns>
    public bool VerifyHash(string snapshotContent, ConfigurationHash expectedHash)
    {
        var computedHash = ComputeHash(snapshotContent);
        return computedHash == expectedHash;
    }

    /// <summary>
    /// Computes a hash for the snapshot content.
    /// </summary>
    public ConfigurationHash ComputeHash(string snapshotContent)
    {
        var bytes = Encoding.UTF8.GetBytes(snapshotContent);
        var hashBytes = SHA256.HashData(bytes);
        return ConfigurationHash.FromBytes(hashBytes);
    }

    /// <summary>
    /// Verifies that a snapshot is immutable (has not been modified since creation).
    /// </summary>
    /// <param name="snapshotContent">The current snapshot content.</param>
    /// <param name="originalHash">The hash at snapshot creation time.</param>
    /// <returns>True if the snapshot is immutable; otherwise false.</returns>
    public bool VerifyImmutability(string snapshotContent, ConfigurationHash originalHash)
    {
        return VerifyHash(snapshotContent, originalHash);
    }

    /// <summary>
    /// Validates snapshot integrity and throws if invalid.
    /// </summary>
    public void ValidateIntegrity(string snapshotContent, ConfigurationHash expectedHash, SnapshotVersion version)
    {
        if (!VerifyHash(snapshotContent, expectedHash))
        {
            throw new DomainException(
                $"Snapshot {version} integrity verification failed. Hash mismatch.",
                "SNAPSHOT_INTEGRITY_FAILED");
        }
    }
}

/// <summary>
/// Allocates snapshot versions (§9A.5).
/// This is a domain-level policy backed by infrastructure.
/// </summary>
public class SnapshotVersionAllocator
{
    private SnapshotVersion _currentVersion;
    private readonly object _lock = new();

    public SnapshotVersionAllocator(SnapshotVersion initialVersion)
    {
        _currentVersion = initialVersion;
    }

    public SnapshotVersionAllocator() : this(SnapshotVersion.First())
    {
    }

    /// <summary>
    /// Gets the current version.
    /// </summary>
    public SnapshotVersion CurrentVersion
    {
        get
        {
            lock (_lock)
            {
                return _currentVersion;
            }
        }
    }

    /// <summary>
    /// Allocates the next version number.
    /// </summary>
    /// <returns>The newly allocated version.</returns>
    public SnapshotVersion AllocateNext()
    {
        lock (_lock)
        {
            _currentVersion = _currentVersion.Next();
            return _currentVersion;
        }
    }

    /// <summary>
    /// Allocates a specific version (for rollback scenarios).
    /// </summary>
    /// <param name="version">The version to allocate.</param>
    /// <returns>The allocated version if valid; otherwise throws.</returns>
    public SnapshotVersion AllocateSpecific(SnapshotVersion version)
    {
        lock (_lock)
        {
            if (version <= _currentVersion)
            {
                throw new DomainException(
                    $"Cannot allocate version {version} as it is less than or equal to current version {_currentVersion}",
                    "INVALID_VERSION_ALLOCATION");
            }

            _currentVersion = version;
            return _currentVersion;
        }
    }

    /// <summary>
    /// Resets the allocator to a specific version (for testing or recovery).
    /// </summary>
    /// <param name="version">The version to reset to.</param>
    public void ResetTo(SnapshotVersion version)
    {
        lock (_lock)
        {
            _currentVersion = version;
        }
    }
}