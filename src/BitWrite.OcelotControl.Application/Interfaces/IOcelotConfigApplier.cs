using BitWrite.OcelotControl.Application.Interfaces;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IOcelotConfigApplier
{
    Task ApplyAsync(string configuration, CancellationToken cancellationToken = default);
}