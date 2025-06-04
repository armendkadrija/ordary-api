using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public class TenantSettings : BaseEntity
{
    [MaxLength(50)]
    public string TenantId { get; set; } = string.Empty;
    
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;
    
    [MaxLength(10)]
    public string Currency { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string DateFormat { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string TimeFormat { get; set; } = string.Empty;

    // Navigation property
    public virtual Tenant Tenant { get; private set; } = null!;

    // Parameterless constructor for EF Core
    private TenantSettings() { }

    public TenantSettings(string tenantId, string language, string currency, string dateFormat, string timeFormat)
    {
        TenantId = tenantId;
        Language = language;
        Currency = currency;
        DateFormat = dateFormat;
        TimeFormat = timeFormat;
    }
} 