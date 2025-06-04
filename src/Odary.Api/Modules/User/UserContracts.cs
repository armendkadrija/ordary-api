using Odary.Api.Common.Pagination;

namespace Odary.Api.Modules.User;

public class UserQueries
{
    public class V1
    {
        public record GetUser(string Id)
        {
            public record Response
            {
                public string Id { get; init; } = string.Empty;
                public string Email { get; init; } = string.Empty;
                public DateTimeOffset CreatedAt { get; init; }
                public DateTimeOffset? UpdatedAt { get; init; }
            }
        }

        public class GetUsers : PaginatedRequest
        {
            public string? Email { get; set; }

            public class Response : PaginatedResponse<UserResources.V1.User>;
        }
    }
}

public class UserCommands
{
    public class V1
    {
        public record CreateUser(string Email, string Password);
        public record UpdateUser(string Id, string Email);
        public record UpdatePassword(string Id, string CurrentPassword, string NewPassword);
        public record DeleteUser(string Id);
    }
}

public class UserResources
{
    public class V1
    {
        public record User
        {
            public string Id { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }
    }
} 