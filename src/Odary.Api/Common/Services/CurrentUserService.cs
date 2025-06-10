using System.Security.Claims;
using Odary.Api.Extensions;

namespace Odary.Api.Common.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string TenantId { get; }
    string Role { get; }
    string Email { get; }
    bool IsSuperAdmin { get; }
    bool IsAdmin { get; }
}

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal CurrentUser => httpContextAccessor.HttpContext?.User 
                                           ?? throw new UnauthorizedAccessException("No authenticated user found");

    public string UserId => CurrentUser.GetUserId();
    public string TenantId => CurrentUser.GetTenantId();
    public string Role => CurrentUser.GetUserRole();
    public string Email => CurrentUser.GetUserEmail();
    public bool IsSuperAdmin => CurrentUser.IsSuperAdmin();
    public bool IsAdmin => CurrentUser.IsAdmin();
} 