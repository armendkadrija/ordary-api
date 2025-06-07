using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Odary.Api.Domain;

public class Role : IdentityRole
{
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public Role() { }

    public Role(string name, string description)
    {
        Id = Guid.NewGuid().ToString("N");
        Name = name;
        NormalizedName = name.ToUpperInvariant();
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
    }
} 