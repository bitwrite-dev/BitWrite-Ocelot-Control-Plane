using BitWrite.OcelotControl.Domain.Aggregates.License;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface ILicenseRepository
{
    Task<License?> GetAsync(LicenseId id, CancellationToken cancellationToken = default);
    Task<License?> GetByProductCodeAsync(string productCode, CancellationToken cancellationToken = default);
    Task<List<License>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<License>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<List<License>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task AddAsync(License license, CancellationToken cancellationToken = default);
    Task UpdateAsync(License license, CancellationToken cancellationToken = default);
    Task DeleteAsync(LicenseId id, CancellationToken cancellationToken = default);
}