// Program.cs
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using ClinicBooking.Api.Api.Middleware;
using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Application.Validators;
using ClinicBooking.Api.Infrastructure.Data;

using FluentValidation;
using FluentValidation.AspNetCore;

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

// ===== Swagger + API Key support =====
var apiKeyHeaderName = builder.Configuration
    .GetSection(ApiKeyOptions.SectionName)
    .GetValue<string>("HeaderName") ?? "X-API-KEY";

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

// ===== Rate limiting (anti-bypass + réponse JSON propre) =====

// Support "ApiKey:Keys": ["a","b"] + fallback "ApiKey:Key": "a"
var allowedKeys = builder.Configuration
    .GetSection(ApiKeyOptions.SectionName)
    .GetSection("Keys")
    .Get<string[]>();

if (allowedKeys is null || allowedKeys.Length == 0)
{
    var single = builder.Configuration
        .GetSection(ApiKeyOptions.SectionName)
        .GetValue<string>("Key");

    allowedKeys = string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single };
}

var allowedKeySet = new HashSet<string>(allowedKeys, StringComparer.Ordinal);

static string GetClientIp(HttpContext ctx)
    => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static string? GetProvidedApiKey(HttpContext ctx, string headerName)
{
    if (!ctx.Request.Headers.TryGetValue(headerName, out var v)) return null;
    var s = v.ToString();
    return string.IsNullOrWhiteSpace(s) ? null : s;
}

static bool IsSwagger(HttpContext ctx)
    => ctx.Request.Path.StartsWithSegments("/swagger");

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

    // ✅ Global limiter : clé valide => par KEY (60/min), sinon => par IP (30/min)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (IsSwagger(ctx)) return RateLimitPartition.GetNoLimiter("swagger");

        var provided = GetProvidedApiKey(ctx, apiKeyHeaderName);

        if (provided is not null && allowedKeySet.Contains(provided))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"key:{provided}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        var ip = GetClientIp(ctx);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // ✅ Endpoint policy : /appointments => clé valide 10/min, sinon IP 5/min
    options.AddPolicy("appointments-10rpm", ctx =>
    {
        var provided = GetProvidedApiKey(ctx, apiKeyHeaderName);

        if (provided is not null && allowedKeySet.Contains(provided))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"appointments:key:{provided}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        var ip = GetClientIp(ctx);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"appointments:ip:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

// ✅ Global JSON errors (très tôt)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

// ✅ IMPORTANT : routing avant rate limiter pour que [EnableRateLimiting] marche
app.UseRouting();

// ✅ Rate limiter AVANT ApiKeyMiddleware (pour throttler les abus)
app.UseRateLimiter();

// ✅ Auth API Key
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

// Seed DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.Run();
