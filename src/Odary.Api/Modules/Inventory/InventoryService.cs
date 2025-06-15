using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;
using Odary.Api.Domain;
using Odary.Api.Domain.Enums;
using Odary.Api.Infrastructure.Database;

namespace Odary.Api.Modules.Inventory;

public interface IInventoryService
{
    Task<InventoryResources.V1.CreateInventoryItemResponse> CreateInventoryItemAsync(InventoryCommands.V1.CreateInventoryItem command, CancellationToken cancellationToken);
    Task<InventoryQueries.V1.GetInventoryItem.Response> GetInventoryItemAsync(InventoryQueries.V1.GetInventoryItem query, CancellationToken cancellationToken);
    Task<InventoryQueries.V1.GetInventoryItems.Response> GetInventoryItemsAsync(InventoryQueries.V1.GetInventoryItems query, CancellationToken cancellationToken);
    Task<InventoryResources.V1.InventoryItem> UpdateInventoryItemAsync(InventoryCommands.V1.UpdateInventoryItem command, CancellationToken cancellationToken);
    Task<InventoryResources.V1.InventoryItem> UpdateStockAsync(InventoryCommands.V1.UpdateStock command, CancellationToken cancellationToken);
    Task<InventoryResources.V1.InventoryItem> DeductStockAsync(InventoryCommands.V1.DeductStock command, CancellationToken cancellationToken);
    Task<InventoryResources.V1.InventoryItem> DeductStockByUnitsAsync(InventoryCommands.V1.DeductStockByUnits command, CancellationToken cancellationToken);
    Task ArchiveInventoryItemAsync(InventoryCommands.V1.ArchiveInventoryItem command, CancellationToken cancellationToken);
    Task<InventoryQueries.V1.GetCategories.Response> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<InventoryQueries.V1.GetUnitTypes.Response> GetUnitTypesAsync(CancellationToken cancellationToken);
}

public class InventoryService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    ILogger<InventoryService> logger,
    ICurrentUserService currentUserService) : BaseService(currentUserService), IInventoryService
{
    public async Task<InventoryResources.V1.CreateInventoryItemResponse> CreateInventoryItemAsync(
        InventoryCommands.V1.CreateInventoryItem command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Check for duplicate name within the same tenant and category
        var existingItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.TenantId == CurrentUser.TenantId && 
                                     i.Name == command.Name && 
                                     i.Category == command.Category && 
                                     !i.IsArchived, cancellationToken);

        if (existingItem != null)
            throw new BusinessException($"An inventory item with name '{command.Name}' already exists in category '{InventoryCategoryConstants.GetDisplayName(command.Category)}'");

        var inventoryItem = new InventoryItem(
            CurrentUser.TenantId ?? throw new InvalidOperationException("User must belong to a tenant"),
            command.Name,
            command.Category,
            command.UnitType,
            command.UnitSize,
            command.Quantity,
            command.MinThreshold,
            command.ExpiryDate,
            command.BatchNumber);

        dbContext.InventoryItems.Add(inventoryItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inventory item created successfully with ID: {InventoryItemId} in Tenant: {TenantId}", 
            inventoryItem.Id, CurrentUser.TenantId);

        return inventoryItem.ToCreateResponse();
    }

    public async Task<InventoryQueries.V1.GetInventoryItem.Response> GetInventoryItemAsync(
        InventoryQueries.V1.GetInventoryItem query,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == query.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {query.Id} not found");

        return inventoryItem.ToGetInventoryItemResponse();
    }

    public async Task<InventoryQueries.V1.GetInventoryItems.Response> GetInventoryItemsAsync(
        InventoryQueries.V1.GetInventoryItems query,
        CancellationToken cancellationToken)
    {
        var inventoryQuery = dbContext.InventoryItems
            .Where(i => i.TenantId == CurrentUser.TenantId);

        // Apply filters
        if (!query.IncludeArchived.HasValue)
            inventoryQuery = inventoryQuery.Where(i => !i.IsArchived);

        if (query.Category.HasValue)
            inventoryQuery = inventoryQuery.Where(i => i.Category == query.Category.Value);

        if (query.LowStock.HasValue && query.LowStock.Value)
            inventoryQuery = inventoryQuery.Where(i => i.Quantity <= i.MinThreshold);

        if (query.ExpiringSoon.HasValue && query.ExpiringSoon.Value)
        {
            var thirtyDaysFromNow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
            inventoryQuery = inventoryQuery.Where(i => i.ExpiryDate.HasValue && i.ExpiryDate.Value <= thirtyDaysFromNow);
        }

        var totalCount = await inventoryQuery.CountAsync(cancellationToken);

        var inventoryItems = await inventoryQuery
            .OrderBy(i => i.Name)
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return new InventoryQueries.V1.GetInventoryItems.Response(
            inventoryItems.Select(i => i.ToContract()).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    public async Task<InventoryResources.V1.InventoryItem> UpdateInventoryItemAsync(
        InventoryCommands.V1.UpdateInventoryItem command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == command.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {command.Id} not found");

        // Check for duplicate name within the same tenant and category (excluding current item)
        var existingItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.TenantId == CurrentUser.TenantId && 
                                     i.Name == command.Name && 
                                     i.Category == command.Category && 
                                     i.Id != command.Id && 
                                     !i.IsArchived, cancellationToken);

        if (existingItem != null)
            throw new BusinessException($"An inventory item with name '{command.Name}' already exists in category '{InventoryCategoryConstants.GetDisplayName(command.Category)}'");

        // Update properties
        inventoryItem.Name = command.Name;
        inventoryItem.Category = command.Category;
        inventoryItem.UnitType = command.UnitType;
        inventoryItem.UnitSize = command.UnitSize;
        inventoryItem.Quantity = command.Quantity;
        inventoryItem.MinThreshold = command.MinThreshold;
        inventoryItem.ExpiryDate = command.ExpiryDate;
        inventoryItem.BatchNumber = command.BatchNumber;
        inventoryItem.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inventory item updated successfully with ID: {InventoryItemId}", inventoryItem.Id);
        return inventoryItem.ToContract();
    }

    public async Task<InventoryResources.V1.InventoryItem> UpdateStockAsync(
        InventoryCommands.V1.UpdateStock command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == command.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {command.Id} not found");

        var oldQuantity = inventoryItem.Quantity;
        inventoryItem.UpdateStock(command.NewQuantity, command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stock updated for inventory item {InventoryItemId}: {OldQuantity} -> {NewQuantity}. Reason: {Reason}", 
            inventoryItem.Id, oldQuantity, command.NewQuantity, command.Reason);

        return inventoryItem.ToContract();
    }

    public async Task<InventoryResources.V1.InventoryItem> DeductStockAsync(
        InventoryCommands.V1.DeductStock command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == command.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {command.Id} not found");

        try
        {
            inventoryItem.DeductStock(command.QuantityUsed);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Stock deducted for inventory item {InventoryItemId}: -{QuantityUsed}. New quantity: {NewQuantity}", 
                inventoryItem.Id, command.QuantityUsed, inventoryItem.Quantity);

            return inventoryItem.ToContract();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    public async Task<InventoryResources.V1.InventoryItem> DeductStockByUnitsAsync(
        InventoryCommands.V1.DeductStockByUnits command,
        CancellationToken cancellationToken)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == command.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {command.Id} not found");

        try
        {
            inventoryItem.DeductStockByUnits(command.UnitsUsed);
            await dbContext.SaveChangesAsync(cancellationToken);

            var quantityDeducted = command.UnitsUsed * inventoryItem.UnitSize;
            logger.LogInformation("Stock deducted by units for inventory item {InventoryItemId}: -{UnitsUsed} units ({QuantityDeducted} base units). New quantity: {NewQuantity}", 
                inventoryItem.Id, command.UnitsUsed, quantityDeducted, inventoryItem.Quantity);

            return inventoryItem.ToContract();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessException(ex.Message);
        }
    }

    public async Task ArchiveInventoryItemAsync(
        InventoryCommands.V1.ArchiveInventoryItem command,
        CancellationToken cancellationToken)
    {
        var inventoryItem = await dbContext.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == command.Id && i.TenantId == CurrentUser.TenantId, cancellationToken);

        if (inventoryItem == null)
            throw new NotFoundException($"Inventory item with ID {command.Id} not found");

        inventoryItem.IsArchived = true;
        inventoryItem.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Inventory item archived successfully with ID: {InventoryItemId}", inventoryItem.Id);
    }

    public Task<InventoryQueries.V1.GetCategories.Response> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = InventoryCategoryConstants.GetAll()
            .Select(c => c.ToCategoryInfo())
            .ToList();

        return Task.FromResult(new InventoryQueries.V1.GetCategories.Response(categories));
    }

    public Task<InventoryQueries.V1.GetUnitTypes.Response> GetUnitTypesAsync(CancellationToken cancellationToken)
    {
        var unitTypes = InventoryUnitTypeConstants.GetAll()
            .Select(ut => ut.ToUnitTypeInfo())
            .ToList();

        return Task.FromResult(new InventoryQueries.V1.GetUnitTypes.Response(unitTypes));
    }
} 