using Microsoft.AspNetCore.Identity;
using Odary.Api.Common.Database;
using Odary.Api.Common.Services;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Infrastructure.Database;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class UserModuleIntegrationTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    // Test data
    private string _testTenantId = string.Empty;
    private string _testUserId = string.Empty;
    private string _adminUserId = string.Empty;
    private string _adminTenantId = string.Empty;
    private string _jwtToken = string.Empty;
    private string _superAdminJwtToken = string.Empty;

    public UserModuleIntegrationTests()
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
        await AuthenticateAsync();
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
        var tenant = new Tenant("Test Clinic", "US", "America/New_York", "test-clinic");
        tenant.IsActive = true;
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        _testTenantId = tenant.Id;

        // Create admin tenant for separation tests
        var adminTenant = new Tenant("Admin Clinic", "CA", "America/Toronto", "admin-clinic");
        adminTenant.IsActive = true;
        context.Tenants.Add(adminTenant);
        await context.SaveChangesAsync();
        _adminTenantId = adminTenant.Id;

        // Create test user with ADMIN role (has user management permissions except creation)
        var user = new User(_testTenantId, "test@example.com", "Test", "User", Roles.ADMIN);
        user.IsActive = true;
        await userManager.CreateAsync(user, "TestPassword123!");
        await userManager.AddToRoleAsync(user, Roles.ADMIN);
        _testUserId = user.Id;

        // Create admin user for testing admin restrictions
        var adminUser = new User(_adminTenantId, "admin@example.com", "Admin", "User", Roles.ADMIN);
        adminUser.IsActive = true;
        await userManager.CreateAsync(adminUser, "AdminPassword123!");
        await userManager.AddToRoleAsync(adminUser, Roles.ADMIN);
        _adminUserId = adminUser.Id;

        // Create super admin user for tests that need user creation permission
        var superAdminUser = new User(null, "superadmin@example.com", "Super", "Admin", Roles.SUPER_ADMIN);
        superAdminUser.IsActive = true;
        await userManager.CreateAsync(superAdminUser, "SuperAdminPassword123!");
        await userManager.AddToRoleAsync(superAdminUser, Roles.SUPER_ADMIN);

        await context.SaveChangesAsync();

        // Run seeder again to ensure claims are properly assigned to the newly created users
        await seeder.SeedAsync();

        // Manually invalidate cache for the roles to ensure fresh claims are loaded
        var claimsService = scope.ServiceProvider.GetRequiredService<IClaimsService>();
        await claimsService.InvalidateRoleCacheAsync(Roles.ADMIN);
        await claimsService.InvalidateRoleCacheAsync(Roles.SUPER_ADMIN);
    }

    private async Task AuthenticateAsync()
    {
        var signInCommand = new
        {
            email = "test@example.com",
            password = "TestPassword123!",
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
        _superAdminJwtToken = authResponse.GetProperty("accessToken").GetString()!;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _superAdminJwtToken);
    }

    #region Create User Tests

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsCreatedUser()
    {
        // Arrange - Switch to super admin for user creation
        await AuthenticateAsSuperAdminAsync();

        var command = new
        {
            email = "newuser@example.com",
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("email").GetString().Should().Be("newuser@example.com");
        result.GetProperty("role").GetString().Should().Be(Roles.ASSISTANT);
        result.GetProperty("isActive").GetBoolean().Should().BeTrue();
        result.GetProperty("generatedPassword").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("generatedPassword").GetString()!.Length.Should().Be(12);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - Switch to super admin for user creation
        await AuthenticateAsSuperAdminAsync();

        var command = new
        {
            email = "test@example.com", // Already exists
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("User with this email already exists");
    }

    [Fact]
    public async Task CreateUser_WithInvalidTenant_ReturnsBadRequest()
    {
        // Arrange - Switch to super admin for user creation
        await AuthenticateAsSuperAdminAsync();

        var command = new
        {
            email = "newuser@example.com",
            tenantId = Guid.NewGuid().ToString("N"),
            role = Roles.ASSISTANT
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Tenant with ID");
    }

    [Fact]
    public async Task CreateUser_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange - Switch to super admin for user creation
        await AuthenticateAsSuperAdminAsync();

        var command = new
        {
            email = "newuser@example.com",
            tenantId = _testTenantId,
            role = "InvalidRole"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Role must be one of");
    }

    [Fact]
    public async Task CreateUser_WithoutEmail_ReturnsBadRequest()
    {
        // Arrange - Switch to super admin for user creation
        await AuthenticateAsSuperAdminAsync();

        var command = new
        {
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Email is required");
    }

    [Fact]
    public async Task CreateUser_AsAdmin_ReturnsForbidden()
    {
        // Arrange - Use regular admin user (not super admin)
        var command = new
        {
            email = "newuser@example.com",
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Get User Tests

    [Fact]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/users/{_testUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().Be(_testUserId);
        result.GetProperty("email").GetString().Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_FromDifferentTenant_ReturnsNotFound()
    {
        // Act - Try to get admin user from different tenant
        var response = await _httpClient.GetAsync($"/api/v1/users/{_adminUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Users Tests

    [Fact]
    public async Task GetUsers_WithoutFilters_ReturnsPaginatedUsers()
    {
        // Arrange - Use SuperAdmin to bypass permission issues
        await AuthenticateAsSuperAdminAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/v1/users?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetUsers_WithEmailFilter_ReturnsFilteredUsers()
    {
        // Arrange - Use SuperAdmin to bypass permission issues
        await AuthenticateAsSuperAdminAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/v1/users?email=test&page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        result.GetProperty("items")[0].GetProperty("email").GetString().Should().Contain("test");
    }

    [Fact]
    public async Task GetUsers_WithPagination_ReturnsCorrectPage()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/v1/users?page=1&pageSize=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("page").GetInt32().Should().Be(1);
        result.GetProperty("pageSize").GetInt32().Should().Be(1);
        result.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    #endregion

    #region Update User Tests

    [Fact]
    public async Task UpdateUserEmail_WithValidData_ReturnsUpdatedUser()
    {
        // Arrange
        var command = new { email = "updated@example.com" };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/{_testUserId}/email", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("email").GetString().Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateUserEmail_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new { email = "invalid-email" };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/{_testUserId}/email", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Valid email format is required");
    }

    [Fact]
    public async Task UpdateUserEmail_ForNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var command = new { email = "test@example.com" };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/email", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete User Tests

    [Fact]
    public async Task DeleteUser_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create a user to delete
        await AuthenticateAsSuperAdminAsync();
        var createCommand = new
        {
            email = "todelete@example.com",
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        var createResponse = await _httpClient.PostAsJsonAsync("/api/v1/users", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent, _jsonOptions);
        var userIdToDelete = createResult.GetProperty("id").GetString()!;

        // Switch back to admin for delete operation
        await AuthenticateAsync();

        // Act
        var response = await _httpClient.DeleteAsync($"/api/v1/users/{userIdToDelete}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_OwnAccount_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.DeleteAsync($"/api/v1/users/{_testUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot delete your own account");
    }

    #endregion

    #region Invite User Tests

    [Fact]
    public async Task InviteUser_WithValidData_ReturnsInvitationResponse()
    {
        // Arrange - Use proper GUID format for tenant ID
        var command = new
        {
            email = "invite@example.com",
            firstName = "Invited",
            lastName = "User",
            role = Roles.ASSISTANT,
            tenantId = _testTenantId // Use actual tenant ID
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("email").GetString().Should().Be("invite@example.com");
        result.GetProperty("invitationToken").GetString().Should().NotBeNullOrEmpty();
        result.GetProperty("expiresAt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InviteUser_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            email = "test@example.com", // Already exists
            firstName = "Test",
            lastName = "User",
            role = Roles.ASSISTANT,
            tenantId = _testTenantId
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("already exists");
    }

    [Fact]
    public async Task InviteUser_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            email = "invalid@example.com",
            firstName = "Invalid",
            lastName = "User",
            role = "InvalidRole",
            tenantId = _testTenantId
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/v1/users/invite", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Role must be one of");
    }

    #endregion

    #region Update User Profile Tests

    [Fact]
    public async Task UpdateUserProfile_WithValidData_ReturnsUpdatedProfile()
    {
        // Arrange
        var command = new
        {
            firstName = "Updated",
            lastName = "Name",
            role = Roles.DENTIST,
            isActive = true
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/profiles/{_testUserId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

        result.GetProperty("firstName").GetString().Should().Be("Updated");
        result.GetProperty("lastName").GetString().Should().Be("Name");
        result.GetProperty("role").GetString().Should().Be(Roles.DENTIST);
    }

    [Fact]
    public async Task UpdateUserProfile_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            firstName = "", // Required field
            lastName = "Name",
            role = Roles.DENTIST,
            isActive = true
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/profiles/{_testUserId}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("First name is required");
    }

    [Fact]
    public async Task UpdateUserProfile_ForNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var command = new
        {
            firstName = "Test",
            lastName = "User",
            role = Roles.DENTIST,
            isActive = true
        };

        // Act
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/users/profiles/{Guid.NewGuid()}", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Lock/Unlock User Tests

    [Fact]
    public async Task LockUser_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create a user to lock
        await AuthenticateAsSuperAdminAsync();
        var createCommand = new
        {
            email = "tolock@example.com",
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        var createResponse = await _httpClient.PostAsJsonAsync("/api/v1/users", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent, _jsonOptions);
        var userIdToLock = createResult.GetProperty("id").GetString()!;

        // Switch back to admin for lock operation
        await AuthenticateAsync();

        // Act
        var response = await _httpClient.PostAsync($"/api/v1/users/{userIdToLock}/lock", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is locked by checking database
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var lockedUser = await userManager.FindByIdAsync(userIdToLock);
        lockedUser!.LockedUntil.Should().NotBeNull();
        lockedUser.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task LockUser_OwnAccount_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.PostAsync($"/api/v1/users/{_testUserId}/lock", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Cannot lock your own account");
    }

    [Fact]
    public async Task UnlockUser_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create and lock a user first
        await AuthenticateAsSuperAdminAsync();
        var createCommand = new
        {
            email = "tounlock@example.com",
            tenantId = _testTenantId,
            role = Roles.ASSISTANT
        };

        var createResponse = await _httpClient.PostAsJsonAsync("/api/v1/users", createCommand);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent, _jsonOptions);
        var userIdToUnlock = createResult.GetProperty("id").GetString()!;

        // Switch back to admin and lock the user
        await AuthenticateAsync();
        await _httpClient.PostAsync($"/api/v1/users/{userIdToUnlock}/lock", null);

        // Act - Unlock the user
        var response = await _httpClient.PostAsync($"/api/v1/users/{userIdToUnlock}/unlock", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is unlocked
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var unlockedUser = await userManager.FindByIdAsync(userIdToUnlock);
        unlockedUser!.LockedUntil.Should().BeNull();
        unlockedUser.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task UnlockUser_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _httpClient.PostAsync($"/api/v1/users/{Guid.NewGuid()}/unlock", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task UserEndpoints_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Remove authentication
        _httpClient.DefaultRequestHeaders.Authorization = null;

        // Act & Assert - Test GET endpoints
        var getResponse = await _httpClient.GetAsync("/api/v1/users");
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var getUserResponse = await _httpClient.GetAsync($"/api/v1/users/{_testUserId}");
        getUserResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}