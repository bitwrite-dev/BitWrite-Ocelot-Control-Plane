using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public class GetServiceQueryHandler
{
    private readonly IServiceRepository _serviceRepository;

    public GetServiceQueryHandler(IServiceRepository serviceRepository)
    {
        _serviceRepository = serviceRepository;
    }

    public async Task<ServiceResponse?> HandleAsync(GetServiceQuery query, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetAsync(query.Id, cancellationToken);
        return service != null ? MapToResponse(service) : null;
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