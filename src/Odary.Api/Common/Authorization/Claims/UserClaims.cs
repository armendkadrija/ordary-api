using Odary.Api.Constants;

namespace Odary.Api.Common.Authorization.Claims;

public static class UserClaims
{
    public const string Create = $"{Constants.Modules.USER}_{Actions.CREATE}";
    public const string Invite = $"{Constants.Modules.USER}_{Actions.INVITE}";
    public const string Read = $"{Constants.Modules.USER}_{Actions.READ}";
    public const string Update = $"{Constants.Modules.USER}_{Actions.UPDATE}";
    public const string Delete = $"{Constants.Modules.USER}_{Actions.DELETE}";

    public static ClaimDefinition[] All =>
    [
        new()
        {
            ClaimName = Create,
            Description = "Create new users",
            DefaultAssignments = [Roles.SUPER_ADMIN]
        },
        new()
        {
            ClaimName = Invite,
            Description = "Invite new users",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Read,
            Description = "View user information",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Update,
            Description = "Update user information",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        },
        new()
        {
            ClaimName = Delete,
            Description = "Delete users",
            DefaultAssignments = [Roles.SUPER_ADMIN, Roles.ADMIN]
        }
    ];
} 