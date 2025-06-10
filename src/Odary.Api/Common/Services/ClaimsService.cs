using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Odary.Api.Common.Database;

namespace Odary.Api.Common.Services;

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

    Task InvalidateRoleCacheAsync(string role);

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