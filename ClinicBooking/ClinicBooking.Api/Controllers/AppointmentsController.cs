using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _service;

    public AppointmentsController(AppointmentService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        var (ok, error, appt) = await _service.CreateAsync(req, ct);

        if (!ok)
            return Conflict(new { message = error }); // 409 = créneau pris / règles métier

        return Created($"/appointments/{appt!.Id}", new
        {
            appt.Id,
            appt.PatientId,
            appt.PractitionerId,
            appt.StartUtc,
            appt.EndUtc,
            appt.Status,
            appt.Notes
        });
    }
}
