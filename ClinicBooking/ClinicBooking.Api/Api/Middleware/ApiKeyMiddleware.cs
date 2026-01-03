using ClinicBooking.Api.Api.Options;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Api.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _opt;
    private readonly HashSet<string> _allowedKeys;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> opt)
    {
        _next = next;
        _opt = opt.Value;

        _allowedKeys = new HashSet<string>(_opt.Keys ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public async Task Invoke(HttpContext ctx)
    {
        // ✅ Endpoints publics (portfolio-friendly) + Swagger
        // (ces routes doivent répondre même sans API key)
        if (IsPublicEndpoint(ctx) || IsSwagger(ctx))
        {
            await _next(ctx);
            return;
        }

        // Si aucune clé configurée => 500 (config bug)
        if (_allowedKeys.Count == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_NOT_CONFIGURED",
                message = "Aucune API key n'est configurée côté serveur."
            });
            return;
        }

        // Header manquant => 401
        if (!ctx.Request.Headers.TryGetValue(_opt.HeaderName, out var providedRaw))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_MISSING",
                message = $"API Key manquante. Header attendu: {_opt.HeaderName}"
            });
            return;
        }

        var provided = providedRaw.ToString();
        if (string.IsNullOrWhiteSpace(provided) || !_allowedKeys.Contains(provided))
        {
            // Clé présente mais invalide => 403
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_INVALID",
                message = "API key invalide."
            });
            return;
        }

        await _next(ctx);
    }

    private static bool IsSwagger(HttpContext ctx)
        => ctx.Request.Path.StartsWithSegments("/swagger");

    private static bool IsPublicEndpoint(HttpContext ctx)
    {
        var p = ctx.Request.Path.Value ?? "";
        return p == "/" || p.StartsWith("/health") || p.StartsWith("/version");
    }
}
