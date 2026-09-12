using FluentValidation;
using BitWrite.OcelotControl.Api.DTOs;

namespace BitWrite.OcelotControl.Api.Validators;

public class CreateSnapshotRequestValidator : AbstractValidator<CreateSnapshotRequest>
{
    public CreateSnapshotRequestValidator()
    {
        RuleFor(x => x.InitiatedBy)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public class SnapshotPublishRequestValidator : AbstractValidator<SnapshotPublishRequest>
{
    public SnapshotPublishRequestValidator()
    {
        RuleFor(x => x.InitiatedBy)
            .NotEmpty()
            .MaximumLength(200);
    }
}

public class SnapshotRollbackRequestValidator : AbstractValidator<SnapshotRollbackRequest>
{
    public SnapshotRollbackRequestValidator()
    {
        RuleFor(x => x.InitiatedBy)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.TargetVersion)
            .GreaterThan(0);

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}

public class SnapshotCloneRequestValidator : AbstractValidator<SnapshotCloneRequest>
{
    public SnapshotCloneRequestValidator()
    {
        RuleFor(x => x.InitiatedBy)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.NewName)
            .NotEmpty()
            .MaximumLength(200);
    }
}