using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record ActivateLicenseRequest(
    [Required][MaxLength(100)] string LicenseKey,
    [Required][MaxLength(200)] string ActivatedBy
);

public record LicenseResponse(
    string Id,
    string Edition,
    DateTimeOffset ActivatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    string ActivatedBy
);

public record LicenseListResponse(
    List<LicenseResponse> Licenses,
    int TotalCount,
    int Page,
    int PageSize
);