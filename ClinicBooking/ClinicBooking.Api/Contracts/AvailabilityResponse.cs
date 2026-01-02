namespace ClinicBooking.Api.Contracts;

public record TimeRangeDto(DateTime StartUtc, DateTime EndUtc);

public record AvailabilityResponse(
    Guid PractitionerId,
    DateTime FromUtc,
    DateTime ToUtc,
    int DurationMinutes,
    int StepMinutes,
    IReadOnlyList<TimeRangeDto> Busy,
    IReadOnlyList<TimeRangeDto> AvailableSlots
);
