using System.Security.Claims;
using Odary.Api.Common.Services;
using Odary.Api.Constants;

namespace Odary.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetTenantId(this ClaimsPrincipal principal)
    {
        var tenantId = principal.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantId))
            return tenantId;

        if (principal.IsSuperAdmin())
            return null;

        throw new UnauthorizedAccessException("Tenant information not found in user context");
    }

    public static string GetUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User information not found in user context");

        return userId;
    }

    public static string GetUserEmail(this ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
            throw new UnauthorizedAccessException("Email information not found in user context");

        return email;
    }

    public static string GetUserRole(this ClaimsPrincipal principal)
    {
        var role = principal.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(role))
            throw new UnauthorizedAccessException("Role information not found in user context");

        return role;
    }
    
    public static bool IsSuperAdmin(this ClaimsPrincipal principal) => principal.HasRole(Roles.SUPER_ADMIN);

    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.HasRole(Roles.ADMIN);

    public static async Task<bool> HasClaimAsync(this ClaimsPrincipal principal, IClaimsService claimsService, string requiredClaim) =>
        await claimsService.HasClaimAsync(principal.GetUserRole(), requiredClaim);

    static bool HasRole(this ClaimsPrincipal principal, string role) => principal.IsInRole(role);
}