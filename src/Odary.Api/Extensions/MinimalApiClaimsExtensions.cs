using Odary.Api.Common.Services;
using Odary.Api.Constants.Claims;

namespace Odary.Api.Extensions;

public static class MinimalApiClaimsExtensions
{
    public static RouteHandlerBuilder WithClaim(this RouteHandlerBuilder builder, string requiredClaim)
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var claimsService = context.HttpContext.RequestServices.GetRequiredService<IClaimsService>();
            var user = context.HttpContext.User;

            if (!user.Identity?.IsAuthenticated == true)
                return Results.Unauthorized();

            var hasRequiredClaim = await user.HasClaimAsync(claimsService, requiredClaim);
            if (!hasRequiredClaim)
                return Results.Forbid();

            return await next(context);
        });
    }

    public static RouteHandlerBuilder RequireSuperAdmin(this RouteHandlerBuilder builder) => builder.WithClaim(TenantClaims.Create);
}