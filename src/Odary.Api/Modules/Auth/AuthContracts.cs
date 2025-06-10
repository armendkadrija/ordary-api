namespace Odary.Api.Modules.Auth;

public class AuthQueries
{
    public class V1 
    {
        public class GetCurrentUser
        {
            public record Response
            {
                public string Id { get; init; } = string.Empty;
                public string Email { get; init; } = string.Empty;
                public string FirstName { get; init; } = string.Empty;
                public string LastName { get; init; } = string.Empty;
                public string Role { get; init; } = string.Empty;
                public DateTime? LastLoginAt { get; init; }
            }
        }


    }
}

public class AuthCommands
{
    public class V1
    {
        public record SignIn(string Email, string Password, bool RememberMe = false);
        public record RefreshToken(string Token);
        public record ForgotPassword(string Email);
        public record ResetPassword(string Token, string NewPassword);
        public record ChangePassword(string CurrentPassword, string NewPassword);

    }
}

public class AuthResources
{
    public class V1
    {
        public record TokenResponse
        {
            public string AccessToken { get; init; } = string.Empty;
            public string RefreshToken { get; init; } = string.Empty;
            public DateTime ExpiresAt { get; init; }
            public string TokenType { get; init; } = "Bearer";
            public UserProfile User { get; init; } = null!;
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


    }
} 