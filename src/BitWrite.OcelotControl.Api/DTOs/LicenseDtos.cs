using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record ActivateLicenseRequest(
    [Required][MaxLength(100)] string LicenseKey,
    [Required][MaxLength(200)] string ActivatedBy
);

public record CreateLicenseRequest(
    [Required][MaxLength(100)] string Name,
    [Required][MaxLength(50)] string ProductCode,
    [Required] DateTimeOffset ExpirationDate,
    int MaxGateways = 1,
    int MaxRoutes = 10,
    string InitiatedBy = ""
);

public record UpdateLicenseRequest(
    [MaxLength(100)] string? Name = null,
    [MaxLength(500)] string? Description = null
);

public record RenewLicenseRequest(
    [Required] DateTimeOffset NewExpirationDate
);

public record RevokeLicenseRequest(
    [Required][MaxLength(500)] string Reason
);

public record LicenseResponse(
    string Id,
    string Name,
    string ProductCode,
    string Status,
    DateTimeOffset ExpirationDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsActive,
    string ActivatedAt
);

public record LicenseListResponse(
    List<LicenseResponse> Licenses,
    int TotalCount,
    int Page,
    int PageSize
);
