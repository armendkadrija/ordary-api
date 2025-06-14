using Odary.Api.Modules.Auth;

namespace Odary.Api.Tests.Mocks;

public class MockAuthEmailService : IAuthEmailService
{
    public List<PasswordResetEmail> SentPasswordResetEmails { get; } = new();

    public Task SendPasswordResetEmailAsync(string email, string firstName, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        SentPasswordResetEmails.Add(new PasswordResetEmail
        {
            Email = email,
            FirstName = firstName,
            ResetToken = resetToken,
            ExpiresAt = expiresAt
        });
        
        return Task.CompletedTask;
    }

    public void Clear()
    {
        SentPasswordResetEmails.Clear();
    }
}

public class PasswordResetEmail
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string ResetToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
} 