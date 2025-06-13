using Odary.Api.Common.Pagination;

namespace Odary.Api.Modules.Tenant;

public class TenantQueries
{
    public class V1
    {
        public record GetTenant(string Id)
        {
            public record Response
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; init; } = string.Empty;
                public string Country { get; init; } = string.Empty;
                public string Timezone { get; init; } = string.Empty;
                public string? LogoUrl { get; init; }
                public bool IsActive { get; init; }
                public DateTimeOffset CreatedAt { get; init; }
                public DateTimeOffset? UpdatedAt { get; init; }
                public TenantSettingsResources.V1.TenantSettings? Settings { get; init; }
            }
        }

        public class GetTenants : PaginatedRequest
        {
            public string? Name { get; set; }
            public bool? IsActive { get; set; }

            public class Response : PaginatedResponse<TenantResources.V1.Tenant> { }
        }

        public record GetTenantSettings(string TenantId)
        {
            public record Response
            {
                public string Id { get; init; } = string.Empty;
                public string TenantId { get; init; } = string.Empty;
                public string Language { get; init; } = string.Empty;
                public string Currency { get; init; } = string.Empty;
                public string DateFormat { get; init; } = string.Empty;
                public string TimeFormat { get; init; } = string.Empty;
                public DateTimeOffset CreatedAt { get; init; }
                public DateTimeOffset? UpdatedAt { get; init; }
            }
        }
    }
}

public class TenantCommands
{
    public class V1
    {
        public record CreateTenant(
            string Name,
            string Country,
            string Timezone,
            string? LogoUrl = null);

        public record UpdateTenant(
            string Id,
            string Name,
            string Country,
            string Timezone,
            string? LogoUrl = null);

        public record DeactivateTenant(string Id);
        public record ActivateTenant(string Id);

        public record CreateTenantSettings(
            string TenantId,
            string Language,
            string Currency,
            string DateFormat,
            string TimeFormat);

        public record UpdateTenantSettings(
            string TenantId,
            string Language,
            string Currency,
            string DateFormat,
            string TimeFormat);

        public record InviteUser(
            string TenantId,
            string Name,
            string Email,
            string Role);
    }
}

public class TenantResources
{
    public class V1
    {
        public record Tenant
        {
            public string Id { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string Country { get; init; } = string.Empty;
            public string Timezone { get; init; } = string.Empty;
            public string? LogoUrl { get; init; }
            public bool IsActive { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }
    }
}

public class TenantSettingsResources
{
    public class V1
    {
        public record TenantSettings
        {
            public string Id { get; init; } = string.Empty;
            public string TenantId { get; init; } = string.Empty;
            public string Language { get; init; } = string.Empty;
            public string Currency { get; init; } = string.Empty;
            public string DateFormat { get; init; } = string.Empty;
            public string TimeFormat { get; init; } = string.Empty;
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }
    }
} 