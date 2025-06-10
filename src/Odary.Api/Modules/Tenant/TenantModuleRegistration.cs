using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Modules.Tenant.Validators;
using Odary.Api.Common.Authorization.Claims;
using System.Security.Claims;
using Odary.Api.Common.Services;
using Odary.Api.Extensions;

namespace Odary.Api.Modules.Tenant;

public static class TenantModuleRegistration
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddScoped<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<TenantCommands.V1.CreateTenant>, CreateTenantValidator>();
        services.AddScoped<IValidator<TenantCommands.V1.UpdateTenant>, UpdateTenantValidator>();
        services.AddScoped<IValidator<TenantCommands.V1.UpdateTenantSettings>, UpdateTenantSettingsValidator>();

        // Register services
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }

    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var tenantGroup = app.MapGroup("/api/v1/tenants").WithTags("Tenant Management");

        // Create tenant (clinic signup)
        tenantGroup.MapPost("/", async (
            [FromBody] TenantCommands.V1.CreateTenant command,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantService.CreateTenantAsync(command, cancellationToken);
            return Results.Created($"/api/v1/tenants/{result.Id}", result);
        })
        .RequireSuperAdmin()
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
        .WithClaim(TenantClaims.Read)
        .WithName("GetTenant")
        .WithSummary("Get tenant by ID")
        .Produces<TenantQueries.V1.GetTenant.Response>();

        // Get tenants with pagination and filtering
        tenantGroup.MapGet("/", async (
            [AsParameters] TenantQueries.V1.GetTenants query,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantService.GetTenantsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .RequireSuperAdmin()
        .WithName("GetTenants")
        .WithSummary("Get tenants with pagination and filtering (Admin Only)")
        .Produces<TenantQueries.V1.GetTenants.Response>();

        // Update tenant
        tenantGroup.MapPut("/{id}", async (
            string id,
            [FromBody] TenantCommands.V1.UpdateTenant command,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var updatedCommand = command with { Id = id };
            var result = await tenantService.UpdateTenantAsync(updatedCommand, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(TenantClaims.Update)
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
        .WithClaim(TenantClaims.Delete)
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
        .WithClaim(TenantClaims.Update)
        .WithName("ActivateTenant")
        .WithSummary("Activate tenant (Admin Only)")
        .Produces(StatusCodes.Status204NoContent);

        // Tenant Settings endpoints
        var settingsGroup = app.MapGroup("/api/v1/tenants/{tenantId}/settings").WithTags("Tenant Settings");

        // Get tenant settings
        settingsGroup.MapGet("/", async (
            string tenantId,
            ITenantService tenantService,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken) =>
        {
            var query = new TenantQueries.V1.GetTenantSettings(tenantId);
            var result = await tenantService.GetTenantSettingsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(TenantClaims.Read)
        .WithName("GetTenantSettings")
        .WithSummary("Get tenant-specific configuration settings")
        .Produces<TenantQueries.V1.GetTenantSettings.Response>();

        // Update tenant settings
        settingsGroup.MapPut("/", async (
            string tenantId,
            [FromBody] TenantCommands.V1.UpdateTenantSettings command,
            ITenantService tenantService,
            ClaimsPrincipal currentUser,
            CancellationToken cancellationToken) =>
        {
            var updatedCommand = command with { TenantId = tenantId };
            var result = await tenantService.UpdateTenantSettingsAsync(updatedCommand, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(TenantClaims.Update)
        .WithName("UpdateTenantSettings")
        .WithSummary("Update tenant-specific configuration settings")
        .Produces<TenantSettingsResources.V1.TenantSettings>()
        .ProducesValidationProblem();

        // User Management endpoints
        var userGroup = app.MapGroup("/api/v1/tenants/{tenantId}/users").WithTags("Tenant Users");

        // Invite user to tenant
        userGroup.MapPost("/invite", async (
            string tenantId,
            [FromBody] TenantCommands.V1.InviteUser command,
            ITenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            var updatedCommand = command with { TenantId = tenantId };
            await tenantService.InviteUserAsync(updatedCommand, cancellationToken);
            return Results.Accepted();
        })
        .WithClaim(UserClaims.Invite)
        .WithName("InviteUserToTenant")
        .WithSummary("Invite user to join tenant (Role and User Management)")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesValidationProblem();

        return app;
    }
}

 