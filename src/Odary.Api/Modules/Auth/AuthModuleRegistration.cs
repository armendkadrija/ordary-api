using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Common.Validation;
using Odary.Api.Modules.Auth.Validators;

namespace Odary.Api.Modules.Auth;

public static class AuthModuleRegistration
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddSingleton<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<AuthCommands.V1.SignIn>, SignInValidator>();

        // Register services
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        // Sign in
        group.MapPost("/sign-in", async (
            [FromBody] SignInRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var command = new AuthCommands.V1.SignIn(request.Email, request.Password);
            var result = await authService.SignInAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("SignIn")
        .WithSummary("Sign in and get access token")
        .Produces<AuthResources.V1.TokenResponse>()
        .ProducesValidationProblem()
        .AllowAnonymous();

        return app;
    }
}

// Request models for minimal API binding
public record SignInRequest(string Email, string Password); 