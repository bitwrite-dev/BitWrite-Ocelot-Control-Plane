using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class InstallPluginRequestValidator : AbstractValidator<InstallPluginRequest>
{
    public InstallPluginRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Version)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);

        RuleFor(x => x.Scope)
            .Must(scope => new[] { "Global", "Route" }.Contains(scope))
            .WithMessage("Scope must be one of: Global, Route");
    }
}

public class UpdatePluginRequestValidator : AbstractValidator<UpdatePluginRequest>
{
    public UpdatePluginRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name != null);

        RuleFor(x => x.Version)
            .MaximumLength(50)
            .When(x => x.Version != null);

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => x.Description != null);
    }
}