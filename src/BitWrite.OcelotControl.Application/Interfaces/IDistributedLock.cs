namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IDistributedLock
{
    Task<LockResult> AcquireAsync(string resource, TimeSpan expiry, TimeSpan? waitTimeout = null, TimeSpan? retryInterval = null, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string resource, string lockId, CancellationToken cancellationToken = default);
}

public record LockResult(
    bool Success,
    string? LockId,
    string? ErrorMessage
);