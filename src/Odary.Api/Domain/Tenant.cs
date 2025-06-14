using System.ComponentModel.DataAnnotations;
using Odary.Api.Common.Interfaces;

namespace Odary.Api.Domain;

public class Tenant : BaseEntity, IAuditable
{
    [MaxLength(255)]
    public string Name { get; set; }
    
    [MaxLength(100)]
    public string Country { get; set; }
    
    [MaxLength(100)]
    public string Timezone { get; set; }
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    [MaxLength(50)]
    public string Slug { get; set; }
    
    public bool IsActive { get; set; }

    // Navigation properties
    public virtual ICollection<User> Users { get; private set; } = new List<User>();

    public virtual TenantSettings? Settings { get; private set; }
    
    public Tenant(string name, string country, string timezone, string slug, string? logoUrl = null)
    {
        Name = name;
        Country = country;
        Timezone = timezone;
        Slug = slug;
        LogoUrl = logoUrl;
    }

    /// <summary>
    /// Gets the properties that should be included in audit logs
    /// </summary>
    public Dictionary<string, object?> GetAuditableProperties()
    {
        return new Dictionary<string, object?>
        {
            [nameof(Name)] = Name,
            [nameof(Country)] = Country,
            [nameof(Timezone)] = Timezone,
            [nameof(Slug)] = Slug,
            [nameof(LogoUrl)] = LogoUrl,
            [nameof(IsActive)] = IsActive
        };
    }
} 