using Odary.Api.Modules.User;

namespace Odary.Api.Tests.Mocks;

public class MockUserEmailService : IUserEmailService
{
    public List<InvitationEmail> SentInvitations { get; } = new();
    public List<OnboardingEmail> SentOnboardingEmails { get; } = new();

    public Task SendUserInvitationEmailAsync(string email, string firstName, string lastName, string invitationToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        SentInvitations.Add(new InvitationEmail
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            InvitationToken = invitationToken,
            ExpiresAt = expiresAt
        });
        
        return Task.CompletedTask;
    }

    public Task SendOnboardingCompleteEmailAsync(string email, string firstName, string lastName, string role, CancellationToken cancellationToken = default)
    {
        SentOnboardingEmails.Add(new OnboardingEmail
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role
        });
        
        return Task.CompletedTask;
    }

    public void Clear()
    {
        SentInvitations.Clear();
        SentOnboardingEmails.Clear();
    }
}

public class InvitationEmail
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string InvitationToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

public class OnboardingEmail
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
} 