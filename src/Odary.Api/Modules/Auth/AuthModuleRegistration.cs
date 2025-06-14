using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Modules.Auth.Validators;
using System.Security.Claims;
using Odary.Api.Common.Services;
using Odary.Api.Extensions;

namespace Odary.Api.Modules.Auth;

public static class AuthModuleRegistration
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddScoped<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<AuthCommands.V1.SignIn>, SignInValidator>();
        services.AddScoped<IValidator<AuthCommands.V1.RefreshToken>, RefreshTokenValidator>();
        services.AddScoped<IValidator<AuthCommands.V1.ForgotPassword>, ForgotPasswordValidator>();
        services.AddScoped<IValidator<AuthCommands.V1.ResetPassword>, ResetPasswordValidator>();
        services.AddScoped<IValidator<AuthCommands.V1.ChangePassword>, ChangePasswordValidator>();


        // Register services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthEmailService, AuthEmailService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        // Public endpoints (no authorization required)
        authGroup.MapPost("/signin",
            async (
                [FromBody] AuthCommands.V1.SignIn command,
                [FromServices] IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var result = await authService.SignInAsync(command, ipAddress, userAgent, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("SignIn")
            .WithSummary("Authenticate user")
            .Produces<AuthResources.V1.TokenResponse>()
            .ProducesValidationProblem();

        authGroup.MapPost("/refresh-token",
            async (
                [FromBody] AuthCommands.V1.RefreshToken command,
                [FromServices] IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var result = await authService.RefreshTokenAsync(command, ipAddress, userAgent, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("RefreshToken")
            .WithSummary("Refresh access token using refresh token")
            .Produces<AuthResources.V1.TokenResponse>()
            .ProducesValidationProblem();

        authGroup.MapPost("/forgot-password",
            async (
                [FromBody] AuthCommands.V1.ForgotPassword command,
                [FromServices] IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.ForgotPasswordAsync(command, cancellationToken);
                return Results.Accepted();
            })
            .WithName("ForgotPassword")
            .WithSummary("Request password reset email")
            .Produces(202)
            .ProducesValidationProblem();

        authGroup.MapPost("/reset-password",
            async (
                [FromBody] AuthCommands.V1.ResetPassword command,
                [FromServices] IAuthService authService,
                CancellationToken cancellationToken) =>
            {
                await authService.ResetPasswordAsync(command, cancellationToken);
                return Results.Ok();
            })
            .WithName("ResetPassword")
            .WithSummary("Reset password using token")
            .Produces(200)
            .ProducesValidationProblem();

        // Protected endpoints (require authentication and specific claims)
        authGroup.MapPost("/change-password",
            async (
                [FromBody] AuthCommands.V1.ChangePassword command,
                [FromServices] IAuthService authService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var userId = user.GetUserId();
                await authService.ChangePasswordAsync(command, userId, cancellationToken);
                return Results.Ok();
            })
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithSummary("Change current user's password")
            .Produces(200)
            .ProducesValidationProblem();

        authGroup.MapGet("/me",
            async (
                [FromServices] IAuthService authService,
                ClaimsPrincipal user,
                CancellationToken cancellationToken) =>
            {
                var userId = user.GetUserId();
                var result = await authService.GetCurrentUserAsync(userId);
                return Results.Ok(result);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get current user profile")
            .Produces<AuthQueries.V1.GetCurrentUser.Response>()
            .Produces(404);

        return app;
    }
}

 