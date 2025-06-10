using Microsoft.AspNetCore.Authorization;
using Odary.Api.Common.Services;
using Odary.Api.Extensions;

namespace Odary.Api.Common.Authorization;

public class ClaimAuthorizationHandler(IClaimsService claimsService) : AuthorizationHandler<ClaimRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClaimRequirement requirement)
    {
        if (!context.User.Identity!.IsAuthenticated)
        {
            context.Fail();
            return;
        }

        try
        {
            var hasRequiredClaim = await context.User.HasClaimAsync(claimsService, requirement.RequiredClaim);
            
            if (hasRequiredClaim)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
        catch
        {
            context.Fail();
        }
    }
}

public class ClaimRequirement(string requiredClaim) : IAuthorizationRequirement
{
    public string RequiredClaim { get; } = requiredClaim;
} 