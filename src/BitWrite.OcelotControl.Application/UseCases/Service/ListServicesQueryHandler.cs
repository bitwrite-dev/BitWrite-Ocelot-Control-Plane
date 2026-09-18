using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public class ListServicesQueryHandler
{
    private readonly IServiceRepository _serviceRepository;

    public ListServicesQueryHandler(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task<ServiceListResponse> HandleAsync(ListServicesQuery query, CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllAsync(cancellationToken);

        var totalCount = services.Count;
        var pagedServices = services
            .OrderBy(s => s.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new ServiceListResponse(
            pagedServices.Select(MapToResponse).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
    }

    private static ServiceResponse MapToResponse(DomainService service)
    {
        return new ServiceResponse(
            service.Id,
            service.Name,
            service.Description,
            service.Endpoints.Select(e => new ServiceEndpoint(e.Host, e.Port, e.Weight, e.IsActive)).ToList(),
            service.CreatedAt,
            service.UpdatedAt
        );
    }
}