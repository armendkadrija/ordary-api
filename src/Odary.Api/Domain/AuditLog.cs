using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public class AuditLog : BaseEntity
{
    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string EntityId { get; set; } = string.Empty;
    
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    
    [MaxLength(255)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Navigation properties
    public virtual User User { get; private set; } = null!;

    public AuditLog() { }

    public AuditLog(string userId, string action, string entityType, string entityId, 
        string? oldValues = null, string? newValues = null, string? ipAddress = null, string? userAgent = null)
    {
        UserId = userId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        OldValues = oldValues;
        NewValues = newValues;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
} 