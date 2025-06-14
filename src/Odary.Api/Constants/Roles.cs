using Odary.Api.Common.Authorization;

namespace Odary.Api.Constants;

public static class Roles
{
    public const string SUPER_ADMIN = "SuperAdmin";
    public const string ADMIN = "Admin";
    public const string DENTIST = "Dentist";
    public const string ASSISTANT = "Assistant";

    public static RoleDefinition[] ALL =
    [
        new() { Name = SUPER_ADMIN, Description = "Platform administrator who manages tenants and system-wide operations" },
        new() { Name = ADMIN, Description = "Practice administrator with full access within their tenant" },
        new() { Name = DENTIST, Description = "Licensed dentist with clinical and administrative access within their practice" },
        new() { Name = ASSISTANT, Description = "Dental assistant with limited clinical access within their practice" }
    ];
}