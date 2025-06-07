using Odary.Api.Common.Extensions;
using Odary.Api.Common.Authorization.Claims;

namespace Odary.Api.Common.Authorization;

public static class MinimalApiClaimsExtensions
{
    /// <summary>
    /// Requires a single claim for this endpoint
    /// </summary>
    /// <param name="builder">The route handler builder</param>
    /// <param name="requiredClaim">The required claim (e.g., "patient_read")</param>
    /// <returns>The route handler builder for chaining</returns>
    public static RouteHandlerBuilder WithClaim(this RouteHandlerBuilder builder, string requiredClaim)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var claimsService = context.HttpContext.RequestServices.GetRequiredService<IClaimsService>();
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated == true)
            {
                return Results.Unauthorized();
            }

            var hasRequiredClaim = await user.HasClaimAsync(claimsService, requiredClaim);
            if (!hasRequiredClaim)
            {
                return Results.Forbid();
            }

            return await next(context);
        });
    }


    /// <summary>
    /// Requires Super Admin role for this endpoint
    /// </summary>
    public static RouteHandlerBuilder RequireSuperAdmin(this RouteHandlerBuilder builder)
    {
        return builder.WithClaim(TenantClaims.Create); // Only SuperAdmin can create tenants
    }
} 