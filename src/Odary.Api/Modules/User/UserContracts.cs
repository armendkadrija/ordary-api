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
                public string FirstName { get; init; } = string.Empty;
                public string LastName { get; init; } = string.Empty;
                public string Role { get; init; } = string.Empty;
                public bool IsActive { get; init; }
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
        public record CreateUser(string Email, string TenantId, string Role);
        public record InviteUser(string Email, string FirstName, string LastName, string Role, string TenantId);
        public record OnboardUser(string Email, string Token, string FirstName, string LastName, string Password);
        public record UpdateEmail(string Id, string Email);
        public record DeleteUser(string Id);
        public record UpdateUserProfile(string Id, string FirstName, string LastName, string Role, bool IsActive);
        public record LockUser(string Id);
        public record UnlockUser(string Id);
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
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string Role { get; init; } = string.Empty;
            public bool IsActive { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }

        public record CreateUserResponse
        {
            public string Id { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string Role { get; init; } = string.Empty;
            public bool IsActive { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
            public string GeneratedPassword { get; init; } = string.Empty;
        }

        public record UserProfile
        {
            public string Id { get; init; } = string.Empty;
            public string Email { get; init; } = string.Empty;
            public string FirstName { get; init; } = string.Empty;
            public string LastName { get; init; } = string.Empty;
            public string Role { get; init; } = string.Empty;
            public bool IsActive { get; init; }
            public DateTime? LastLoginAt { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
        }

        public record OnboardingResponse
        {
            public bool Success { get; init; } = true;
            public string Message { get; init; } = "User onboarding completed successfully";
        }
    }
} 