using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

public record AuthenticationOptions : ValueObject
{
    public string? Scheme { get; init; }
    public string? Provider { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();

    private AuthenticationOptions() { }

    public static AuthenticationOptions Create(string scheme, string? provider = null, Dictionary<string, string>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new DomainException("Authentication scheme cannot be empty", "INVALID_AUTH_SCHEME");

        return new AuthenticationOptions
        {
            Scheme = scheme.Trim(),
            Provider = provider?.Trim(),
            Properties = properties ?? new Dictionary<string, string>()
        };
    }

    public static AuthenticationOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Scheme ?? string.Empty;
        yield return Provider ?? string.Empty;
        foreach (var kvp in Properties.OrderBy(k => k.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}

public record AuthorizationOptions : ValueObject
{
    public List<string> Policies { get; init; } = new();
    public List<string> Scopes { get; init; } = new();
    public Dictionary<string, string> Requirements { get; init; } = new();

    private AuthorizationOptions() { }

    public static AuthorizationOptions Create(
        List<string>? policies = null,
        List<string>? scopes = null,
        Dictionary<string, string>? requirements = null)
    {
        return new AuthorizationOptions
        {
            Policies = policies?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList() ?? new(),
            Scopes = scopes?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new(),
            Requirements = requirements ?? new Dictionary<string, string>()
        };
    }

    public bool HasPolicy(string policy) => Policies.Contains(policy, StringComparer.OrdinalIgnoreCase);

    public bool HasScope(string scope) => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var policy in Policies.OrderBy(p => p))
            yield return policy;
        foreach (var scope in Scopes.OrderBy(s => s))
            yield return scope;
        foreach (var kvp in Requirements.OrderBy(k => k.Key))
        {
            yield return kvp.Key;
            yield return kvp.Value;
        }
    }
}

public record RateLimitOptions : ValueObject
{
    public int? Limit { get; init; }
    public int? PeriodSeconds { get; init; }
    public string? ClientIdHeader { get; init; }
    public List<string> Whitelist { get; init; } = new();
    public string? Period { get; init; }

    private RateLimitOptions() { }

    public static RateLimitOptions Create(int limit, string period, string? clientIdHeader = null, List<string>? whitelist = null)
    {
        if (limit <= 0)
            throw new DomainException("Rate limit must be positive", "INVALID_RATE_LIMIT");

        var validPeriods = new[] { "Second", "Minute", "Hour", "Day" };
        if (!validPeriods.Contains(period, StringComparer.OrdinalIgnoreCase))
            throw new DomainException($"Invalid period: {period}. Valid: {string.Join(", ", validPeriods)}", "INVALID_RATE_LIMIT_PERIOD");

        var periodSeconds = period.ToLowerInvariant() switch
        {
            "second" => 1,
            "minute" => 60,
            "hour" => 3600,
            "day" => 86400,
            _ => throw new DomainException($"Invalid period: {period}", "INVALID_RATE_LIMIT_PERIOD")
        };

        return new RateLimitOptions
        {
            Limit = limit,
            Period = period,
            PeriodSeconds = periodSeconds,
            ClientIdHeader = clientIdHeader?.Trim(),
            Whitelist = whitelist?.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList() ?? new()
        };
    }

    public static RateLimitOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Limit ?? 0;
        yield return Period ?? string.Empty;
        yield return PeriodSeconds ?? 0;
        yield return ClientIdHeader ?? string.Empty;
        foreach (var item in Whitelist.OrderBy(w => w))
            yield return item;
    }
}

public record QoSOptions : ValueObject
{
    public int? TimeoutSeconds { get; init; }
    public int? RetryCount { get; init; }
    public bool? UseCircuitBreaker { get; init; }
    public int? CircuitBreakerTimeoutSeconds { get; init; }
    public int? CircuitBreakerExceptionsAllowedBeforeBreaking { get; init; }

    private QoSOptions() { }

    public static QoSOptions Create(
        int? timeoutSeconds = null,
        int? retryCount = null,
        bool? useCircuitBreaker = null,
        int? circuitBreakerTimeoutSeconds = null,
        int? circuitBreakerExceptionsAllowedBeforeBreaking = null)
    {
        if (timeoutSeconds.HasValue && timeoutSeconds <= 0)
            throw new DomainException("Timeout must be positive", "INVALID_QOS_TIMEOUT");

        if (retryCount.HasValue && retryCount < 0)
            throw new DomainException("Retry count cannot be negative", "INVALID_QOS_RETRY_COUNT");

        return new QoSOptions
        {
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount,
            UseCircuitBreaker = useCircuitBreaker,
            CircuitBreakerTimeoutSeconds = circuitBreakerTimeoutSeconds,
            CircuitBreakerExceptionsAllowedBeforeBreaking = circuitBreakerExceptionsAllowedBeforeBreaking
        };
    }

    public static QoSOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TimeoutSeconds ?? 0;
        yield return RetryCount ?? 0;
        yield return UseCircuitBreaker ?? false;
        yield return CircuitBreakerTimeoutSeconds ?? 0;
        yield return CircuitBreakerExceptionsAllowedBeforeBreaking ?? 0;
    }
}

public record CacheOptions : ValueObject
{
    public int TtlSeconds { get; init; }
    public string? Key { get; init; }
    public string? Region { get; init; }
    public List<string> HeaderNames { get; init; } = new();

    private CacheOptions() { }

    public static CacheOptions Create(int ttlSeconds, string? key = null, string? region = null, List<string>? headerNames = null)
    {
        if (ttlSeconds <= 0)
            throw new DomainException("Cache TTL must be positive", "INVALID_CACHE_TTL");

        return new CacheOptions
        {
            TtlSeconds = ttlSeconds,
            Key = key?.Trim(),
            Region = region?.Trim(),
            HeaderNames = headerNames?.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).ToList() ?? new()
        };
    }

    public static CacheOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return TtlSeconds;
        yield return Key ?? string.Empty;
        yield return Region ?? string.Empty;
        foreach (var header in HeaderNames.OrderBy(h => h))
            yield return header;
    }
}

public record LoadBalancerOptions : ValueObject
{
    public string Algorithm { get; init; }
    public string? Key { get; init; }
    public string? Type { get; init; }

    private LoadBalancerOptions(string algorithm, string? key, string? type)
    {
        Algorithm = algorithm;
        Key = key;
        Type = type;
    }

    public static LoadBalancerOptions RoundRobin(string? key = null) => new("RoundRobin", key, null);
    public static LoadBalancerOptions LeastConnection(string? key = null) => new("LeastConnection", key, null);
    public static LoadBalancerOptions NoLoadBalancer(string? key = null) => new("NoLoadBalancer", key, null);
    public static LoadBalancerOptions CookieStickySession(string? key = null) => new("CookieStickySession", key, null);

    public static LoadBalancerOptions Create(string algorithm, string? key = null)
    {
        var validAlgorithms = new[] { "RoundRobin", "LeastConnection", "NoLoadBalancer", "CookieStickySession" };
        if (!validAlgorithms.Contains(algorithm, StringComparer.OrdinalIgnoreCase))
            throw new DomainException($"Invalid load balancer algorithm: {algorithm}. Valid: {string.Join(", ", validAlgorithms)}", "INVALID_LOAD_BALANCER_ALGORITHM");

        return new LoadBalancerOptions(algorithm, key, null);
    }

    public static LoadBalancerOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Algorithm;
        yield return Key ?? string.Empty;
        yield return Type ?? string.Empty;
    }
}

public record HeaderOptions : ValueObject
{
    public List<HeaderTransform> Add { get; init; } = new();
    public List<string> Remove { get; init; } = new();
    public List<HeaderTransform> Transform { get; init; } = new();

    private HeaderOptions() { }

    public static HeaderOptions Create(
        List<HeaderTransform>? add = null,
        List<string>? remove = null,
        List<HeaderTransform>? transform = null)
    {
        return new HeaderOptions
        {
            Add = add ?? new(),
            Remove = remove?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList() ?? new(),
            Transform = transform ?? new()
        };
    }

    public static HeaderOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var header in Add.OrderBy(h => h.Key))
        {
            yield return header.Key;
            yield return header.Value;
        }
        foreach (var header in Remove.OrderBy(r => r))
            yield return header;
        foreach (var header in Transform.OrderBy(h => h.Key))
        {
            yield return header.Key;
            yield return header.Value;
        }
    }
}

public record HeaderTransform : ValueObject
{
    public string Key { get; init; }
    public string Value { get; init; }

    private HeaderTransform(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public static HeaderTransform Create(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Header key cannot be empty", "INVALID_HEADER_KEY");

        return new HeaderTransform(key.Trim(), value ?? string.Empty);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Key;
        yield return Value;
    }
}

public record ClaimOptions : ValueObject
{
    public List<ClaimTransform> Add { get; init; } = new();
    public List<string> Remove { get; init; } = new();
    public List<ClaimTransform> Transform { get; init; } = new();

    private ClaimOptions() { }

    public static ClaimOptions Create(
        List<ClaimTransform>? add = null,
        List<string>? remove = null,
        List<ClaimTransform>? transform = null)
    {
        return new ClaimOptions
        {
            Add = add ?? new(),
            Remove = remove?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList() ?? new(),
            Transform = transform ?? new()
        };
    }

    public static ClaimOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var claim in Add.OrderBy(c => c.Key))
        {
            yield return claim.Key;
            yield return claim.Value;
        }
        foreach (var claim in Remove.OrderBy(r => r))
            yield return claim;
        foreach (var claim in Transform.OrderBy(c => c.Key))
        {
            yield return claim.Key;
            yield return claim.Value;
        }
    }
}

public record ClaimTransform : ValueObject
{
    public string Key { get; init; }
    public string Value { get; init; }

    private ClaimTransform(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public static ClaimTransform Create(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Claim key cannot be empty", "INVALID_CLAIM_KEY");

        return new ClaimTransform(key.Trim(), value ?? string.Empty);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Key;
        yield return Value;
    }
}

public record QueryOptions : ValueObject
{
    public List<QueryTransform> Add { get; init; } = new();
    public List<string> Remove { get; init; } = new();
    public List<QueryTransform> Transform { get; init; } = new();

    private QueryOptions() { }

    public static QueryOptions Create(
        List<QueryTransform>? add = null,
        List<string>? remove = null,
        List<QueryTransform>? transform = null)
    {
        return new QueryOptions
        {
            Add = add ?? new(),
            Remove = remove?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList() ?? new(),
            Transform = transform ?? new()
        };
    }

    public static QueryOptions? None() => null;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        foreach (var query in Add.OrderBy(q => q.Key))
        {
            yield return query.Key;
            yield return query.Value;
        }
        foreach (var query in Remove.OrderBy(r => r))
            yield return query;
        foreach (var query in Transform.OrderBy(q => q.Key))
        {
            yield return query.Key;
            yield return query.Value;
        }
    }
}

public record QueryTransform : ValueObject
{
    public string Key { get; init; }
    public string Value { get; init; }

    private QueryTransform(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public static QueryTransform Create(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Query key cannot be empty", "INVALID_QUERY_KEY");

        return new QueryTransform(key.Trim(), value ?? string.Empty);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Key;
        yield return Value;
    }
}