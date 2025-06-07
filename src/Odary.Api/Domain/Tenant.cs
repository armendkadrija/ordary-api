using System.ComponentModel.DataAnnotations;
using Odary.Api.Common.Interfaces;

namespace Odary.Api.Domain;

public class Tenant : BaseEntity, IAuditable
{
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Timezone { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    public bool IsActive { get; set; }

    // Navigation properties
    public virtual ICollection<User> Users { get; private set; } = new List<User>();

    public virtual TenantSettings? Settings { get; private set; }

    // Parameterless constructor for EF Core
    private Tenant() { }

    public Tenant(string name, string country, string timezone, string? logoUrl = null)
    {
        Name = name;
        Country = country;
        Timezone = timezone;
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
            [nameof(LogoUrl)] = LogoUrl,
            [nameof(IsActive)] = IsActive
        };
    }
} 