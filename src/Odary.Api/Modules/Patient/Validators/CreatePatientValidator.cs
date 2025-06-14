using FluentValidation;
using System.Text.RegularExpressions;

namespace Odary.Api.Modules.Patient.Validators;

public class CreatePatientValidator : AbstractValidator<PatientCommands.V1.CreatePatient>
{
    public CreatePatientValidator()
    {
        // Required fields
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(50).WithMessage("First name cannot exceed 50 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(50).WithMessage("Last name cannot exceed 50 characters");

        RuleFor(x => x.DateOfBirth)
            .NotEmpty().WithMessage("Date of birth is required")
            .Must(BeValidAge).WithMessage("Patient must be between 0 and 150 years old");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender is required")
            .Must(BeValidGender).WithMessage("Gender must be Male, Female, or Other");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .MaximumLength(20).WithMessage("Phone number cannot exceed 20 characters")
            .Must(BeValidPhoneNumber).WithMessage("Phone number must be in valid format with country code");

        // Optional fields with validation when provided
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be in valid format")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Street)
            .MaximumLength(255).WithMessage("Street cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.Street));

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.ZipCode)
            .MaximumLength(20).WithMessage("ZIP code cannot exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.ZipCode));

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Country));

        RuleFor(x => x.InsuranceProvider)
            .MaximumLength(255).WithMessage("Insurance provider cannot exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.InsuranceProvider));

        RuleFor(x => x.InsurancePolicyNumber)
            .MaximumLength(100).WithMessage("Insurance policy number cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.InsurancePolicyNumber));

        // Array validations for medical information
        RuleFor(x => x.Allergies)
            .Must(BeValidStringArray).WithMessage("Each allergy entry cannot exceed 100 characters")
            .When(x => x.Allergies != null && x.Allergies.Length > 0);

        RuleFor(x => x.MedicalConditions)
            .Must(BeValidStringArray).WithMessage("Each medical condition entry cannot exceed 200 characters")
            .When(x => x.MedicalConditions != null && x.MedicalConditions.Length > 0);

        RuleFor(x => x.CurrentMedications)
            .Must(BeValidStringArray).WithMessage("Each medication entry cannot exceed 200 characters")
            .When(x => x.CurrentMedications != null && x.CurrentMedications.Length > 0);

        RuleFor(x => x.EmergencyContactName)
            .MaximumLength(100).WithMessage("Emergency contact name cannot exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactName));

        RuleFor(x => x.EmergencyContactNumber)
            .MaximumLength(20).WithMessage("Emergency contact number cannot exceed 20 characters")
            .Must(BeValidPhoneNumber).WithMessage("Emergency contact number must be in valid format with country code")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactNumber));

        RuleFor(x => x.EmergencyContactRelationship)
            .MaximumLength(50).WithMessage("Emergency contact relationship cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContactRelationship));
    }

    private static bool BeValidAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
            age--;
            
        return age >= 0 && age <= 150;
    }

    private static bool BeValidGender(string gender)
    {
        var validGenders = new[] { "Male", "Female", "Other" };
        return validGenders.Contains(gender, StringComparer.OrdinalIgnoreCase);
    }

    private static bool BeValidPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return false;

        // Basic phone number validation - must start with + and contain only digits, spaces, hyphens, parentheses
        var phoneRegex = new Regex(@"^\+[1-9]\d{1,14}$|^\+[1-9]\d{0,3}[\s\-\(\)]*\d{4,14}$");
        return phoneRegex.IsMatch(phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", ""));
    }

    private static bool BeValidStringArray(string[]? array)
    {
        if (array == null || array.Length == 0)
            return true;

        // Check that no entry is null, empty, or exceeds reasonable length
        return array.All(item => !string.IsNullOrWhiteSpace(item) && item.Length <= 200);
    }
} 