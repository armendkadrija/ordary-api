using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Odary.Api.Common.Database;
using Odary.Api.Common.Authorization.Claims;

namespace Odary.Api.Common.Authorization;

public interface IClaimsService
{
    /// <summary>
    /// Gets all claims for a specific role (cached)
    /// </summary>
    Task<string[]> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a role has a specific claim
    /// </summary>
    Task<bool> HasClaimAsync(string role, string claim, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Seeds all claim definitions and role assignments
    /// </summary>
    Task SeedClaimsAsync(CancellationToken cancellationToken = default);
}

public class ClaimsService(
    OdaryDbContext dbContext,
    IDistributedCache cache,
    ILogger<ClaimsService> logger) : IClaimsService
{
    private const string CACHE_KEY_PREFIX = "role_claims:";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public async Task<string[]> GetRoleClaimsAsync(string role, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}{role}";
        
        // Try cache first
        var cachedClaims = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedClaims))
        {
            var claims = JsonSerializer.Deserialize<string[]>(cachedClaims);
            if (claims != null)
            {
                logger.LogDebug("Retrieved {ClaimCount} claims for role {Role} from cache", claims.Length, role);
                return claims;
            }
        }

        // Cache miss - fetch from database
        // First get the role entity to get its ID
        var roleEntity = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == role, cancellationToken);
        if (roleEntity == null)
        {
            logger.LogWarning("Role {Role} not found", role);
            return [];
        }

        var roleClaims = await dbContext.RoleClaims
            .Where(rc => rc.RoleId == roleEntity.Id)
            .Select(rc => rc.ClaimValue!)
            .ToArrayAsync(cancellationToken);

        // Cache the result
        var serializedClaims = JsonSerializer.Serialize(roleClaims);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiry
        };
        
        await cache.SetStringAsync(cacheKey, serializedClaims, cacheOptions, cancellationToken);
        
        logger.LogDebug("Retrieved {ClaimCount} claims for role {Role} from database and cached", roleClaims.Length, role);
        return roleClaims;
    }

    public async Task<bool> HasClaimAsync(string role, string claim, CancellationToken cancellationToken = default)
    {
        var claims = await GetRoleClaimsAsync(role, cancellationToken);
        return claims.Contains(claim);
    }

    public async Task SeedClaimsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting claim seeding process");

        // Get all claim definitions
        var allClaimDefinitions = new List<ClaimDefinition>();
        allClaimDefinitions.AddRange(TenantClaims.All);
        allClaimDefinitions.AddRange(UserClaims.All);
        allClaimDefinitions.AddRange(AuditClaims.All);

        foreach (var definition in allClaimDefinitions)
        {
            await SeedClaimDefinition(definition, cancellationToken);
        }

        logger.LogInformation("Completed seeding {ClaimCount} claims", allClaimDefinitions.Count);
    }

    private async Task SeedClaimDefinition(ClaimDefinition definition, CancellationToken cancellationToken)
    {
        var claimName = definition.ClaimName;

        // Assign to roles (we don't need a separate Claims table, just use RoleClaims)
        foreach (var roleName in definition.DefaultAssignments)
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
            if (role == null)
            {
                logger.LogWarning("Role {RoleName} not found for claim {ClaimName}", roleName, claimName);
                continue;
            }

            var existingRoleClaim = await dbContext.RoleClaims
                .FirstOrDefaultAsync(rc => rc.RoleId == role.Id && rc.ClaimValue == claimName, cancellationToken);

            if (existingRoleClaim == null)
            {
                var roleClaim = new Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>
                {
                    RoleId = role.Id,
                    ClaimType = "permission",
                    ClaimValue = claimName
                };

                dbContext.RoleClaims.Add(roleClaim);
                await dbContext.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Assigned claim {ClaimName} to role {RoleName}", claimName, roleName);
                
                // Invalidate cache for this role
                await InvalidateRoleCacheAsync(roleName);
            }
        }
    }

    public async Task InvalidateRoleCacheAsync(string role)
    {
        try
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{role}";
            await cache.RemoveAsync(cacheKey);
            logger.LogDebug("Invalidated cache for role {Role}", role);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to invalidate cache for role {Role} - continuing without cache invalidation", role);
        }
    }
} 