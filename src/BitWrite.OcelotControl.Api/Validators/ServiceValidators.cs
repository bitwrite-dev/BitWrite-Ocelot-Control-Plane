using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class CreateServiceRequestValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);

        RuleForEach(x => x.DownstreamTargets)
            .SetValidator(new ServiceEndpointRequestValidator())
            .When(x => x.DownstreamTargets != null);
    }
}

/// <summary>
/// A service endpoint, which is host, port and weight — and not the route's
/// scheme and path, which a <c>ServiceEndpoint</c> does not store.
/// </summary>
/// <remarks>
/// Reusing the route validator here is what let a caller send a scheme and a path
/// for a service endpoint: the fields were validated and then discarded, and echoed
/// back as the invented constants "http" and "/". The request shape no longer has
/// them, so there is nothing left to validate wrongly.
/// </remarks>
public class ServiceEndpointRequestValidator : AbstractValidator<ServiceEndpointRequest>
{
    public ServiceEndpointRequestValidator()
    {
        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535);

        RuleFor(x => x.Weight)
            .InclusiveBetween(1, 1000)
            .WithMessage("Weight must be between 1 and 1000");
    }
}

public class UpdateServiceRequestValidator : AbstractValidator<UpdateServiceRequest>
{
    public UpdateServiceRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);

        RuleForEach(x => x.DownstreamTargets)
            .SetValidator(new ServiceEndpointRequestValidator())
            .When(x => x.DownstreamTargets != null);
    }
}