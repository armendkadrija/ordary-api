using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Infrastructure.Database;
using Odary.Api.Modules.Patient;
using Odary.Api.Modules.Auth;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class PatientModuleIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PatientModuleIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreatePatient_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var request = new PatientCommands.V1.CreatePatient(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateOnly(1990, 1, 1),
            Gender: "Male",
            PhoneNumber: "+1234567890",
            Email: "john.doe@example.com",
            Street: "123 Main St",
            City: "Anytown",
            ZipCode: "12345",
            Country: "USA",
            InsuranceProvider: "Health Insurance Co",
            InsurancePolicyNumber: "POL123456",
            Allergies: ["Penicillin", "Shellfish"],
            MedicalConditions: ["Hypertension", "Type 2 Diabetes"],
            CurrentMedications: ["Lisinopril 10mg", "Metformin 500mg"],
            EmergencyContactName: "Jane Doe",
            EmergencyContactNumber: "+1234567891",
            EmergencyContactRelationship: "Spouse",
            Notes: "Patient prefers morning appointments");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/patients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<PatientResources.V1.CreatePatientResponse>();
        result.Should().NotBeNull();
        result!.Id.Should().NotBeEmpty();
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.FullName.Should().Be("John Doe");
        result.Age.Should().Be(35); // Calculated from 1990 birth year (2025 - 1990)
        result.Gender.Should().Be("Male");
        result.PhoneNumber.Should().Be("+1234567890");
        result.Email.Should().Be("john.doe@example.com");
    }

    [Fact]
    public async Task CreatePatient_WithDuplicatePhoneNumber_ShouldReturnBadRequest()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var phoneNumber = "+1234567890";
        
        // Create first patient
        var firstRequest = new PatientCommands.V1.CreatePatient(
            FirstName: "John",
            LastName: "Doe",
            DateOfBirth: new DateOnly(1990, 1, 1),
            Gender: "Male",
            PhoneNumber: phoneNumber);

        await _client.PostAsJsonAsync("/api/v1/patients", firstRequest);

        // Try to create second patient with same phone number
        var secondRequest = new PatientCommands.V1.CreatePatient(
            FirstName: "Jane",
            LastName: "Smith",
            DateOfBirth: new DateOnly(1985, 5, 15),
            Gender: "Female",
            PhoneNumber: phoneNumber);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/patients", secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("A patient with this phone number already exists");
    }

    [Fact]
    public async Task GetPatient_WithValidId_ShouldReturnPatient()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var patientId = await CreateTestPatientAsync();

        // Act
        var response = await _client.GetAsync($"/api/v1/patients/{patientId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientQueries.V1.GetPatient.Response>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(patientId);
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("Patient");
    }

    [Fact]
    public async Task GetPatients_WithoutFilters_ShouldReturnPaginatedResults()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        await CreateTestPatientAsync("John", "Doe");
        await CreateTestPatientAsync("Jane", "Smith");

        // Act
        var response = await _client.GetAsync("/api/v1/patients?page=1&pageSize=10&includeArchived=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientQueries.V1.GetPatients.Response>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCountGreaterOrEqualTo(2);
        result.TotalCount.Should().BeGreaterOrEqualTo(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetPatients_WithFirstNameFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        await CreateTestPatientAsync("John", "Doe");
        await CreateTestPatientAsync("Jane", "Smith");

        // Act
        var response = await _client.GetAsync("/api/v1/patients?page=1&pageSize=10&firstName=John&includeArchived=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientQueries.V1.GetPatients.Response>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items.First().FirstName.Should().Be("John");
    }

    [Fact]
    public async Task UpdatePatient_WithValidData_ShouldReturnUpdatedPatient()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        var patientId = await CreateTestPatientAsync();

        var updateRequest = new PatientCommands.V1.UpdatePatient(
            Id: patientId,
            FirstName: "Updated",
            LastName: "Name",
            DateOfBirth: new DateOnly(1985, 5, 15),
            Gender: "Female",
            PhoneNumber: "+9876543210",
            Email: "updated@example.com");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/patients/{patientId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientResources.V1.Patient>();
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Updated");
        result.LastName.Should().Be("Name");
        result.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task ArchivePatient_WithValidReason_ShouldArchivePatient()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        var patientId = await CreateTestPatientAsync();

        var archiveRequest = new PatientCommands.V1.ArchivePatient(patientId, "Patient moved to another clinic");

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/patients/{patientId}/archive", archiveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientResources.V1.Patient>();
        result.Should().NotBeNull();
        result!.IsArchived.Should().BeTrue();
        result.ArchiveReason.Should().Be("Patient moved to another clinic");
        result.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UnarchivePatient_WithArchivedPatient_ShouldUnarchivePatient()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        var patientId = await CreateTestPatientAsync();

        // First archive the patient
        var archiveRequest = new PatientCommands.V1.ArchivePatient(patientId, "Test archive");
        await _client.PostAsJsonAsync($"/api/v1/patients/{patientId}/archive", archiveRequest);

        // Act - Unarchive the patient
        var response = await _client.PostAsync($"/api/v1/patients/{patientId}/unarchive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PatientResources.V1.Patient>();
        result.Should().NotBeNull();
        result!.IsArchived.Should().BeFalse();
        result.ArchiveReason.Should().BeNull();
        result.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetPatients_WithIncludeArchived_ShouldReturnArchivedPatients()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        var activePatientId = await CreateTestPatientAsync("Active", "Patient");
        var archivedPatientId = await CreateTestPatientAsync("Archived", "Patient");

        // Archive one patient
        var archiveRequest = new PatientCommands.V1.ArchivePatient(archivedPatientId, "Test archive");
        await _client.PostAsJsonAsync($"/api/v1/patients/{archivedPatientId}/archive", archiveRequest);

        // Act - Get patients without including archived
        var responseWithoutArchived = await _client.GetAsync("/api/v1/patients?page=1&pageSize=10&includeArchived=false");
        var resultWithoutArchived = await responseWithoutArchived.Content.ReadFromJsonAsync<PatientQueries.V1.GetPatients.Response>();

        // Act - Get patients including archived
        var responseWithArchived = await _client.GetAsync("/api/v1/patients?page=1&pageSize=10&includeArchived=true");
        var resultWithArchived = await responseWithArchived.Content.ReadFromJsonAsync<PatientQueries.V1.GetPatients.Response>();

        // Assert
        responseWithoutArchived.StatusCode.Should().Be(HttpStatusCode.OK);
        resultWithoutArchived.Should().NotBeNull();
        resultWithoutArchived!.Items.Should().HaveCount(1);
        resultWithoutArchived.Items.First().FirstName.Should().Be("Active");
        resultWithoutArchived.Items.First().IsArchived.Should().BeFalse();

        responseWithArchived.StatusCode.Should().Be(HttpStatusCode.OK);
        resultWithArchived.Should().NotBeNull();
        resultWithArchived!.Items.Should().HaveCount(2);
        resultWithArchived.Items.Should().Contain(p => p.FirstName == "Active" && !p.IsArchived);
        resultWithArchived.Items.Should().Contain(p => p.FirstName == "Archived" && p.IsArchived);
    }

    [Fact]
    public async Task CreatePatient_WithInvalidData_ShouldReturnValidationErrors()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        var request = new PatientCommands.V1.CreatePatient(
            FirstName: "", // Invalid - empty
            LastName: "", // Invalid - empty
            DateOfBirth: new DateOnly(1800, 1, 1), // Invalid - too old
            Gender: "Invalid", // Invalid - not Male/Female/Other
            PhoneNumber: "invalid"); // Invalid - not proper format

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/patients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("First name is required");
        errorContent.Should().Contain("Last name is required");
        errorContent.Should().Contain("Gender must be Male, Female, or Other");
        errorContent.Should().Contain("Phone number must be in valid format");
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var email = $"admin{Guid.NewGuid():N}@test.com"; // Unique email for each test
        await CreateTestUserAsync(email, "TestPassword123", Roles.ADMIN);
        
        var signInCommand = new AuthCommands.V1.SignIn(email, "TestPassword123", false);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.AccessToken);
    }

    private async Task<User> CreateTestUserAsync(string email, string password, string role, string firstName = "Test", string lastName = "User")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create test tenant first
        var tenant = new Tenant("Test Clinic", "test-clinic", "US", "America/New_York");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var user = new User(tenant.Id, email, firstName, lastName, role)
        {
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }

    private async Task<string> CreateTestPatientAsync(string firstName = "Test", string lastName = "Patient")
    {
        var request = new PatientCommands.V1.CreatePatient(
            FirstName: firstName,
            LastName: lastName,
            DateOfBirth: new DateOnly(1990, 1, 1),
            Gender: "Male",
            PhoneNumber: $"+123456789{Random.Shared.Next(10, 99)}"); // Random phone to avoid duplicates

        var response = await _client.PostAsJsonAsync("/api/v1/patients", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PatientResources.V1.CreatePatientResponse>();
        return result!.Id;
    }
} 