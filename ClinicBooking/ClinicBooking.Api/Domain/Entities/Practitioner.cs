namespace ClinicBooking.Api.Domain.Entities;

public class Practitioner
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Specialty { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();
}
