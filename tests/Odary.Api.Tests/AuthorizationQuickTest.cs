using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Odary.Api.Common.Authorization.Claims;
using Odary.Api.Constants;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Odary.Api.Common.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Odary.Api.Tests;

public class AuthorizationQuickTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthorizationQuickTest(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private string GenerateJwtToken(string role)
    {
        var jwtKey = "your-super-secret-key-that-should-be-at-least-32-characters-long-for-security";
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtKey);

        var tokenClaims = new List<Claim>
        {
            new("user_id", Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, $"test@{role.ToLower()}.com"),
            new(ClaimTypes.Role, role),
            new("tenant_id", role == Roles.SUPER_ADMIN ? "" : "tenant-1")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(tokenClaims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "Odary.Api",
            Audience = "Odary.Api.Users",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task PublicEndpoints_ShouldWork_WithoutAuthentication()
    {
        // Auth signin should be public
        var signInResponse = await _client.PostAsJsonAsync("/api/v1/auth/signin", new
        {
            Email = "test@test.com",
            Password = "Password123!",
            RememberMe = false
        });
        
        // Should not be unauthorized (might be other errors like user not found, but not 401)
        Assert.NotEqual(HttpStatusCode.Unauthorized, signInResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_ShouldReturn401_WithoutAuthentication()
    {
        // Auth /me should require authentication
        var getMeResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, getMeResponse.StatusCode);

        // Users endpoints should require authentication
        var usersResponse = await _client.GetAsync("/api/v1/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Unauthorized, usersResponse.StatusCode);
    }

    [Fact]
    public async Task ClaimProtectedEndpoints_ShouldReturn403_WithoutClaims()
    {
        // Arrange - Authenticated user with role that has no claims (create a custom role for testing)
        var token = GenerateJwtToken("NoClaimsRole");
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Should return 403 Forbidden for endpoints requiring specific claims
        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users", new
        {
            Email = "test@test.com",
            TenantId = Guid.NewGuid().ToString(),
            Role = Roles.ASSISTANT
        });
        Assert.Equal(HttpStatusCode.Forbidden, createUserResponse.StatusCode);

        var getUsersResponse = await _client.GetAsync("/api/v1/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, getUsersResponse.StatusCode);
    }

    [Fact]
    public async Task ClaimProtectedEndpoints_ShouldAllow_WithCorrectClaims()
    {
        // Arrange - Authenticated Admin user (Admin role has the required claims in database)
        var token = GenerateJwtToken(Roles.ADMIN);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Should allow access because Admin role has user_invite and user_read claims
        var createUserResponse = await _client.PostAsJsonAsync("/api/v1/users/invite", new
        {
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            Role = Roles.ASSISTANT
        });
        Assert.NotEqual(HttpStatusCode.Forbidden, createUserResponse.StatusCode);

        var getUsersResponse = await _client.GetAsync("/api/v1/users?page=1&pageSize=10");
        Assert.NotEqual(HttpStatusCode.Forbidden, getUsersResponse.StatusCode);
    }

    [Fact]
    public async Task SuperAdminEndpoints_ShouldReturn403_ForNonSuperAdmin()
    {
        // Arrange - Regular admin trying to access SuperAdmin endpoints
        var token = GenerateJwtToken(Roles.ADMIN);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Should return 403 for SuperAdmin-only endpoints
        var createTenantResponse = await _client.PostAsJsonAsync("/api/v1/tenants", new
        {
            Name = "Test Clinic",
            AdminEmail = "admin@test.com",
            AdminPassword = "Password123!",
            Country = "US",
            Timezone = "UTC"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createTenantResponse.StatusCode);

        var getTenantsResponse = await _client.GetAsync("/api/v1/tenants?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, getTenantsResponse.StatusCode);
    }

    [Fact]
    public async Task SuperAdminEndpoints_ShouldAllow_ForSuperAdmin()
    {
        // Arrange - SuperAdmin user
        var token = GenerateJwtToken(Roles.SUPER_ADMIN);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Should allow SuperAdmin access
        var createTenantResponse = await _client.PostAsJsonAsync("/api/v1/tenants", new
        {
            Name = "Test Clinic",
            AdminEmail = "admin@test.com",
            AdminPassword = "Password123!",
            Country = "US",
            Timezone = "UTC"
        });
        Assert.NotEqual(HttpStatusCode.Forbidden, createTenantResponse.StatusCode);

        var getTenantsResponse = await _client.GetAsync("/api/v1/tenants?page=1&pageSize=10");
        Assert.NotEqual(HttpStatusCode.Forbidden, getTenantsResponse.StatusCode);
    }

    [Theory]
    [InlineData(UserClaims.Invite)]
    [InlineData(UserClaims.Read)]
    [InlineData(UserClaims.Update)]
    [InlineData(UserClaims.Delete)]
    [InlineData(TenantClaims.Read)]
    [InlineData(TenantClaims.Update)]
    [InlineData(AuditClaims.Read)]
    public async Task SpecificClaims_ShouldGrantAccess_ToCorrespondingEndpoints(string claim)
    {
        // Arrange - Use Admin role which has all these claims in the database
        var token = GenerateJwtToken(Roles.ADMIN);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act & Assert - Admin role should have access to all these operations
        var response = claim switch
        {
            UserClaims.Invite => await _client.PostAsJsonAsync("/api/v1/users/invite", new { Email = "test@test.com", FirstName = "Test", LastName = "User", Role = Roles.ASSISTANT }),
            UserClaims.Read => await _client.GetAsync("/api/v1/users?page=1&pageSize=10"),
            UserClaims.Update => await _client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/email", new { Email = "updated@test.com" }),
            UserClaims.Delete => await _client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}"),
            TenantClaims.Read => await _client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}"),
            TenantClaims.Update => await _client.PutAsJsonAsync($"/api/v1/tenants/{Guid.NewGuid()}", new { Name = "Updated", Country = "US", Timezone = "UTC" }),
            AuditClaims.Read => await _client.GetAsync("/api/v1/auth/audit-logs?page=1&pageSize=10"),
            _ => throw new ArgumentException($"Unknown claim: {claim}")
        };

        // Should not be forbidden (Admin role has the required claim in database)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

/// <summary>
/// Proper unit tests for authorization using NSubstitute - following established patterns!
/// </summary>
public class AuthorizationUnitTests
{
    private readonly IClaimsService _claimsService;

    public AuthorizationUnitTests()
    {
        _claimsService = Substitute.For<IClaimsService>();
    }

    private WebApplicationFactory<Program> CreateFactoryWithSubstitutes()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Replace the real ClaimsService with our substitute
                    services.AddSingleton(_claimsService);
                    
                    // Could also substitute business services to avoid database dependencies:
                    // services.AddSingleton(Substitute.For<IUserService>());
                    // services.AddSingleton(Substitute.For<ITenantService>());
                    // services.AddSingleton(Substitute.For<IAuthService>());
                });
            });
    }

    [Theory]
    [InlineData(Roles.ADMIN, UserClaims.Read, true)]
    [InlineData(Roles.ADMIN, UserClaims.Invite, false)]
    [InlineData(Roles.SUPER_ADMIN, TenantClaims.Create, true)]
    [InlineData(Roles.ASSISTANT, TenantClaims.Create, false)]
    public async Task AuthorizationFilter_ShouldCheckClaims_BasedOnRole(string role, string requiredClaim, bool shouldHaveClaim)
    {
        // Arrange
        _claimsService.HasClaimAsync(role, requiredClaim, Arg.Any<CancellationToken>())
            .Returns(shouldHaveClaim);

        using var factory = CreateFactoryWithSubstitutes();
        var client = factory.CreateClient();

        var token = GenerateJwtTokenForTest(role);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/v1/users?page=1&pageSize=10"); // This requires UserClaims.Read

        // Assert
        if (requiredClaim == UserClaims.Read)
        {
            if (shouldHaveClaim)
            {
                Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }

        // Verify the substitute was called correctly
        await _claimsService.Received().HasClaimAsync(role, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private string GenerateJwtTokenForTest(string role)
    {
        var jwtKey = "your-super-secret-key-that-should-be-at-least-32-characters-long-for-security";
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtKey);

        var tokenClaims = new List<Claim>
        {
            new("user_id", "test-user-id"),
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Email, $"test@{role.ToLower()}.com"),
            new(ClaimTypes.Role, role),
            new("tenant_id", role == Roles.SUPER_ADMIN ? "" : "test-tenant-id")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(tokenClaims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "Odary.Api",
            Audience = "Odary.Api.Users",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
} 