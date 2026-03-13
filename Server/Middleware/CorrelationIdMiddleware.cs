using Serilog.Context;

namespace Server.Middleware;

/// <summary>
/// Reads (or generates) an X-Correlation-ID header on every request and pushes it
/// into Serilog's LogContext so every log entry for that request carries the ID.
/// Clients can trace a full request lifecycle across log lines using this value.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        // Accept a correlation ID from an upstream caller, or generate a fresh one
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..12];

        // Echo it back in the response so clients can correlate
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Push it into Serilog's LogContext for the duration of the request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
