using System.Text.RegularExpressions;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.Status;

public record SnapshotStatus : ValueObject
{
    public string Value { get; init; }

    private SnapshotStatus(string value)
    {
        Value = value;
    }

    public static SnapshotStatus Ready => new("Ready");
    public static SnapshotStatus Published => new("Published");
    public static SnapshotStatus Archived => new("Archived");

    public static SnapshotStatus From(string value)
    {
        return value switch
        {
            "Ready" => Ready,
            "Published" => Published,
            "Archived" => Archived,
            _ => throw new DomainException($"Invalid SnapshotStatus: {value}", "INVALID_SNAPSHOT_STATUS")
        };
    }

    public bool CanTransitionTo(SnapshotStatus newStatus)
    {
        return (Value, newStatus.Value) switch
        {
            ("Ready", "Published") => true,
            ("Ready", "Archived") => true, // Direct archive without publishing
            ("Published", "Archived") => true,
            _ => false
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(SnapshotStatus status) => status.Value;
}

public record PublicationStatus : ValueObject
{
    public string Value { get; init; }

    private PublicationStatus(string value)
    {
        Value = value;
    }

    public static PublicationStatus Pending => new("Pending");
    public static PublicationStatus Published => new("Published");
    public static PublicationStatus Failed => new("Failed");
    public static PublicationStatus RolledBack => new("RolledBack");

    public static PublicationStatus From(string value)
    {
        return value switch
        {
            "Pending" => Pending,
            "Published" => Published,
            "Failed" => Failed,
            "RolledBack" => RolledBack,
            _ => throw new DomainException($"Invalid PublicationStatus: {value}", "INVALID_PUBLICATION_STATUS")
        };
    }

    public bool IsTerminal => Value is "Published" or "Failed" or "RolledBack";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PublicationStatus status) => status.Value;
}

public record RuntimeStatus : ValueObject
{
    public string Value { get; init; }

    private RuntimeStatus(string value)
    {
        Value = value;
    }

    public static RuntimeStatus Disconnected => new("Disconnected");
    public static RuntimeStatus Connecting => new("Connecting");
    public static RuntimeStatus Synchronized => new("Synchronized");
    public static RuntimeStatus Applying => new("Applying");
    public static RuntimeStatus Active => new("Active");
    public static RuntimeStatus Degraded => new("Degraded");

    public static RuntimeStatus From(string value)
    {
        return value switch
        {
            "Disconnected" => Disconnected,
            "Connecting" => Connecting,
            "Synchronized" => Synchronized,
            "Applying" => Applying,
            "Active" => Active,
            "Degraded" => Degraded,
            _ => throw new DomainException($"Invalid RuntimeStatus: {value}", "INVALID_RUNTIME_STATUS")
        };
    }

    public bool IsHealthy => Value is "Synchronized" or "Active";
    public bool IsDegraded => Value == "Degraded";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(RuntimeStatus status) => status.Value;
}

public record PluginScope : ValueObject
{
    public string Value { get; init; }

    private PluginScope(string value)
    {
        Value = value;
    }

    public static PluginScope Global => new("Global");
    public static PluginScope Route => new("Route");

    public static PluginScope From(string value)
    {
        return value switch
        {
            "Global" => Global,
            "Route" => Route,
            _ => throw new DomainException($"Invalid PluginScope: {value}. Valid: Global, Route", "INVALID_PLUGIN_SCOPE")
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PluginScope scope) => scope.Value;
}

public record CapabilityKey : ValueObject
{
    public string Value { get; init; }

    private static readonly Regex ValidKeyPattern = new(@"^[a-z][a-z0-9.-]*$", RegexOptions.Compiled);

    private CapabilityKey(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static CapabilityKey From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("CapabilityKey cannot be empty", "INVALID_CAPABILITY_KEY");

        var normalized = value.ToLowerInvariant().Trim();
        if (!ValidKeyPattern.IsMatch(normalized))
            throw new DomainException($"Invalid CapabilityKey format: {value}", "INVALID_CAPABILITY_KEY_FORMAT");

        return new CapabilityKey(normalized);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(CapabilityKey key) => key.Value;
}

public record LicenseStatus : ValueObject
{
    public string Value { get; init; }

    private LicenseStatus(string value)
    {
        Value = value;
    }

    public static LicenseStatus Pending => new("Pending");
    public static LicenseStatus Active => new("Active");
    public static LicenseStatus Expired => new("Expired");
    public static LicenseStatus Revoked => new("Revoked");

    public static LicenseStatus From(string value)
    {
        return value switch
        {
            "Pending" => Pending,
            "Active" => Active,
            "Expired" => Expired,
            "Revoked" => Revoked,
            _ => throw new DomainException($"Invalid LicenseStatus: {value}", "INVALID_LICENSE_STATUS")
        };
    }

    public bool IsActive => Value == "Active";
    public bool IsValid => Value == "Active";
    public bool IsExpired => Value == "Expired";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(LicenseStatus status) => status.Value;
}