namespace Odary.Api.Modules.Patient;

public static class PatientMappings
{
    public static PatientResources.V1.Patient ToContract(this Domain.Patient patient)
    {
        return new PatientResources.V1.Patient
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            FullName = $"{patient.FirstName} {patient.LastName}".Trim(),
            DateOfBirth = patient.DateOfBirth,
            Age = CalculateAge(patient.DateOfBirth),
            Gender = patient.Gender,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.Email,
            Street = patient.Street,
            City = patient.City,
            ZipCode = patient.ZipCode,
            Country = patient.Country,
            InsuranceProvider = patient.InsuranceProvider,
            InsurancePolicyNumber = patient.InsurancePolicyNumber,
            Allergies = patient.Allergies,
            MedicalConditions = patient.MedicalConditions,
            CurrentMedications = patient.CurrentMedications,
            EmergencyContactName = patient.EmergencyContactName,
            EmergencyContactNumber = patient.EmergencyContactNumber,
            EmergencyContactRelationship = patient.EmergencyContactRelationship,
            Notes = patient.Notes,
            IsArchived = patient.IsArchived,
            ArchivedAt = patient.ArchivedAt,
            ArchiveReason = patient.ArchiveReason,
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };
    }

    public static PatientQueries.V1.GetPatient.Response ToGetPatientResponse(this Domain.Patient patient)
    {
        return new PatientQueries.V1.GetPatient.Response
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Age = CalculateAge(patient.DateOfBirth),
            Gender = patient.Gender,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.Email,
            Street = patient.Street,
            City = patient.City,
            ZipCode = patient.ZipCode,
            Country = patient.Country,
            InsuranceProvider = patient.InsuranceProvider,
            InsurancePolicyNumber = patient.InsurancePolicyNumber,
            Allergies = patient.Allergies,
            MedicalConditions = patient.MedicalConditions,
            CurrentMedications = patient.CurrentMedications,
            EmergencyContactName = patient.EmergencyContactName,
            EmergencyContactNumber = patient.EmergencyContactNumber,
            EmergencyContactRelationship = patient.EmergencyContactRelationship,
            Notes = patient.Notes,
            IsArchived = patient.IsArchived,
            ArchivedAt = patient.ArchivedAt,
            ArchiveReason = patient.ArchiveReason,
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };
    }

    public static PatientResources.V1.CreatePatientResponse ToCreatePatientResponse(this Domain.Patient patient)
    {
        return new PatientResources.V1.CreatePatientResponse
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            FullName = $"{patient.FirstName} {patient.LastName}".Trim(),
            DateOfBirth = patient.DateOfBirth,
            Age = CalculateAge(patient.DateOfBirth),
            Gender = patient.Gender,
            PhoneNumber = patient.PhoneNumber,
            Email = patient.Email,
            CreatedAt = patient.CreatedAt
        };
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
            age--;
            
        return age;
    }
} 