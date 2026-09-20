using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.License;

public record LicenseResponse(
    LicenseId Id,
    string Name,
    string ProductCode,
    LicenseStatus Status,
    DateTimeOffset ExpirationDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RevokedAt,
    string? RevocationReason,
    int MaxGateways,
    int MaxRoutes,
    IReadOnlyList<string> FeatureKeys
);

public record LicenseListResponse(
    IReadOnlyList<LicenseResponse> Licenses,
    int TotalCount,
    int Page,
    int PageSize
);

public record ActivateLicenseResponse(
    LicenseId Id,
    string ProductCode,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ExpiresAt
);