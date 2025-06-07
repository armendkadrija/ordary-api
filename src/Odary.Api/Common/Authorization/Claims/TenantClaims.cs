using Odary.Api.Constants;

namespace Odary.Api.Common.Authorization.Claims;

public static class TenantClaims
{
    public const string Create = $"{Constants.Modules.TENANT}_{Actions.CREATE}";
    public const string Read = $"{Constants.Modules.TENANT}_{Actions.READ}";
    public const string Update = $"{Constants.Modules.TENANT}_{Actions.UPDATE}";
    public const string Delete = $"{Constants.Modules.TENANT}_{Actions.DELETE}";

    public static ClaimDefinition[] All =>
    [
        new()
        {
            ClaimName = Create,
            Description = "Create new tenants",
            DefaultAssignments = [Roles.SUPER_ADMIN]
        },
        new()
        {
            ClaimName = Read,
            Description = "View tenant information", 
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Update,
            Description = "Update tenant settings",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Delete,
            Description = "Delete tenants",
            DefaultAssignments = [Roles.SUPER_ADMIN]
        }
    ];
} 