namespace ClinicBooking.Api.Contracts;

public record AvailabilitySlot(DateTime StartUtc, DateTime EndUtc);

public record PractitionerAvailabilityResponse(
    Guid PractitionerId,
    DateTime FromUtc,
    DateTime ToUtc,
    int SlotMinutes,
    IReadOnlyList<AvailabilitySlot> AvailableSlots
);
