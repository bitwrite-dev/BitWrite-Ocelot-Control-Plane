using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.License;
using DomainLicense = BitWrite.OcelotControl.Domain.Aggregates.License.License;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.License;

public class RevokeLicenseCommandHandler
{
    private readonly ILicenseRepository _licenseRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RevokeLicenseCommandHandler(
        ILicenseRepository licenseRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _licenseRepository = licenseRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<LicenseResponse?> HandleAsync(RevokeLicenseCommand command, CancellationToken cancellationToken = default)
    {
        var license = await _licenseRepository.GetAsync(command.Id, cancellationToken);
        if (license == null)
            return null;

        license.Revoke(command.Reason);

        await _licenseRepository.UpdateAsync(license, cancellationToken);

        foreach (var domainEvent in license.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        license.ClearDomainEvents();

        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "RevokeLicense",
            "License",
            license.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(license);
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