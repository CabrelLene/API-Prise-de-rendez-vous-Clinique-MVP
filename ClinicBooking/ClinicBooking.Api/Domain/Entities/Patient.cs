namespace ClinicBooking.Api.Domain.Entities;

public class Patient
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
