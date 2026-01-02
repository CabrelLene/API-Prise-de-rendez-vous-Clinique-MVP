// Program.cs
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.ApiMiddleware;
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

// ✅ Validation JSON propre (au lieu du ProblemDetails par défaut)
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

// ===== Rate limiting (anti-abuse) + réponse JSON propre =====
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

    // 60 req / minute par API Key (global)
    options.AddPolicy("apikey-60rpm", httpContext =>
    {
        var key = httpContext.Request.Headers["X-API-KEY"].ToString();
        if (string.IsNullOrWhiteSpace(key)) key = "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // 10 req / minute par API Key pour /appointments
    options.AddPolicy("appointments-10rpm", httpContext =>
    {
        var key = httpContext.Request.Headers["X-API-KEY"].ToString();
        if (string.IsNullOrWhiteSpace(key)) key = "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"appointments:{key}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// ===== Swagger + API Key support =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ClinicBooking API", Version = "v1" });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Description = "API Key required. Put your key in X-API-KEY header."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
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

// ===== Options =====
builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SectionName)
);
builder.Services.Configure<AvailabilityOptions>(
    builder.Configuration.GetSection(AvailabilityOptions.SectionName)
);

var app = builder.Build();

// ✅ Global JSON errors (doit être très tôt)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// ✅ Rate limit avant la protection API key
app.UseRateLimiter();

// API Key
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

// Seed DB
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.Run();
