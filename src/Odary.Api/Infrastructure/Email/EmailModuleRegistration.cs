namespace Odary.Api.Infrastructure.Email;

public static class EmailModuleRegistration
{
    public static IServiceCollection AddEmailModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register email settings
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        
        // Register email services
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailService, EmailService>();
        
        return services;
    }
} 