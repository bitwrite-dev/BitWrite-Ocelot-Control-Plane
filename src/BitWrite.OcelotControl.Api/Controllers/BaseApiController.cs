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

    protected ActionResult HandleError(Exception ex)
    {
        var error = new
        {
            correlationId = CorrelationId,
            error = ex.Message,
            type = ex.GetType().Name
        };

        return ex switch
        {
            ArgumentException => BadRequest(error),
            InvalidOperationException => Conflict(error),
            KeyNotFoundException => NotFound(error),
            UnauthorizedAccessException => Unauthorized(error),
            _ => StatusCode(500, error)
        };
    }
}