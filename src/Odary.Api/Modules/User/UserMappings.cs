using Odary.Api.Domain;

namespace Odary.Api.Modules.User;

public static class UserMappings
{
    public static UserResources.V1.User ToContract(this Domain.User user)
    {
        return new UserResources.V1.User
        {
            Id = user.Id,
            Email = user.Email,
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
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
} 