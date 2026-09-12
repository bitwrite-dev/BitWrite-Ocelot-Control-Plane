using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements ISnapshotVersionAllocator using Domain.SnapshotVersionAllocator.
/// </summary>
public class SnapshotVersionAllocatorAdapter : ISnapshotVersionAllocator
{
    private readonly SnapshotVersionAllocator _domainAllocator;

    public SnapshotVersionAllocatorAdapter(SnapshotVersionAllocator domainAllocator)
    {
        _domainAllocator = domainAllocator;
    }

    public SnapshotVersion CurrentVersion => _domainAllocator.CurrentVersion;

    public SnapshotVersion AllocateNext()
    {
        return _domainAllocator.AllocateNext();
    }

    public SnapshotVersion AllocateSpecific(SnapshotVersion version)
    {
        return _domainAllocator.AllocateSpecific(version);
    }

    public void ResetTo(SnapshotVersion version)
    {
        _domainAllocator.ResetTo(version);
    }
}