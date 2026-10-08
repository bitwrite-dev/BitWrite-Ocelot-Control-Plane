using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

/// <summary>
/// One endpoint of a service, as the domain actually stores one.
/// </summary>
/// <remarks>
/// This used to be the route's <c>DownstreamTargetRequest</c>, which also carries a
/// scheme and a path. A <c>ServiceEndpoint</c> has host, port, weight and isActive and
/// nothing else, so the two fields were accepted and then dropped — and echoed back
/// as the invented constants "http" and "/". A caller could type a path, see it come
/// back as "/", and have no way to tell it was never stored.
/// <para>
/// Weight is here because the domain has it and uses it for load balancing, but it
/// appeared in neither the request nor the response: a caller could neither see what
/// an endpoint weighed nor change it.
/// </para>
/// </remarks>
public record ServiceEndpointRequest(
    [Required][MaxLength(200)] string Host,
    [Required][Range(1, 65535)] int Port,
    [Range(1, 1000)] int Weight = 1
);

/// <summary>
/// A service endpoint as stored, which is the same four fields and no more.
/// </summary>
public record ServiceEndpointResponse(
    string Host,
    int Port,
    int Weight,
    bool IsActive
);

public record CreateServiceRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    List<ServiceEndpointRequest>? DownstreamTargets
);

public record UpdateServiceRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description,
    List<ServiceEndpointRequest>? DownstreamTargets
);

public record ServiceResponse(
    string Id,
    string Name,
    string? Description,
    List<ServiceEndpointResponse> DownstreamTargets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ServiceListResponse(
    List<ServiceResponse> Services,
    int TotalCount,
    int Page,
    int PageSize
);

public record ServiceRoutesResponse(
    List<RouteResponse> Routes
);
