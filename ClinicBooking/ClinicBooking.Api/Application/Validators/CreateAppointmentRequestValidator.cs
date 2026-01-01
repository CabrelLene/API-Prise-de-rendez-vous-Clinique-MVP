using FluentValidation;
using ClinicBooking.Api.Contracts;

namespace ClinicBooking.Api.Application.Validators;

public class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.PractitionerId).NotEmpty();
        RuleFor(x => x.StartUtc).NotEmpty();
        RuleFor(x => x.EndUtc).NotEmpty();
        RuleFor(x => x).Must(x => x.EndUtc > x.StartUtc)
            .WithMessage("EndUtc doit être après StartUtc.");
    }
}
