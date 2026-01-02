using ClinicBooking.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace ClinicBooking.Api.ApiMiddleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (AppointmentConflictException ex)
        {
            await WriteJson(httpContext, StatusCodes.Status409Conflict,
                code: "APPOINTMENT_OVERLAP",
                message: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // 404 si "introuvable", sinon 400
            var status = ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            var code = status == StatusCodes.Status404NotFound ? "NOT_FOUND" : "BAD_REQUEST";

            await WriteJson(httpContext, status, code, ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "23P01" || ex.ConstraintName == "EX_Appointments_NoOverlap")
        {
            // exclusion_violation (DB overlap)
            await WriteJson(httpContext, StatusCodes.Status409Conflict,
                code: "APPOINTMENT_OVERLAP",
                message: "Créneau déjà pris pour ce praticien (DB).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            await WriteJson(httpContext, StatusCodes.Status500InternalServerError,
                code: "INTERNAL_ERROR",
                message: "Erreur interne. Réessaie plus tard.");
        }
    }

    private static async Task WriteJson(HttpContext httpContext, int statusCode, string code, string message)
    {
        if (httpContext.Response.HasStarted) return;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await httpContext.Response.WriteAsJsonAsync(new { code, message });
    }
}
