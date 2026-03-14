using System.Net;
using System.Text.Json;

namespace Server.Middleware;

/// <summary>
/// Catches any unhandled exception that bubbles past all other middleware,
/// logs it as a structured Error entry via Serilog (which is injected through
/// ILogger<T> so the host's Serilog sink is used automatically), and returns
/// a generic 500 JSON response so internal details are never leaked to clients.
/// </summary>
public class GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Structured properties let Seq / file sinks index each field independently.
            logger.LogError(
                ex,
                "Unhandled exception on {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;

        var response = new
        {
            status  = 500,
            message = "An unexpected error occurred. Please try again later.",
            traceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
