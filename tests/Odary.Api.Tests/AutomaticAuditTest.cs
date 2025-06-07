using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Odary.Api.Common.Database;
using Odary.Api.Domain;
using Odary.Api.Constants;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace Odary.Api.Tests;

public class AutomaticAuditTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AutomaticAuditTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private string GenerateJwtToken(string role, string userId, string tenantId = "test-tenant-id")
    {
        // Use the EXACT same key as in appsettings.json and Program.cs
        var jwtKey = "your-super-secret-key-that-should-be-at-least-32-characters-long-for-security";
        var key = Encoding.ASCII.GetBytes(jwtKey);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId), // Standard NameIdentifier claim
            new Claim(ClaimTypes.Email, $"test{userId}@example.com"), // Standard Email claim
            new Claim(ClaimTypes.Role, role), // Standard Role claim
            new Claim("user_id", userId), // Custom user_id claim
            new Claim("tenant_id", tenantId), // Custom tenant_id claim
        };

        // Add specific permission claims based on role to bypass database lookup in tests
        if (role == Constants.Roles.ADMIN || role == Constants.Roles.SUPER_ADMIN)
        {
            claims.Add(new Claim("permission", "tenant_update"));
            claims.Add(new Claim("permission", "tenant_read"));
            claims.Add(new Claim("permission", "user_update"));
            claims.Add(new Claim("permission", "user_read"));
            claims.Add(new Claim("permission", "user_invite"));
            claims.Add(new Claim("permission", "audit_read"));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = "Odary.Api", // Must match appsettings.json
            Audience = "Odary.Api.Users", // Must match appsettings.json
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task AutomaticAudit_ShouldCreateAuditLog_WhenTenantIsUpdated_WithRealUser()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        // Create a real tenant with unique name
        var uniqueName = $"Audit Test Clinic {Guid.NewGuid().ToString("N")[..8]}";
        var tenant = new Tenant(uniqueName, "US", "UTC");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        // Create a real user with proper password and unique email
        var uniqueEmail = $"testuser{Guid.NewGuid().ToString("N")[..8]}@audit.com";
        var user = new User(tenant.Id, uniqueEmail, "Test", "User", Constants.Roles.ADMIN);
        var createResult = await userManager.CreateAsync(user, "TestPassword123!");
        Assert.True(createResult.Succeeded, $"User creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        // Clear any existing audit logs for clean test
        var existingAuditLogs = await dbContext.AuditLogs.ToListAsync();
        dbContext.AuditLogs.RemoveRange(existingAuditLogs);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var token = GenerateJwtToken(Constants.Roles.ADMIN, user.Id, tenant.Id);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act - Update the tenant (this should trigger automatic audit logging)
        var updatedName = $"Updated Audit Clinic {Guid.NewGuid().ToString("N")[..8]}";
        var updateTenantResponse = await client.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}", new
        {
            Name = updatedName,
            Country = "CA", 
            Timezone = "EST"
        });

        // Debug the response
        var responseContent = await updateTenantResponse.Content.ReadAsStringAsync();
        Assert.True(updateTenantResponse.IsSuccessStatusCode, 
            $"HTTP request failed with status {updateTenantResponse.StatusCode}. Response: {responseContent}");

        // Assert - Check if audit log was created automatically
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.EntityType == nameof(Tenant) && a.Action == Actions.UPDATE)
            .ToListAsync();

        Assert.NotEmpty(auditLogs);
        
        var auditLog = auditLogs.First();
        Assert.Equal(Actions.UPDATE, auditLog.Action);
        Assert.Equal(nameof(Tenant), auditLog.EntityType);
        Assert.Equal(user.Id, auditLog.UserId);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
        Assert.Contains(uniqueName, auditLog.OldValues);
        Assert.Contains(updatedName, auditLog.NewValues);
    }

    [Fact]
    public async Task AutomaticAudit_ShouldSkipAuditLog_WhenUserDoesNotExist()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        
        // Clear any existing audit logs for clean test
        var existingAuditLogs = await dbContext.AuditLogs.ToListAsync();
        dbContext.AuditLogs.RemoveRange(existingAuditLogs);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        // Use a fake user ID that doesn't exist in the database
        var token = GenerateJwtToken(Constants.Roles.SUPER_ADMIN, "fake-user-id", "");
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act - Create a tenant (this should NOT create audit logs due to non-existent user)
        var uniqueCreateName = $"Test Audit Clinic No User {Guid.NewGuid().ToString("N")[..8]}";
        var createTenantResponse = await client.PostAsJsonAsync("/api/v1/tenants", new
        {
            Name = uniqueCreateName,
            AdminEmail = $"admin{Guid.NewGuid().ToString("N")[..8]}@testauditnouser.com",
            AdminPassword = "Password123!",
            Country = "US",
            Timezone = "UTC"
        });

        // Assert - Check that NO audit logs were created (because user doesn't exist)
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.EntityType == nameof(Tenant) && a.Action == Actions.CREATE)
            .ToListAsync();

        Assert.Empty(auditLogs); // Should be empty because user doesn't exist
    }

    [Fact]
    public async Task AutomaticAudit_ShouldCreateAuditLog_WhenUserIsUpdated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        // Create a real tenant with unique name
        var uniqueTenantName = $"User Audit Test Clinic {Guid.NewGuid().ToString("N")[..8]}";
        var tenant = new Tenant(uniqueTenantName, "US", "UTC");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        // Create a real user with proper password and unique email
        var uniqueUserEmail = $"useraudit{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = new User(tenant.Id, uniqueUserEmail, "User", "Audit", Constants.Roles.ADMIN);
        var createResult = await userManager.CreateAsync(user, "TestPassword123!");
        Assert.True(createResult.Succeeded);

        // Clear any existing audit logs for clean test
        var existingAuditLogs = await dbContext.AuditLogs.ToListAsync();
        dbContext.AuditLogs.RemoveRange(existingAuditLogs);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var token = GenerateJwtToken(Constants.Roles.ADMIN, user.Id, tenant.Id);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act - Update the user (this should trigger automatic audit logging)
        var updateUserResponse = await client.PutAsJsonAsync($"/api/v1/users/profiles/{user.Id}", new
        {
            FirstName = "Updated User",
            LastName = "Updated Audit",
            Role = Constants.Roles.ADMIN,
            IsActive = true
        });

        // Debug the response
        var userResponseContent = await updateUserResponse.Content.ReadAsStringAsync();
        Assert.True(updateUserResponse.IsSuccessStatusCode, 
            $"User update HTTP request failed with status {updateUserResponse.StatusCode}. Response: {userResponseContent}");

        // Assert - Check if audit log was created automatically
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.EntityType == nameof(User) && a.Action == Actions.UPDATE)
            .ToListAsync();

        Assert.NotEmpty(auditLogs);
        
        var auditLog = auditLogs.First();
        Assert.Equal(Actions.UPDATE, auditLog.Action);
        Assert.Equal(nameof(User), auditLog.EntityType);
        Assert.Equal(user.Id, auditLog.UserId);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
        Assert.Contains("User", auditLog.OldValues);
        Assert.Contains("Updated User", auditLog.NewValues);
    }

    [Fact]
    public async Task AutomaticAudit_ShouldCreateAuditLog_WhenTenantSettingsIsUpdated()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        // Create a real tenant with unique name
        var uniqueTenantName = $"TenantSettings Audit Test {Guid.NewGuid().ToString("N")[..8]}";
        var tenant = new Tenant(uniqueTenantName, "US", "UTC");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        // Create tenant settings
        var tenantSettings = new TenantSettings(tenant.Id, "en", "USD", "MM/dd/yyyy", "12-hour");
        dbContext.TenantSettings.Add(tenantSettings);
        await dbContext.SaveChangesAsync();

        // Create a real user with proper password and unique email
        var uniqueUserEmail = $"settingsaudit{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = new User(tenant.Id, uniqueUserEmail, "Settings", "Audit", Constants.Roles.ADMIN);
        var createResult = await userManager.CreateAsync(user, "TestPassword123!");
        Assert.True(createResult.Succeeded);

        // Clear any existing audit logs for clean test
        var existingAuditLogs = await dbContext.AuditLogs.ToListAsync();
        dbContext.AuditLogs.RemoveRange(existingAuditLogs);
        await dbContext.SaveChangesAsync();

        // Set up HTTP context manually to simulate authenticated user
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("user_id", user.Id),
            new Claim("tenant_id", tenant.Id),
            new Claim(ClaimTypes.Role, Constants.Roles.ADMIN)
        }, "Bearer"));

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = httpContext;

        // Act - Update the tenant settings (this should trigger automatic audit logging)
        tenantSettings.Language = "fr";
        tenantSettings.Currency = "EUR";
        tenantSettings.DateFormat = "dd/MM/yyyy";
        tenantSettings.TimeFormat = "24-hour";
        dbContext.TenantSettings.Update(tenantSettings);
        await dbContext.SaveChangesAsync();

        // Assert - Check if audit log was created automatically
        var auditLogs = await dbContext.AuditLogs
            .Where(a => a.EntityType == nameof(TenantSettings) && a.Action == Actions.UPDATE)
            .ToListAsync();

        Assert.NotEmpty(auditLogs);
        
        var auditLog = auditLogs.First();
        Assert.Equal(Actions.UPDATE, auditLog.Action);
        Assert.Equal(nameof(TenantSettings), auditLog.EntityType);
        Assert.Equal(user.Id, auditLog.UserId);
        Assert.NotNull(auditLog.OldValues);
        Assert.NotNull(auditLog.NewValues);
        Assert.Contains("en", auditLog.OldValues);
        Assert.Contains("USD", auditLog.OldValues);
        Assert.Contains("fr", auditLog.NewValues);
        Assert.Contains("EUR", auditLog.NewValues);
    }
} 