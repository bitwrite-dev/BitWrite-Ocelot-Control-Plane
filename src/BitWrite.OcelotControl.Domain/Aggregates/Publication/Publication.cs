using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.Publication;

/// <summary>
/// Publication Aggregate Root - Snapshot Deployment Operation (§9A.2)
/// Represents the deployment of a snapshot to one or more gateways.
/// </summary>
public class Publication
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly Dictionary<GatewayId, GatewayDeploymentState> _gatewayStates = new();

    public PublicationId Id { get; private set; }
    public SnapshotVersion SnapshotVersion { get; private set; }
    public PublicationStatus Status { get; private set; } = PublicationStatus.Pending;
    public string InitiatedBy { get; private set; } = string.Empty;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public IReadOnlyDictionary<GatewayId, GatewayDeploymentState> GatewayStates => _gatewayStates;
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Publication() { }

    /// <summary>
    /// Factory method to start a new publication.
    /// </summary>
    public static Publication Start(
        SnapshotVersion snapshotVersion,
        string initiatedBy,
        IReadOnlyList<GatewayId> targetGateways,
        string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(initiatedBy))
            throw new DomainException("Initiated by cannot be empty", "INVALID_INITIATED_BY");

        if (targetGateways == null || targetGateways.Count == 0)
            throw new DomainException("Publication must have at least one target gateway", "NO_TARGET_GATEWAYS");

        var publication = new Publication
        {
            Id = PublicationId.New(),
            SnapshotVersion = snapshotVersion,
            Status = PublicationStatus.Pending,
            InitiatedBy = initiatedBy.Trim(),
            StartedAt = DateTimeOffset.UtcNow
        };

        foreach (var gatewayId in targetGateways)
        {
            publication._gatewayStates[gatewayId] = new GatewayDeploymentState
            {
                GatewayId = gatewayId,
                Status = "Pending"
            };
        }

        publication.AddDomainEvent(new PublicationStarted(publication.Id, snapshotVersion));
        return publication;
    }

    /// <summary>
    /// Marks a gateway as having received the configuration.
    /// </summary>
    public void RecordGatewayReceived(GatewayId gatewayId, string correlationId = "")
    {
        EnsureGatewayExists(gatewayId);
        EnsurePublicationInProgress();

        _gatewayStates[gatewayId].Status = "Received";
        _gatewayStates[gatewayId].ReceivedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GatewayConfigurationApplied(gatewayId, SnapshotVersion));
    }

    /// <summary>
    /// Marks a gateway as having validated the configuration.
    /// </summary>
    public void RecordGatewayValidated(GatewayId gatewayId, bool isValid, string correlationId = "")
    {
        EnsureGatewayExists(gatewayId);
        EnsurePublicationInProgress();

        _gatewayStates[gatewayId].Status = isValid ? "Validated" : "ValidationFailed";
        _gatewayStates[gatewayId].ValidatedAt = DateTimeOffset.UtcNow;
        _gatewayStates[gatewayId].IsValid = isValid;
    }

    /// <summary>
    /// Marks a gateway as having applied the configuration.
    /// </summary>
    public void RecordGatewayApplied(GatewayId gatewayId, string correlationId = "")
    {
        EnsureGatewayExists(gatewayId);
        EnsurePublicationInProgress();

        _gatewayStates[gatewayId].Status = "Applied";
        _gatewayStates[gatewayId].AppliedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks a gateway as healthy after applying.
    /// </summary>
    public void RecordGatewayHealthy(GatewayId gatewayId, string correlationId = "")
    {
        EnsureGatewayExists(gatewayId);

        _gatewayStates[gatewayId].Status = "Healthy";
        _gatewayStates[gatewayId].HealthyAt = DateTimeOffset.UtcNow;

        CheckCompletion();
    }

    /// <summary>
    /// Marks a gateway deployment as failed.
    /// </summary>
    public void RecordGatewayFailed(GatewayId gatewayId, string reason, string correlationId = "")
    {
        EnsureGatewayExists(gatewayId);

        _gatewayStates[gatewayId].Status = "Failed";
        _gatewayStates[gatewayId].FailedAt = DateTimeOffset.UtcNow;
        _gatewayStates[gatewayId].FailureReason = reason;

        Status = PublicationStatus.Failed;
        FailureReason = $"Gateway {gatewayId} failed: {reason}";
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PublicationFailed(Id, FailureReason));
    }

    /// <summary>
    /// Marks the publication as completed.
    /// </summary>
    public void Complete(string correlationId = "")
    {
        if (Status != PublicationStatus.Pending)
            throw new DomainException($"Cannot complete publication in {Status} status", "INVALID_PUBLICATION_STATUS");

        Status = PublicationStatus.Published;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PublicationCompleted(Id));
    }

    /// <summary>
    /// Rolls back the publication.
    /// </summary>
    public void Rollback(SnapshotVersion targetVersion, string reason, string correlationId = "")
    {
        if (Status == PublicationStatus.RolledBack)
            throw new DomainException("Publication is already rolled back", "ALREADY_ROLLED_BACK");

        Status = PublicationStatus.RolledBack;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PublicationRolledBack(Id, targetVersion));
    }

    /// <summary>
    /// Checks if all gateways have received the configuration.
    /// </summary>
    public bool AllGatewaysReceived => _gatewayStates.Values.All(s => s.Status == "Received" || s.Status == "Validated" || s.Status == "Applied" || s.Status == "Healthy");

    /// <summary>
    /// Checks if all gateways are healthy.
    /// </summary>
    public bool AllGatewaysHealthy => _gatewayStates.Values.All(s => s.Status == "Healthy");

    /// <summary>
    /// Gets the deployment progress percentage.
    /// </summary>
    public double ProgressPercentage
    {
        get
        {
            if (_gatewayStates.Count == 0) return 0;
            var completed = _gatewayStates.Values.Count(s => s.Status == "Healthy");
            return (double)completed / _gatewayStates.Count * 100;
        }
    }

    private void CheckCompletion()
    {
        if (AllGatewaysHealthy && Status == PublicationStatus.Pending)
        {
            Complete();
        }
    }

    private void EnsureGatewayExists(GatewayId gatewayId)
    {
        if (!_gatewayStates.ContainsKey(gatewayId))
            throw new DomainException($"Gateway {gatewayId} is not a target of this publication", "GATEWAY_NOT_TARGET");
    }

    private void EnsurePublicationInProgress()
    {
        if (Status != PublicationStatus.Pending)
            throw new DomainException($"Publication is not in progress (status: {Status})", "PUBLICATION_NOT_IN_PROGRESS");
    }

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Gateway deployment state entity.
/// </summary>
public class GatewayDeploymentState
{
    public GatewayId GatewayId { get; init; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset? HealthyAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public bool? IsValid { get; set; }
    public string? FailureReason { get; set; }
}