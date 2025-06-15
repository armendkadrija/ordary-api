namespace Odary.Api.Domain.Enums;

/// <summary>
/// Categories of dental materials and supplies
/// </summary>
public enum InventoryCategory
{
    Composite,          // Composite resins (Filtek Z250, etc.)
    GIC,               // Glass Ionomer Cement
    Anesthetic,        // Local anesthetics (Lidocaine, etc.)
    Impression,        // Impression materials (Alginate, PVS)
    Cement,            // Dental cements (Zinc phosphate, etc.)
    Endodontic,        // Root canal materials (Gutta percha, sealers)
    Periodontal,       // Periodontal treatment materials
    Orthodontic,       // Brackets, wires, elastics
    Prosthetic,        // Crown and bridge materials
    Surgical,          // Surgical instruments and materials
    Preventive,        // Fluoride, sealants
    Whitening,         // Bleaching agents
    Adhesive,          // Bonding agents
    Consumables,       // Gloves, masks, disposables
    Equipment,         // Small instruments and tools
    Other              // Miscellaneous items
}

/// <summary>
/// Unit types for measuring dental materials
/// </summary>
public enum InventoryUnitType
{
    Syringe,           // Per syringe (composites, cements)
    Carpule,           // Per carpule (anesthetics)
    Gram,              // Per gram (powders, pastes)
    Milliliter,        // Per milliliter (liquids)
    Piece,             // Per piece (instruments, brackets)
    Tube,              // Per tube (gels, pastes)
    Bottle,            // Per bottle (solutions)
    Pack,              // Per pack/package
    Roll,              // Per roll (cotton, gauze)
    Sheet,             // Per sheet (barriers, films)
    Capsule,           // Per capsule (GIC capsules)
    Tip,               // Per tip (mixing tips, irrigation tips)
    Needle,            // Per needle
    Blade,             // Per blade (scalpel blades)
    File,              // Per file (endodontic files)
    Bur,               // Per bur (dental burs)
    Other              // Other units
}

/// <summary>
/// Constants for inventory categories with display names
/// </summary>
public static class InventoryCategoryConstants
{
    public static readonly Dictionary<InventoryCategory, string> DisplayNames = new()
    {
        { InventoryCategory.Composite, "Composite Resins" },
        { InventoryCategory.GIC, "Glass Ionomer Cement" },
        { InventoryCategory.Anesthetic, "Local Anesthetics" },
        { InventoryCategory.Impression, "Impression Materials" },
        { InventoryCategory.Cement, "Dental Cements" },
        { InventoryCategory.Endodontic, "Endodontic Materials" },
        { InventoryCategory.Periodontal, "Periodontal Materials" },
        { InventoryCategory.Orthodontic, "Orthodontic Materials" },
        { InventoryCategory.Prosthetic, "Prosthetic Materials" },
        { InventoryCategory.Surgical, "Surgical Materials" },
        { InventoryCategory.Preventive, "Preventive Materials" },
        { InventoryCategory.Whitening, "Whitening Materials" },
        { InventoryCategory.Adhesive, "Adhesive Materials" },
        { InventoryCategory.Consumables, "Consumables" },
        { InventoryCategory.Equipment, "Equipment & Tools" },
        { InventoryCategory.Other, "Other" }
    };

    public static string GetDisplayName(InventoryCategory category) => DisplayNames[category];
    
    public static InventoryCategory[] GetAll() => Enum.GetValues<InventoryCategory>();
}

/// <summary>
/// Constants for inventory unit types with display names
/// </summary>
public static class InventoryUnitTypeConstants
{
    public static readonly Dictionary<InventoryUnitType, string> DisplayNames = new()
    {
        { InventoryUnitType.Syringe, "Syringe" },
        { InventoryUnitType.Carpule, "Carpule" },
        { InventoryUnitType.Gram, "Gram" },
        { InventoryUnitType.Milliliter, "Milliliter" },
        { InventoryUnitType.Piece, "Piece" },
        { InventoryUnitType.Tube, "Tube" },
        { InventoryUnitType.Bottle, "Bottle" },
        { InventoryUnitType.Pack, "Pack" },
        { InventoryUnitType.Roll, "Roll" },
        { InventoryUnitType.Sheet, "Sheet" },
        { InventoryUnitType.Capsule, "Capsule" },
        { InventoryUnitType.Tip, "Tip" },
        { InventoryUnitType.Needle, "Needle" },
        { InventoryUnitType.Blade, "Blade" },
        { InventoryUnitType.File, "File" },
        { InventoryUnitType.Bur, "Bur" },
        { InventoryUnitType.Other, "Other" }
    };

    public static string GetDisplayName(InventoryUnitType unitType) => DisplayNames[unitType];
    
    public static InventoryUnitType[] GetAll() => Enum.GetValues<InventoryUnitType>();
} 