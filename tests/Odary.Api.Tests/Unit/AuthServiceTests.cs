using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;
using Odary.Api.Domain;
using Odary.Api.Modules.Auth;
using Odary.Api.Modules.Email;
using Xunit;

namespace Odary.Api.Tests.Unit;

public class AuthServiceTests
{
    private readonly IValidationService _validationService;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly OdaryDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _validationService = Substitute.For<IValidationService>();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _signInManager = Substitute.For<SignInManager<User>>(
            _userManager, Substitute.For<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(), null, null, null, null);
        
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<OdaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OdaryDbContext(options);

        _configuration = Substitute.For<IConfiguration>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<AuthService>>();

        // Setup JWT configuration
        var jwtSection = Substitute.For<IConfigurationSection>();
        jwtSection["SecretKey"].Returns("ThisIsASecretKeyForTestingPurposesOnly1234567890");
        jwtSection["Issuer"].Returns("OdaryTestIssuer");
        jwtSection["Audience"].Returns("OdaryTestAudience");
        jwtSection["ExpiryHours"].Returns("1");
        jwtSection["RefreshTokenExpiryDays"].Returns("7");
        _configuration.GetSection("JwtSettings").Returns(jwtSection);

        _authService = new AuthService(
            _validationService,
            _userManager,
            _signInManager,
            _dbContext,
            _configuration,
            _emailService,
            _logger);
    }

    [Fact]
    public async Task SignInAsync_ShouldReturnTokenResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("test@example.com", "Password123!", false);
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.IsActive = true;

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, command.Password, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        var result = await _authService.SignInAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(user.Email, result.User.Email);
        
        // Verify user state was updated
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.True(user.LastLoginAt.HasValue);
    }

    [Fact]
    public async Task SignInAsync_ShouldThrowBusinessException_WhenUserNotFound()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("nonexistent@example.com", "Password123!", false);
        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.SignInAsync(command));
        Assert.Equal("Invalid email or password", exception.Message);
    }

    [Fact]
    public async Task SignInAsync_ShouldThrowBusinessException_WhenUserIsLocked()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("test@example.com", "Password123!", false);
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);

        _userManager.FindByEmailAsync(command.Email).Returns(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.SignInAsync(command));
        Assert.Contains("Account is locked until", exception.Message);
    }

    [Fact]
    public async Task SignInAsync_ShouldThrowBusinessException_WhenUserIsInactive()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("test@example.com", "Password123!", false);
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.IsActive = false;

        _userManager.FindByEmailAsync(command.Email).Returns(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.SignInAsync(command));
        Assert.Equal("Account is inactive", exception.Message);
    }

    [Fact]
    public async Task SignInAsync_ShouldIncrementFailedAttempts_WhenPasswordIsWrong()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("test@example.com", "WrongPassword", false);
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.IsActive = true;
        user.FailedLoginAttempts = 2;

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, command.Password, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.SignInAsync(command));
        
        Assert.Equal("Invalid email or password", exception.Message);
        Assert.Equal(3, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task SignInAsync_ShouldLockAccount_WhenMaxFailedAttemptsReached()
    {
        // Arrange
        var command = new AuthCommands.V1.SignIn("test@example.com", "WrongPassword", false);
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.IsActive = true;
        user.FailedLoginAttempts = 4; // One more will reach max of 5

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _signInManager.CheckPasswordSignInAsync(user, command.Password, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.SignInAsync(command));
        
        Assert.Equal("Invalid email or password", exception.Message);
        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.NotNull(user.LockedUntil);
        Assert.True(user.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldGenerateToken_WhenUserExists()
    {
        // Arrange
        var command = new AuthCommands.V1.ForgotPassword("test@example.com");
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        var generatedToken = "test-token-123";

        _userManager.FindByEmailAsync(command.Email).Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns(generatedToken);

        // Act
        var result = await _authService.ForgotPasswordAsync(command);

        // Assert
        Assert.Equal("If the email exists, a password reset link has been sent.", result);
        
        // Verify token generation was called
        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);

        // Verify email was sent
        await _emailService.Received(1).SendPasswordResetAsync(
            Arg.Is<EmailCommands.V1.SendPasswordReset>(cmd => 
                cmd.Email == command.Email && 
                cmd.FirstName == user.FirstName && 
                cmd.ResetToken == generatedToken),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldReturnGenericMessage_WhenUserNotFound()
    {
        // Arrange
        var command = new AuthCommands.V1.ForgotPassword("nonexistent@example.com");
        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);

        // Act
        var result = await _authService.ForgotPasswordAsync(command);

        // Assert
        Assert.Equal("If the email exists, a password reset link has been sent.", result);
        
        // Verify no token was generated
        await _userManager.DidNotReceive().GeneratePasswordResetTokenAsync(Arg.Any<User>());

        // Verify no email was sent
        await _emailService.DidNotReceive().SendPasswordResetAsync(
            Arg.Any<EmailCommands.V1.SendPasswordReset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldSucceed_WhenTokenIsValid()
    {
        // Arrange
        var command = new AuthCommands.V1.ResetPassword("test-token", "NewPassword123!");
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.FailedLoginAttempts = 3;
        user.LockedUntil = DateTime.UtcNow.AddMinutes(5);

        // Add user to the test database
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        // Setup UserManager to verify the token for the specific user
        _userManager.VerifyUserTokenAsync(user, "Default", "ResetPassword", command.Token)
            .Returns(true);
        _userManager.ResetPasswordAsync(user, command.Token, command.NewPassword)
            .Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Act
        await _authService.ResetPasswordAsync(command);

        // Assert
        // Verify failed attempts and lockout were cleared
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);

        // Verify token was verified
        await _userManager.Received(1).VerifyUserTokenAsync(
            user, 
            "Default", 
            "ResetPassword", 
            command.Token);

        // Verify password was reset
        await _userManager.Received(1).ResetPasswordAsync(user, command.Token, command.NewPassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldThrowBusinessException_WhenTokenIsInvalid()
    {
        // Arrange
        var command = new AuthCommands.V1.ResetPassword("invalid-token", "NewPassword123!");
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");

        // Add user to the test database
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        _userManager.VerifyUserTokenAsync(user, "Default", "ResetPassword", command.Token)
            .Returns(false); // Token is invalid

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.ResetPasswordAsync(command));
        Assert.Equal("Invalid or expired reset token", exception.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldThrowBusinessException_WhenPasswordResetFails()
    {
        // Arrange
        var command = new AuthCommands.V1.ResetPassword("test-token", "WeakPassword");
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");

        // Add user to the test database
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        _userManager.VerifyUserTokenAsync(user, "Default", "ResetPassword", command.Token)
            .Returns(true);
        
        var identityErrors = new List<IdentityError>
        {
            new() { Code = "PasswordTooWeak", Description = "Password is too weak" }
        };
        _userManager.ResetPasswordAsync(user, command.Token, command.NewPassword)
            .Returns(IdentityResult.Failed(identityErrors.ToArray()));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _authService.ResetPasswordAsync(command));
        Assert.Equal("Password is too weak", exception.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenCurrentPasswordIsCorrect()
    {
        // Arrange
        var userId = "user123";
        var command = new AuthCommands.V1.ChangePassword("CurrentPassword123!", "NewPassword123!");
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        // Use valid base64 encoded strings for password history
        user.PasswordHistory = new List<string> { 
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("old-password-1")),
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("old-password-2"))
        };

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword)
            .Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);

        // Setup password hasher to return different results for old passwords
        var hasher = Substitute.For<IPasswordHasher<User>>();
        hasher.VerifyHashedPassword(user, Arg.Any<string>(), command.NewPassword)
            .Returns(PasswordVerificationResult.Failed);

        // Act
        await _authService.ChangePasswordAsync(command, userId);

        // Assert
        // Verify password was changed
        await _userManager.Received(1).ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        // Arrange
        var userId = "nonexistent";
        var command = new AuthCommands.V1.ChangePassword("CurrentPassword123!", "NewPassword123!");
        _userManager.FindByIdAsync(userId).Returns((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.ChangePasswordAsync(command, userId));
        Assert.Equal("User not found", exception.Message);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldReturnUserProfile_WhenUserExists()
    {
        // Arrange
        var userId = "user123";
        var user = new User("tenant1", "test@example.com", "Test", "User", "ADMIN");
        user.LastLoginAt = DateTime.UtcNow.AddHours(-1);

        _userManager.FindByIdAsync(userId).Returns(user);

        // Act
        var result = await _authService.GetCurrentUserAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.FirstName, result.FirstName);
        Assert.Equal(user.LastName, result.LastName);
        Assert.Equal(user.Role, result.Role);
        Assert.Equal(user.LastLoginAt, result.LastLoginAt);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ShouldThrowNotFoundException_WhenUserNotFound()
    {
        // Arrange
        var userId = "nonexistent";
        _userManager.FindByIdAsync(userId).Returns((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _authService.GetCurrentUserAsync(userId));
        Assert.Equal("User not found", exception.Message);
    }
} 