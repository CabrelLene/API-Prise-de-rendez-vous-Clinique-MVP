using ClinicBooking.Api.Contracts;
using ClinicBooking.Api.Domain.Entities;
using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicBooking.Api.Application.Services;

public class AppointmentService
{
    private readonly ClinicDbContext _db;

    public AppointmentService(ClinicDbContext db)
    {
        _db = db;
    }

    public async Task<(bool ok, string? error, Appointment? created)> CreateAsync(CreateAppointmentRequest req, CancellationToken ct)
    {
        // 1) Existence patient/praticien
        var patientExists = await _db.Patients.AnyAsync(p => p.Id == req.PatientId, ct);
        if (!patientExists) return (false, "Patient introuvable.", null);

        var practitioner = await _db.Practitioners.FirstOrDefaultAsync(p => p.Id == req.PractitionerId && p.IsActive, ct);
        if (practitioner is null) return (false, "Praticien introuvable ou inactif.", null);

        // 2) Vérif chevauchement (interval overlap)
        // Overlap si: Start < existingEnd ET End > existingStart
        var overlap = await _db.Appointments.AnyAsync(a =>
            a.PractitionerId == req.PractitionerId &&
            a.Status == "Booked" &&
            req.StartUtc < a.EndUtc &&
            req.EndUtc > a.StartUtc
        , ct);

        if (overlap) return (false, "Créneau déjà pris pour ce praticien.", null);

        // 3) Création
        var appt = new Appointment
        {
            PatientId = req.PatientId,
            PractitionerId = req.PractitionerId,
            StartUtc = req.StartUtc,
            EndUtc = req.EndUtc,
            Notes = req.Notes,
            Status = "Booked"
        };

        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(ct);

        return (true, null, appt);
    }
}
