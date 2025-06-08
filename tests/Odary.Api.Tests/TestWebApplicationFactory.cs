using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Odary.Api.Modules.Email;

namespace Odary.Api.Tests;

// Mock email service that doesn't try to send real emails
public class MockEmailService : IEmailService
{
    public Task<EmailResources.V1.EmailSent> SendEmailAsync(EmailCommands.V1.SendEmail command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EmailResources.V1.EmailSent
        {
            To = command.To,
            Subject = command.Subject,
            SentAt = DateTimeOffset.UtcNow
        });
    }

    public Task<EmailResources.V1.EmailSent> SendUserInvitationAsync(EmailCommands.V1.SendUserInvitation command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EmailResources.V1.EmailSent
        {
            To = command.Email,
            Subject = "Welcome to Odary - Complete Your Account Setup",
            SentAt = DateTimeOffset.UtcNow
        });
    }

    public Task<EmailResources.V1.EmailSent> SendPasswordResetAsync(EmailCommands.V1.SendPasswordReset command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EmailResources.V1.EmailSent
        {
            To = command.Email,
            Subject = "Reset Your Odary Password",
            SentAt = DateTimeOffset.UtcNow
        });
    }
}

public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace email service with mock (keep PostgreSQL and Redis real)
            var emailServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailServiceDescriptor != null)
            {
                services.Remove(emailServiceDescriptor);
            }
            services.AddSingleton<IEmailService, MockEmailService>();
        });

        builder.UseEnvironment("Testing");
    }
}