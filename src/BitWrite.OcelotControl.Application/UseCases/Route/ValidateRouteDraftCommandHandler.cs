using BitWrite.OcelotControl.Application.UseCases.Route;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// Validates a route the operator has not saved yet.
/// </summary>
/// <remarks>
/// Thin by design: the body is mapped by the API layer and the checks live in
/// <see cref="RouteValidator"/>, so this endpoint and the stored-route one
/// cannot disagree about what makes a route valid. Nothing is persisted and no
/// events are raised, which is what makes it safe for a draft.
/// </remarks>
public class ValidateRouteDraftCommandHandler
{
    private readonly RouteValidator _validator;

    public ValidateRouteDraftCommandHandler(RouteValidator validator)
    {
        _validator = validator;
    }

    public Task<RouteValidationResult> HandleAsync(
        ValidateRouteDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _validator.ValidateAsync(command.Input, cancellationToken);
}
