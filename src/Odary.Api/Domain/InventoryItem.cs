using System.ComponentModel.DataAnnotations;
using Odary.Api.Domain.Enums;

namespace Odary.Api.Domain;

public class InventoryItem : BaseEntity
{
    public InventoryItem(
        string tenantId,
        string name,
        InventoryCategory category,
        InventoryUnitType unitType,
        decimal unitSize,
        decimal quantity,
        decimal minThreshold,
        DateOnly? expiryDate = null,
        string? batchNumber = null)
    {
        TenantId = tenantId;
        Name = name;
        Category = category;
        UnitType = unitType;
        UnitSize = unitSize;
        Quantity = quantity;
        MinThreshold = minThreshold;
        ExpiryDate = expiryDate;
        BatchNumber = batchNumber;
    }

    // Required for EF Core
    private InventoryItem() { }

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public InventoryCategory Category { get; set; }

    [Required]
    public InventoryUnitType UnitType { get; set; }

    /// <summary>
    /// Size of each unit (e.g., 4.0 for a 4g syringe, 1.8 for a 1.8ml carpule)
    /// This defines how much material is in one "unit" of this item
    /// </summary>
    [Range(0.01, 1000000)]
    public decimal UnitSize { get; set; }

    /// <summary>
    /// Current quantity in stock (in base units - grams, ml, pieces, etc.)
    /// This allows for fractional quantities like 2.5g remaining from a 4g syringe
    /// </summary>
    [Range(0, 1000000)]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Minimum threshold in base units to trigger low stock alert
    /// </summary>
    [Range(0, 1000000)]
    public decimal MinThreshold { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(50)]
    public string? BatchNumber { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation property
    public Tenant Tenant { get; set; } = null!;

    // Helper properties
    public bool IsLowStock => Quantity <= MinThreshold;
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow);
    public bool IsExpiringSoon => ExpiryDate.HasValue && ExpiryDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    
    /// <summary>
    /// Number of complete units available (e.g., how many full syringes)
    /// </summary>
    public decimal CompleteUnitsAvailable => Quantity / UnitSize;
    
    /// <summary>
    /// Remaining partial unit quantity (e.g., 2.5g remaining from a 4g syringe)
    /// </summary>
    public decimal PartialUnitRemaining => Quantity % UnitSize;

    /// <summary>
    /// Display name for the category
    /// </summary>
    public string CategoryDisplayName => InventoryCategoryConstants.GetDisplayName(Category);
    
    /// <summary>
    /// Display name for the unit type
    /// </summary>
    public string UnitTypeDisplayName => InventoryUnitTypeConstants.GetDisplayName(UnitType);

    // Business methods
    public void UpdateStock(decimal newQuantity, string reason)
    {
        if (newQuantity < 0)
            throw new ArgumentException("Quantity cannot be negative.", nameof(newQuantity));
            
        Quantity = newQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DeductStock(decimal quantityUsed)
    {
        if (quantityUsed <= 0)
            throw new ArgumentException("Quantity used must be positive.", nameof(quantityUsed));
            
        if (quantityUsed > Quantity)
            throw new InvalidOperationException($"Cannot deduct {quantityUsed:F2} units. Only {Quantity:F2} units available.");
        
        Quantity -= quantityUsed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
    /// <summary>
    /// Deduct stock by number of units (e.g., 0.5 syringes = 2g if syringe is 4g)
    /// </summary>
    public void DeductStockByUnits(decimal unitsUsed)
    {
        var quantityToDeduct = unitsUsed * UnitSize;
        DeductStock(quantityToDeduct);
    }
    
    /// <summary>
    /// Add stock to inventory
    /// </summary>
    public void AddStock(decimal quantityToAdd)
    {
        if (quantityToAdd <= 0)
            throw new ArgumentException("Quantity to add must be positive.", nameof(quantityToAdd));
            
        Quantity += quantityToAdd;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
} 