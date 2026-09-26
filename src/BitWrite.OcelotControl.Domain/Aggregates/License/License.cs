using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.License;

/// <summary>
/// License Aggregate Root - License Management (§9A.2)
/// Represents a software license with expiration, features, and activation state.
/// </summary>
public class License
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<LicenseFeature> _features = new();

    public LicenseId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ProductCode { get; private set; } = string.Empty;
    public LicenseStatus Status { get; private set; } = LicenseStatus.Pending;
    public DateTimeOffset ExpirationDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? Description { get; private set; }
    public int MaxGateways { get; private set; }
    public int MaxRoutes { get; private set; }

    public IReadOnlyList<LicenseFeature> Features => _features.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private License() { }

    /// <summary>
    /// Factory method to create a new license.
    /// </summary>
    public static License Create(
        string name,
        string productCode,
        DateTimeOffset expirationDate,
        int maxGateways = 1,
        int maxRoutes = 10,
        IReadOnlyList<LicenseFeature>? features = null,
        string? description = null,
        string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("License name cannot be empty", "INVALID_LICENSE_NAME");

        if (string.IsNullOrWhiteSpace(productCode))
            throw new DomainException("Product code cannot be empty", "INVALID_PRODUCT_CODE");

        if (expirationDate <= DateTimeOffset.UtcNow)
            throw new DomainException("Expiration date must be in the future", "INVALID_EXPIRATION_DATE");

        if (maxGateways <= 0)
            throw new DomainException("Max gateways must be positive", "INVALID_MAX_GATEWAYS");

        if (maxRoutes <= 0)
            throw new DomainException("Max routes must be positive", "INVALID_MAX_ROUTES");

        var license = new License
        {
            Id = LicenseId.New(),
            Name = name.Trim(),
            ProductCode = productCode.Trim().ToUpperInvariant(),
            ExpirationDate = expirationDate,
            Status = LicenseStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            MaxGateways = maxGateways,
            MaxRoutes = maxRoutes,
            Description = description?.Trim()
        };

        if (features != null)
        {
            license._features.AddRange(features);
        }

        license.AddDomainEvent(new LicenseCreated(license.Id, license.Name, license.ProductCode, license.ExpirationDate));
        return license;
    }

    /// <summary>
    /// Reconstitutes a license from persisted state, preserving its identity,
    /// status, lifecycle timestamps and features.
    ///
    /// Infrastructure adapters must use this rather than <see cref="Create"/> for
    /// two reasons. Create mints a new <see cref="LicenseId"/> and resets the
    /// lifecycle state, so a read would change the id and lose activation. It
    /// also rejects an expiration date in the past, so listing licenses would
    /// throw as soon as one expired.
    ///
    /// Validation of *new* licenses still happens in <see cref="Create"/>; this
    /// path restores state that was already accepted once.
    /// </summary>
    public static License Reconstitute(
        LicenseId id,
        string name,
        string productCode,
        LicenseStatus status,
        DateTimeOffset expirationDate,
        int maxGateways,
        int maxRoutes,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? activatedAt = null,
        DateTimeOffset? revokedAt = null,
        string? revocationReason = null,
        string? description = null,
        IReadOnlyList<LicenseFeature>? features = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("License name cannot be empty", "INVALID_LICENSE_NAME");

        if (string.IsNullOrWhiteSpace(productCode))
            throw new DomainException("Product code cannot be empty", "INVALID_PRODUCT_CODE");

        var license = new License
        {
            Id = id,
            Name = name.Trim(),
            ProductCode = productCode.Trim(),
            Status = status,
            ExpirationDate = expirationDate,
            MaxGateways = maxGateways,
            MaxRoutes = maxRoutes,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ActivatedAt = activatedAt,
            RevokedAt = revokedAt,
            RevocationReason = revocationReason,
            Description = description?.Trim(),
        };

        if (features is not null)
        {
            license._features.AddRange(features);
        }

        return license;
    }

    /// <summary>
    /// Activates the license.
    /// </summary>
    public void Activate(string correlationId = "")
    {
        if (Status == LicenseStatus.Active)
            return;

        if (Status == LicenseStatus.Revoked)
            throw new DomainException("Cannot activate a revoked license", "LICENSE_REVOKED");

        if (Status == LicenseStatus.Expired)
            throw new DomainException("Cannot activate an expired license", "LICENSE_EXPIRED");

        if (DateTimeOffset.UtcNow > ExpirationDate)
        {
            Status = LicenseStatus.Expired;
            AddDomainEvent(new LicenseExpired(Id));
            return;
        }

        Status = LicenseStatus.Active;
        ActivatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new LicenseActivated(Id));
    }

    /// <summary>
    /// Renews the license with a new expiration date.
    /// </summary>
    public void Renew(DateTimeOffset newExpirationDate, string correlationId = "")
    {
        if (newExpirationDate <= DateTimeOffset.UtcNow)
            throw new DomainException("New expiration date must be in the future", "INVALID_EXPIRATION_DATE");

        if (newExpirationDate <= ExpirationDate)
            throw new DomainException("New expiration date must be after current expiration date", "INVALID_EXPIRATION_DATE");

        var oldExpirationDate = ExpirationDate;
        ExpirationDate = newExpirationDate;
        UpdatedAt = DateTimeOffset.UtcNow;

        if (Status == LicenseStatus.Expired)
        {
            Status = LicenseStatus.Pending;
        }

        AddDomainEvent(new LicenseRenewed(Id, newExpirationDate));
    }

    /// <summary>
    /// Revokes the license.
    /// </summary>
    public void Revoke(string reason, string correlationId = "")
    {
        if (Status == LicenseStatus.Revoked)
            return;

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Revocation reason cannot be empty", "INVALID_REVOCATION_REASON");

        Status = LicenseStatus.Revoked;
        RevokedAt = DateTimeOffset.UtcNow;
        RevocationReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new LicenseRevoked(Id, reason.Trim()));
    }

    /// <summary>
    /// Updates the license metadata.
    /// </summary>
    public void UpdateMetadata(string name, string? description, string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("License name cannot be empty", "INVALID_LICENSE_NAME");

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new LicenseUpdated(Id));
    }

    /// <summary>
    /// Adds a feature to the license.
    /// </summary>
    public void AddFeature(LicenseFeature feature)
    {
        if (_features.Any(f => f.Key == feature.Key))
            throw new DomainException($"Feature {feature.Key} already exists", "DUPLICATE_FEATURE");

        _features.Add(feature);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a feature from the license.
    /// </summary>
    public void RemoveFeature(string featureKey)
    {
        var feature = _features.FirstOrDefault(f => f.Key == featureKey);
        if (feature != null)
        {
            _features.Remove(feature);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Checks if the license has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow > ExpirationDate;

    /// <summary>
    /// Checks if the license is currently valid.
    /// </summary>
    public bool IsValid => Status == LicenseStatus.Active && !IsExpired;

    /// <summary>
    /// Checks if the license has a specific feature.
    /// </summary>
    public bool HasFeature(string featureKey) => _features.Any(f => f.Key == featureKey);

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
/// License feature entity.
/// </summary>
public class LicenseFeature
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsEnabled { get; init; } = true;
}