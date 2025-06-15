using Odary.Api.Domain.Enums;

namespace Odary.Api.Modules.Inventory;

public static class InventoryCommands
{
    public static class V1
    {
        public record CreateInventoryItem(
            string Name,
            InventoryCategory Category,
            InventoryUnitType UnitType,
            decimal UnitSize,
            decimal Quantity,
            decimal MinThreshold,
            DateOnly? ExpiryDate = null,
            string? BatchNumber = null);

        public record UpdateInventoryItem(
            string Id,
            string Name,
            InventoryCategory Category,
            InventoryUnitType UnitType,
            decimal UnitSize,
            decimal Quantity,
            decimal MinThreshold,
            DateOnly? ExpiryDate = null,
            string? BatchNumber = null);

        public record UpdateStock(
            string Id,
            decimal NewQuantity,
            string Reason);

        public record DeductStock(
            string Id,
            decimal QuantityUsed);

        public record DeductStockByUnits(
            string Id,
            decimal UnitsUsed);

        public record ArchiveInventoryItem(string Id);
    }
}

public static class InventoryQueries
{
    public static class V1
    {
        public record GetInventoryItem(string Id)
        {
            public record Response(InventoryResources.V1.InventoryItem InventoryItem);
        }

        public record GetInventoryItems(
            int Page = 1,
            int PageSize = 10,
            InventoryCategory? Category = null,
            bool? LowStock = null,
            bool? ExpiringSoon = null,
            bool? IncludeArchived = false)
        {
            public int Skip => (Page - 1) * PageSize;
            public int Take => PageSize;

            public record Response(
                List<InventoryResources.V1.InventoryItem> Items,
                int TotalCount,
                int Page,
                int PageSize);
        }

        public record GetCategories
        {
            public record Response(List<InventoryResources.V1.CategoryInfo> Categories);
        }

        public record GetUnitTypes
        {
            public record Response(List<InventoryResources.V1.UnitTypeInfo> UnitTypes);
        }
    }
}

public static class InventoryResources
{
    public static class V1
    {
        public record InventoryItem
        {
            public required string Id { get; init; }
            public required string Name { get; init; }
            public required InventoryCategory Category { get; init; }
            public required string CategoryDisplayName { get; init; }
            public required InventoryUnitType UnitType { get; init; }
            public required string UnitTypeDisplayName { get; init; }
            public required decimal UnitSize { get; init; }
            public required decimal Quantity { get; init; }
            public required decimal MinThreshold { get; init; }
            public DateOnly? ExpiryDate { get; init; }
            public string? BatchNumber { get; init; }
            public required bool IsArchived { get; init; }
            public required bool IsLowStock { get; init; }
            public required bool IsExpired { get; init; }
            public required bool IsExpiringSoon { get; init; }
            public required decimal CompleteUnitsAvailable { get; init; }
            public required decimal PartialUnitRemaining { get; init; }
            public required DateTimeOffset CreatedAt { get; init; }
            public required DateTimeOffset UpdatedAt { get; init; }
        }

        public record CreateInventoryItemResponse
        {
            public required string Id { get; init; }
            public required string Name { get; init; }
            public required InventoryCategory Category { get; init; }
            public required string CategoryDisplayName { get; init; }
            public required InventoryUnitType UnitType { get; init; }
            public required string UnitTypeDisplayName { get; init; }
            public required decimal UnitSize { get; init; }
            public required decimal Quantity { get; init; }
            public required decimal MinThreshold { get; init; }
            public DateOnly? ExpiryDate { get; init; }
            public string? BatchNumber { get; init; }
            public required bool IsLowStock { get; init; }
            public required bool IsExpired { get; init; }
            public required bool IsExpiringSoon { get; init; }
            public required decimal CompleteUnitsAvailable { get; init; }
            public required decimal PartialUnitRemaining { get; init; }
            public required DateTimeOffset CreatedAt { get; init; }
            public required DateTimeOffset UpdatedAt { get; init; }
        }

        public record CategoryInfo
        {
            public required InventoryCategory Value { get; init; }
            public required string DisplayName { get; init; }
        }

        public record UnitTypeInfo
        {
            public required InventoryUnitType Value { get; init; }
            public required string DisplayName { get; init; }
        }
    }
} 