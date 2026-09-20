using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.License;
using DomainLicense = BitWrite.OcelotControl.Domain.Aggregates.License.License;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.License;

public class GetLicenseQueryHandler
{
    private readonly ILicenseRepository _licenseRepository;

    public GetLicenseQueryHandler(ILicenseRepository licenseRepository)
    {
        _licenseRepository = licenseRepository;
    }

    public async Task<LicenseResponse?> HandleAsync(GetLicenseQuery query, CancellationToken cancellationToken = default)
    {
        var license = await _licenseRepository.GetAsync(query.Id, cancellationToken);
        return license != null ? MapToResponse(license) : null;
    }

    private static LicenseResponse MapToResponse(DomainLicense license)
    {
        return new LicenseResponse(
            license.Id,
            license.Name,
            license.ProductCode,
            license.Status,
            license.ExpirationDate,
            license.CreatedAt,
            license.UpdatedAt,
            license.ActivatedAt,
            license.RevokedAt,
            license.RevocationReason,
            license.MaxGateways,
            license.MaxRoutes,
            license.Features.Select(f => f.Key).ToList()
        );
    }
}