using System.Threading.RateLimiting;
using ClinicBooking.Api.Api.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Api.Api.Middleware;

public sealed class PreAuthRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _opt;
    private readonly HashSet<string> _allowedKeys;

    // ✅ Limiteur "général" (invalid/missing) : 30/min par IP
    private static readonly PartitionedRateLimiter<string> _generalIpLimiter =
        PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    // ✅ Limiteur "appointments" (invalid/missing) : 5/min par IP
    private static readonly PartitionedRateLimiter<string> _appointmentsIpLimiter =
        PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public PreAuthRateLimitMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> opt)
    {
        _next = next;
        _opt = opt.Value;
        _allowedKeys = new HashSet<string>(_opt.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public async Task Invoke(HttpContext ctx)
    {
        // Swagger pas limité (DEV)
        if (ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(ctx);
            return;
        }

        // Si clé valide => pas de pré-auth rate limit (on limitera par KEY après)
        var provided = GetProvidedApiKey(ctx, _opt.HeaderName);
        if (!string.IsNullOrWhiteSpace(provided) && _allowedKeys.Contains(provided))
        {
            await _next(ctx);
            return;
        }

        // Sinon => throttle par IP
        var ip = GetClientIp(ctx);

        var isAppointments = ctx.Request.Path.StartsWithSegments("/appointments");
        var limiter = isAppointments ? _appointmentsIpLimiter : _generalIpLimiter;

        using var lease = await limiter.AcquireAsync(ip, 1, ctx.RequestAborted);
        if (lease.IsAcquired)
        {
            await _next(ctx);
            return;
        }

        int? retryAfterSeconds = null;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) && ra is TimeSpan ts)
        {
            retryAfterSeconds = (int)Math.Ceiling(ts.TotalSeconds);
            ctx.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new
        {
            code = "RATE_LIMITED",
            message = "Trop de requêtes. Réessaye un peu plus tard.",
            retryAfterSeconds
        }, ctx.RequestAborted);
    }

    private static string GetClientIp(HttpContext ctx)
        => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string? GetProvidedApiKey(HttpContext ctx, string headerName)
    {
        if (!ctx.Request.Headers.TryGetValue(headerName, out var v)) return null;
        var s = v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
