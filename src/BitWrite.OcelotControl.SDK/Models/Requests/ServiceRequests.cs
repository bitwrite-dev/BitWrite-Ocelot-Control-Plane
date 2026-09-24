using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Requests;

public record CreateServiceRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    List<DownstreamTargetRequest>? DownstreamTargets
);

public record UpdateServiceRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description,
    List<DownstreamTargetRequest>? DownstreamTargets
);