using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Odary.Api.Common.Interfaces;

namespace Odary.Api.Domain;

public class User : IdentityUser, IAuditable
{
    [MaxLength(50)]
    public string? TenantId { get; set; } // Nullable for BusinessAdmin users

    [MaxLength(255)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }

    // Password history for policy enforcement
    public List<string> PasswordHistory { get; set; } = [];

    // Navigation property
    public virtual Tenant Tenant { get; private set; } = null!;

    // Parameterless constructor for EF Core
    public User() { }

    public User(string? tenantId, string email, string firstName, string lastName, string role)
    {
        TenantId = tenantId;
        Email = email;
        UserName = email; // Identity requires UserName
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Dictionary<string, object?> GetAuditableProperties()
    {
        return new Dictionary<string, object?>
        {
            [nameof(Email)] = Email,
            [nameof(FirstName)] = FirstName,
            [nameof(LastName)] = LastName,
            [nameof(Role)] = Role,
            [nameof(IsActive)] = IsActive,
            [nameof(FailedLoginAttempts)] = FailedLoginAttempts,
            [nameof(LockedUntil)] = LockedUntil
        };
    }
}