namespace Odary.Api.Modules.Email;

public static class EmailModuleRegistration
{
    public static IServiceCollection AddEmailModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Register email settings
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        
        // Register email service
        services.AddScoped<IEmailService, EmailService>();
        
        return services;
    }
} 