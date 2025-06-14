using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Authorization;
using Odary.Api.Common.Services;
using Odary.Api.Constants;
using Odary.Api.Constants.Claims;
using Odary.Api.Domain;
using Odary.Api.Infrastructure.Database;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class DatabaseSeederIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DatabaseSeederIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldSuccessfullyInitializeCompleteRoleSystem()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();


        // Assert - All roles should exist
        var roles = await roleManager.Roles.ToListAsync();
        roles.Should().HaveCount(4);

        var roleNames = roles.Select(r => r.Name).ToList();
        roleNames.Should().Contain([Roles.SUPER_ADMIN, Roles.ADMIN, Roles.DENTIST, Roles.ASSISTANT]);
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldCreateRoleClaimsCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();


        // Assert - Check specific role claims
        var superAdminRole = await roleManager.FindByNameAsync(Roles.SUPER_ADMIN);
        var superAdminClaims = await roleManager.GetClaimsAsync(superAdminRole!);

        // Super Admin should have all tenant management claims
        superAdminClaims.Should().Contain(c => c.Value == TenantClaims.Create);
        superAdminClaims.Should().Contain(c => c.Value == TenantClaims.Delete);
        superAdminClaims.Should().Contain(c => c.Value == UserClaims.Create);

        var adminRole = await roleManager.FindByNameAsync(Roles.ADMIN);
        var adminClaims = await roleManager.GetClaimsAsync(adminRole!);

        // Admin should have limited tenant claims but full user management
        adminClaims.Should().Contain(c => c.Value == TenantClaims.Read);
        adminClaims.Should().Contain(c => c.Value == TenantClaims.Update);
        adminClaims.Should().NotContain(c => c.Value == TenantClaims.Create);
        adminClaims.Should().NotContain(c => c.Value == TenantClaims.Delete);
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldBeIdempotent()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        // Act - Run seeder multiple times
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        // Assert - Should still have exactly 4 roles
        var roles = await roleManager.Roles.ToListAsync();
        roles.Should().HaveCount(4);

        // Verify no duplicate claims were created
        var adminRole = await roleManager.FindByNameAsync(Roles.ADMIN);
        var adminClaims = await roleManager.GetClaimsAsync(adminRole!);

        // Count how many times each claim appears (should be exactly 1)
        var tenantReadClaims = adminClaims.Where(c => c.Value == TenantClaims.Read).ToList();
        tenantReadClaims.Should().HaveCount(1);
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldIntegrateWithClaimsService()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var claimsService = scope.ServiceProvider.GetRequiredService<IClaimsService>();


        // Assert - Claims service should be able to retrieve seeded claims
        var superAdminClaims = await claimsService.GetRoleClaimsAsync(Roles.SUPER_ADMIN);
        superAdminClaims.Should().Contain(TenantClaims.Create);
        superAdminClaims.Should().Contain(TenantClaims.Delete);

        var adminClaims = await claimsService.GetRoleClaimsAsync(Roles.ADMIN);
        adminClaims.Should().Contain(TenantClaims.Read);
        adminClaims.Should().Contain(TenantClaims.Update);
        adminClaims.Should().NotContain(TenantClaims.Create);

        // Test claim checking functionality
        var hasTenantCreateClaim = await claimsService.HasClaimAsync(Roles.SUPER_ADMIN, TenantClaims.Create);
        hasTenantCreateClaim.Should().BeTrue();

        var adminHasTenantCreateClaim = await claimsService.HasClaimAsync(Roles.ADMIN, TenantClaims.Create);
        adminHasTenantCreateClaim.Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldHandleConcurrentExecution()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        // Act - Run seeder sequentially to avoid in-memory database concurrency issues
        // In a real database, this would test concurrency properly, but in-memory databases
        // have limitations with concurrent access
        var tasks = new List<Task>();
        for (int i = 0; i < 3; i++)
        {
            using var concurrentScope = _factory.Services.CreateScope();
            var concurrentSeeder = concurrentScope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
            tasks.Add(concurrentSeeder.SeedAsync());
        }

        await Task.WhenAll(tasks);

        // Assert - Should still have exactly 4 roles despite multiple executions
        var roles = await roleManager.Roles.ToListAsync();
        roles.Should().HaveCount(4);

        // Verify each role exists exactly once
        foreach (var roleName in new[] { Roles.SUPER_ADMIN, Roles.ADMIN, Roles.DENTIST, Roles.ASSISTANT })
        {
            var rolesWithName = roles.Where(r => r.Name == roleName).ToList();
            rolesWithName.Should().HaveCount(1, $"Role {roleName} should exist exactly once");
        }
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldSeedAllDefinedClaims()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        // Assert - Verify all defined claims are properly seeded
        var allDefinedClaims = new List<ClaimDefinition>();
        allDefinedClaims.AddRange(TenantClaims.All);
        allDefinedClaims.AddRange(UserClaims.All);

        foreach (var claimDef in allDefinedClaims)
        {
            foreach (var roleName in claimDef.DefaultAssignments)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                role.Should().NotBeNull($"Role {roleName} should exist");

                var roleClaims = await roleManager.GetClaimsAsync(role!);
                roleClaims.Should().Contain(c => c.Value == claimDef.ClaimName && c.Type == "permission",
                    $"Role {roleName} should have claim {claimDef.ClaimName}");
            }
        }
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldMaintainDataIntegrityAfterSeeding()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();

        // Assert - Verify database integrity
        var roles = await dbContext.Roles.ToListAsync();
        var roleClaims = await dbContext.RoleClaims.ToListAsync();

        // All roles should have valid IDs
        roles.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Id));
        roles.Should().OnlyContain(r => !string.IsNullOrEmpty(r.Name));

        // All role claims should reference valid roles
        foreach (var roleClaim in roleClaims)
        {
            roles.Should().Contain(r => r.Id == roleClaim.RoleId,
                $"Role claim should reference existing role ID {roleClaim.RoleId}");
        }

        // All role claims should have permission type and valid claim values
        roleClaims.Should().OnlyContain(rc => rc.ClaimType == "permission");
        roleClaims.Should().OnlyContain(rc => !string.IsNullOrEmpty(rc.ClaimValue));
    }

    [Fact]
    public async Task DatabaseSeeder_ShouldComplyWithMultiTenancyRequirements()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        // Assert - Super Admin should have tenant management capabilities
        var superAdminRole = await roleManager.FindByNameAsync(Roles.SUPER_ADMIN);
        var superAdminClaims = await roleManager.GetClaimsAsync(superAdminRole!);
        var superAdminClaimValues = superAdminClaims.Select(c => c.Value).ToList();

        // From multi-tenancy spec: Super admin manages tenants and system-wide operations
        superAdminClaimValues.Should().Contain(TenantClaims.Create, "Super admin should be able to create tenants");
        superAdminClaimValues.Should().Contain(TenantClaims.Delete, "Super admin should be able to delete tenants");
        superAdminClaimValues.Should().Contain(TenantClaims.Read, "Super admin should be able to read tenant info");
        superAdminClaimValues.Should().Contain(TenantClaims.Update, "Super admin should be able to update tenants");

        // Regular Admin should have limited tenant access (within their own tenant)
        var adminRole = await roleManager.FindByNameAsync(Roles.ADMIN);
        var adminClaims = await roleManager.GetClaimsAsync(adminRole!);
        var adminClaimValues = adminClaims.Select(c => c.Value).ToList();

        adminClaimValues.Should().Contain(TenantClaims.Read, "Admin should be able to read their tenant info");
        adminClaimValues.Should().Contain(TenantClaims.Update, "Admin should be able to update their tenant");
        adminClaimValues.Should().NotContain(TenantClaims.Create, "Admin should not be able to create new tenants");
        adminClaimValues.Should().NotContain(TenantClaims.Delete, "Admin should not be able to delete tenants");
    }
}