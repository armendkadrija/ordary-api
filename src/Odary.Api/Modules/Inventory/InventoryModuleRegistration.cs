using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Domain.Enums;

namespace Odary.Api.Modules.Inventory;

public static class InventoryModuleRegistration
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<IInventoryService, InventoryService>();

        // Register validators
        services.AddScoped<IValidator<InventoryCommands.V1.CreateInventoryItem>, Validators.CreateInventoryItemValidator>();
        services.AddScoped<IValidator<InventoryCommands.V1.UpdateInventoryItem>, Validators.UpdateInventoryItemValidator>();
        services.AddScoped<IValidator<InventoryCommands.V1.UpdateStock>, Validators.UpdateStockValidator>();
        services.AddScoped<IValidator<InventoryCommands.V1.DeductStock>, Validators.DeductStockValidator>();
        services.AddScoped<IValidator<InventoryCommands.V1.DeductStockByUnits>, Validators.DeductStockByUnitsValidator>();

        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inventory")
            .WithTags("Inventory")
            .RequireAuthorization();

        // Create inventory item
        group.MapPost("/", async (
            [FromBody] InventoryCommands.V1.CreateInventoryItem command,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var result = await inventoryService.CreateInventoryItemAsync(command, cancellationToken);
            return Results.Created($"/api/v1/inventory/{result.Id}", result);
        })
        .WithName("CreateInventoryItem")
        .WithSummary("Create a new inventory item")
        .Produces<InventoryResources.V1.CreateInventoryItemResponse>(201)
        .ProducesValidationProblem()
        .ProducesProblem(400);

        // Get inventory item by ID
        group.MapGet("/{id}", async (
            string id,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var query = new InventoryQueries.V1.GetInventoryItem(id);
            var result = await inventoryService.GetInventoryItemAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetInventoryItem")
        .WithSummary("Get inventory item by ID")
        .Produces<InventoryQueries.V1.GetInventoryItem.Response>()
        .ProducesProblem(404);

        // Get inventory items with filtering and pagination
        group.MapGet("/", async (
            [AsParameters] InventoryQueries.V1.GetInventoryItems query,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var result = await inventoryService.GetInventoryItemsAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetInventoryItems")
        .WithSummary("Get inventory items with filtering and pagination")
        .Produces<InventoryQueries.V1.GetInventoryItems.Response>()
        .ProducesValidationProblem();

        // Update inventory item
        group.MapPut("/{id}", async (
            string id,
            [FromBody] InventoryCommands.V1.UpdateInventoryItem command,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID in URL does not match ID in request body");

            var result = await inventoryService.UpdateInventoryItemAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateInventoryItem")
        .WithSummary("Update inventory item")
        .Produces<InventoryResources.V1.InventoryItem>()
        .ProducesValidationProblem()
        .ProducesProblem(400)
        .ProducesProblem(404);

        // Update stock manually
        group.MapPatch("/{id}/stock", async (
            string id,
            [FromBody] InventoryCommands.V1.UpdateStock command,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID in URL does not match ID in request body");

            var result = await inventoryService.UpdateStockAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateStock")
        .WithSummary("Update inventory stock manually")
        .Produces<InventoryResources.V1.InventoryItem>()
        .ProducesValidationProblem()
        .ProducesProblem(400)
        .ProducesProblem(404);

        // Deduct stock (for treatment usage)
        group.MapPatch("/{id}/deduct", async (
            string id,
            [FromBody] InventoryCommands.V1.DeductStock command,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID in URL does not match ID in request body");

            var result = await inventoryService.DeductStockAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("DeductStock")
        .WithSummary("Deduct stock for treatment usage")
        .Produces<InventoryResources.V1.InventoryItem>()
        .ProducesValidationProblem()
        .ProducesProblem(400)
        .ProducesProblem(404);

        // Deduct stock by units (e.g., 0.5 syringes)
        group.MapPatch("/{id}/deduct-units", async (
            string id,
            [FromBody] InventoryCommands.V1.DeductStockByUnits command,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            if (id != command.Id)
                return Results.BadRequest("ID in URL does not match ID in request body");

            var result = await inventoryService.DeductStockByUnitsAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("DeductStockByUnits")
        .WithSummary("Deduct stock by units (e.g., 0.5 syringes)")
        .Produces<InventoryResources.V1.InventoryItem>()
        .ProducesValidationProblem()
        .ProducesProblem(400)
        .ProducesProblem(404);

        // Archive inventory item
        group.MapDelete("/{id}", async (
            string id,
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var command = new InventoryCommands.V1.ArchiveInventoryItem(id);
            await inventoryService.ArchiveInventoryItemAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("ArchiveInventoryItem")
        .WithSummary("Archive inventory item")
        .Produces(204)
        .ProducesProblem(404);

        // Get categories
        group.MapGet("/categories", async (
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var result = await inventoryService.GetCategoriesAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetInventoryCategories")
        .WithSummary("Get all inventory categories")
        .Produces<InventoryQueries.V1.GetCategories.Response>();

        // Get unit types
        group.MapGet("/unit-types", async (
            IInventoryService inventoryService,
            CancellationToken cancellationToken) =>
        {
            var result = await inventoryService.GetUnitTypesAsync(cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetInventoryUnitTypes")
        .WithSummary("Get all inventory unit types with typical sizes")
        .Produces<InventoryQueries.V1.GetUnitTypes.Response>();

        return app;
    }
} 