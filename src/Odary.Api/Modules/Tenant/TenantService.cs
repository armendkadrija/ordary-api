using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;
using Odary.Api.Domain;

namespace Odary.Api.Modules.Tenant;

public interface ITenantService
{
    Task<TenantResources.V1.Tenant> CreateTenantAsync(TenantCommands.V1.CreateTenant command, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenant.Response> GetTenantAsync(TenantQueries.V1.GetTenant query, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenants.Response> GetTenantsAsync(TenantQueries.V1.GetTenants query, CancellationToken cancellationToken = default);
    Task<TenantResources.V1.Tenant> UpdateTenantAsync(TenantCommands.V1.UpdateTenant command, CancellationToken cancellationToken = default);
    Task DeactivateTenantAsync(TenantCommands.V1.DeactivateTenant command, CancellationToken cancellationToken = default);
    Task ActivateTenantAsync(TenantCommands.V1.ActivateTenant command, CancellationToken cancellationToken = default);
    Task<TenantSettingsResources.V1.TenantSettings> UpdateTenantSettingsAsync(TenantCommands.V1.UpdateTenantSettings command, CancellationToken cancellationToken = default);
    Task<TenantQueries.V1.GetTenantSettings.Response> GetTenantSettingsAsync(TenantQueries.V1.GetTenantSettings query, CancellationToken cancellationToken = default);
    Task InviteUserAsync(TenantCommands.V1.InviteUser command, CancellationToken cancellationToken = default);
}

public class TenantService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    ILogger<TenantService> logger) : ITenantService
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

        // Check if admin email is already used
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.AdminEmail, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("A user with this email already exists");

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Create tenant
            var tenant = new Domain.Tenant(command.Name, command.Country, command.Timezone, command.LogoUrl);
            
            // Apply business logic - set default active state
            tenant.IsActive = true;
            
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Create default tenant settings
            var settings = new Domain.TenantSettings(
                tenant.Id,
                "en-US",              // Default language
                "USD",                // Default currency
                "MM/dd/yyyy",         // Default date format
                "h:mm tt"             // Default time format
            );
            dbContext.TenantSettings.Add(settings);

            // Create admin user for the tenant
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.AdminPassword);
            var adminUser = new Domain.User(
                tenant.Id, 
                command.AdminEmail, 
                passwordHash,
                command.AdminEmail.Split('@')[0], // Default first name from email
                "",                               // Default empty last name
                "Admin"                           // Admin role
            );
            
            // Apply business logic - set default active state
            adminUser.IsActive = true;
            
            dbContext.Users.Add(adminUser);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Tenant created successfully with ID: {TenantId}, Admin User ID: {UserId}", 
                tenant.Id, adminUser.Id);

            return tenant.ToContract();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<TenantQueries.V1.GetTenant.Response> GetTenantAsync(
        TenantQueries.V1.GetTenant query, 
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .Include(t => t.Settings)
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {query.Id} not found");

        return tenant.ToGetTenantResponse();
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

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);

        if (tenant == null)
            throw new NotFoundException($"Tenant with ID {command.Id} not found");

        // Check if name is already taken by another tenant
        var existingTenant = await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Name == command.Name && t.Id != command.Id, cancellationToken);
        
        if (existingTenant != null)
            throw new BusinessException("A clinic with this name already exists");

        tenant.Name = command.Name;
        tenant.Country = command.Country;
        tenant.Timezone = command.Timezone;
        tenant.LogoUrl = command.LogoUrl;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tenant updated successfully with ID: {TenantId}", tenant.Id);
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

    public async Task<TenantSettingsResources.V1.TenantSettings> UpdateTenantSettingsAsync(
        TenantCommands.V1.UpdateTenantSettings command, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

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
        var settings = await dbContext.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == query.TenantId, cancellationToken);

        if (settings == null)
            throw new NotFoundException($"Tenant settings for tenant {query.TenantId} not found");

        return settings.ToGetTenantSettingsResponse();
    }

    public async Task InviteUserAsync(
        TenantCommands.V1.InviteUser command, 
        CancellationToken cancellationToken = default)
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