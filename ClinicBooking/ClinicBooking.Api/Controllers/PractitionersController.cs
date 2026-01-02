// Controllers/PractitionersController.cs
using ClinicBooking.Api.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("practitioners")]
[EnableRateLimiting("apikey-60rpm")]
public class PractitionersController : ControllerBase
{
    private readonly AppointmentService _service;

    public PractitionersController(AppointmentService service) => _service = service;

    // GET /practitioners/{id}/availability?fromDate=2026-01-02&toDate=2026-01-07
    [HttpGet("{id:guid}/availability")]
    public async Task<IActionResult> GetAvailabilityPro(
        [FromRoute] Guid id,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken ct = default)
    {
        var result = await _service.GetAvailabilityProAsync(id, fromDate, toDate, ct);
        return Ok(result);
    }
}
