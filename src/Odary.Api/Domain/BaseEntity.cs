using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public abstract class BaseEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset CreatedAt { get; set; }
    
    public DateTimeOffset? UpdatedAt { get; set; }
} 