using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Controllers;

[ApiController]
[Route("api/practitioners")]
public class PractitionersController : ControllerBase
{
    private readonly ClinicDbContext _db;

    public PractitionersController(ClinicDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var practitioners = await _db.Practitioners
            .OrderBy(p => p.FullName)
            .Select(p => new { p.Id, p.FullName, p.Specialty, p.IsActive, p.CreatedAtUtc })
            .ToListAsync(ct);

        return Ok(practitioners);
    }
}
