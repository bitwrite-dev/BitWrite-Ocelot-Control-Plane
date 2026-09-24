using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.License;

public record CreateLicenseCommand(
    string Name,
    string ProductCode,
    DateTimeOffset ExpirationDate,
    int MaxGateways = 1,
    int MaxRoutes = 10,
    string? Description = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record GetLicenseQuery(
    LicenseId Id
);

public record ListLicensesQuery(
    int Page = 1,
    int PageSize = 20,
    LicenseStatus? Status = null
);

public record ActivateLicenseCommand(
    string LicenseKey,
    string ActivatedBy,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record UpdateLicenseCommand(
    LicenseId Id,
    string? Name = null,
    string? Description = null,
    DateTimeOffset? ExpirationDate = null,
    int? MaxGateways = null,
    int? MaxRoutes = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record RenewLicenseCommand(
    LicenseId Id,
    DateTimeOffset NewExpirationDate,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record RevokeLicenseCommand(
    LicenseId Id,
    string Reason,
    string InitiatedBy = "",
    string CorrelationId = ""
);