using FluentValidation;

namespace Odary.Api.Modules.Patient.Validators;

public class ArchivePatientValidator : AbstractValidator<PatientCommands.V1.ArchivePatient>
{
    public ArchivePatientValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Patient ID is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Archive reason is required")
            .MinimumLength(10).WithMessage("Archive reason must be at least 10 characters")
            .MaximumLength(255).WithMessage("Archive reason cannot exceed 255 characters");
    }
} 