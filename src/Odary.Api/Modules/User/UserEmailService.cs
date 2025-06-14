using System.Web;
using Microsoft.Extensions.Options;
using Odary.Api.Infrastructure.Email;

namespace Odary.Api.Modules.User;

public interface IUserEmailService
{
    Task SendUserInvitationEmailAsync(string email, string firstName, string lastName, string invitationToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}

public class UserEmailService(IEmailService emailService, IOptions<EmailSettings> emailOptions) : IUserEmailService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task SendUserInvitationEmailAsync(string email, string firstName, string lastName, string invitationToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to Odary - Complete Your Account Setup";
        var toName = $"{firstName} {lastName}".Trim();
        
        // URL encode the email and token for safe inclusion in URL
        var encodedEmail = HttpUtility.UrlEncode(email);
        var encodedToken = HttpUtility.UrlEncode(invitationToken);
        
        // Build the invitation link with email and token as parameters
        var invitationLink = $"{_emailSettings.FrontendUrl}/accept-invitation?email={encodedEmail}&token={encodedToken}";
        
        var model = new
        {
            FirstName = firstName,
            LastName = lastName,
            InvitationLink = invitationLink,
            ExpiresAt = expiresAt.DateTime
        };
        
        await emailService.SendTemplateEmailAsync(email, subject, "user-invitation", model, toName, cancellationToken);
    }
} 