using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class CreateRouteRequestValidator : AbstractValidator<CreateRouteRequest>
{
    public CreateRouteRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(method => new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "*" }.Contains(method.ToUpper()))
            .WithMessage("Method must be a valid HTTP method or *");

        RuleFor(x => x.UpstreamPath)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Host)
            .MaximumLength(100)
            .When(x => x.Host != null);

        RuleFor(x => x.ServiceId)
            .NotEmpty();

        RuleFor(x => x.DownstreamTargets)
            .NotEmpty()
            .Must(targets => targets.Count > 0)
            .WithMessage("At least one downstream target is required");

        RuleForEach(x => x.DownstreamTargets)
            .SetValidator(new DownstreamTargetRequestValidator());

        RuleFor(x => x.AuthenticationOptions)
            .SetValidator(new AuthenticationOptionsRequestValidator())
            .When(x => x.AuthenticationOptions != null);

        RuleFor(x => x.RateLimitOptions)
            .SetValidator(new RateLimitOptionsRequestValidator())
            .When(x => x.RateLimitOptions != null);

        RuleFor(x => x.QoSOptions)
            .SetValidator(new QoSOptionsRequestValidator())
            .When(x => x.QoSOptions != null);

        RuleFor(x => x.CacheOptions)
            .SetValidator(new CacheOptionsRequestValidator())
            .When(x => x.CacheOptions != null);

        RuleFor(x => x.LoadBalancerOptions)
            .SetValidator(new LoadBalancerOptionsRequestValidator())
            .When(x => x.LoadBalancerOptions != null);
    }
}

public class DownstreamTargetRequestValidator : AbstractValidator<DownstreamTargetRequest>
{
    public DownstreamTargetRequestValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535);

        RuleFor(x => x.Scheme)
            .Must(scheme => new[] { "http", "https", "grpc", "grpcs" }.Contains(scheme.ToLower()))
            .WithMessage("Scheme must be one of: http, https, grpc, grpcs");
    }
}

public class AuthenticationOptionsRequestValidator : AbstractValidator<AuthenticationOptionsRequest>
{
    public AuthenticationOptionsRequestValidator()
    {
        RuleFor(x => x.AllowedScopes)
            .NotNull();
    }
}

public class RateLimitOptionsRequestValidator : AbstractValidator<RateLimitOptionsRequest>
{
    public RateLimitOptionsRequestValidator()
    {
        RuleFor(x => x.EnableRateLimiting)
            .NotNull();

        RuleFor(x => x.Period)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Limit)
            .GreaterThan(0);
    }
}

public class QoSOptionsRequestValidator : AbstractValidator<QoSOptionsRequest>
{
    public QoSOptionsRequestValidator()
    {
        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, 3600);

        RuleFor(x => x.CircuitBreakerTimeoutSeconds)
            .InclusiveBetween(1, 3600)
            .When(x => x.CircuitBreakerTimeoutSeconds.HasValue);
    }
}

public class CacheOptionsRequestValidator : AbstractValidator<CacheOptionsRequest>
{
    public CacheOptionsRequestValidator()
    {
        RuleFor(x => x.TtlSeconds)
            .InclusiveBetween(1, 86400);
    }
}

public class LoadBalancerOptionsRequestValidator : AbstractValidator<LoadBalancerOptionsRequest>
{
    public LoadBalancerOptionsRequestValidator()
    {
        RuleFor(x => x.Algorithm)
            .NotEmpty()
            .MaximumLength(50)
            .Must(alg => new[] { "RoundRobin", "LeastConnections", "IpHash", "CookieSticky" }.Contains(alg))
            .WithMessage("Algorithm must be one of: RoundRobin, LeastConnections, IpHash, CookieSticky");
    }
}