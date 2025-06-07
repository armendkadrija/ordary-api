namespace Odary.Api.Modules.Auth;

public static class AuthMappings
{
    public static AuthResources.V1.UserProfile ToUserProfile(this Domain.User user)
    {
        return new AuthResources.V1.UserProfile
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }
} 