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
    Task<AuthResources.V1.TokenResponse> SignInAsync(AuthCommands.V1.SignIn command, CancellationToken cancellationToken = default);
}

public class AuthService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResources.V1.TokenResponse> SignInAsync(
        AuthCommands.V1.SignIn command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Find user by email
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash))
            throw new BusinessException("Invalid email or password");

        // Generate JWT token
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is required"));
        var expiresAt = DateTime.UtcNow.AddHours(int.Parse(jwtSettings["ExpiryHours"] ?? "24"));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("user_id", user.Id)
            }),
            Expires = expiresAt,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // For now, using the same token as refresh token (in production, implement proper refresh token logic)
        var refreshToken = Guid.NewGuid().ToString();

        logger.LogInformation("User signed in successfully: {Email}", user.Email);

        return new AuthResources.V1.TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            TokenType = "Bearer"
        };
    }
} 