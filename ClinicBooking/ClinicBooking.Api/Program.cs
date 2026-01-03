// Program.cs
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using ClinicBooking.Api.Api.Middleware;
using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Application.Validators;
using ClinicBooking.Api.Infrastructure.Data;

using FluentValidation;                 // ✅ IMPORTANT
using FluentValidation.AspNetCore;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ✅ Enums en string dans le JSON
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ✅ Validation JSON propre
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        return new BadRequestObjectResult(new
        {
            code = "VALIDATION_ERROR",
            message = "Requête invalide.",
            errors
        });
    };
});

// ===== Options =====
builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SectionName)
);
builder.Services.Configure<AvailabilityOptions>(
    builder.Configuration.GetSection(AvailabilityOptions.SectionName)
);

// Pour Swagger + extraction header
var apiKeyHeaderName = builder.Configuration
    .GetSection(ApiKeyOptions.SectionName)
    .GetValue<string>("HeaderName") ?? "X-API-KEY";

static string? GetProvidedApiKey(HttpContext ctx, string headerName)
{
    if (!ctx.Request.Headers.TryGetValue(headerName, out var v)) return null;
    var s = v.ToString();
    return string.IsNullOrWhiteSpace(s) ? null : s;
}

static bool IsSwagger(HttpContext ctx)
    => ctx.Request.Path.StartsWithSegments("/swagger");

// ✅ Endpoints publics (portfolio-friendly)
static bool IsPublicEndpoint(HttpContext ctx)
{
    var p = ctx.Request.Path.Value ?? "";
    return p == "/" || p.StartsWith("/health") || p.StartsWith("/version");
}

// ===== Swagger + API Key support =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ClinicBooking API", Version = "v1" });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = apiKeyHeaderName,
        In = ParameterLocation.Header,
        Description = $"API Key required. Put your key in {apiKeyHeaderName} header."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== EF Core =====
builder.Services.AddDbContext<ClinicDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default");
    opt.UseNpgsql(cs);
});

// ===== FluentValidation =====
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateAppointmentRequestValidator>();

// ===== Business services =====
builder.Services.AddScoped<AppointmentService>();

// ===== ForwardedHeaders (proxy/LB) =====
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.ForwardLimit = builder.Configuration.GetSection("ForwardedHeaders").GetValue<int?>("ForwardLimit") ?? 2;

    if (builder.Environment.IsDevelopment())
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        return;
    }

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    var proxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>();
    foreach (var p in proxies)
    {
        if (IPAddress.TryParse(p, out var ip))
            options.KnownProxies.Add(ip);
    }

    var networks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
    foreach (var n in networks)
    {
        var parts = n.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) continue;

        if (!IPAddress.TryParse(parts[0], out var prefix)) continue;
        if (!int.TryParse(parts[1], out var prefixLen)) continue;

        // ✅ FIX ambiguité IPNetwork
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLen));
    }
});

// ===== Rate limiting + réponse JSON propre =====
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        var http = context.HttpContext;

        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) && ra is TimeSpan ts)
        {
            retryAfterSeconds = (int)Math.Ceiling(ts.TotalSeconds);
            http.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsJsonAsync(new
        {
            code = "RATE_LIMITED",
            message = "Trop de requêtes. Réessaye un peu plus tard.",
            retryAfterSeconds
        }, cancellationToken: ct);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        // ✅ ne limite pas swagger / endpoints publics
        if (IsSwagger(ctx) || IsPublicEndpoint(ctx)) return RateLimitPartition.GetNoLimiter("public");

        var key = GetProvidedApiKey(ctx, apiKeyHeaderName) ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"key:{key}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("appointments-10rpm", ctx =>
    {
        // ✅ ne limite pas swagger / endpoints publics
        if (IsSwagger(ctx) || IsPublicEndpoint(ctx)) return RateLimitPartition.GetNoLimiter("public");

        var key = GetProvidedApiKey(ctx, apiKeyHeaderName) ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"appointments:key:{key}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

// ✅ Endpoints publics AVANT middlewares (sinon ApiKeyMiddleware bloque)
app.MapGet("/", () => Results.Ok(new
{
    name = "ClinicBooking API",
    status = "ok",
    docs = "/swagger",
    health = "/health",
    version = "/version"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTime.UtcNow
}));

app.MapGet("/version", () => Results.Ok(new
{
    environment = app.Environment.EnvironmentName,
    commit = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") ?? "local",
    utc = DateTime.UtcNow
}));

app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<PreAuthRateLimitMiddleware>();

app.UseMiddleware<ApiKeyMiddleware>();

app.UseRateLimiter();

app.MapControllers();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { }
