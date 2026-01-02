// Controllers/AppointmentsController.cs
using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("appointments")]
[EnableRateLimiting("appointments-10rpm")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _service;

    public AppointmentsController(AppointmentService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        try
        {
            var created = await _service.CreateAsync(req, ct);
            return Created($"/appointments/{created.Id}", created);
        }
        catch (AppointmentConflictException ex)
        {
            return Conflict(new { code = "APPOINTMENT_OVERLAP", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { code = "NOT_FOUND", message = ex.Message });

            return BadRequest(new { code = "BAD_REQUEST", message = ex.Message });
        }
    }

    // PATCH /appointments/{id}/cancel
    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var updated = await _service.CancelAsync(id, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { code = "NOT_FOUND", message = ex.Message });

            return BadRequest(new { code = "BAD_REQUEST", message = ex.Message });
        }
    }
}
