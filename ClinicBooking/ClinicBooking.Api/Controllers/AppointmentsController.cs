using ClinicBooking.Api.Application.Services;
using ClinicBooking.Api.Contracts;
using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly AppointmentService _service;
    private readonly ClinicDbContext _db;

    public AppointmentsController(AppointmentService service, ClinicDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        var (ok, error, created) = await _service.CreateAsync(req, ct);
        if (!ok) return BadRequest(new { error });

        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, new { created!.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var appt = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Practitioner)
            .Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.StartUtc,
                a.EndUtc,
                a.Notes,
                Patient = new { a.Patient.Id, a.Patient.FullName },
                Practitioner = new { a.Practitioner.Id, a.Practitioner.FullName, a.Practitioner.Specialty }
            })
            .FirstOrDefaultAsync(ct);

        return appt is null ? NotFound() : Ok(appt);
    }

    // Planning du jour (UTC). Exemple: /api/appointments/day?practitionerId=...&date=2026-01-01
    [HttpGet("day")]
    public async Task<IActionResult> GetDay([FromQuery] Guid practitionerId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        var items = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a =>
                a.PractitionerId == practitionerId &&
                a.Status == "Booked" &&
                a.StartUtc >= start &&
                a.StartUtc < end
            )
            .OrderBy(a => a.StartUtc)
            .Select(a => new
            {
                a.Id,
                a.StartUtc,
                a.EndUtc,
                Patient = new { a.PatientId, a.Patient.FullName },
                a.Notes
            })
            .ToListAsync(ct);

        return Ok(new { practitionerId, date = date.ToString("yyyy-MM-dd"), count = items.Count, items });
    }
}
