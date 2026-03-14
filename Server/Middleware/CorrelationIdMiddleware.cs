using Serilog.Context;

namespace Server.Middleware;

/// <summary>
/// Middleware that reads or generates an X-Correlation-ID header for every request
/// and pushes it into the Serilog LogContext so every log entry within the request
/// automatically carries the correlation ID as a structured property.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        // Honour an existing correlation ID forwarded by a client/gateway,
        // otherwise generate a fresh one for this request.
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // Echo it back on the response so callers can correlate client-side.
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Push into Serilog's LogContext for the duration of this request.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
