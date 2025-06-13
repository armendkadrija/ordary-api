using Microsoft.AspNetCore.Identity;
using Odary.Api.Common.Database;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Infrastructure.Database;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class TenantModuleIntegrationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    // Test data
    private string _testTenantId = string.Empty;
    private string _testTenantSettingsId = string.Empty;
    private string _jwtToken = string.Empty;
    private string _adminJwtToken = string.Empty;

    public TenantModuleIntegrationTests()
    {
        _factory = new TestWebApplicationFactory();
        _httpClient = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await SeedTestDataAsync();
        await AuthenticateAsSuperAdminAsync();
    }

    public async Task DisposeAsync()
    {
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

        // Seed roles and claims first
        await seeder.SeedAsync();

        // Create test tenant
        var tenant = new Tenant("Test Clinic", "US", "America/New_York", "https://example.com/logo.png");
        tenant.IsActive = true;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        _testTenantId = tenant.Id;

        // Create tenant settings
        var settings = new TenantSettings(_testTenantId, "en-US", "USD", "MM/dd/yyyy", "h:mm tt");
        context.TenantSettings.Add(settings);
        await context.SaveChangesAsync();
        _testTenantSettingsId = settings.Id;

        // Create super admin user (no tenant)
        var superAdminUser = new User(null, "superadmin@example.com", "Super", "Admin", Roles.SUPER_ADMIN);
        superAdminUser.IsActive = true;
        await userManager.CreateAsync(superAdminUser, "SuperAdminPassword123!");
        await userManager.AddToRoleAsync(superAdminUser, Roles.SUPER_ADMIN);

        // Create admin user for the test tenant
        var adminUser = new User(_testTenantId, "admin@example.com", "Admin", "User", Roles.ADMIN);
        adminUser.IsActive = true;
        await userManager.CreateAsync(adminUser, "AdminPassword123!");
        await userManager.AddToRoleAsync(adminUser, Roles.ADMIN);

        await context.SaveChangesAsync();

        // Run seeder again to ensure claims are properly assigned to the newly created users
        await seeder.SeedAsync();
    }

    private async Task AuthenticateAsSuperAdminAsync()
    {
        var signInCommand = new
        {
            email = "superadmin@example.com",
            password = "SuperAdminPassword123!",
            rememberMe = false
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        response.IsSuccessStatusCode.Should().BeTrue();

        var content = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
        _jwtToken = authResponse.GetProperty("accessToken").GetString()!;

        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _jwtToken);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var signInCommand = new
        {
            email = "admin@example.com",
            password = "AdminPassword123!",
            rememberMe = false
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        response.IsSuccessStatusCode.Should().BeTrue();

        var content = await response.Content.ReadAsStringAsync();
        var authResponse = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);
        _adminJwtToken = authResponse.GetProperty("accessToken").GetString()!;

        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminJwtToken);
    }

    #region Create Tenant Tests

    [Fact]
    public async Task CreateTenant_WithValidData_ReturnsCreatedTenant()
    {
        // Arrange
        var command = new
        {
            name = "New Test Clinic",
            country = "CA",
            timezone = "America/Toronto",
            logoUrl = "https://example.com/newlogo.png"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tenants", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("name").GetString().Should().Be("New Test Clinic");
        result.GetProperty("country").GetString().Should().Be("CA");
        result.GetProperty("timezone").GetString().Should().Be("America/Toronto");
        result.GetProperty("logoUrl").GetString().Should().Be("https://example.com/newlogo.png");
        result.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateTenant_WithDuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            name = "Test Clinic", // Already exists
            country = "CA",
            timezone = "America/Toronto"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tenants", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("A clinic with this name already exists");
    }



    [Fact]
    public async Task CreateTenant_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            name = "", // Required
            country = "",
            timezone = ""
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tenants", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenant_AsNonSuperAdmin_ReturnsForbidden()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();
        
        var command = new
        {
            name = "Forbidden Clinic",
            country = "US",
            timezone = "America/New_York"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tenants", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Get Tenant Tests

    [Fact]
    public async Task GetTenant_WithValidId_ReturnsTenant()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{_testTenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().Be(_testTenantId);
        result.GetProperty("name").GetString().Should().Be("Test Clinic");
        result.GetProperty("country").GetString().Should().Be("US");
        result.GetProperty("timezone").GetString().Should().Be("America/New_York");
        result.GetProperty("logoUrl").GetString().Should().Be("https://example.com/logo.png");
        result.GetProperty("isActive").GetBoolean().Should().BeTrue();
        result.GetProperty("settings").Should().NotBeNull();
    }

    [Fact]
    public async Task GetTenant_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTenant_AsAdminForOwnTenant_ReturnsOk()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();

        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{_testTenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenant_AsAdminForDifferentTenant_ReturnsBadRequest()
    {
        // Arrange - Create another tenant and switch to admin user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        
        var otherTenant = new Tenant("Other Clinic", "CA", "America/Toronto");
        context.Tenants.Add(otherTenant);
        await context.SaveChangesAsync();

        await AuthenticateAsAdminAsync();

        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{otherTenant.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("You can only access your own tenant information");
    }

    #endregion

    #region Get Tenants Tests

    [Fact]
    public async Task GetTenants_AsSuperAdmin_ReturnsPaginatedTenants()
    {
        // Act - Add explicit pagination parameters
        var response = await _httpClient.GetAsync("/api/v1/tenants?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        result.GetProperty("page").GetInt32().Should().Be(1);
        result.GetProperty("pageSize").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task GetTenants_WithNameFilter_ReturnsFilteredTenants()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/v1/tenants?name=Test&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        var items = result.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("name").GetString().Should().Contain("Test");
        }
    }

    [Fact]
    public async Task GetTenants_WithActiveFilter_ReturnsFilteredTenants()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/v1/tenants?isActive=true&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        var items = result.GetProperty("items");
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("isActive").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetTenants_AsNonSuperAdmin_ReturnsForbidden()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/v1/tenants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Update Tenant Tests

    [Fact]
    public async Task UpdateTenant_WithValidData_ReturnsUpdatedTenant()
    {
        // Arrange
        var command = new
        {
            name = "Updated Test Clinic",
            country = "CA",
            timezone = "America/Toronto",
            logoUrl = "https://example.com/updated-logo.png"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("name").GetString().Should().Be("Updated Test Clinic");
        result.GetProperty("country").GetString().Should().Be("CA");
        result.GetProperty("timezone").GetString().Should().Be("America/Toronto");
        result.GetProperty("logoUrl").GetString().Should().Be("https://example.com/updated-logo.png");
    }

    [Fact]
    public async Task UpdateTenant_WithDuplicateName_ReturnsBadRequest()
    {
        // Arrange - Create another tenant first
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        
        var otherTenant = new Tenant("Other Clinic", "CA", "America/Toronto");
        context.Tenants.Add(otherTenant);
        await context.SaveChangesAsync();

        var command = new
        {
            name = "Other Clinic", // Already exists
            country = "CA",
            timezone = "America/Toronto"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("A clinic with this name already exists");
    }

    [Fact]
    public async Task UpdateTenant_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            name = "", // Required
            country = "",
            timezone = ""
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTenant_AsAdminForOwnTenant_ReturnsOk()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();
        
        var command = new
        {
            name = "Admin Updated Clinic",
            country = "CA",
            timezone = "America/Toronto"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTenant_ForNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var command = new
        {
            name = "Non-existent Clinic",
            country = "US",
            timezone = "America/New_York"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{Guid.NewGuid()}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Activate/Deactivate Tenant Tests

    [Fact]
    public async Task DeactivateTenant_WithValidId_ReturnsNoContent()
    {
        // Act
        var response = await _httpClient.PostAsync($"/api/v1/tenants/{_testTenantId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify tenant was deactivated
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var tenant = await context.Tenants.FindAsync(_testTenantId);
        tenant!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateTenant_WithValidId_ReturnsNoContent()
    {
        // Arrange - First deactivate the tenant
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var tenant = await context.Tenants.FindAsync(_testTenantId);
        tenant!.IsActive = false;
        await context.SaveChangesAsync();

        // Act
        var response = await _httpClient.PostAsync($"/api/v1/tenants/{_testTenantId}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify tenant was activated
        await context.Entry(tenant).ReloadAsync();
        tenant.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateTenant_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.PostAsync($"/api/v1/tenants/{Guid.NewGuid()}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateTenant_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.PostAsync($"/api/v1/tenants/{Guid.NewGuid()}/activate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Tenant Settings Tests

    [Fact]
    public async Task CreateTenantSettings_WithValidData_ReturnsCreatedSettings()
    {
        // Arrange - Create a new tenant without settings first
        var tenantCommand = new
        {
            name = "Settings Test Clinic",
            country = "FR",
            timezone = "Europe/Paris"
        };

        var tenantResponse = await _httpClient.PostAsJsonAsync("/api/v1/tenants", tenantCommand);
        tenantResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var tenantContent = await tenantResponse.Content.ReadAsStringAsync();
        var tenant = JsonSerializer.Deserialize<JsonElement>(tenantContent, _jsonOptions);
        var newTenantId = tenant.GetProperty("id").GetString();

        var settingsCommand = new
        {
            language = "en-US",
            currency = "USD",
            dateFormat = "MM/dd/yyyy",
            timeFormat = "h:mm tt"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{newTenantId}/settings", settingsCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("tenantId").GetString().Should().Be(newTenantId);
        result.GetProperty("language").GetString().Should().Be("en-US");
        result.GetProperty("currency").GetString().Should().Be("USD");
        result.GetProperty("dateFormat").GetString().Should().Be("MM/dd/yyyy");
        result.GetProperty("timeFormat").GetString().Should().Be("h:mm tt");
    }

    [Fact]
    public async Task CreateTenantSettings_ForExistingSettings_ReturnsBadRequest()
    {
        // Arrange - The test tenant already has settings from seed data
        var settingsCommand = new
        {
            language = "en-GB",
            currency = "GBP",
            dateFormat = "dd/MM/yyyy",
            timeFormat = "HH:mm"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{_testTenantId}/settings", settingsCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Settings already exist for tenant");
    }

    [Fact]
    public async Task CreateTenantSettings_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange - Create a new tenant without settings first
        var tenantCommand = new
        {
            name = "Invalid Settings Test Clinic",
            country = "DE",
            timezone = "Europe/Berlin"
        };

        var tenantResponse = await _httpClient.PostAsJsonAsync("/api/v1/tenants", tenantCommand);
        var tenantContent = await tenantResponse.Content.ReadAsStringAsync();
        var tenant = JsonSerializer.Deserialize<JsonElement>(tenantContent, _jsonOptions);
        var newTenantId = tenant.GetProperty("id").GetString();

        var settingsCommand = new
        {
            language = "invalid-lang",
            currency = "INVALID",
            dateFormat = "invalid-format",
            timeFormat = "invalid-time"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{newTenantId}/settings", settingsCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetTenantSettings_WithValidTenantId_ReturnsSettings()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{_testTenantId}/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().Be(_testTenantSettingsId);
        result.GetProperty("tenantId").GetString().Should().Be(_testTenantId);
        result.GetProperty("language").GetString().Should().Be("en-US");
        result.GetProperty("currency").GetString().Should().Be("USD");
        result.GetProperty("dateFormat").GetString().Should().Be("MM/dd/yyyy");
        result.GetProperty("timeFormat").GetString().Should().Be("h:mm tt");
    }

    [Fact]
    public async Task GetTenantSettings_AsAdminForOwnTenant_ReturnsOk()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();

        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{_testTenantId}/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTenantSettings_WithValidData_ReturnsUpdatedSettings()
    {
        // Arrange
        var command = new
        {
            language = "en-GB",
            currency = "GBP",
            dateFormat = "dd/MM/yyyy",
            timeFormat = "HH:mm"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}/settings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("language").GetString().Should().Be("en-GB");
        result.GetProperty("currency").GetString().Should().Be("GBP");
        result.GetProperty("dateFormat").GetString().Should().Be("dd/MM/yyyy");
        result.GetProperty("timeFormat").GetString().Should().Be("HH:mm");
    }

    [Fact]
    public async Task UpdateTenantSettings_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            language = "invalid-lang",
            currency = "INVALID",
            dateFormat = "invalid-format",
            timeFormat = "invalid-time"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}/settings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateTenantSettings_AsAdminForOwnTenant_ReturnsOk()
    {
        // Arrange - Switch to admin user
        await AuthenticateAsAdminAsync();
        
        var command = new
        {
            language = "fr-FR",
            currency = "EUR",
            dateFormat = "dd/MM/yyyy",
            timeFormat = "HH:mm"
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}/settings", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTenantSettings_ForNonExistentTenant_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Invite User to Tenant Tests

    [Fact]
    public async Task InviteUserToTenant_WithValidData_ReturnsAccepted()
    {
        // Arrange
        var command = new
        {
            name = "John Doe",
            email = "john.doe@example.com",
            role = Roles.DENTIST
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{_testTenantId}/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task InviteUserToTenant_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            name = "Admin User",
            email = "admin@example.com", // Already exists
            role = Roles.DENTIST
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{_testTenantId}/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("already exists");
    }

    [Fact]
    public async Task InviteUserToTenant_ForInactiveTenant_ReturnsNotFound()
    {
        // Arrange - Create an inactive tenant
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        
        var inactiveTenant = new Tenant("Inactive Clinic", "US", "America/New_York");
        inactiveTenant.IsActive = false;
        context.Tenants.Add(inactiveTenant);
        await context.SaveChangesAsync();

        var command = new
        {
            name = "John Doe",
            email = "john.doe@example.com",
            role = Roles.DENTIST
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{inactiveTenant.Id}/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InviteUserToTenant_ForNonExistentTenant_ReturnsNotFound()
    {
        // Arrange
        var command = new
        {
            name = "John Doe",
            email = "john.doe@example.com",
            role = Roles.DENTIST
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tenants/{Guid.NewGuid()}/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authentication Tests

    [Fact]
    public async Task TenantEndpoints_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory.CreateClient();

        // Act & Assert - Test multiple endpoints
        var responses = await Task.WhenAll(
            unauthenticatedClient.GetAsync("/api/v1/tenants"),
            unauthenticatedClient.GetAsync($"/api/v1/tenants/{_testTenantId}"),
            unauthenticatedClient.PostAsJsonAsync("/api/v1/tenants", new { }),
            unauthenticatedClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}", new { }),
            unauthenticatedClient.PostAsync($"/api/v1/tenants/{_testTenantId}/activate", null),
            unauthenticatedClient.PostAsync($"/api/v1/tenants/{_testTenantId}/deactivate", null),
            unauthenticatedClient.GetAsync($"/api/v1/tenants/{_testTenantId}/settings"),
            unauthenticatedClient.PutAsJsonAsync($"/api/v1/tenants/{_testTenantId}/settings", new { }),
            unauthenticatedClient.PostAsJsonAsync($"/api/v1/tenants/{_testTenantId}/users/invite", new { })
        );

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    #endregion
}