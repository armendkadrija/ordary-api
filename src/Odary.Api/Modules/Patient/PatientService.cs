using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;
using Odary.Api.Infrastructure.Database;

namespace Odary.Api.Modules.Patient;

public interface IPatientService
{
    Task<PatientResources.V1.CreatePatientResponse> CreatePatientAsync(PatientCommands.V1.CreatePatient command, CancellationToken cancellationToken);
    Task<PatientQueries.V1.GetPatient.Response> GetPatientAsync(PatientQueries.V1.GetPatient query, CancellationToken cancellationToken);
    Task<PatientQueries.V1.GetPatients.Response> GetPatientsAsync(PatientQueries.V1.GetPatients query, CancellationToken cancellationToken);
    Task<PatientResources.V1.Patient> UpdatePatientAsync(PatientCommands.V1.UpdatePatient command, CancellationToken cancellationToken);
    Task<PatientResources.V1.Patient> ArchivePatientAsync(PatientCommands.V1.ArchivePatient command, CancellationToken cancellationToken);
    Task<PatientResources.V1.Patient> UnarchivePatientAsync(PatientCommands.V1.UnarchivePatient command, CancellationToken cancellationToken);
}

public class PatientService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    ILogger<PatientService> logger,
    ICurrentUserService currentUserService) : BaseService(currentUserService), IPatientService
{
    public async Task<PatientResources.V1.CreatePatientResponse> CreatePatientAsync(
        PatientCommands.V1.CreatePatient command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Check if phone number already exists in the same tenant (exact match)
        var existingPatient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.PhoneNumber == command.PhoneNumber && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (existingPatient != null)
            throw new BusinessException("A patient with this phone number already exists. Please check for duplicates.");

        // Create patient
        var patient = new Domain.Patient(
            CurrentUser.TenantId!,
            command.FirstName,
            command.LastName,
            command.DateOfBirth,
            command.Gender,
            command.PhoneNumber)
        {
            Email = command.Email,
            Street = command.Street,
            City = command.City,
            ZipCode = command.ZipCode,
            Country = command.Country ?? "Default", // Default to clinic location if not set
            InsuranceProvider = command.InsuranceProvider,
            InsurancePolicyNumber = command.InsurancePolicyNumber,
            Allergies = command.Allergies ?? [],
            MedicalConditions = command.MedicalConditions ?? [],
            CurrentMedications = command.CurrentMedications ?? [],
            EmergencyContactName = command.EmergencyContactName,
            EmergencyContactNumber = command.EmergencyContactNumber,
            EmergencyContactRelationship = command.EmergencyContactRelationship,
            Notes = command.Notes
        };

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Patient created successfully with ID: {PatientId} in Tenant: {TenantId}", patient.Id, CurrentUser.TenantId);

        return patient.ToCreatePatientResponse();
    }

    public async Task<PatientQueries.V1.GetPatient.Response> GetPatientAsync(
        PatientQueries.V1.GetPatient query,
        CancellationToken cancellationToken)
    {
        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.Id == query.Id && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (patient == null)
            throw new NotFoundException($"Patient with ID {query.Id} not found");

        return patient.ToGetPatientResponse();
    }

    public async Task<PatientQueries.V1.GetPatients.Response> GetPatientsAsync(
        PatientQueries.V1.GetPatients query,
        CancellationToken cancellationToken)
    {
        var patientsQuery = dbContext.Patients
            .Where(p => p.TenantId == CurrentUser.TenantId)
            .AsQueryable();

        // Filter out archived patients unless explicitly requested
        if (!query.IncludeArchived)
            patientsQuery = patientsQuery.Where(p => !p.IsArchived);

        // Apply search filters
        if (!string.IsNullOrEmpty(query.FirstName))
            patientsQuery = patientsQuery.Where(p => p.FirstName.Contains(query.FirstName));

        if (!string.IsNullOrEmpty(query.LastName))
            patientsQuery = patientsQuery.Where(p => p.LastName.Contains(query.LastName));

        if (!string.IsNullOrEmpty(query.PhoneNumber))
            patientsQuery = patientsQuery.Where(p => p.PhoneNumber.Contains(query.PhoneNumber));

        if (!string.IsNullOrEmpty(query.Email))
            patientsQuery = patientsQuery.Where(p => p.Email != null && p.Email.Contains(query.Email));

        var totalCount = await patientsQuery.CountAsync(cancellationToken);

        var patients = await patientsQuery
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return new PatientQueries.V1.GetPatients.Response
        {
            Items = patients.Select(p => p.ToContract()).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }



    public async Task<PatientResources.V1.Patient> UpdatePatientAsync(
        PatientCommands.V1.UpdatePatient command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (patient == null)
            throw new NotFoundException($"Patient with ID {command.Id} not found");

        // Check if phone number is already taken by another patient in the same tenant
        var existingPatient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.PhoneNumber == command.PhoneNumber && p.Id != command.Id && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (existingPatient != null)
            throw new BusinessException("Phone number is already taken by another patient");

        // Update patient properties
        patient.FirstName = command.FirstName;
        patient.LastName = command.LastName;
        patient.DateOfBirth = command.DateOfBirth;
        patient.Gender = command.Gender;
        patient.PhoneNumber = command.PhoneNumber;
        patient.Email = command.Email;
        patient.Street = command.Street;
        patient.City = command.City;
        patient.ZipCode = command.ZipCode;
        patient.Country = command.Country;
        patient.InsuranceProvider = command.InsuranceProvider;
        patient.InsurancePolicyNumber = command.InsurancePolicyNumber;
        patient.Allergies = command.Allergies ?? [];
        patient.MedicalConditions = command.MedicalConditions ?? [];
        patient.CurrentMedications = command.CurrentMedications ?? [];
        patient.EmergencyContactName = command.EmergencyContactName;
        patient.EmergencyContactNumber = command.EmergencyContactNumber;
        patient.EmergencyContactRelationship = command.EmergencyContactRelationship;
        patient.Notes = command.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Patient updated successfully with ID: {PatientId}", patient.Id);
        return patient.ToContract();
    }

    public async Task<PatientResources.V1.Patient> ArchivePatientAsync(
        PatientCommands.V1.ArchivePatient command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (patient == null)
            throw new NotFoundException($"Patient with ID {command.Id} not found");

        if (patient.IsArchived)
            throw new BusinessException("Patient is already archived");

        // Archive the patient
        patient.IsArchived = true;
        patient.ArchivedAt = DateTimeOffset.UtcNow;
        patient.ArchiveReason = command.Reason;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Patient archived successfully with ID: {PatientId}, Reason: {Reason}", patient.Id, command.Reason);
        return patient.ToContract();
    }

    public async Task<PatientResources.V1.Patient> UnarchivePatientAsync(
        PatientCommands.V1.UnarchivePatient command,
        CancellationToken cancellationToken)
    {
        var patient = await dbContext.Patients
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.TenantId == CurrentUser.TenantId, cancellationToken);

        if (patient == null)
            throw new NotFoundException($"Patient with ID {command.Id} not found");

        if (!patient.IsArchived)
            throw new BusinessException("Patient is not archived");

        // Unarchive the patient
        patient.IsArchived = false;
        patient.ArchivedAt = null;
        patient.ArchiveReason = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Patient unarchived successfully with ID: {PatientId}", patient.Id);
        return patient.ToContract();
    }
} 