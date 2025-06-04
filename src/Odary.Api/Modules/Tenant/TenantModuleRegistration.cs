using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Common.Validation;
using Odary.Api.Modules.Tenant.Validators;

namespace Odary.Api.Modules.Tenant;

public static class TenantModuleRegistration
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddSingleton<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<TenantCommands.V1.CreateTenant>, CreateTenantValidator>();
        services.AddScoped<IValidator<TenantCommands.V1.UpdateTenant>, UpdateTenantValidator>();
        services.AddScoped<IValidator<TenantCommands.V1.UpdateTenantSettings>, UpdateTenantSettingsValidator>();

        // Register services
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }

    public static WebApplication MapTenantEndpoints(this WebApplication app)
    {
        var tenantGroup = app.MapGroup("/api/v1/tenants").WithTags("Tenant Management");

        // Create tenant (clinic signup)
        tenantGroup.MapPost("/", async (
            [FromBody] CreateTenantRequest request,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.CreateTenant(
                request.Name, 
                request.AdminEmail, 
                request.AdminPassword, 
                request.Country, 
                request.Timezone, 
                request.LogoUrl);
            var result = await tenantService.CreateTenantAsync(command, cancellationToken);
            return Results.Created($"/api/v1/tenants/{result.Id}", result);
        })
        .WithName("CreateTenant")
        .WithSummary("Create a new clinic/tenant (Clinic Owner Signs Up)")
        .Produces<TenantResources.V1.Tenant>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // Get tenant by ID
        tenantGroup.MapGet("/{id}", async (
            string id,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var query = new TenantQueries.V1.GetTenant(id);
            var result = await tenantService.GetTenantAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetTenant")
        .WithSummary("Get tenant by ID")
        .Produces<TenantQueries.V1.GetTenant.Response>();

        // Get tenants with pagination and filtering
        tenantGroup.MapGet("/", async (
            [AsParameters] GetTenantsRequest request,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var query = new TenantQueries.V1.GetTenants
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Name = request.Name,
                IsActive = request.IsActive
            };
            var result = await tenantService.GetTenantsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetTenants")
        .WithSummary("Get tenants with pagination and filtering (Admin Only)")
        .Produces<TenantQueries.V1.GetTenants.Response>();

        // Update tenant
        tenantGroup.MapPut("/{id}", async (
            string id,
            [FromBody] UpdateTenantRequest request,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.UpdateTenant(
                id, 
                request.Name, 
                request.Country, 
                request.Timezone, 
                request.LogoUrl);
            var result = await tenantService.UpdateTenantAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateTenant")
        .WithSummary("Update tenant information")
        .Produces<TenantResources.V1.Tenant>()
        .ProducesValidationProblem();

        // Deactivate tenant
        tenantGroup.MapPost("/{id}/deactivate", async (
            string id,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.DeactivateTenant(id);
            await tenantService.DeactivateTenantAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeactivateTenant")
        .WithSummary("Deactivate tenant (Admin Only)")
        .Produces(StatusCodes.Status204NoContent);

        // Activate tenant
        tenantGroup.MapPost("/{id}/activate", async (
            string id,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.ActivateTenant(id);
            await tenantService.ActivateTenantAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("ActivateTenant")
        .WithSummary("Activate tenant (Admin Only)")
        .Produces(StatusCodes.Status204NoContent);

        // Tenant Settings endpoints
        var settingsGroup = app.MapGroup("/api/v1/tenants/{tenantId}/settings").WithTags("Tenant Settings");

        // Get tenant settings
        settingsGroup.MapGet("/", async (
            string tenantId,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var query = new TenantQueries.V1.GetTenantSettings(tenantId);
            var result = await tenantService.GetTenantSettingsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetTenantSettings")
        .WithSummary("Get tenant-specific configuration settings")
        .Produces<TenantQueries.V1.GetTenantSettings.Response>();

        // Update tenant settings
        settingsGroup.MapPut("/", async (
            string tenantId,
            [FromBody] UpdateTenantSettingsRequest request,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.UpdateTenantSettings(
                tenantId,
                request.Language,
                request.Currency,
                request.DateFormat,
                request.TimeFormat);
            var result = await tenantService.UpdateTenantSettingsAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateTenantSettings")
        .WithSummary("Update tenant-specific configuration settings")
        .Produces<TenantSettingsResources.V1.TenantSettings>()
        .ProducesValidationProblem();

        // User Management endpoints
        var userGroup = app.MapGroup("/api/v1/tenants/{tenantId}/users").WithTags("Tenant Users");

        // Invite user to tenant
        userGroup.MapPost("/invite", async (
            string tenantId,
            [FromBody] InviteUserRequest request,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var command = new TenantCommands.V1.InviteUser(
                tenantId,
                request.Name,
                request.Email,
                request.Role);
            await tenantService.InviteUserAsync(command, cancellationToken);
            return Results.Accepted();
        })
        .WithName("InviteUser")
        .WithSummary("Invite user to join tenant (Role and User Management)")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        return app;
    }
}

// Request models for minimal API binding
public record CreateTenantRequest(
    string Name,
    string AdminEmail,
    string AdminPassword,
    string Country,
    string Timezone,
    string? LogoUrl = null);

public record UpdateTenantRequest(
    string Name,
    string Country,
    string Timezone,
    string? LogoUrl = null);

public record GetTenantsRequest(
    int Page = 1,
    int PageSize = 20,
    string? Name = null,
    bool? IsActive = null);

public record UpdateTenantSettingsRequest(
    string Language,
    string Currency,
    string DateFormat,
    string TimeFormat);

public record InviteUserRequest(
    string Name,
    string Email,
    string Role); 