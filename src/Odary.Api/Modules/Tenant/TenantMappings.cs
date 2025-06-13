using Odary.Api.Domain;

namespace Odary.Api.Modules.Tenant;

public static class TenantMappings
{
    public static TenantResources.V1.Tenant ToContract(this Domain.Tenant tenant)
    {
        return new TenantResources.V1.Tenant
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Country = tenant.Country,
            Timezone = tenant.Timezone,
            LogoUrl = tenant.LogoUrl,
            Slug = tenant.Slug,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
    }

    public static TenantQueries.V1.GetTenant.Response ToGetTenantResponse(this Domain.Tenant tenant)
    {
        return new TenantQueries.V1.GetTenant.Response
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Country = tenant.Country,
            Timezone = tenant.Timezone,
            LogoUrl = tenant.LogoUrl,
            Slug = tenant.Slug,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            Settings = tenant.Settings?.ToContract()
        };
    }

    public static TenantQueries.V1.GetTenantBySlug.Response ToGetTenantBySlugResponse(this Domain.Tenant tenant)
    {
        return new TenantQueries.V1.GetTenantBySlug.Response
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Country = tenant.Country,
            Timezone = tenant.Timezone,
            LogoUrl = tenant.LogoUrl,
            Slug = tenant.Slug,
            IsActive = tenant.IsActive,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            Settings = tenant.Settings?.ToContract()
        };
    }

    public static TenantSettingsResources.V1.TenantSettings ToContract(this TenantSettings settings)
    {
        return new TenantSettingsResources.V1.TenantSettings
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            Language = settings.Language,
            Currency = settings.Currency,
            DateFormat = settings.DateFormat,
            TimeFormat = settings.TimeFormat,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }

    public static TenantQueries.V1.GetTenantSettings.Response ToGetTenantSettingsResponse(this TenantSettings settings)
    {
        return new TenantQueries.V1.GetTenantSettings.Response
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            Language = settings.Language,
            Currency = settings.Currency,
            DateFormat = settings.DateFormat,
            TimeFormat = settings.TimeFormat,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }
} 