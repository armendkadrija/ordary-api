using Odary.Api.Common.Authorization;

namespace Odary.Api.Constants.Claims;

public static class TenantClaims
{
    public const string Create = $"{Modules.TENANT}_{Actions.CREATE}";
    public const string Read = $"{Modules.TENANT}_{Actions.READ}";
    public const string Update = $"{Modules.TENANT}_{Actions.UPDATE}";
    public const string Delete = $"{Modules.TENANT}_{Actions.DELETE}";

    public static ClaimDefinition[] All =>
    [
        new()
        {
            ClaimName = Create,
            DefaultAssignments = [Roles.SUPER_ADMIN]
        },
        new()
        {
            ClaimName = Read, 
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Update,
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Delete,
            DefaultAssignments = [Roles.SUPER_ADMIN]
        }
    ];
} 