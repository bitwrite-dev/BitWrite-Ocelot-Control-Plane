using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() 
            ?? context.TraceIdentifier;

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