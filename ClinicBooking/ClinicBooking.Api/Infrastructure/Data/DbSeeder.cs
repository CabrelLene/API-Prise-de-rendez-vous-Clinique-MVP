using ClinicBooking.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ClinicDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Patients.AnyAsync(ct))
        {
            db.Patients.Add(new Patient { FullName = "Jean Tremblay", Email = "jean@example.com", Phone = "514-000-0000" });
        }

        if (!await db.Practitioners.AnyAsync(ct))
        {
            db.Practitioners.AddRange(
                new Practitioner { FullName = "Dr. Sophie Roy", Specialty = "Family Medicine" },
                new Practitioner { FullName = "Dr. Karim Diallo", Specialty = "Dermatology" }
            );
        }

        await db.SaveChangesAsync(ct);
    }
}
