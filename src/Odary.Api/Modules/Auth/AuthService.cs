using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Odary.Api.Modules.Auth;

public interface IAuthService
{
    Task<AuthResources.V1.TokenResponse> SignInAsync(AuthCommands.V1.SignIn command, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthResources.V1.TokenResponse> RefreshTokenAsync(AuthCommands.V1.RefreshToken command, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<AuthQueries.V1.GetCurrentUser.Response> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> ForgotPasswordAsync(AuthCommands.V1.ForgotPassword command, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(AuthCommands.V1.ResetPassword command, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(AuthCommands.V1.ChangePassword command, string userId, CancellationToken cancellationToken = default);
}

public class AuthService(
    IValidationService validationService,
    UserManager<Domain.User> userManager,
    SignInManager<Domain.User> signInManager,
    OdaryDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int PasswordHistoryCount = 3;


    public async Task<AuthResources.V1.TokenResponse> SignInAsync(
        AuthCommands.V1.SignIn command,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user == null)
            throw new BusinessException("Invalid email or password");

        // Check if user is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            throw new BusinessException($"Account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm}");

        // Check if account is active
        if (!user.IsActive)
            throw new BusinessException("Account is inactive");

        // Attempt sign in
        var result = await signInManager.CheckPasswordSignInAsync(user, command.Password, false);

        if (!result.Succeeded)
        {
            // Handle failed login
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            }

            await userManager.UpdateAsync(user);
            throw new BusinessException("Invalid email or password");
        }

        // Reset failed attempts on successful login
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Generate JWT token
        var token = await GenerateJwtTokenAsync(user, command.RememberMe, ipAddress, userAgent);

        logger.LogInformation("User signed in successfully: {Email}", user.Email);

        return new AuthResources.V1.TokenResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = token.ExpiresAt,
            TokenType = "Bearer",
            User = user.ToUserProfile()
        };
    }

    public async Task<AuthQueries.V1.GetCurrentUser.Response> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found");

        return new AuthQueries.V1.GetCurrentUser.Response
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<AuthResources.V1.TokenResponse> RefreshTokenAsync(
        AuthCommands.V1.RefreshToken command, 
        string? ipAddress = null, 
        string? userAgent = null, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Find the refresh token in the database
        var refreshToken = await dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == command.Token, cancellationToken);

        if (refreshToken == null)
            throw new BusinessException("Invalid refresh token");

        if (!refreshToken.IsActive)
            throw new BusinessException("Refresh token is expired or revoked");

        var user = refreshToken.User;

        // Check if user is still active
        if (!user.IsActive)
            throw new BusinessException("User account is inactive");

        // Check if user is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            throw new BusinessException($"Account is locked until {user.LockedUntil.Value:yyyy-MM-dd HH:mm}");

        // Revoke the used refresh token
        refreshToken.Revoke();

        // Generate new tokens
        var newToken = await GenerateJwtTokenAsync(user, false, ipAddress, userAgent);

        // Save the revoked token
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refresh token used successfully for user: {Email}", user.Email);

        return new AuthResources.V1.TokenResponse
        {
            AccessToken = newToken.AccessToken,
            RefreshToken = newToken.RefreshToken,
            ExpiresAt = newToken.ExpiresAt,
            TokenType = "Bearer",
            User = user.ToUserProfile()
        };
    }



    public async Task<string> ForgotPasswordAsync(AuthCommands.V1.ForgotPassword command, CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await userManager.FindByEmailAsync(command.Email);
        if (user == null)
            return "If the email exists, a password reset link has been sent."; // Don't reveal if email exists

        var token = await userManager.GeneratePasswordResetTokenAsync(user);

        // In production, send email with reset link containing the token
        // For now, just return the token (you'd typically send an email)
        return token;
    }

    public async Task ResetPasswordAsync(AuthCommands.V1.ResetPassword command, CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Extract user ID from token or implement your token validation logic
        // For now, assuming token contains user ID (in production, use proper token validation)
        var tokenParts = command.Token.Split('|');
        if (tokenParts.Length != 2)
            throw new BusinessException("Invalid reset token");

        var user = await userManager.FindByIdAsync(tokenParts[0]);
        if (user == null)
            throw new BusinessException("Invalid reset token");

        var result = await userManager.ResetPasswordAsync(user, tokenParts[1], command.NewPassword);
        if (!result.Succeeded)
            throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));

        // Clear failed attempts
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await userManager.UpdateAsync(user);
    }

    public async Task ChangePasswordAsync(AuthCommands.V1.ChangePassword command, string userId, CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found");

        // Check if new password was used recently
        var hasher = new PasswordHasher<Domain.User>();
        foreach (var oldPasswordHash in user.PasswordHistory.TakeLast(PasswordHistoryCount))
        {
            if (hasher.VerifyHashedPassword(user, oldPasswordHash, command.NewPassword) == PasswordVerificationResult.Success)
                throw new BusinessException($"Cannot reuse any of the last {PasswordHistoryCount} passwords");
        }

        var result = await userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
            throw new BusinessException(string.Join(", ", result.Errors.Select(e => e.Description)));

        // Add current password to history
        user.PasswordHistory.Add(user.PasswordHash ?? string.Empty);
        if (user.PasswordHistory.Count > PasswordHistoryCount)
            user.PasswordHistory.RemoveAt(0);

        await userManager.UpdateAsync(user);
    }



    private async Task<(string AccessToken, string RefreshToken, DateTime ExpiresAt)> GenerateJwtTokenAsync(
        Domain.User user, 
        bool rememberMe, 
        string? ipAddress = null, 
        string? userAgent = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is required"));

        var expiryHours = rememberMe ? 24 * 7 : int.Parse(jwtSettings["ExpiryHours"] ?? "1"); // 7 days if remember me, otherwise 1 hour
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("tenant_id", user.TenantId ?? string.Empty),
                new Claim("user_id", user.Id)
            ]),
            Expires = expiresAt,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generate and store refresh token securely
        var refreshTokenValue = Guid.NewGuid().ToString();
        var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        var refreshToken = new Domain.RefreshToken(
            refreshTokenValue, 
            user.Id, 
            refreshTokenExpiresAt, 
            ipAddress, 
            userAgent);

        // Clean up expired refresh tokens for this user before adding a new one
        var expiredTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && (rt.IsRevoked || rt.ExpiresAt <= DateTime.UtcNow))
            .ToListAsync();
        
        if (expiredTokens.Any())
        {
            dbContext.RefreshTokens.RemoveRange(expiredTokens);
        }

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        return (accessToken, refreshTokenValue, expiresAt);
    }


}