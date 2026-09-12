using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class UpdateGlobalConfigurationRequestValidator : AbstractValidator<UpdateGlobalConfigurationRequest>
{
    public UpdateGlobalConfigurationRequestValidator()
    {
        RuleFor(x => x.BaseUrl)
            .MaximumLength(200)
            .When(x => x.BaseUrl != null);

        RuleFor(x => x.RequestIdKey)
            .MaximumLength(100)
            .When(x => x.RequestIdKey != null);

        RuleFor(x => x.DownstreamScheme)
            .Must(scheme => new[] { "http", "https", "grpc", "grpcs" }.Contains(scheme?.ToLower()))
            .WithMessage("Scheme must be one of: http, https, grpc, grpcs")
            .When(x => x.DownstreamScheme != null);

        RuleFor(x => x.Timeout)
            .InclusiveBetween(1, 300000)
            .When(x => x.Timeout.HasValue);

        RuleFor(x => x.RateLimit)
            .SetValidator(new RateLimitConfigRequestValidator())
            .When(x => x.RateLimit != null);

        RuleFor(x => x.QoS)
            .SetValidator(new QoSConfigRequestValidator())
            .When(x => x.QoS != null);

        RuleFor(x => x.HttpHandler)
            .SetValidator(new HttpHandlerConfigRequestValidator())
            .When(x => x.HttpHandler != null);

        RuleFor(x => x.ServiceDiscovery)
            .SetValidator(new ServiceDiscoveryConfigRequestValidator())
            .When(x => x.ServiceDiscovery != null);
    }
}

public class RateLimitConfigRequestValidator : AbstractValidator<RateLimitConfigRequest>
{
    public RateLimitConfigRequestValidator()
    {
        // All fields optional
    }
}

public class QoSConfigRequestValidator : AbstractValidator<QoSConfigRequest>
{
    public QoSConfigRequestValidator()
    {
        RuleFor(x => x.TimeoutValue)
            .InclusiveBetween(1, 3600);

        RuleFor(x => x.DurationOfBreak)
            .InclusiveBetween(1, 3600);
    }
}

public class HttpHandlerConfigRequestValidator : AbstractValidator<HttpHandlerConfigRequest>
{
    public HttpHandlerConfigRequestValidator()
    {
        RuleFor(x => x.MaxConnectionsPerServer)
            .InclusiveBetween(1, 10000)
            .When(x => x.MaxConnectionsPerServer.HasValue);
    }
}

public class ServiceDiscoveryConfigRequestValidator : AbstractValidator<ServiceDiscoveryConfigRequest>
{
    public ServiceDiscoveryConfigRequestValidator()
    {
        RuleFor(x => x.Provider)
            .MaximumLength(100)
            .When(x => x.Provider != null);

        RuleFor(x => x.Host)
            .MaximumLength(200)
            .When(x => x.Host != null);

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535)
            .When(x => x.Port.HasValue);

        RuleFor(x => x.Type)
            .MaximumLength(100)
            .When(x => x.Type != null);
    }
}