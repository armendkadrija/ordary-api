using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;

namespace Odary.Api.Modules.Tenant;

public interface ITenantService
{
    Task<TenantResources.V1.Tenant> CreateTenantAsync(TenantCommands.V1.CreateTenant command, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenant.Response> GetTenantAsync(TenantQueries.V1.GetTenant query, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenantBySlug.Response> GetTenantBySlugAsync(TenantQueries.V1.GetTenantBySlug query, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenants.Response> GetTenantsAsync(TenantQueries.V1.GetTenants query, CancellationToken cancellationToken = default);
    Task<TenantResources.V1.Tenant> UpdateTenantAsync(TenantCommands.V1.UpdateTenant command, CancellationToken cancellationToken = default);
    Task DeactivateTenantAsync(TenantCommands.V1.DeactivateTenant command, CancellationToken cancellationToken = default);
    Task ActivateTenantAsync(TenantCommands.V1.ActivateTenant command, CancellationToken cancellationToken = default);
    Task<TenantSettingsResources.V1.TenantSettings> CreateTenantSettingsAsync(TenantCommands.V1.CreateTenantSettings command, CancellationToken cancellationToken = default);
    Task<TenantSettingsResources.V1.TenantSettings> UpdateTenantSettingsAsync(TenantCommands.V1.UpdateTenantSettings command, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenantSettings.Response> GetTenantSettingsAsync(TenantQueries.V1.GetTenantSettings query, CancellationToken cancellationToken = default);
    Task InviteUserAsync(TenantCommands.V1.InviteUser command, CancellationToken cancellationToken = default);
}

public class TenantService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    ILogger<TenantService> logger,
    ICurrentUserService currentUserService) : BaseService(currentUserService), ITenantService
{
    public async Task<TenantResources.V1.Tenant> CreateTenantAsync(
        TenantCommands.V1.CreateTenant command, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Check if tenant name is already taken
        var existingTenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Name == command.Name, cancellationToken);
        
        if (existingTenant != null)
            throw new BusinessException("A clinic with this name already exists");

        // Check if slug is already taken
        var existingSlug = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == command.Slug, cancellationToken);
        
        if (existingSlug != null)
            throw new BusinessException("This slug is already taken. Please choose a different one.");

        try
        {
            // Create tenant
            var tenant = new Domain.Tenant(command.Name, command.Country, command.Timezone, command.Slug, command.LogoUrl);
            
            // Apply business logic - set default active state
            tenant.IsActive = true;
            
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Tenant created successfully with ID: {TenantId} and slug: {Slug}", tenant.Id, tenant.Slug);

            return tenant.ToContract();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create tenant: {TenantName} with slug: {Slug}", command.Name, command.Slug);
            throw;
        }
    }

    public async Task<TenantQueries.V1.GetTenant.Response> GetTenantAsync(
        TenantQueries.V1.GetTenant query,
        CancellationToken cancellationToken = default)
    {
        // Admin users can only access their own tenant information
        if (CurrentUser.IsAdmin && query.Id != CurrentUser.TenantId)
            throw new BusinessException("You can only access your own tenant information");

        var tenant = await dbContext.Tenants
            .Include(t => t.Settings)
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {query.Id} not found");

        return tenant.ToGetTenantResponse();
    }

    public async Task<TenantQueries.V1.GetTenantBySlug.Response> GetTenantBySlugAsync(
        TenantQueries.V1.GetTenantBySlug query,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.Settings)
            .FirstOrDefaultAsync(t => t.Slug == query.Slug && t.IsActive, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Active tenant with slug '{query.Slug}' not found");

        return tenant.ToGetTenantBySlugResponse();
    }

    public async Task<TenantQueries.V1.GetTenants.Response> GetTenantsAsync(
        TenantQueries.V1.GetTenants query, 
        CancellationToken cancellationToken = default)
    {
        var tenantsQuery = dbContext.Tenants.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(query.Name))
        {
            tenantsQuery = tenantsQuery.Where(t => t.Name.Contains(query.Name));
        }

        if (!string.IsNullOrEmpty(query.Slug))
        {
            tenantsQuery = tenantsQuery.Where(t => t.Slug.Contains(query.Slug));
        }

        if (query.IsActive.HasValue)
        {
            tenantsQuery = tenantsQuery.Where(t => t.IsActive == query.IsActive.Value);
        }

        var totalCount = await tenantsQuery.CountAsync(cancellationToken);

        var tenants = await tenantsQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return new TenantQueries.V1.GetTenants.Response
        {
            Items = tenants.Select(t => t.ToContract()).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<TenantResources.V1.Tenant> UpdateTenantAsync(
        TenantCommands.V1.UpdateTenant command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Admin users can only update their own tenant
        if (CurrentUser.IsAdmin && command.Id != CurrentUser.TenantId)
            throw new BusinessException("You can only update your own tenant");

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {command.Id} not found");

        // Check if name is already taken by another tenant
        var existingTenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Name == command.Name && t.Id != command.Id, cancellationToken);
        
        if (existingTenant != null)
            throw new BusinessException("A clinic with this name already exists");

        // Check if slug is already taken by another tenant
        var existingSlug = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == command.Slug && t.Id != command.Id, cancellationToken);
        
        if (existingSlug != null)
            throw new BusinessException("This slug is already taken. Please choose a different one.");

        tenant.Name = command.Name;
        tenant.Country = command.Country;
        tenant.Timezone = command.Timezone;
        tenant.Slug = command.Slug;
        tenant.LogoUrl = command.LogoUrl;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant updated successfully with ID: {TenantId} and slug: {Slug}", tenant.Id, tenant.Slug);
        return tenant.ToContract();
    }

    public async Task DeactivateTenantAsync(
        TenantCommands.V1.DeactivateTenant command, 
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {command.Id} not found");

        tenant.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant deactivated successfully with ID: {TenantId}", tenant.Id);
    }

    public async Task ActivateTenantAsync(
        TenantCommands.V1.ActivateTenant command, 
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {command.Id} not found");

        tenant.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant activated successfully with ID: {TenantId}", tenant.Id);
    }

    public async Task<TenantSettingsResources.V1.TenantSettings> CreateTenantSettingsAsync(
        TenantCommands.V1.CreateTenantSettings command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Admin users can only create settings for their own tenant
        if (CurrentUser.IsAdmin && command.TenantId != CurrentUser.TenantId)
            throw new BusinessException("You can only create settings for your own tenant");

        // Verify tenant exists and is active
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.TenantId && t.IsActive, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Active tenant with ID {command.TenantId} not found");

        // Check if settings already exist for this tenant
        var existingSettings = await dbContext.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == command.TenantId, cancellationToken);

        if (existingSettings != null)
            throw new BusinessException($"Settings already exist for tenant {command.TenantId}");

        // Create new tenant settings
        var settings = new Domain.TenantSettings(
            command.TenantId,
            command.Language,
            command.Currency,
            command.DateFormat,
            command.TimeFormat);

        dbContext.TenantSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant settings created successfully for tenant: {TenantId}", command.TenantId);

        return settings.ToContract();
    }

    public async Task<TenantSettingsResources.V1.TenantSettings> UpdateTenantSettingsAsync(
        TenantCommands.V1.UpdateTenantSettings command,
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Admin users can only update their own tenant settings
        if (CurrentUser.IsAdmin && command.TenantId != CurrentUser.TenantId)
            throw new BusinessException("You can only update your own tenant settings");

        var settings = await dbContext.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == command.TenantId, cancellationToken);

        if (settings == null)
            throw new NotFoundException($"Tenant settings for tenant {command.TenantId} not found");

        settings.Language = command.Language;
        settings.Currency = command.Currency;
        settings.DateFormat = command.DateFormat;
        settings.TimeFormat = command.TimeFormat;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant settings updated successfully for tenant: {TenantId}", command.TenantId);
        return settings.ToContract();
    }

    public async Task<TenantQueries.V1.GetTenantSettings.Response> GetTenantSettingsAsync(
        TenantQueries.V1.GetTenantSettings query,
        CancellationToken cancellationToken = default)
    {
        // Admin users can only access their own tenant settings
        if (CurrentUser.IsAdmin && query.TenantId != CurrentUser.TenantId)
            throw new BusinessException("You can only access your own tenant settings");

        var settings = await dbContext.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == query.TenantId, cancellationToken);

        if (settings == null)
            throw new NotFoundException($"Tenant settings for tenant {query.TenantId} not found");

        return settings.ToGetTenantSettingsResponse();
    }

    public async Task InviteUserAsync(TenantCommands.V1.InviteUser command, CancellationToken cancellationToken = default)
    {
        // Verify tenant exists
        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.TenantId && t.IsActive, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Active tenant with ID {command.TenantId} not found");

        // Check if user already exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (existingUser != null)
            throw new BusinessException("A user with this email already exists");

        // In a real implementation, this would send an invitation email
        // For now, we'll just log the invitation
        logger.LogInformation("User invitation sent to {Email} for tenant {TenantId} with role {Role}", 
            command.Email, command.TenantId, command.Role);

        // TODO: Implement email invitation system
        // - Generate invitation token
        // - Send email with signup link
        // - Store pending invitation in database
    }
} 