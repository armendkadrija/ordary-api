using Odary.Api.Common.Authorization;

namespace Odary.Api.Constants.Claims;

public static class UserClaims
{
    public const string Create = $"{Modules.USER}_{Actions.CREATE}";
    public const string Invite = $"{Modules.USER}_{Actions.INVITE}";
    public const string Read = $"{Modules.USER}_{Actions.READ}";
    public const string Update = $"{Modules.USER}_{Actions.UPDATE}";
    public const string Delete = $"{Modules.USER}_{Actions.DELETE}";

    public static ClaimDefinition[] All =>
    [
        new()
        {
            ClaimName = Create,
            DefaultAssignments = [Roles.SUPER_ADMIN]
        },
        new()
        {
            ClaimName = Invite,
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
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
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        }
    ];
} 