using ClinicBooking.Api.Api.Options;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Api.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _opt;
    private readonly HashSet<string> _allowed;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> opt)
    {
        _next = next;
        _opt = opt.Value;

        _allowed = new HashSet<string>(_opt.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public async Task Invoke(HttpContext ctx)
    {
        // Swagger ouvert (DEV-friendly)
        if (ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(ctx);
            return;
        }

        if (_allowed.Count == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_NOT_CONFIGURED",
                message = "Aucune API key configurée côté serveur."
            });
            return;
        }

        if (!ctx.Request.Headers.TryGetValue(_opt.HeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided) ||
            !_allowed.Contains(provided.ToString()))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_INVALID",
                message = $"API key invalide. Header attendu: {_opt.HeaderName}"
            });
            return;
        }

        await _next(ctx);
    }
}
