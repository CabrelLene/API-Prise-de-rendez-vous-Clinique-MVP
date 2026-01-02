using ClinicBooking.Api.Api.Options;
using Microsoft.Extensions.Options;

namespace ClinicBooking.Api.ApiMiddleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;

    public ApiKeyMiddleware(RequestDelegate next, IOptions<ApiKeyOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Laisse passer Swagger
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var headerName = string.IsNullOrWhiteSpace(_options.HeaderName) ? "X-API-KEY" : _options.HeaderName;

        if (!context.Request.Headers.TryGetValue(headerName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { code = "API_KEY_MISSING", message = "API key manquante." });
            return;
        }

        if (!string.Equals(providedKey.ToString(), _options.Key, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { code = "API_KEY_INVALID", message = "API key invalide." });
            return;
        }

        await _next(context);
    }
}
