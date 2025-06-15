using Odary.Api.Domain;
using Odary.Api.Domain.Enums;

namespace Odary.Api.Modules.Inventory;

public static class InventoryMappings
{
    public static InventoryResources.V1.InventoryItem ToContract(this InventoryItem inventoryItem)
    {
        return new InventoryResources.V1.InventoryItem
        {
            Id = inventoryItem.Id,
            Name = inventoryItem.Name,
            Category = inventoryItem.Category,
            CategoryDisplayName = inventoryItem.CategoryDisplayName,
            UnitType = inventoryItem.UnitType,
            UnitTypeDisplayName = inventoryItem.UnitTypeDisplayName,
            UnitSize = inventoryItem.UnitSize,
            Quantity = inventoryItem.Quantity,
            MinThreshold = inventoryItem.MinThreshold,
            ExpiryDate = inventoryItem.ExpiryDate,
            BatchNumber = inventoryItem.BatchNumber,
            IsArchived = inventoryItem.IsArchived,
            IsLowStock = inventoryItem.IsLowStock,
            IsExpired = inventoryItem.IsExpired,
            IsExpiringSoon = inventoryItem.IsExpiringSoon,
            CompleteUnitsAvailable = inventoryItem.CompleteUnitsAvailable,
            PartialUnitRemaining = inventoryItem.PartialUnitRemaining,
            CreatedAt = inventoryItem.CreatedAt,
            UpdatedAt = inventoryItem.UpdatedAt ?? inventoryItem.CreatedAt
        };
    }

    public static InventoryResources.V1.CreateInventoryItemResponse ToCreateResponse(this InventoryItem inventoryItem)
    {
        return new InventoryResources.V1.CreateInventoryItemResponse
        {
            Id = inventoryItem.Id,
            Name = inventoryItem.Name,
            Category = inventoryItem.Category,
            CategoryDisplayName = inventoryItem.CategoryDisplayName,
            UnitType = inventoryItem.UnitType,
            UnitTypeDisplayName = inventoryItem.UnitTypeDisplayName,
            UnitSize = inventoryItem.UnitSize,
            Quantity = inventoryItem.Quantity,
            MinThreshold = inventoryItem.MinThreshold,
            ExpiryDate = inventoryItem.ExpiryDate,
            BatchNumber = inventoryItem.BatchNumber,
            IsLowStock = inventoryItem.IsLowStock,
            IsExpired = inventoryItem.IsExpired,
            IsExpiringSoon = inventoryItem.IsExpiringSoon,
            CompleteUnitsAvailable = inventoryItem.CompleteUnitsAvailable,
            PartialUnitRemaining = inventoryItem.PartialUnitRemaining,
            CreatedAt = inventoryItem.CreatedAt,
            UpdatedAt = inventoryItem.UpdatedAt ?? inventoryItem.CreatedAt
        };
    }

    public static InventoryQueries.V1.GetInventoryItem.Response ToGetInventoryItemResponse(this InventoryItem inventoryItem)
    {
        return new InventoryQueries.V1.GetInventoryItem.Response(inventoryItem.ToContract());
    }

    public static InventoryResources.V1.CategoryInfo ToCategoryInfo(this InventoryCategory category)
    {
        return new InventoryResources.V1.CategoryInfo
        {
            Value = category,
            DisplayName = InventoryCategoryConstants.GetDisplayName(category)
        };
    }

    public static InventoryResources.V1.UnitTypeInfo ToUnitTypeInfo(this InventoryUnitType unitType)
    {
        return new InventoryResources.V1.UnitTypeInfo
        {
            Value = unitType,
            DisplayName = InventoryUnitTypeConstants.GetDisplayName(unitType)
        };
    }
} 