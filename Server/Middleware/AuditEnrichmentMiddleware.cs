using Serilog.Context;
using System.Security.Claims;

namespace Server.Middleware;

/// <summary>
/// Middleware that extracts audit context from every authenticated (or anonymous)
/// request and pushes it into Serilog's LogContext.  Every log entry written
/// during the lifetime of the request will automatically carry:
///
///   • UserId      — NameIdentifier claim from the JWT, or "anonymous"
///   • IpAddress   — real client IP (honours X-Forwarded-For for reverse proxies)
///   • RequestPath — the raw request path
///   • RequestId   — the ASP.NET Core TraceIdentifier (ties logs to HTTP access log)
///
/// Placement: register AFTER app.UseAuthentication() so that HttpContext.User is
/// already populated with the decoded JWT claims before this middleware runs.
/// </summary>
public class AuditEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // UserId — extracted from the NameIdentifier claim set during JWT sign-in.
        // Falls back to "anonymous" for unauthenticated requests (e.g. /login).
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? "anonymous";

        // IP Address — respect X-Forwarded-For so the real client IP is logged
        // when the API sits behind a load balancer or reverse proxy.
        var ipAddress = context.Request.Headers["X-Forwarded-For"]
                            .FirstOrDefault()
                            ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault()
                            ?.Trim()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";

        var requestPath = context.Request.Path.Value ?? "/";
        var requestId   = context.TraceIdentifier;

        // Push all four properties for the lifetime of this request only.
        // Each 'using' disposes the LogContext property when the request ends.
        using (LogContext.PushProperty("UserId",      userId))
        using (LogContext.PushProperty("IpAddress",   ipAddress))
        using (LogContext.PushProperty("RequestPath", requestPath))
        using (LogContext.PushProperty("RequestId",   requestId))
        {
            await next(context);
        }
    }
}
