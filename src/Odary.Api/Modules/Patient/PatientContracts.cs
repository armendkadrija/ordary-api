using Odary.Api.Common.Pagination;

namespace Odary.Api.Modules.Patient;

public class PatientQueries
{
    public class V1
    {
        public record GetPatient(string Id)
        {
            public record Response
            {
                public string Id { get; init; } = string.Empty;
                public string FirstName { get; init; } = string.Empty;
                public string LastName { get; init; } = string.Empty;
                public DateOnly DateOfBirth { get; init; }
                public int Age { get; init; }
                public string Gender { get; init; } = string.Empty;
                public string PhoneNumber { get; init; } = string.Empty;
                public string? Email { get; init; }
                public string? Street { get; init; }
                public string? City { get; init; }
                public string? ZipCode { get; init; }
                public string? Country { get; init; }
                public string? InsuranceProvider { get; init; }
                public string? InsurancePolicyNumber { get; init; }
                public string[] Allergies { get; init; } = [];
                public string[] MedicalConditions { get; init; } = [];
                public string[] CurrentMedications { get; init; } = [];
                public string? EmergencyContactName { get; init; }
                public string? EmergencyContactNumber { get; init; }
                public string? EmergencyContactRelationship { get; init; }
                public string? Notes { get; init; }
                public bool IsArchived { get; init; }
                public DateTimeOffset? ArchivedAt { get; init; }
                public string? ArchiveReason { get; init; }
                public DateTimeOffset CreatedAt { get; init; }
                public DateTimeOffset? UpdatedAt { get; init; }
            }
        }

        public class GetPatients : PaginatedRequest
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Email { get; set; }
            public bool IncludeArchived { get; set; } = false;

            public class Response : PaginatedResponse<PatientResources.V1.Patient>;
        }
    }
}

public class PatientCommands
{
    public class V1
    {
        public record CreatePatient(
            string FirstName,
            string LastName,
            DateOnly DateOfBirth,
            string Gender,
            string PhoneNumber,
            string? Email = null,
            string? Street = null,
            string? City = null,
            string? ZipCode = null,
            string? Country = null,
            string? InsuranceProvider = null,
            string? InsurancePolicyNumber = null,
            string[]? Allergies = null,
            string[]? MedicalConditions = null,
            string[]? CurrentMedications = null,
            string? EmergencyContactName = null,
            string? EmergencyContactNumber = null,
            string? EmergencyContactRelationship = null,
            string? Notes = null);

        public record UpdatePatient(
            string Id,
            string FirstName,
            string LastName,
            DateOnly DateOfBirth,
            string Gender,
            string PhoneNumber,
            string? Email = null,
            string? Street = null,
            string? City = null,
            string? ZipCode = null,
            string? Country = null,
            string? InsuranceProvider = null,
            string? InsurancePolicyNumber = null,
            string[]? Allergies = null,
            string[]? MedicalConditions = null,
            string[]? CurrentMedications = null,
            string? EmergencyContactName = null,
            string? EmergencyContactNumber = null,
            string? EmergencyContactRelationship = null,
            string? Notes = null);

        public record ArchivePatient(string Id, string Reason);

        public record UnarchivePatient(string Id);
    }
}

public class PatientResources
{
    public class V1
    {
        public record Patient
        {
            public string Id { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string FullName { get; init; } = string.Empty;
            public DateOnly DateOfBirth { get; init; }
            public int Age { get; init; }
            public string Gender { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public string? Email { get; init; }
            public string? Street { get; init; }
            public string? City { get; init; }
            public string? ZipCode { get; init; }
            public string? Country { get; init; }
            public string? InsuranceProvider { get; init; }
            public string? InsurancePolicyNumber { get; init; }
            public string[] Allergies { get; init; } = [];
            public string[] MedicalConditions { get; init; } = [];
            public string[] CurrentMedications { get; init; } = [];
            public string? EmergencyContactName { get; init; }
            public string? EmergencyContactNumber { get; init; }
            public string? EmergencyContactRelationship { get; init; }
            public string? Notes { get; init; }
            public bool IsArchived { get; init; }
            public DateTimeOffset? ArchivedAt { get; init; }
            public string? ArchiveReason { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }

        public record CreatePatientResponse
        {
            public string Id { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string FullName { get; init; } = string.Empty;
            public DateOnly DateOfBirth { get; init; }
            public int Age { get; init; }
            public string Gender { get; init; } = string.Empty;
            public string PhoneNumber { get; init; } = string.Empty;
            public string? Email { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
        }
    }
} 