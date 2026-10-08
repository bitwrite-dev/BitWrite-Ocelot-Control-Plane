using BitWrite.OcelotControl.Api.Middleware;
using BitWrite.OcelotControl.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected string CorrelationId => HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? HttpContext.TraceIdentifier;

    protected ActionResult<T> HandleResult<T>(T result)
    {
        return Ok(result);
    }

    /// <summary>
    /// Turns an exception into a response, with the same mapping the global
    /// middleware applies.
    /// </summary>
    /// <remarks>
    /// Both paths exist, and they used to disagree. This one ran first — the
    /// controllers catch broadly and call this — so the middleware's mapping never
    /// saw a domain failure, and a <c>DomainException</c> came out as 500 with the
    /// message intact but the error code missing. Sharing the classifier is the
    /// only way the two can stay in step; a third copy elsewhere would still be
    /// free to drift, which is why the codes, not the status numbers, are the
    /// thing being decided in one place.
    /// </remarks>
    protected ActionResult HandleError(Exception ex)
    {
        // Genuinely unexpected failures keep a generic message: their text is not
        // written for a caller, and it may contain internals.
        if (ex is not DomainException && ex is not ArgumentException
            && ex is not FormatException
            && ex is not InvalidOperationException && ex is not KeyNotFoundException
            && ex is not UnauthorizedAccessException)
        {
            return StatusCode(500, new
            {
                correlationId = CorrelationId,
                error = "An internal server error occurred",
                type = "InternalServerError",
            });
        }

        var domain = ex as DomainException;

        var error = new
        {
            correlationId = CorrelationId,
            error = ex.Message,
            type = domain is not null
                ? DomainErrorClassification.KindFor(domain.ErrorCode)
                : ex.GetType().Name,
            errorCode = domain?.ErrorCode,
        };

        if (domain is not null)
        {
            return StatusCode(DomainErrorClassification.StatusFor(domain.ErrorCode), error);
        }

        return ex switch
        {
            FormatException => BadRequest(error),
            ArgumentException => BadRequest(error),
            InvalidOperationException => Conflict(error),
            KeyNotFoundException => NotFound(error),
            UnauthorizedAccessException => Unauthorized(error),
            _ => StatusCode(500, error)
        };
    }
}
