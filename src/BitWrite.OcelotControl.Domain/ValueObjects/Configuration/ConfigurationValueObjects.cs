using System.Text.RegularExpressions;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

public record RouteKey : ValueObject
{
    public HttpMethod Method { get; init; }
    public UpstreamPath Path { get; init; }
    public string? Host { get; init; }

    private RouteKey(HttpMethod method, UpstreamPath path, string? host)
    {
        Method = method;
        Path = path;
        Host = host?.ToLowerInvariant().Trim();
    }

    public static RouteKey Create(HttpMethod method, UpstreamPath path, string? host = null)
    {
        return new RouteKey(method, path, host);
    }

    public static RouteKey Parse(string routeKey)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
            throw new DomainException("RouteKey cannot be empty", "INVALID_ROUTE_KEY");

        var parts = routeKey.Split(':', 2);
        if (parts.Length != 2)
            throw new DomainException($"Invalid RouteKey format: {routeKey}. Expected 'Method:Path'", "INVALID_ROUTE_KEY_FORMAT");

        var method = HttpMethod.Parse(parts[0]);
        var pathAndHost = parts[1].Split(' ', 2);
        var path = UpstreamPath.From(pathAndHost[0]);
        var host = pathAndHost.Length > 1 ? pathAndHost[1] : null;

        return new RouteKey(method, path, host);
    }

    public string ToSignature() => $"{Method}:{Path}{(Host != null ? $" {Host}" : "")}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Method;
        yield return Path;
        yield return Host ?? string.Empty;
    }

    public override string ToString() => ToSignature();
}

public record UpstreamPath : ValueObject
{
    public string Value { get; init; }

    private static readonly Regex ValidPathPattern = new(@"^(/[a-zA-Z0-9\-._~!$&'()*+,;=:@%]*)*$", RegexOptions.Compiled);

    private UpstreamPath(string value)
    {
        Value = value;
    }

    public static UpstreamPath From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("UpstreamPath cannot be empty", "INVALID_UPSTREAM_PATH");

        var normalized = value.Trim();
        if (!normalized.StartsWith("/"))
            normalized = "/" + normalized;

        // Basic validation - more sophisticated validation can be added
        if (!ValidPathPattern.IsMatch(normalized))
            throw new DomainException($"Invalid UpstreamPath format: {value}", "INVALID_UPSTREAM_PATH_FORMAT");

        return new UpstreamPath(normalized);
    }

    public UpstreamPath Append(string segment)
    {
        var newValue = Value.TrimEnd('/') + "/" + segment.TrimStart('/');
        return From(newValue);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(UpstreamPath path) => path.Value;
}

public record HttpMethod : ValueObject
{
    public string Value { get; init; }

    private static readonly HashSet<string> ValidMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE", "CONNECT", "*"
    };

    private HttpMethod(string value)
    {
        Value = value.ToUpperInvariant();
    }

    public static HttpMethod Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("HttpMethod cannot be empty", "INVALID_HTTP_METHOD");

        var normalized = value.Trim().ToUpperInvariant();
        if (!ValidMethods.Contains(normalized))
            throw new DomainException($"Invalid HttpMethod: {value}. Valid methods: {string.Join(", ", ValidMethods)}", "INVALID_HTTP_METHOD");

        return new HttpMethod(normalized);
    }

    public static HttpMethod Get => new("GET");
    public static HttpMethod Post => new("POST");
    public static HttpMethod Put => new("PUT");
    public static HttpMethod Delete => new("DELETE");
    public static HttpMethod Patch => new("PATCH");
    public static HttpMethod Head => new("HEAD");
    public static HttpMethod Options => new("OPTIONS");
    public static HttpMethod Any => new("*");

    public bool IsAny => Value == "*";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(HttpMethod method) => method.Value;
}

public record DownstreamTarget : ValueObject
{
    public string Scheme { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string Path { get; init; }

    private DownstreamTarget(string scheme, string host, int port, string path)
    {
        Scheme = scheme.ToLowerInvariant();
        Host = host.ToLowerInvariant();
        Port = port;
        Path = path.StartsWith("/") ? path : "/" + path;
    }

    public static DownstreamTarget Create(string scheme, string host, int port, string path = "/")
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new DomainException("Scheme cannot be empty", "INVALID_DOWNSTREAM_TARGET");

        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host cannot be empty", "INVALID_DOWNSTREAM_TARGET");

        if (port <= 0 || port > 65535)
            throw new DomainException($"Invalid port: {port}", "INVALID_DOWNSTREAM_PORT");

        var validSchemes = new[] { "http", "https", "grpc", "grpcs" };
        if (!validSchemes.Contains(scheme.ToLowerInvariant()))
            throw new DomainException($"Invalid scheme: {scheme}. Valid: {string.Join(", ", validSchemes)}", "INVALID_DOWNSTREAM_SCHEME");

        return new DownstreamTarget(scheme, host, port, path);
    }

    public string ToUri() => $"{Scheme}://{Host}:{Port}{Path}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Scheme;
        yield return Host;
        yield return Port;
        yield return Path;
    }

    public override string ToString() => ToUri();
}

public record ConfigurationHash : ValueObject
{
    public string Value { get; init; }

    private static readonly Regex ValidHashPattern = new(@"^[a-f0-9]{64}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private ConfigurationHash(string value)
    {
        Value = value.ToLowerInvariant();
    }

    public static ConfigurationHash FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            throw new DomainException("Cannot create hash from empty bytes", "INVALID_HASH");

        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return new ConfigurationHash(hex);
    }

    public static ConfigurationHash FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("ConfigurationHash cannot be empty", "INVALID_HASH");

        var normalized = value.ToLowerInvariant().Trim();
        if (!ValidHashPattern.IsMatch(normalized))
            throw new DomainException($"Invalid ConfigurationHash format: {value}. Expected 64-char hex string", "INVALID_HASH_FORMAT");

        return new ConfigurationHash(normalized);
    }

    public byte[] ToBytes() => Convert.FromHexString(Value);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(ConfigurationHash hash) => hash.Value;
}

public record OcelotVersion : ValueObject, IComparable<OcelotVersion>
{
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
    public string? PreRelease { get; init; }

    private OcelotVersion(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
    }

    public static OcelotVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("OcelotVersion cannot be empty", "INVALID_OCELOT_VERSION");

        var parts = value.Split('-', 2);
        var versionPart = parts[0];
        var preRelease = parts.Length > 1 ? parts[1] : null;

        var versionNumbers = versionPart.Split('.');
        if (versionNumbers.Length < 2 || versionNumbers.Length > 3)
            throw new DomainException($"Invalid OcelotVersion format: {value}. Expected Major.Minor or Major.Minor.Patch", "INVALID_OCELOT_VERSION_FORMAT");

        var major = int.Parse(versionNumbers[0]);
        var minor = int.Parse(versionNumbers[1]);
        var patch = versionNumbers.Length == 3 ? int.Parse(versionNumbers[2]) : 0;

        return new OcelotVersion(major, minor, patch, preRelease);
    }

    public static OcelotVersion V18_0 => new(18, 0, 0);
    public static OcelotVersion V19_0 => new(19, 0, 0);
    public static OcelotVersion V20_0 => new(20, 0, 0);

    public bool IsAtLeast(OcelotVersion other) => CompareTo(other) >= 0;

    public bool SupportsFeature(string featureKey, Dictionary<string, OcelotVersion> featureRegistry)
    {
        if (!featureRegistry.TryGetValue(featureKey, out var minVersion))
            return false;
        return IsAtLeast(minVersion);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Major;
        yield return Minor;
        yield return Patch;
        yield return PreRelease ?? string.Empty;
    }

    public int CompareTo(OcelotVersion? other)
    {
        if (other is null) return 1;

        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        if (PreRelease == null && other.PreRelease != null) return 1;
        if (PreRelease != null && other.PreRelease == null) return -1;
        if (PreRelease != null && other.PreRelease != null)
            return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);

        return 0;
    }

    public static bool operator <(OcelotVersion left, OcelotVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(OcelotVersion left, OcelotVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(OcelotVersion left, OcelotVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(OcelotVersion left, OcelotVersion right) => left.CompareTo(right) >= 0;

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        return PreRelease != null ? $"{version}-{PreRelease}" : version;
    }
}