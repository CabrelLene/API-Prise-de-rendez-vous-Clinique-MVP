namespace ClinicBooking.Api.Api.Options;

public sealed class ClinicHoursOptions
{
    public const string SectionName = "ClinicHours";

    public string TimeZoneId { get; set; } = "America/Toronto"; // Québec
    public int SlotMinutes { get; set; } = 30;

    public string WorkStart { get; set; } = "09:00";
    public string WorkEnd { get; set; } = "17:00";

    public string BreakStart { get; set; } = "12:00";
    public string BreakEnd { get; set; } = "13:00";

    public string[] WorkingDays { get; set; } = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
}
