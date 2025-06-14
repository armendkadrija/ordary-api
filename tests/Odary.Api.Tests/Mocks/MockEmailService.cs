using System.Text.RegularExpressions;
using Odary.Api.Infrastructure.Email;

namespace Odary.Api.Tests.Mocks;

public class MockEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = new();

    public Task<EmailSentResult> SendEmailAsync(string to, string subject, string htmlContent, string? toName = null, CancellationToken cancellationToken = default)
    {
        var sentEmail = new SentEmail
        {
            To = to,
            ToName = toName,
            Subject = subject,
            HtmlContent = htmlContent,
            SentAt = DateTimeOffset.UtcNow
        };

        SentEmails.Add(sentEmail);

        return Task.FromResult(new EmailSentResult
        {
            To = to,
            Subject = subject,
            SentAt = sentEmail.SentAt
        });
    }

    public Task<EmailSentResult> SendTemplateEmailAsync(string to, string subject, string templateName, object model, string? toName = null, CancellationToken cancellationToken = default)
    {
        // For testing, we'll simulate the template rendering by creating a simple HTML structure
        var htmlContent = SimulateTemplateRendering(templateName, model);
        
        return SendEmailAsync(to, subject, htmlContent, toName, cancellationToken);
    }

    public void Clear()
    {
        SentEmails.Clear();
    }

    // Helper methods to extract information from emails
    public SentEmail? GetLatestEmailTo(string email)
    {
        return SentEmails.LastOrDefault(e => e.To == email);
    }

    public List<SentEmail> GetEmailsTo(string email)
    {
        return SentEmails.Where(e => e.To == email).ToList();
    }

    public SentEmail? GetLatestEmailWithSubjectContaining(string subjectPart)
    {
        return SentEmails.LastOrDefault(e => e.Subject.Contains(subjectPart, StringComparison.OrdinalIgnoreCase));
    }

    // Extract tokens from HTML content using regex
    public string? ExtractTokenFromHtml(string htmlContent)
    {
        // Look for token parameter in URLs like: ?token=ABC123 or &token=ABC123
        var tokenMatch = Regex.Match(htmlContent, @"[?&]token=([^&\s<>""']+)", RegexOptions.IgnoreCase);
        return tokenMatch.Success ? Uri.UnescapeDataString(tokenMatch.Groups[1].Value) : null;
    }

    public string? ExtractEmailFromHtml(string htmlContent)
    {
        // Look for email parameter in URLs like: ?email=test@example.com or &email=test@example.com
        var emailMatch = Regex.Match(htmlContent, @"[?&]email=([^&\s<>""']+)", RegexOptions.IgnoreCase);
        return emailMatch.Success ? Uri.UnescapeDataString(emailMatch.Groups[1].Value) : null;
    }

    public string? ExtractLinkFromHtml(string htmlContent)
    {
        // Look for href attributes in anchor tags
        var linkMatch = Regex.Match(htmlContent, @"href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        return linkMatch.Success ? linkMatch.Groups[1].Value : null;
    }

    // Get invitation token for a specific email
    public string? GetInvitationTokenFor(string email)
    {
        var invitationEmail = SentEmails
            .Where(e => e.To == email && (e.Subject.Contains("invitation", StringComparison.OrdinalIgnoreCase) || e.Subject.Contains("Welcome", StringComparison.OrdinalIgnoreCase)))
            .LastOrDefault();
        
        return invitationEmail != null ? ExtractTokenFromHtml(invitationEmail.HtmlContent) : null;
    }

    // Get password reset token for a specific email
    public string? GetPasswordResetTokenFor(string email)
    {
        var resetEmail = SentEmails
            .Where(e => e.To == email && e.Subject.Contains("reset", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();
        
        return resetEmail != null ? ExtractTokenFromHtml(resetEmail.HtmlContent) : null;
    }

    private string SimulateTemplateRendering(string templateName, object model)
    {
        // Simulate template rendering for testing purposes
        return templateName.ToLower() switch
        {
            "user-invitation" => SimulateUserInvitationTemplate(model),
            "password-reset" => SimulatePasswordResetTemplate(model),
            "onboarding-complete" => SimulateOnboardingCompleteTemplate(model),
            _ => $"<html><body><h1>Template: {templateName}</h1><p>Model: {System.Text.Json.JsonSerializer.Serialize(model)}</p></body></html>"
        };
    }

    private string SimulateUserInvitationTemplate(object model)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        var firstName = data?.GetValueOrDefault("FirstName")?.ToString() ?? "User";
        var invitationLink = data?.GetValueOrDefault("InvitationLink")?.ToString() ?? "#";
        
        return $@"
        <html>
        <body>
            <h2>Welcome to Odary - Complete Your Account Setup</h2>
            <p>Hi {firstName},</p>
            <p>You've been invited to join Odary. Click the button below to complete your account setup:</p>
            <a href=""{invitationLink}"" class=""button"">Accept Invitation</a>
            <p>If the button doesn't work, copy and paste this link: {invitationLink}</p>
        </body>
        </html>";
    }

    private string SimulatePasswordResetTemplate(object model)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        var firstName = data?.GetValueOrDefault("FirstName")?.ToString() ?? "User";
        var resetLink = data?.GetValueOrDefault("ResetLink")?.ToString() ?? "#";
        
        return $@"
        <html>
        <body>
            <h2>Reset Your Password</h2>
            <p>Hi {firstName},</p>
            <p>We received a request to reset your password. Click the button below to set a new password:</p>
            <a href=""{resetLink}"" class=""button"">Reset Password</a>
            <p>If the button doesn't work, copy and paste this link: {resetLink}</p>
        </body>
        </html>";
    }

    private string SimulateOnboardingCompleteTemplate(object model)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(model);
        var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        
        var firstName = data?.GetValueOrDefault("FirstName")?.ToString() ?? "User";
        var loginUrl = data?.GetValueOrDefault("LoginUrl")?.ToString() ?? "#";
        
        return $@"
        <html>
        <body>
            <h2>Welcome to Odary - Onboarding Complete</h2>
            <p>Hi {firstName},</p>
            <p>Your account setup is complete! You can now log in to Odary.</p>
            <a href=""{loginUrl}"" class=""button"">Login to Odary</a>
        </body>
        </html>";
    }
}

public class SentEmail
{
    public string To { get; set; } = string.Empty;
    public string? ToName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
} 