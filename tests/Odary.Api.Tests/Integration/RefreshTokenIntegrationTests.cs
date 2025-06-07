using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Odary.Api.Common.Database;
using Odary.Api.Domain;
using Odary.Api.Constants;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Odary.Api.Modules.Auth;

namespace Odary.Api.Tests.Integration;

public class RefreshTokenIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RefreshTokenIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnNewTokens_WhenValidRefreshTokenProvided()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create a test tenant
        var tenant = new Tenant($"Refresh Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        // Create a test user
        var uniqueEmail = $"refreshtest{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = new User(tenant.Id, uniqueEmail, "Refresh", "Test", Roles.ADMIN);
        var createResult = await userManager.CreateAsync(user, "TestPassword123!");
        Assert.True(createResult.Succeeded);

        var client = _factory.CreateClient();

        // Act 1 - Sign in to get initial tokens
        var signInResponse = await client.PostAsJsonAsync("/api/v1/auth/signin", new
        {
            Email = uniqueEmail,
            Password = "TestPassword123!",
            RememberMe = false
        });

        signInResponse.EnsureSuccessStatusCode();
        var signInContent = await signInResponse.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AuthResources.V1.TokenResponse>(signInContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        var accessToken = tokenResponse!.AccessToken;
        var refreshToken = tokenResponse.RefreshToken;

        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);

        // Act 2 - Use refresh token to get new tokens
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new
        {
            Token = refreshToken
        });

        // Assert
        refreshResponse.EnsureSuccessStatusCode();
        var refreshContent = await refreshResponse.Content.ReadAsStringAsync();
        var newTokenResponse = JsonSerializer.Deserialize<AuthResources.V1.TokenResponse>(refreshContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var newAccessToken = newTokenResponse!.AccessToken;
        var newRefreshToken = newTokenResponse.RefreshToken;

        Assert.NotNull(newAccessToken);
        Assert.NotNull(newRefreshToken);
        // Access tokens might be the same if generated at the same time (same expiry), but refresh tokens should always be different
        Assert.NotEqual(refreshToken, newRefreshToken);

        // Verify old refresh token is revoked
        var oldTokenRecord = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);
        
        Assert.NotNull(oldTokenRecord);
        Assert.True(oldTokenRecord.IsRevoked);
        Assert.NotNull(oldTokenRecord.RevokedAt);
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnError_WhenInvalidRefreshTokenProvided()
    {
        // Arrange
        var client = _factory.CreateClient();
        var invalidRefreshToken = Guid.NewGuid().ToString();

        // Act
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new
        {
            Token = invalidRefreshToken
        });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_ShouldReturnError_WhenRefreshTokenIsReused()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create a test tenant
        var tenant = new Tenant($"Reuse Test Clinic {Guid.NewGuid().ToString("N")[..8]}", "US", "UTC");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        // Create a test user
        var uniqueEmail = $"reusetest{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = new User(tenant.Id, uniqueEmail, "Reuse", "Test", Roles.ADMIN);
        var createResult = await userManager.CreateAsync(user, "TestPassword123!");
        Assert.True(createResult.Succeeded);

        var client = _factory.CreateClient();

        // Sign in to get tokens
        var signInResponse = await client.PostAsJsonAsync("/api/v1/auth/signin", new
        {
            Email = uniqueEmail,
            Password = "TestPassword123!",
            RememberMe = false
        });

        signInResponse.EnsureSuccessStatusCode();
        var signInContent = await signInResponse.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<AuthResources.V1.TokenResponse>(signInContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var refreshToken = tokenResponse!.RefreshToken;

        // Use refresh token once
        var firstRefreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new
        {
            Token = refreshToken
        });
        firstRefreshResponse.EnsureSuccessStatusCode();

        // Act - Try to use the same refresh token again
        var secondRefreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new
        {
            Token = refreshToken
        });

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, secondRefreshResponse.StatusCode);
    }
} 