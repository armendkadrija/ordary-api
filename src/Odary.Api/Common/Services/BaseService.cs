namespace Odary.Api.Common.Services;

public abstract class BaseService(ICurrentUserService currentUserService)
{
    protected readonly ICurrentUserService CurrentUser = currentUserService;

    protected bool HasTenantAccess(string? resourceTenantId) => CurrentUser.IsAdmin && resourceTenantId == CurrentUser.TenantId;
}