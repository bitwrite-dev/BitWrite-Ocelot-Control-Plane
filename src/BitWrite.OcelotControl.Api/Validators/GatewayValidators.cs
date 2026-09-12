using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class CreateGatewayRequestValidator : AbstractValidator<CreateGatewayRequest>
{
    public CreateGatewayRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}

public class UpdateGatewayRequestValidator : AbstractValidator<UpdateGatewayRequest>
{
    public UpdateGatewayRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);
    }
}

public class UpdateGatewayStatusRequestValidator : AbstractValidator<UpdateGatewayStatusRequest>
{
    public UpdateGatewayStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(status => new[] { "Active", "Inactive", "Maintenance" }.Contains(status))
            .WithMessage("Status must be one of: Active, Inactive, Maintenance");
    }
}