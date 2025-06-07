using Odary.Api.Constants;

namespace Odary.Api.Common.Authorization.Claims;

public static class AuditClaims
{
    public const string Read = $"{Constants.Modules.AUDIT}_{Actions.READ}";
    public const string Export = $"{Constants.Modules.AUDIT}_{Actions.EXPORT}";
    public const string Search = $"{Constants.Modules.AUDIT}_{Actions.SEARCH}";

    public static ClaimDefinition[] All =>
    [
        new()
        {
            ClaimName = Read,
            Description = "View audit logs and system activity",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Export,
            Description = "Export audit logs for compliance",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Search,
            Description = "Search and filter audit logs",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        }
    ];
} 