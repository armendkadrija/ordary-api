namespace Odary.Api.Modules.Auth;

public class AuthCommands
{
    public class V1
    {
        public record SignIn(string Email, string Password);
        public record RefreshToken(string Token);
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
        }
    }
} 