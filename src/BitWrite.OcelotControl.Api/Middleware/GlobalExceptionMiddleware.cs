using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // A rule the operator broke, not a fault. Logged as a warning: logging
            // every rejected configuration as an error trains whoever reads the
            // log to ignore it.
            _logger.LogWarning(
                ex,
                "A domain rule rejected this request: {ErrorCode}",
                ex.ErrorCode);
            await HandleDomainExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Answers a domain rule violation with its own status, code and message.
    /// </summary>
    /// <remarks>
    /// Before this, a <c>DomainException</c> matched none of the cases below and
    /// fell through to 500 with the message replaced. That made every rejected
    /// configuration — a conflicting route, a version that cannot be targeted, a
    /// choice already made — look like a broken server, and left the dashboard
    /// with nothing to match on.
    /// </remarks>
    private static async Task HandleDomainExceptionAsync(HttpContext context, DomainException exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = DomainErrorClassification.StatusFor(exception.ErrorCode);

        var error = new
        {
            correlationId = CorrelationIdFor(context),
            error = DomainErrorClassification.MessageFor(exception),
            type = DomainErrorClassification.KindFor(exception.ErrorCode),
            // The machine-readable half. These are what a client should branch
            // on, and they were being thrown away.
            errorCode = exception.ErrorCode,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }

    private static string CorrelationIdFor(HttpContext context) =>
        context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? context.TraceIdentifier;

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var correlationId = CorrelationIdFor(context);

        var (statusCode, error) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, new { CorrelationId = correlationId, Error = exception.Message, Type = "ValidationError" }),
            InvalidOperationException => (StatusCodes.Status409Conflict, new { CorrelationId = correlationId, Error = exception.Message, Type = "ConflictError" }),
            KeyNotFoundException => (StatusCodes.Status404NotFound, new { CorrelationId = correlationId, Error = exception.Message, Type = "NotFoundError" }),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new { CorrelationId = correlationId, Error = exception.Message, Type = "AuthorizationError" }),
            _ => (StatusCodes.Status500InternalServerError, new { CorrelationId = correlationId, Error = "An internal server error occurred", Type = "InternalServerError" })
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}