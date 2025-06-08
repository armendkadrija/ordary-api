using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Modules.Auth;
using Xunit;

namespace Odary.Api.Tests.Integration;

public class PasswordResetIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IServiceScope _scope;
    private readonly OdaryDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IAuthService _authService;

    public PasswordResetIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });

        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        _authService = _scope.ServiceProvider.GetRequiredService<IAuthService>();
    }

    [Fact]
    public async Task ForgotPassword_And_ResetPassword_EndToEnd_WorksCorrectly()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Roles.DENTIST);
        var initialPassword = "InitialPassword123!";
        
        var createResult = await _userManager.CreateAsync(user, initialPassword);
        Assert.True(createResult.Succeeded);

        // Act 1 - Request password reset
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword(uniqueEmail);
        var forgotPasswordResult = await _authService.ForgotPasswordAsync(forgotPasswordCommand);

        // Assert 1 - Forgot password should return standard message
        Assert.NotNull(forgotPasswordResult);
        Assert.Equal("If the email exists, a password reset link has been sent.", forgotPasswordResult);

        // Act 2 - Generate a valid reset token manually for testing (since email isn't actually sent)
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var newPassword = "NewPassword123!";
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(resetToken, newPassword);
        
        await _authService.ResetPasswordAsync(resetPasswordCommand);

        // Assert 2 - Password should be reset successfully
        var refreshedUser = await _userManager.FindByEmailAsync(uniqueEmail);
        Assert.NotNull(refreshedUser);
        
        // Verify old password no longer works
        var oldPasswordCheck = await _userManager.CheckPasswordAsync(refreshedUser!, initialPassword);
        Assert.False(oldPasswordCheck);
        
        // Verify new password works
        var newPasswordCheck = await _userManager.CheckPasswordAsync(refreshedUser!, newPassword);
        Assert.True(newPasswordCheck);

        // Act 3 - Verify can sign in with new password
        var signInCommand = new AuthCommands.V1.SignIn(uniqueEmail, newPassword);
        var signInResult = await _authService.SignInAsync(signInCommand);

        // Assert 3 - Sign in should work with new password
        Assert.NotNull(signInResult);
        Assert.NotEmpty(signInResult.AccessToken);
        Assert.Equal(uniqueEmail, signInResult.User.Email);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ThrowsBusinessException()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Roles.DENTIST);
        var initialPassword = "InitialPassword123!";
        
        var createResult = await _userManager.CreateAsync(user, initialPassword);
        Assert.True(createResult.Succeeded);

        // Simulate token expiry by using an invalid/expired token
        var expiredToken = "invalid-expired-token";
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(expiredToken, "NewPassword123!");

        // Act & Assert - Should throw exception for expired token
        await Assert.ThrowsAsync<BusinessException>(() => _authService.ResetPasswordAsync(resetPasswordCommand));
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ThrowsBusinessException()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Roles.DENTIST);
        var initialPassword = "InitialPassword123!";
        
        var createResult = await _userManager.CreateAsync(user, initialPassword);
        Assert.True(createResult.Succeeded);

        // Act & Assert - Should throw exception for invalid token
        var invalidToken = "completely-invalid-token";
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(invalidToken, "NewPassword123!");
        
        await Assert.ThrowsAsync<BusinessException>(() => _authService.ResetPasswordAsync(resetPasswordCommand));
    }

    [Fact]
    public async Task ResetPassword_TokenCanOnlyBeUsedOnce()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Roles.DENTIST);
        var initialPassword = "InitialPassword123!";
        
        var createResult = await _userManager.CreateAsync(user, initialPassword);
        Assert.True(createResult.Succeeded);

        // Act 1 - Generate reset token
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Act 2 - Use token first time (should work)
        var resetPasswordCommand1 = new AuthCommands.V1.ResetPassword(resetToken, "NewPassword123!");
        await _authService.ResetPasswordAsync(resetPasswordCommand1);

        // Act 3 - Try to use same token again (should fail)
        var resetPasswordCommand2 = new AuthCommands.V1.ResetPassword(resetToken, "AnotherPassword123!");

        // Assert - Should throw exception for reused token
        await Assert.ThrowsAsync<BusinessException>(() => _authService.ResetPasswordAsync(resetPasswordCommand2));
    }

    [Fact]
    public async Task ForgotPassword_WithNonExistentEmail_ReturnsStandardMessage()
    {
        // Act - Request password reset for non-existent email
        var nonExistentEmail = $"nonexistent{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword(nonExistentEmail);
        var result = await _authService.ForgotPasswordAsync(forgotPasswordCommand);

        // Assert - Should return standard message (don't reveal if email exists)
        Assert.Equal("If the email exists, a password reset link has been sent.", result);
    }

    [Fact]
    public async Task ForgotPassword_WithValidEmail_ReturnsStandardMessage()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"validuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Valid", "User", Roles.DENTIST);
        
        var createResult = await _userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Act - Request password reset
        var forgotPasswordCommand = new AuthCommands.V1.ForgotPassword(uniqueEmail);
        var result = await _authService.ForgotPasswordAsync(forgotPasswordCommand);

        // Assert - Should return standard message
        Assert.Equal("If the email exists, a password reset link has been sent.", result);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ResetsPasswordSuccessfully()
    {
        // Arrange - Create a tenant and user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Roles.DENTIST);
        var initialPassword = "InitialPassword123!";
        
        var createResult = await _userManager.CreateAsync(user, initialPassword);
        Assert.True(createResult.Succeeded);

        // Act - Generate token and reset password
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var newPassword = "NewPassword123!";
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(resetToken, newPassword);
        
        await _authService.ResetPasswordAsync(resetPasswordCommand);

        // Assert - Password should be updated
        var refreshedUser = await _userManager.FindByEmailAsync(uniqueEmail);
        Assert.NotNull(refreshedUser);
        
        var passwordCheck = await _userManager.CheckPasswordAsync(refreshedUser!, newPassword);
        Assert.True(passwordCheck);
    }

    [Fact]
    public async Task ResetPassword_ClearsAccountLockout()
    {
        // Arrange - Create a tenant and locked user
        var tenant = new Tenant($"Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var uniqueEmail = $"lockeduser{Guid.NewGuid().ToString("N")[..8]}@example.com";
        var user = new User(tenant.Id, uniqueEmail, "Locked", "User", Roles.DENTIST)
        {
            FailedLoginAttempts = 5,
            LockedUntil = DateTime.UtcNow.AddMinutes(15)
        };
        
        var createResult = await _userManager.CreateAsync(user, "Password123!");
        Assert.True(createResult.Succeeded);

        // Act - Reset password should clear lockout
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetPasswordCommand = new AuthCommands.V1.ResetPassword(resetToken, "NewPassword123!");
        
        await _authService.ResetPasswordAsync(resetPasswordCommand);

        // Assert - Lockout should be cleared
        var refreshedUser = await _userManager.FindByEmailAsync(uniqueEmail);
        Assert.NotNull(refreshedUser);
        Assert.Equal(0, refreshedUser!.FailedLoginAttempts);
        Assert.Null(refreshedUser.LockedUntil);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
} 