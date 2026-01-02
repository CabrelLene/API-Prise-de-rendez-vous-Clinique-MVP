using ClinicBooking.Api.Api.Options;
using ClinicBooking.Api.Contracts;
using ClinicBooking.Api.Domain.Entities;
using ClinicBooking.Api.Domain.Enums;
using ClinicBooking.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ClinicBooking.Api.Application.Services;

public class AppointmentService
{
    private readonly ClinicDbContext _db;
    private readonly AvailabilityOptions _availability;

    public AppointmentService(ClinicDbContext db, IOptions<AvailabilityOptions> availabilityOptions)
    {
        _db = db;
        _availability = availabilityOptions.Value;
    }

    public async Task<Appointment> CreateAsync(CreateAppointmentRequest req, CancellationToken ct = default)
    {
        // 1) existence patient
        var patientExists = await _db.Patients.AnyAsync(p => p.Id == req.PatientId, ct);
        if (!patientExists)
            throw new InvalidOperationException("Patient introuvable.");

        // 2) existence praticien + actif
        var practitioner = await _db.Practitioners
            .Where(p => p.Id == req.PractitionerId)
            .Select(p => new { p.Id, p.IsActive })
            .FirstOrDefaultAsync(ct);

        if (practitioner is null)
            throw new InvalidOperationException("Praticien introuvable.");

        if (!practitioner.IsActive)
            throw new InvalidOperationException("Praticien inactif.");

        // 3) check overlap côté app (UX : renvoie 409 propre)
        var overlaps = await _db.Appointments.AnyAsync(a =>
            a.PractitionerId == req.PractitionerId &&
            a.Status == AppointmentStatus.Scheduled &&
            a.StartUtc < req.EndUtc && req.StartUtc < a.EndUtc, ct); // logique [)

        if (overlaps)
            throw new AppointmentConflictException("Créneau déjà pris pour ce praticien.");

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

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "23P01" || ex.ConstraintName == "EX_Appointments_NoOverlap")
        {
            // 23P01 = exclusion_violation (ton EXCLUDE gist)
            throw new AppointmentConflictException("Créneau déjà pris pour ce praticien (DB).");
        }

        return appt;
    }

    // ✅ Cancel (idempotent)
    public async Task<Appointment> CancelAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appt is null)
            throw new InvalidOperationException("Rendez-vous introuvable.");

        if (appt.Status == AppointmentStatus.Cancelled)
            return appt;

        if (appt.Status == AppointmentStatus.Completed)
            throw new InvalidOperationException("Impossible d'annuler un rendez-vous terminé.");

        appt.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
        return appt;
    }

    // ✅ Legacy simple (from/to brut)
    public async Task<PractitionerAvailabilityResponse> GetAvailabilityAsync(
        Guid practitionerId,
        DateTime fromUtc,
        DateTime toUtc,
        int slotMinutes = 30,
        CancellationToken ct = default)
    {
        if (slotMinutes <= 0 || slotMinutes > 240)
            throw new InvalidOperationException("slotMinutes invalide.");

        if (toUtc <= fromUtc)
            throw new InvalidOperationException("ToUtc doit être après FromUtc.");

        // praticien existe + actif
        var practitioner = await _db.Practitioners
            .Where(p => p.Id == practitionerId)
            .Select(p => new { p.Id, p.IsActive })
            .FirstOrDefaultAsync(ct);

        if (practitioner is null)
            throw new InvalidOperationException("Praticien introuvable.");

        if (!practitioner.IsActive)
            throw new InvalidOperationException("Praticien inactif.");

        // ✅ FIX EF: OrderBy sur entity, puis Select vers BusyRange
        var busy = await _db.Appointments
            .Where(a =>
                a.PractitionerId == practitionerId &&
                a.Status == AppointmentStatus.Scheduled &&
                a.StartUtc < toUtc &&
                fromUtc < a.EndUtc)
            .OrderBy(a => a.StartUtc)
            .Select(a => new BusyRange(a.StartUtc, a.EndUtc))
            .ToListAsync(ct);

        var slots = BuildSlots(fromUtc, toUtc, slotMinutes, busy);

        return new PractitionerAvailabilityResponse(practitionerId, fromUtc, toUtc, slotMinutes, slots);
    }

    // ✅ Availability "PRO"
    public async Task<PractitionerAvailabilityResponse> GetAvailabilityProAsync(
        Guid practitionerId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        if (toDate < fromDate)
            throw new InvalidOperationException("toDate doit être >= fromDate.");

        // praticien existe + actif
        var practitioner = await _db.Practitioners
            .Where(p => p.Id == practitionerId)
            .Select(p => new { p.Id, p.IsActive })
            .FirstOrDefaultAsync(ct);

        if (practitioner is null)
            throw new InvalidOperationException("Praticien introuvable.");

        if (!practitioner.IsActive)
            throw new InvalidOperationException("Praticien inactif.");

        var tz = ResolveTimeZone(_availability.TimeZoneId);
        var slotMinutes = _availability.SlotMinutes <= 0 ? 30 : _availability.SlotMinutes;

        var open = ParseTimeOnly(_availability.OpenTime, "OpenTime");
        var close = ParseTimeOnly(_availability.CloseTime, "CloseTime");
        if (close <= open) throw new InvalidOperationException("CloseTime doit être après OpenTime.");

        TimeOnly? lunchStart = string.IsNullOrWhiteSpace(_availability.LunchStart) ? null : ParseTimeOnly(_availability.LunchStart!, "LunchStart");
        TimeOnly? lunchEnd = string.IsNullOrWhiteSpace(_availability.LunchEnd) ? null : ParseTimeOnly(_availability.LunchEnd!, "LunchEnd");

        if (lunchStart is not null && lunchEnd is not null && lunchEnd <= lunchStart)
            throw new InvalidOperationException("LunchEnd doit être après LunchStart.");

        var workingDays = ParseWorkingDays(_availability.WorkingDays);

        // Fenêtre globale UTC (pour requête DB)
        var globalFromUtc = ToUtc(fromDate, open, tz);
        var globalToUtc = ToUtc(toDate.AddDays(1), close, tz);

        // ✅ FIX EF: OrderBy sur entity, puis Select vers BusyRange
        var busy = await _db.Appointments
            .Where(a =>
                a.PractitionerId == practitionerId &&
                a.Status == AppointmentStatus.Scheduled &&
                a.StartUtc < globalToUtc &&
                globalFromUtc < a.EndUtc)
            .OrderBy(a => a.StartUtc)
            .Select(a => new BusyRange(a.StartUtc, a.EndUtc))
            .ToListAsync(ct);

        var slots = new List<AvailabilitySlot>();

        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            if (!workingDays.Contains(d.DayOfWeek)) continue;

            if (lunchStart is not null && lunchEnd is not null)
            {
                var morningStartUtc = ToUtc(d, open, tz);
                var morningEndUtc = ToUtc(d, lunchStart.Value, tz);
                if (morningEndUtc > morningStartUtc)
                    slots.AddRange(BuildSlots(morningStartUtc, morningEndUtc, slotMinutes, busy));

                var afternoonStartUtc = ToUtc(d, lunchEnd.Value, tz);
                var afternoonEndUtc = ToUtc(d, close, tz);
                if (afternoonEndUtc > afternoonStartUtc)
                    slots.AddRange(BuildSlots(afternoonStartUtc, afternoonEndUtc, slotMinutes, busy));
            }
            else
            {
                var dayStartUtc = ToUtc(d, open, tz);
                var dayEndUtc = ToUtc(d, close, tz);
                if (dayEndUtc > dayStartUtc)
                    slots.AddRange(BuildSlots(dayStartUtc, dayEndUtc, slotMinutes, busy));
            }
        }

        return new PractitionerAvailabilityResponse(
            practitionerId,
            globalFromUtc,
            globalToUtc,
            slotMinutes,
            slots
        );
    }

    // ===== Helpers =====

    private static List<AvailabilitySlot> BuildSlots(DateTime fromUtc, DateTime toUtc, int slotMinutes, List<BusyRange> busy)
    {
        var slots = new List<AvailabilitySlot>();
        var cursor = RoundUpToSlot(fromUtc, slotMinutes);

        while (cursor.AddMinutes(slotMinutes) <= toUtc)
        {
            var slotStart = cursor;
            var slotEnd = cursor.AddMinutes(slotMinutes);

            var overlaps = busy.Any(b => b.StartUtc < slotEnd && slotStart < b.EndUtc);
            if (!overlaps)
                slots.Add(new AvailabilitySlot(slotStart, slotEnd));

            cursor = slotEnd;
        }

        return slots;
    }

    private static DateTime RoundUpToSlot(DateTime dt, int slotMinutes)
    {
        var ticks = dt.Ticks;
        var slotTicks = TimeSpan.FromMinutes(slotMinutes).Ticks;
        var rounded = ((ticks + slotTicks - 1) / slotTicks) * slotTicks;
        return new DateTime(rounded, DateTimeKind.Utc);
    }

    private static TimeOnly ParseTimeOnly(string value, string fieldName)
    {
        if (!TimeOnly.TryParse(value, out var t))
            throw new InvalidOperationException($"{fieldName} invalide (ex: \"09:00\").");
        return t;
    }

    private static HashSet<DayOfWeek> ParseWorkingDays(IReadOnlyList<string>? days)
    {
        if (days is null || days.Count == 0)
            return new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        var set = new HashSet<DayOfWeek>();
        foreach (var s in days)
        {
            if (Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out var d))
                set.Add(d);
            else
                throw new InvalidOperationException($"WorkingDays contient une valeur invalide: {s}");
        }
        return set;
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Utc;

        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch { return TimeZoneInfo.Utc; }
    }

    private static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, tz);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    private readonly record struct BusyRange(DateTime StartUtc, DateTime EndUtc);
}

public sealed class AppointmentConflictException : Exception
{
    public AppointmentConflictException(string message) : base(message) { }
}
