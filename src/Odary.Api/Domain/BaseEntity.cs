using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public abstract class BaseEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; protected set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; }
    
    public DateTimeOffset? UpdatedAt { get; set; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid().ToString("N");
    }
} 