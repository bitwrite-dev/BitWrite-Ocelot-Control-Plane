using BitWrite.OcelotControl.Api.DTOs;
using FluentValidation;

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

        // The environment is the whole point of the request: routes, services and
        // snapshots are stored per environment, and a publication that named none
        // would publish one environment's snapshot to another environment's
        // gateways without anyone saying so.
        RuleFor(x => x.Environment)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.TargetGatewayIds)
            .NotEmpty();
    }
}

public class SnapshotRollbackRequestValidator : AbstractValidator<SnapshotRollbackRequest>
{
    public SnapshotRollbackRequestValidator()
    {
        RuleFor(x => x.InitiatedBy)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Environment)
            .NotEmpty()
            .MaximumLength(100);

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
