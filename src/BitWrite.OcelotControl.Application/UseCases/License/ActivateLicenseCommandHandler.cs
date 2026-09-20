using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.License;
using DomainLicense = BitWrite.OcelotControl.Domain.Aggregates.License.License;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.License;

public class ActivateLicenseCommandHandler
{
    private readonly ILicenseRepository _licenseRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ActivateLicenseCommandHandler(
        ILicenseRepository licenseRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _licenseRepository = licenseRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ActivateLicenseResponse?> HandleAsync(ActivateLicenseCommand command, CancellationToken cancellationToken = default)
    {
        var license = await _licenseRepository.GetByProductCodeAsync(command.LicenseKey, cancellationToken);
        if (license == null)
            return null;

        license.Activate();

        await _licenseRepository.UpdateAsync(license, cancellationToken);

        foreach (var domainEvent in license.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        license.ClearDomainEvents();

        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "ActivateLicense",
            "License",
            license.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return new ActivateLicenseResponse(
            license.Id,
            license.ProductCode,
            license.ActivatedAt!.Value,
            license.ExpirationDate
        );
    }
}