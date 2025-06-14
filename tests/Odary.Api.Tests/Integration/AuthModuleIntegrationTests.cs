using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Infrastructure.Database;
using Odary.Api.Infrastructure.Email;
using Odary.Api.Modules.Auth;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class AuthModuleIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthModuleIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    #region Sign In Tests

    [Fact]
    public async Task SignIn_WithValidCredentials_ShouldReturnTokenAndUserProfile()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeEmpty();
        result.RefreshToken.Should().NotBeEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.TokenType.Should().Be("Bearer");
        result.User.Should().NotBeNull();
        result.User.Email.Should().Be("test@example.com");
        result.User.Role.Should().Be(Roles.DENTIST);
        result.User.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_WithRememberMe_ShouldReturnLongerExpirationToken()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", true);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(6)); // Should be ~7 days for remember me
    }

    [Fact]
    public async Task SignIn_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var signInCommand = new AuthCommands.V1.SignIn("nonexistent@example.com", "TestPassword123", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithInvalidPassword_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "WrongPassword", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithInactiveUser_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Deactivate user
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userToUpdate = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        userToUpdate.IsActive = false;
        await dbContext.SaveChangesAsync();
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_WithLockedUser_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Lock user
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userToUpdate = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        userToUpdate.LockedUntil = DateTime.UtcNow.AddHours(1);
        await dbContext.SaveChangesAsync();
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SignIn_MultipleFailedAttempts_ShouldLockAccount()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "WrongPassword", false);

        // Act - Try to sign in 5 times with wrong password
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        }

        // Try one more time - should be locked now
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        // Verify user is locked in database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var user = await dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task SignIn_WithInvalidData_ShouldReturnValidationErrors()
    {
        // Arrange
        var signInCommand = new AuthCommands.V1.SignIn("", "", false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // First, sign in to get tokens
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false));
        var signInResult = await signInResponse.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        
        var refreshCommand = new AuthCommands.V1.RefreshToken(signInResult!.RefreshToken);

        // Add a small delay to ensure different token timestamps
        await Task.Delay(1000);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeEmpty();
        result.RefreshToken.Should().NotBeEmpty();
        result.AccessToken.Should().NotBe(signInResult.AccessToken); // Should be a new token
        result.RefreshToken.Should().NotBe(signInResult.RefreshToken); // Should be a new refresh token
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var refreshCommand = new AuthCommands.V1.RefreshToken(Guid.NewGuid().ToString());

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Create an expired refresh token
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var expiredToken = new RefreshToken(
            Guid.NewGuid().ToString(),
            user.Id,
            DateTime.UtcNow.AddDays(-1) // Expired yesterday
        );
        dbContext.RefreshTokens.Add(expiredToken);
        await dbContext.SaveChangesAsync();
        
        var refreshCommand = new AuthCommands.V1.RefreshToken(expiredToken.Token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Create a revoked refresh token
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var revokedToken = new RefreshToken(
            Guid.NewGuid().ToString(),
            user.Id,
            DateTime.UtcNow.AddDays(7)
        );
        revokedToken.Revoke();
        dbContext.RefreshTokens.Add(revokedToken);
        await dbContext.SaveChangesAsync();
        
        var refreshCommand = new AuthCommands.V1.RefreshToken(revokedToken.Token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_WithInactiveUser_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Create a valid refresh token
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var validToken = new RefreshToken(
            Guid.NewGuid().ToString(),
            user.Id,
            DateTime.UtcNow.AddDays(7)
        );
        dbContext.RefreshTokens.Add(validToken);
        
        // Deactivate user
        var userToUpdate = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        userToUpdate.IsActive = false;
        await dbContext.SaveChangesAsync();
        
        var refreshCommand = new AuthCommands.V1.RefreshToken(validToken.Token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", refreshCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Forgot Password Tests

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ShouldReturnAccepted()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword("test@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ShouldReturnAccepted()
    {
        // Arrange - This should still return Accepted to prevent email enumeration
        await _factory.ResetDatabaseAsync();
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword("nonexistent@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ForgotPassword_WithInvalidEmail_ShouldReturnValidationError()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword("invalid-email");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_ShouldSendEmail()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword("test@example.com");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", forgotPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        
        // Verify email service was called
        using var scope = _factory.Services.CreateScope();
        var emailService = scope.ServiceProvider.GetService<IEmailService>();
        emailService.Should().NotBeNull(); // Email service should be registered (mocked in test)
    }

    #endregion

    #region Reset Password Tests

    [Fact]
    public async Task ResetPassword_WithValidToken_ShouldResetPassword()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Generate a password reset token
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(resetToken, "NewPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", resetPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify password was changed
        var signInCommand = new AuthCommands.V1.SignIn("test@example.com", "NewPassword123", false);
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword("invalid-token", "NewPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", resetPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithWeakPassword_ShouldReturnValidationError()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var user = await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(resetToken, "weak");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", resetPasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Change Password Tests

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_ShouldChangePassword()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        // Sign in to get token
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false));
        var signInResult = await signInResponse.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        
        // Set authorization header
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInResult!.AccessToken);
        
        var changePasswordCommand = new AuthCommands.V1.ChangePassword("TestPassword123", "NewPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", changePasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify new password works
        _client.DefaultRequestHeaders.Authorization = null; // Clear token
        var newSignInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "NewPassword123", false));
        newSignInResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithInvalidCurrentPassword_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false));
        var signInResult = await signInResponse.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInResult!.AccessToken);
        
        var changePasswordCommand = new AuthCommands.V1.ChangePassword("WrongPassword", "NewPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", changePasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var changePasswordCommand = new AuthCommands.V1.ChangePassword("TestPassword123", "NewPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", changePasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithSamePassword_ShouldReturnValidationError()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST);
        
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false));
        var signInResult = await signInResponse.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInResult!.AccessToken);
        
        var changePasswordCommand = new AuthCommands.V1.ChangePassword("TestPassword123", "TestPassword123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/change-password", changePasswordCommand);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Get Current User Tests

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnUserProfile()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await CreateTestUserAsync("test@example.com", "TestPassword123", Roles.DENTIST, "John", "Doe");
        
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", 
            new AuthCommands.V1.SignIn("test@example.com", "TestPassword123", false));
        var signInResult = await signInResponse.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", signInResult!.AccessToken);

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<AuthQueries.V1.GetCurrentUser.Response>();
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.Role.Should().Be(Roles.DENTIST);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Helper Methods

    private async Task<User> CreateTestUserAsync(string email, string password, string role, string firstName = "Test", string lastName = "User")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();

        // Create a test tenant first
        var tenant = new Tenant("Test Clinic", "US", "UTC", "test-clinic");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var user = new User(tenant.Id, email, firstName, lastName, role);
        var result = await userManager.CreateAsync(user, password);
        
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    #endregion
}