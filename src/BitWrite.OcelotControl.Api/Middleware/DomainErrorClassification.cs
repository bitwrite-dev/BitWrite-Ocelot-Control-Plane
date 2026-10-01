using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Api.Middleware;

/// <summary>
/// What a domain rule violation means to a caller, in HTTP terms.
/// </summary>
/// <remarks>
/// <c>DomainException</c> derives from <see cref="Exception"/>, so before this
/// existed every rule violation in the product answered 500 with its message
/// replaced by "An internal server error occurred" — a rejected configuration and
/// a genuine fault were indistinguishable from outside, and the
/// machine-readable <c>ErrorCode</c> never left the process.
/// <para>
/// The codes carry the meaning already: 105 of them start with <c>INVALID_</c>,
/// which is a bad request, and the rest cluster into "that is not there" and "the
/// current state does not allow it". Matching on that shape keeps the mapping
/// derived from the codes rather than invented per call site.
/// </para>
/// <para>
/// A code nobody has seen maps to 400 rather than 500. A new rule that rejects
/// input should not be reported as a server fault on its first day, and the
/// message is still returned so the operator can see what was wrong.
/// </para>
/// </remarks>
public static class DomainErrorClassification
{
    /// <summary>
    /// The status a domain rule violation should answer with.
    /// </summary>
    public static int StatusFor(string errorCode) => errorCode switch
    {
        // "That does not exist" — a stale id, or a resource someone else removed.
        _ when Contains(errorCode, "NOT_FOUND") => StatusCodes.Status404NotFound,

        // "The current state does not allow this" — a permanent choice, a rollback
        // that already happened, an endpoint that collides, a gateway that still
        // has publication history. Retryable only once something else changes, so
        // 409 rather than 400: the request was well formed, and 400 would tell
        // the caller to fix it rather than to wait or choose differently.
        _ when Contains(errorCode, "ALREADY")
             || Contains(errorCode, "CONFLICT")
             || Contains(errorCode, "DUPLICATE")
             || Contains(errorCode, "TOO_MANY")
             // Something else depends on it: in use, busy, or a history exists.
             || Contains(errorCode, "IN_USE")
             || Contains(errorCode, "BUSY")
             || Contains(errorCode, "HAS_")
             || Contains(errorCode, "IMMUTABLE")
             // Nothing to act on, which is also a state rather than a mistake.
             || Contains(errorCode, "NO_")
             || Contains(errorCode, "NOT_SUPPORTED")
             || Contains(errorCode, "UNSUPPORTED")
             => StatusCodes.Status409Conflict,

        // Everything else the domain rejects on its input: 105 INVALID_ codes,
        // plus the MISSING_ and FEATURE_ families. All of it is a bad request.
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>
    /// A coarse label, so a client can branch without matching on the code itself.
    /// </summary>
    /// <remarks>
    /// The codes are the product's own vocabulary and are free to gain members.
    /// This is the part a dashboard is allowed to depend on.
    /// </remarks>
    public static string KindFor(string errorCode) => StatusFor(errorCode) switch
    {
        StatusCodes.Status404NotFound => "NotFound",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Validation",
    };

    /// <summary>
    /// The domain's own message, kept for a domain failure.
    /// </summary>
    /// <remarks>
    /// These messages are written for the operator reading the screen — which
    /// versions can be targeted, why a choice cannot be undone, which routes
    /// overlap. Discarding them is what made a rejected configuration look like a
    /// broken server. Genuinely unexpected exceptions still get a generic message,
    /// because their text is not written for anyone.
    /// </remarks>
    public static string MessageFor(DomainException exception) => exception.Message;

    private static bool Contains(string errorCode, string fragment) =>
        !string.IsNullOrEmpty(errorCode) &&
        errorCode.Contains(fragment, StringComparison.Ordinal);
}
