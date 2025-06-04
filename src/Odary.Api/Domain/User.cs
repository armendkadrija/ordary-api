using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public class User : BaseEntity
{
    [MaxLength(50)]
    public string TenantId { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string FirstName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string LastName { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string Role { get; set; } = string.Empty;
    
    public bool IsActive { get; set; }

    // Navigation property
    public virtual Tenant Tenant { get; private set; } = null!;

    // Parameterless constructor for EF Core
    private User() { }

    public User(string tenantId, string email, string passwordHash, string firstName, string lastName, string role)
    {
        TenantId = tenantId;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
    }
} 