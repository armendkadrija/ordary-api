using Microsoft.AspNetCore.Identity;
using Odary.Api.Constants;
using Odary.Api.Domain;

namespace Odary.Api.Common.Database;

public class DatabaseSeeder(RoleManager<Role> roleManager, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync()
    {
        await SeedRolesAsync();
    }

    private async Task SeedRolesAsync()
    {
        var roles = new[]
        {
            new { Name = Roles.SUPER_ADMIN, Description = "Platform administrator who manages tenants and system-wide operations" },
            new { Name = Roles.ADMIN, Description = "Practice administrator with full access within their tenant" },
            new { Name = Roles.DENTIST, Description = "Licensed dentist with clinical and administrative access within their practice" },
            new { Name = Roles.ASSISTANT, Description = "Dental assistant with limited clinical access within their practice" }
        };

        foreach (var roleInfo in roles)
        {
            if (await roleManager.RoleExistsAsync(roleInfo.Name))
                continue;
            
            try
            {
                var role = new Role(roleInfo.Name, roleInfo.Description);
                var result = await roleManager.CreateAsync(role);
                    
                if (result.Succeeded)
                {
                    logger.LogInformation("Created role: {RoleName}", roleInfo.Name);
                }
                else
                {
                    logger.LogError("Failed to create role {RoleName}: {Errors}", 
                        roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex) when (ex.Message.Contains("duplicate key") || ex.Message.Contains("23505"))
            {
                // Role already exists due to race condition - this is expected in concurrent scenarios
                logger.LogDebug("Role {RoleName} already exists (race condition handled)", roleInfo.Name);
            }
        }
    }
} 