using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Odary.Api.Infrastructure.Email;

public interface IEmailService
{
    Task<EmailSentResult> SendEmailAsync(string to, string subject, string htmlContent, string? toName = null, CancellationToken cancellationToken = default);
    Task<EmailSentResult> SendTemplateEmailAsync(string to, string subject, string templateName, object model, string? toName = null, CancellationToken cancellationToken = default);
}

public class EmailService(IOptions<EmailSettings> emailOptions, IEmailTemplateService templateService, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _emailSettings = emailOptions.Value;

    public async Task<EmailSentResult> SendEmailAsync(string to, string subject, string htmlContent, string? toName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = CreateSmtpClient();
            using var message = CreateMailMessage(to, subject, htmlContent, toName);
            
            await client.SendMailAsync(message, cancellationToken);
            
            logger.LogInformation("Email sent successfully to {To} with subject: {Subject}", to, subject);
            
            return new EmailSentResult
            {
                To = to,
                Subject = subject,
                SentAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To} with subject: {Subject}", to, subject);
            throw;
        }
    }

    public async Task<EmailSentResult> SendTemplateEmailAsync(string to, string subject, string templateName, object model, string? toName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Render the content template
            var content = await templateService.RenderTemplateAsync(templateName, model, cancellationToken);
            
            // Wrap content in layout
            var layoutModel = new { content, title = subject };
            var htmlContent = await templateService.RenderTemplateAsync("layout", layoutModel, cancellationToken);
            
            return await SendEmailAsync(to, subject, htmlContent, toName, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send template email {TemplateName} to {To}", templateName, to);
            throw;
        }
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

    private MailMessage CreateMailMessage(string to, string subject, string htmlContent, string? toName = null)
    {
        var message = new MailMessage();
        message.From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
        message.To.Add(new MailAddress(to, toName ?? to));
        message.Subject = subject;
        message.Body = htmlContent;
        message.IsBodyHtml = true; // Always HTML
        
        return message;
    }
}

public record EmailSentResult
{
    public string To { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTimeOffset SentAt { get; init; }
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