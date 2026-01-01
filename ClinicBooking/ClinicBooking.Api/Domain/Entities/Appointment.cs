namespace ClinicBooking.Api.Domain.Entities;

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = default!;

    public Guid PractitionerId { get; set; }
    public Practitioner Practitioner { get; set; } = default!;

    // On stocke UTC en DB (propre, standard)
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public string Status { get; set; } = "Booked"; // Booked / Cancelled / Completed
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
