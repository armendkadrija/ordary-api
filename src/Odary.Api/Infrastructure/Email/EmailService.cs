using System.Net;
using System.Net.Mail;
using System.Web;
using Microsoft.Extensions.Options;

namespace Odary.Api.Infrastructure.Email;

public interface IEmailService
{
    Task<EmailResources.V1.EmailSent> SendEmailAsync(EmailCommands.V1.SendEmail command, CancellationToken cancellationToken = default);
    Task<EmailResources.V1.EmailSent> SendUserInvitationAsync(EmailCommands.V1.SendUserInvitation command, CancellationToken cancellationToken = default);
    Task<EmailResources.V1.EmailSent> SendPasswordResetAsync(EmailCommands.V1.SendPasswordReset command, CancellationToken cancellationToken = default);
}

public class EmailService(IOptions<EmailSettings> emailOptions, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task<EmailResources.V1.EmailSent> SendEmailAsync(EmailCommands.V1.SendEmail command, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateSmtpClient();
            using var message = CreateMailMessage(command.To, command.Subject, command.Body, command.IsHtml, command.ToName);
            
            await client.SendMailAsync(message, cancellationToken);
            
            logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", command.To, command.Subject);
            
            return new EmailResources.V1.EmailSent
            {
                To = command.To,
                Subject = command.Subject,
                SentAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To} with subject: {Subject}", command.To, command.Subject);
            throw;
        }
    }

    public async Task<EmailResources.V1.EmailSent> SendUserInvitationAsync(EmailCommands.V1.SendUserInvitation command, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to Odary - Complete Your Account Setup";
        var body = GenerateInvitationEmailBody(command.Email, command.FirstName, command.LastName, command.InvitationToken, command.ExpiresAt);
        
        var emailCommand = new EmailCommands.V1.SendEmail(
            command.Email,
            subject,
            body,
            IsHtml: true,
            ToName: $"{command.FirstName} {command.LastName}".Trim());
        
        return await SendEmailAsync(emailCommand, cancellationToken);
    }

    public async Task<EmailResources.V1.EmailSent> SendPasswordResetAsync(EmailCommands.V1.SendPasswordReset command, CancellationToken cancellationToken = default)
    {
        var subject = "Reset Your Odary Password";
        var body = GeneratePasswordResetEmailBody(command.Email, command.FirstName, command.ResetToken, command.ExpiresAt);
        
        var emailCommand = new EmailCommands.V1.SendEmail(
            command.Email,
            subject,
            body,
            IsHtml: true,
            ToName: command.FirstName);
        
        return await SendEmailAsync(emailCommand, cancellationToken);
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort);
        
        if (!string.IsNullOrEmpty(_emailSettings.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
        }
        
        client.EnableSsl = _emailSettings.SmtpPort == 587 || _emailSettings.SmtpPort == 465;
        
        return client;
    }

    private MailMessage CreateMailMessage(string to, string subject, string body, bool isHtml, string? toName = null)
    {
        var message = new MailMessage();
        message.From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
        message.To.Add(new MailAddress(to, toName ?? to));
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = isHtml;
        
        return message;
    }

    private string GenerateInvitationEmailBody(string email, string firstName, string lastName, string invitationToken, DateTimeOffset expiresAt)
    {
        // URL encode the email and token for safe inclusion in URL
        var encodedEmail = HttpUtility.UrlEncode(email);
        var encodedToken = HttpUtility.UrlEncode(invitationToken);
        
        // Build the invitation link with email and token as parameters
        var invitationLink = $"{_emailSettings.FrontendUrl}/accept-invitation?email={encodedEmail}&token={encodedToken}";
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; }}
        .header {{ color: #2c5282; font-size: 24px; margin-bottom: 20px; }}
        .content {{ line-height: 1.6; color: #333; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #3182ce; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ font-size: 12px; color: #666; margin-top: 30px; border-top: 1px solid #eee; padding-top: 20px; }}
        .button:hover {{ background-color: #2c5282; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='header'>Welcome to Odary!</h1>
        <div class='content'>
            <p>Hi {firstName} {lastName},</p>
            <p>You've been invited to join Odary dental practice management system. To complete your account setup, please click the button below:</p>
            <a href='{invitationLink}' class='button'>Complete Account Setup</a>
            <p><strong>Important:</strong> This invitation will expire on {expiresAt:MMM dd, yyyy 'at' h:mm tt}.</p>
            <p>If the button doesn't work, you can copy and paste this link into your browser:</p>
            <p>If you have any questions, please contact your administrator.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from Odary. Please do not reply.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GeneratePasswordResetEmailBody(string email, string firstName, string resetToken, DateTimeOffset expiresAt)
    {
        // URL encode the email and token for safe inclusion in URL
        var encodedEmail = HttpUtility.UrlEncode(email);
        var encodedToken = HttpUtility.UrlEncode(resetToken);
        
        // Build the reset password link with email and token as parameters
        var resetLink = $"{_emailSettings.FrontendUrl}/reset-password?email={encodedEmail}&token={encodedToken}";
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px; }}
        .header {{ color: #2c5282; font-size: 24px; margin-bottom: 20px; }}
        .content {{ line-height: 1.6; color: #333; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #e53e3e; color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }}
        .footer {{ font-size: 12px; color: #666; margin-top: 30px; border-top: 1px solid #eee; padding-top: 20px; }}
        .button:hover {{ background-color: #c53030; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='header'>Reset Your Password</h1>
        <div class='content'>
            <p>Hi {firstName},</p>
            <p>We received a request to reset your password for your Odary account. Click the button below to set a new password:</p>
            <a href='{resetLink}' class='button'>Reset Password</a>
            <p><strong>Important:</strong> This reset link will expire on {expiresAt:MMM dd, yyyy 'at' h:mm tt}.</p>
            <p>If the button doesn't work, you can copy and paste this link into your browser:</p>
            <p>If you didn't request this password reset, you can safely ignore this email.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from Odary. Please do not reply.</p>
        </div>
    </div>
</body>
</html>";
    }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string FrontendUrl { get; set; } = "https://app.odary.com"; // Default frontend URL
} 