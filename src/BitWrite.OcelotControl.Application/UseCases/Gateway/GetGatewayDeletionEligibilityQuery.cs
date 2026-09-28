using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

/// <summary>
/// Whether a gateway has ever received a publication, which is what decides
/// whether it can be deleted.
/// </summary>
public record GetGatewayDeletionEligibilityQuery(
    GatewayId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

/// <summary>
/// A gateway and whether it can be removed.
/// </summary>
/// <remarks>
/// The delete endpoint refuses a gateway with publication history, because a
/// published snapshot is an immutable hashed record that refers to it. Without
/// this the refusal is the first time the operator hears the rule — after they
/// have already confirmed a deletion. The Services page gets the same treatment
/// for its dependent routes.
/// </remarks>
public class GetGatewayDeletionEligibilityQueryHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IPublicationRepository _publicationRepository;

    public GetGatewayDeletionEligibilityQueryHandler(
        IGatewayRepository gatewayRepository,
        IPublicationRepository publicationRepository)
    {
        _gatewayRepository = gatewayRepository;
        _publicationRepository = publicationRepository;
    }

    public async Task<GatewayDeletionEligibilityResponse> HandleAsync(
        GetGatewayDeletionEligibilityQuery query,
        CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetAsync(query.Id, cancellationToken);
        if (gateway == null)
            throw new KeyNotFoundException($"Gateway {query.Id} not found");

        var hasBeenPublishedTo = await _publicationRepository.HasGatewayBeenPublishedToAsync(
            query.Id,
            cancellationToken);

        return new GatewayDeletionEligibilityResponse(
            query.Id.Value.ToString(),
            hasBeenPublishedTo,
            hasBeenPublishedTo
                ? "This gateway has received a publication. A published snapshot is an " +
                  "immutable, hashed record that refers to it, so it cannot be removed."
                : null);
    }
}

public record GatewayDeletionEligibilityResponse(
    string GatewayId,
    bool HasBeenPublishedTo,
    /// Why deletion is refused, or null when it is allowed.
    string? Reason);
