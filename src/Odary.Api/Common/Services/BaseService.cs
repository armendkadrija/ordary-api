namespace Odary.Api.Common.Services;

public abstract class BaseService(ICurrentUserService currentUserService)
{
    protected readonly ICurrentUserService CurrentUser = currentUserService;

    /// <summary>
    /// Validates that an Admin user can only access resources within their tenant
    /// </summary>
    /// <param name="resourceTenantId">The tenant ID of the resource being accessed</param>
    /// <param name="resourceType">Type of resource for error message</param>
    /// <exception cref="Common.Exceptions.BusinessException">Thrown when Admin tries to access cross-tenant resource</exception>
    protected void ValidateTenantAccess(string resourceTenantId, string resourceType = "resource")
    {
        if (CurrentUser.IsAdmin && resourceTenantId != CurrentUser.TenantId)
        {
            throw new Exceptions.BusinessException($"You can only access {resourceType} within your tenant");
        }
    }

    /// <summary>
    /// Validates that an Admin user can only modify resources within their tenant
    /// </summary>
    /// <param name="resourceTenantId">The tenant ID of the resource being modified</param>
    /// <param name="action">The action being performed (update, delete, etc.)</param>
    /// <param name="resourceType">Type of resource for error message</param>
    /// <exception cref="Common.Exceptions.BusinessException">Thrown when Admin tries to modify cross-tenant resource</exception>
    protected void ValidateTenantModification(string resourceTenantId, string action = "modify", string resourceType = "resource")
    {
        if (CurrentUser.IsAdmin && resourceTenantId != CurrentUser.TenantId)
        {
            throw new Exceptions.BusinessException($"You can only {action} {resourceType} within your tenant");
        }
    }
} 