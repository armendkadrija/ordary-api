namespace Odary.Api.Common.Interfaces;

/// <summary>
/// Marker interface for entities that should be automatically audited
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// The entity ID for audit logging
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Gets the entity type name for audit logging
    /// </summary>
    string GetEntityType() => GetType().Name;
    
    /// <summary>
    /// Gets the properties that should be included in audit logs (for before/after comparison)
    /// </summary>
    Dictionary<string, object?> GetAuditableProperties();
} 