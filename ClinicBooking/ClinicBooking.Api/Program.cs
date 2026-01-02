using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using ClinicBooking.Api.Api.Middleware;
using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Application.Validators;
using ClinicBooking.Api.Infrastructure.Data;

using FluentValidation;
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

// ✅ Forwarded headers (derrière proxy / load balancer)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // ⚠️ PROD: idéalement, configure KnownProxies / KnownNetworks (sinon spoof possible).
    // Ici on clear pour que ça marche derrière proxy sans config additionnelle.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// ===== Rate limiting (post-auth: par KEY) + réponse JSON propre =====
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

    // ✅ Global limiter (s'applique à tout) : 60 req/min par API Key
    // IMPORTANT: ne casse pas les policies endpoint (ex: appointments-10rpm)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (IsSwagger(ctx)) return RateLimitPartition.GetNoLimiter("swagger");

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

    // ✅ Policy spécifique /appointments : 10 req/min par API Key
    options.AddPolicy("appointments-10rpm", ctx =>
    {
        if (IsSwagger(ctx)) return RateLimitPartition.GetNoLimiter("swagger");

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

// ✅ Forwarded headers DOIT être tout en haut du pipeline (avant IP-based logic)
app.UseForwardedHeaders();

// ✅ Global JSON errors (très tôt)
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

// ✅ Pré-auth anti-bypass (invalid/missing => rate limit IP)
app.UseMiddleware<PreAuthRateLimitMiddleware>();

// ✅ Auth API key
app.UseMiddleware<ApiKeyMiddleware>();

// ✅ Rate limiter (global + endpoint policies)
app.UseRateLimiter();

// ✅ IMPORTANT : ne mets PAS RequireRateLimiting ici (sinon tu écrases appointments-10rpm)
app.MapControllers();

// Seed DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.Run();

// ✅ pour WebApplicationFactory<Program> (tests)
public partial class Program { }
