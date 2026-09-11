using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface ISnapshotVersionAllocator
{
    SnapshotVersion CurrentVersion { get; }
    SnapshotVersion AllocateNext();
    SnapshotVersion AllocateSpecific(SnapshotVersion version);
    void ResetTo(SnapshotVersion version);
}