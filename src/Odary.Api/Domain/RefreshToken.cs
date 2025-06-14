using System.ComponentModel.DataAnnotations;

namespace Odary.Api.Domain;

public class RefreshToken : BaseEntity
{
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(50)]
    public string UserId { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(255)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Navigation property
    public virtual User User { get; private set; } = null!;

    public RefreshToken() { }

    public RefreshToken(string token, string userId, DateTime expiresAt, string? ipAddress = null, string? userAgent = null)
    {
        Token = token;
        UserId = userId;
        ExpiresAt = expiresAt;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
} 