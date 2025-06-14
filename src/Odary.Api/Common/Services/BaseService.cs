namespace Odary.Api.Common.Services;

public abstract class BaseService(ICurrentUserService currentUserService)
{
    protected readonly ICurrentUserService CurrentUser = currentUserService;
    
    protected void ValidateTenantAccess(string resourceTenantId, string resourceType = "resource")
    {
        if (CurrentUser.IsAdmin && resourceTenantId != CurrentUser.TenantId)
            throw new Exceptions.BusinessException($"You can only access {resourceType} within your tenant");
    }
    
    protected void ValidateTenantModification(string resourceTenantId, string action = "modify", string resourceType = "resource")
    {
        if (CurrentUser.IsAdmin && resourceTenantId != CurrentUser.TenantId)
            throw new Exceptions.BusinessException($"You can only {action} {resourceType} within your tenant");
    }
} 