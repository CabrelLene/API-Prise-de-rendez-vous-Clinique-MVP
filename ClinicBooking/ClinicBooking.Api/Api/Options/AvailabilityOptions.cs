using System.ComponentModel.DataAnnotations;

namespace ClinicBooking.Api.Api.Options;

public sealed class AvailabilityOptions
{
    public const string SectionName = "Availability";

    // IANA recommandé (container/Linux): "America/Toronto"
    [Required]
    public string TimeZoneId { get; init; } = "UTC";

    // Format: "09:00"
    [Required]
    public string OpenTime { get; init; } = "09:00";

    [Required]
    public string CloseTime { get; init; } = "17:00";

    // Pause optionnelle (laisser null/"" si pas de pause)
    public string? LunchStart { get; init; } = "12:00";
    public string? LunchEnd { get; init; } = "13:00";

    [Range(5, 240)]
    public int SlotMinutes { get; init; } = 30;

    // ✅ IMPORTANT: strings (pas int[])
    // Valeurs attendues: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday
    public List<string> WorkingDays { get; init; } = new()
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
    };
}
