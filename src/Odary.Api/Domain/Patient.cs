using System.ComponentModel.DataAnnotations;
using Odary.Api.Common.Interfaces;

namespace Odary.Api.Domain;

public class Patient : BaseEntity, IAuditable
{
    [MaxLength(50)]
    public string TenantId { get; set; } = string.Empty;

    // Personal Information (Required)
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    [MaxLength(20)]
    public string Gender { get; set; } = string.Empty; // Male, Female, Other

    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    // Contact Information (Optional)
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Street { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    // Insurance Information (Optional)
    [MaxLength(255)]
    public string? InsuranceProvider { get; set; }

    [MaxLength(100)]
    public string? InsurancePolicyNumber { get; set; }

    // Medical Information (Optional)
    public string[] Allergies { get; set; } = [];

    public string[] MedicalConditions { get; set; } = [];

    public string[] CurrentMedications { get; set; } = [];

    // Emergency Contact (Optional)
    [MaxLength(100)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactNumber { get; set; }

    [MaxLength(50)]
    public string? EmergencyContactRelationship { get; set; }

    // Additional Information
    public string? Notes { get; set; }

    public bool IsArchived { get; set; } = false;

    public DateTimeOffset? ArchivedAt { get; set; }

    [MaxLength(255)]
    public string? ArchiveReason { get; set; }

    // Navigation property
    public virtual Tenant Tenant { get; private set; } = null!;

    // Parameterless constructor for EF Core
    public Patient() { }

    public Patient(
        string tenantId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        string gender,
        string phoneNumber)
    {
        TenantId = tenantId;
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        PhoneNumber = phoneNumber;
    }

    public Dictionary<string, object?> GetAuditableProperties()
    {
        return new Dictionary<string, object?>
        {
            [nameof(FirstName)] = FirstName,
            [nameof(LastName)] = LastName,
            [nameof(DateOfBirth)] = DateOfBirth,
            [nameof(Gender)] = Gender,
            [nameof(PhoneNumber)] = PhoneNumber,
            [nameof(Email)] = Email,
            [nameof(Street)] = Street,
            [nameof(City)] = City,
            [nameof(ZipCode)] = ZipCode,
            [nameof(Country)] = Country,
            [nameof(InsuranceProvider)] = InsuranceProvider,
            [nameof(InsurancePolicyNumber)] = InsurancePolicyNumber,
            [nameof(Allergies)] = Allergies,
            [nameof(MedicalConditions)] = MedicalConditions,
            [nameof(CurrentMedications)] = CurrentMedications,
            [nameof(EmergencyContactName)] = EmergencyContactName,
            [nameof(EmergencyContactNumber)] = EmergencyContactNumber,
            [nameof(EmergencyContactRelationship)] = EmergencyContactRelationship,
            [nameof(Notes)] = Notes,
            [nameof(IsArchived)] = IsArchived,
            [nameof(ArchiveReason)] = ArchiveReason
        };
    }
} 