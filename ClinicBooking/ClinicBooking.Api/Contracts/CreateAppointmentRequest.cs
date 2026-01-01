namespace ClinicBooking.Api.Contracts;

public record CreateAppointmentRequest(
    Guid PatientId,
    Guid PractitionerId,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Notes
);
