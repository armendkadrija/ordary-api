namespace Odary.Api.Modules.User;

public static class UserMappings
{
    public static UserResources.V1.User ToContract(this Domain.User user)
    {
        return new UserResources.V1.User
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public static UserQueries.V1.GetUser.Response ToGetUserResponse(this Domain.User user)
    {
        return new UserQueries.V1.GetUser.Response
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public static UserResources.V1.UserProfile ToUserProfile(this Domain.User user)
    {
        return new UserResources.V1.UserProfile
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