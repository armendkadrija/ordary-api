using System.Web;
using Microsoft.Extensions.Options;
using Odary.Api.Infrastructure.Email;

namespace Odary.Api.Modules.Auth;

public interface IAuthEmailService
{
    Task SendPasswordResetEmailAsync(string email, string firstName, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}

public class AuthEmailService(IEmailService emailService, IOptions<EmailSettings> emailOptions) : IAuthEmailService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task SendPasswordResetEmailAsync(string email, string firstName, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var subject = "Reset Your Odary Password";
        
        // URL encode the email and token for safe inclusion in URL
        var encodedEmail = HttpUtility.UrlEncode(email);
        var encodedToken = HttpUtility.UrlEncode(resetToken);
        
        // Build the reset password link with email and token as separate parameters
        var resetLink = $"{_emailSettings.FrontendUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
        
        var model = new
        {
            FirstName = firstName,
            ResetLink = resetLink,
            ExpiresAt = expiresAt.DateTime
        };
        
        await emailService.SendTemplateEmailAsync(email, subject, "password-reset", model, firstName, cancellationToken);
    }
} 