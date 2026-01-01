using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly ClinicDbContext _db;

    public PatientsController(ClinicDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var patients = await _db.Patients
            .OrderBy(p => p.FullName)
            .Select(p => new { p.Id, p.FullName, p.Email, p.Phone, p.CreatedAtUtc })
            .ToListAsync(ct);

        return Ok(patients);
    }
}
