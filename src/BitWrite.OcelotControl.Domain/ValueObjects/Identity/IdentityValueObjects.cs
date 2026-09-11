using System.Text.RegularExpressions;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.Identity;

public record RouteId : ValueObject
{
    public Guid Value { get; init; }

    private RouteId(Guid value)
    {
        Value = value;
    }

    public static RouteId New() => new(Guid.NewGuid());

    public static RouteId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("RouteId cannot be empty", "INVALID_ROUTE_ID");
        return new RouteId(value);
    }

    public static RouteId From(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new DomainException($"Invalid RouteId format: {value}", "INVALID_ROUTE_ID_FORMAT");
        return From(guid);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(RouteId id) => id.Value;
    public static implicit operator string(RouteId id) => id.Value.ToString();
}

public record ServiceId : ValueObject
{
    public Guid Value { get; init; }

    private ServiceId(Guid value)
    {
        Value = value;
    }

    public static ServiceId New() => new(Guid.NewGuid());

    public static ServiceId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("ServiceId cannot be empty", "INVALID_SERVICE_ID");
        return new ServiceId(value);
    }

    public static ServiceId From(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new DomainException($"Invalid ServiceId format: {value}", "INVALID_SERVICE_ID_FORMAT");
        return From(guid);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(ServiceId id) => id.Value;
    public static implicit operator string(ServiceId id) => id.Value.ToString();
}

public record GatewayId : ValueObject
{
    public Guid Value { get; init; }

    private GatewayId(Guid value)
    {
        Value = value;
    }

    public static GatewayId New() => new(Guid.NewGuid());

    public static GatewayId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("GatewayId cannot be empty", "INVALID_GATEWAY_ID");
        return new GatewayId(value);
    }

    public static GatewayId From(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            throw new DomainException($"Invalid GatewayId format: {value}", "INVALID_GATEWAY_ID_FORMAT");
        return From(guid);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(GatewayId id) => id.Value;
    public static implicit operator string(GatewayId id) => id.Value.ToString();
}

public record SnapshotVersion : ValueObject, IComparable<SnapshotVersion>
{
    public int Value { get; init; }

    private SnapshotVersion(int value)
    {
        if (value <= 0)
            throw new DomainException("SnapshotVersion must be positive", "INVALID_SNAPSHOT_VERSION");
        Value = value;
    }

    public static SnapshotVersion From(int value) => new(value);

    public static SnapshotVersion First() => new(1);

    public SnapshotVersion Next() => new(Value + 1);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public int CompareTo(SnapshotVersion? other)
    {
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }

    public static bool operator <(SnapshotVersion left, SnapshotVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SnapshotVersion left, SnapshotVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SnapshotVersion left, SnapshotVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SnapshotVersion left, SnapshotVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value.ToString();

    public static implicit operator int(SnapshotVersion v) => v.Value;
}

public record PublicationId : ValueObject
{
    public Guid Value { get; init; }

    private PublicationId(Guid value)
    {
        Value = value;
    }

    public static PublicationId New() => new(Guid.NewGuid());

    public static PublicationId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("PublicationId cannot be empty", "INVALID_PUBLICATION_ID");
        return new PublicationId(value);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(PublicationId id) => id.Value;
}

public record PluginId : ValueObject
{
    public string Value { get; init; }

    private static readonly Regex ValidPluginIdPattern = new(@"^[a-z][a-z0-9.-]*$", RegexOptions.Compiled);

    private PluginId(string value)
    {
        Value = value;
    }

    public static PluginId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("PluginId cannot be empty", "INVALID_PLUGIN_ID");

        var normalized = value.ToLowerInvariant().Trim();
        if (!ValidPluginIdPattern.IsMatch(normalized))
            throw new DomainException($"Invalid PluginId format: {value}. Must be lowercase, alphanumeric with dots and hyphens", "INVALID_PLUGIN_ID_FORMAT");

        return new PluginId(normalized);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}