using System.Security.Claims;
using Odary.Api.Common.Services;
using Odary.Api.Constants;

namespace Odary.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the tenant ID from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The tenant ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when tenant ID is not found</exception>
    public static string? GetTenantId(this ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
            return tenantId;
        
        // SuperAdmin users don't have tenant IDs - return null instead of throwing
        if (principal.IsSuperAdmin())
            return null;
            
        throw new UnauthorizedAccessException("Tenant information not found in user context");
    }

    /// <summary>
    /// Gets the user ID from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The user ID</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID is not found</exception>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User information not found in user context");
        
        return userId;
    }

    /// <summary>
    /// Gets the user email from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The user email</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when email is not found</exception>
    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            throw new UnauthorizedAccessException("Email information not found in user context");
        
        return email;
    }

    /// <summary>
    /// Gets the user role from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The user role</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when role is not found</exception>
    public static string GetUserRole(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(role))
            throw new UnauthorizedAccessException("Role information not found in user context");
        
        return role;
    }

    /// <summary>
    /// Tries to get the tenant ID from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The tenant ID or null if not found</returns>
    public static string? TryGetTenantId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("tenant_id")?.Value;
    }

    /// <summary>
    /// Tries to get the user ID from the JWT claims
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>The user ID or null if not found</returns>
    public static string? TryGetUserId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("user_id")?.Value;
    }

    /// <summary>
    /// Checks if the user has a specific role
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <param name="role">The role to check</param>
    /// <returns>True if the user has the specified role</returns>
    public static bool HasRole(this ClaimsPrincipal principal, string role)
    {
        return principal.IsInRole(role);
    }

    /// <summary>
    /// Checks if the user is a super administrator
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>True if the user is a super administrator</returns>
    public static bool IsSuperAdmin(this ClaimsPrincipal principal)
    {
        return principal.HasRole(Roles.SUPER_ADMIN);
    }

    /// <summary>
    /// Checks if the user is an administrator
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <returns>True if the user is an administrator</returns>
    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.HasRole(Roles.ADMIN);
    }

    /// <summary>
    /// Gets all available system roles
    /// </summary>
    /// <returns>Array of available role names</returns>
    public static string[] GetAvailableRoles()
    {
        return [Roles.SUPER_ADMIN, Roles.ADMIN, Roles.DENTIST, Roles.ASSISTANT];
    }

    /// <summary>
    /// Validates if a role name is a valid system role
    /// </summary>
    /// <param name="roleName">The role name to validate</param>
    /// <returns>True if the role is valid</returns>
    public static bool IsValidRole(string roleName)
    {
        return GetAvailableRoles().Contains(roleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the user has a specific permission claim
    /// </summary>
    /// <param name="principal">The claims principal</param>
    /// <param name="claimsService">The claims service</param>
    /// <param name="requiredClaim">The required claim (e.g., "user_read")</param>
    /// <returns>True if the user has the required claim</returns>
    public static async Task<bool> HasClaimAsync(this ClaimsPrincipal principal, IClaimsService claimsService, string requiredClaim)
    {
        var userRole = principal.GetUserRole();
        return await claimsService.HasClaimAsync(userRole, requiredClaim);
    }
} 