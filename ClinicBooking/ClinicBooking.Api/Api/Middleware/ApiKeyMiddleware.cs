using ClinicBooking.Api.Api.Options;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Api.Api.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _opt;
    private readonly HashSet<string> _allowedKeys;
    private readonly IWebHostEnvironment _env;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IOptions<ApiKeyOptions> opt,
        IWebHostEnvironment env)
    {
        _next = next;
        _opt = opt.Value;
        _env = env;

        _allowedKeys = new HashSet<string>(
            _opt.Keys ?? Array.Empty<string>(),
            StringComparer.Ordinal
        );

        // compat si quelqu’un utilise encore "Key"
        if (!string.IsNullOrWhiteSpace(_opt.Key))
            _allowedKeys.Add(_opt.Key);
    }

    public async Task Invoke(HttpContext ctx)
    {
        // Swagger ouvert uniquement en DEV
        if (_env.IsDevelopment() && ctx.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(ctx);
            return;
        }

        // header name
        var headerName = string.IsNullOrWhiteSpace(_opt.HeaderName) ? "X-API-KEY" : _opt.HeaderName;

        // sécurité: si aucune clé serveur configurée => 500
        if (_allowedKeys.Count == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "API_KEY_NOT_CONFIGURED",
                message = "API Key non configurée côté serveur."
            });
            return;
        }

        // missing header => 401
        if (!ctx.Request.Headers.TryGetValue(headerName, out var provided) ||
            string.IsNullOrWhiteSpace(provided.ToString()))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new
            {
                code = "UNAUTHORIZED",
                message = $"API Key manquante. Header attendu: {headerName}"
            });
            return;
        }

        // invalid key => 403
        if (!_allowedKeys.Contains(provided.ToString()))
        {
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
}
