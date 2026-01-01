using ClinicBooking.Api.Contracts;
using ClinicBooking.Api.Domain.Entities;
using ClinicBooking.Api.Domain.Enums;
using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

 

namespace ClinicBooking.Api.Application.Services;

public class AppointmentService
{
    private readonly ClinicDbContext _db;

    public AppointmentService(ClinicDbContext db) => _db = db;

    public async Task<(bool Ok, string? Error, Appointment? Appointment)> CreateAsync(CreateAppointmentRequest req, CancellationToken ct)
    {
        var patientExists = await _db.Patients.AnyAsync(x => x.Id == req.PatientId, ct);
        if (!patientExists) return (false, "Patient introuvable.", null);

        var practitioner = await _db.Practitioners.FirstOrDefaultAsync(x => x.Id == req.PractitionerId, ct);
        if (practitioner is null) return (false, "Praticien introuvable.", null);
        if (!practitioner.IsActive) return (false, "Praticien inactif.", null);

        // règle anti-chevauchement :
        // chevauchement si Start < EndExisting && End > StartExisting
        var overlap = await _db.Appointments.AnyAsync(a =>
            a.PractitionerId == req.PractitionerId &&
            a.Status == AppointmentStatus.Scheduled &&
            a.StartUtc < req.EndUtc &&
            a.EndUtc > req.StartUtc
        , ct);

        if (overlap) return (false, "Créneau indisponible (double-booking).", null);

        var appt = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = req.PatientId,
            PractitionerId = req.PractitionerId,
            StartUtc = req.StartUtc,
            EndUtc = req.EndUtc,
            Notes = req.Notes,
            Status = AppointmentStatus.Scheduled,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(ct);

        return (true, null, appt);
    }
}
