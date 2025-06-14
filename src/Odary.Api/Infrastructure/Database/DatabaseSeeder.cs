using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Authorization;
using Odary.Api.Common.Services;
using Odary.Api.Constants;
using Odary.Api.Constants.Claims;
using Odary.Api.Domain;

namespace Odary.Api.Infrastructure.Database;

public interface IDatabaseSeeder
{
    Task SeedAsync();
}

public class DatabaseSeeder(RoleManager<Role> roleManager, IClaimsService claimsService, ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedRoleClaimsAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleInfo in Roles.ALL)
        {
            if (await roleManager.RoleExistsAsync(roleInfo.Name))
                continue;

            var role = new Role(roleInfo.Name, roleInfo.Description);
            var result = await roleManager.CreateAsync(role);

            if (result.Succeeded)
                logger.LogInformation("Created role: {RoleName}", roleInfo.Name);
            else
                logger.LogError("Failed to create role {RoleName}: {Errors}",
                    roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedRoleClaimsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting claim seeding process");

        var allClaimDefinitions = new List<ClaimDefinition>();
        allClaimDefinitions.AddRange(TenantClaims.All);
        allClaimDefinitions.AddRange(UserClaims.All);

        var roles = await roleManager.Roles.ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            var existingClaims = await roleManager.GetClaimsAsync(role);
            var newClaims = allClaimDefinitions
                .Where(c => c.DefaultAssignments.Any(r => r == role.Name) && existingClaims.All(e => e.Value != c.ClaimName))
                .Select(newClaim => new Claim("permission", newClaim.ClaimName))
                .ToList();

            foreach (var claim in newClaims)
                await roleManager.AddClaimAsync(role, claim);

            if (newClaims.Count != 0)
                await claimsService.InvalidateRoleCacheAsync(role.Name!);
        }

        logger.LogInformation("Completed seeding {ClaimCount} claims", allClaimDefinitions.Count);
    }
}