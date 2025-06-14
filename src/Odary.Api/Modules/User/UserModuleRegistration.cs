using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Common.Services;
using Odary.Api.Constants.Claims;
using Odary.Api.Modules.User.Validators;
using Odary.Api.Extensions;

namespace Odary.Api.Modules.User;

public static class UserModuleRegistration
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddScoped<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<UserCommands.V1.CreateUser>, CreateUserValidator>();
        services.AddScoped<IValidator<UserCommands.V1.UpdateEmail>, UpdateEmailValidator>();
        
        // User management validators moved from Auth module
        services.AddScoped<IValidator<UserCommands.V1.InviteUser>, InviteUserValidator>();
        services.AddScoped<IValidator<UserCommands.V1.UpdateUserProfile>, UpdateUserProfileValidator>();

        // Register services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserEmailService, UserEmailService>();

        return services;
    }

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var userGroup = app.MapGroup("/api/v1/users").WithTags("User Management");

        // Create user
        userGroup.MapPost("/", async (
            [FromBody] UserCommands.V1.CreateUser command,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.CreateUserAsync(command, cancellationToken);
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .RequireSuperAdmin()
        .WithName("CreateUser")
        .WithSummary("Create a new user with specified role and generated password")
        .Produces<UserResources.V1.CreateUserResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // Get user by ID
        userGroup.MapGet("/{id}", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var query = new UserQueries.V1.GetUser(id);
            var result = await userService.GetUserAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(UserClaims.Read)
        .WithName("GetUser")
        .WithSummary("Get user by ID")
        .Produces<UserQueries.V1.GetUser.Response>()
        .Produces(StatusCodes.Status404NotFound);

        // Get users with pagination and filtering
        userGroup.MapGet("/", async (
            [AsParameters] UserQueries.V1.GetUsers query,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.GetUsersAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(UserClaims.Read)
        .WithName("GetUsers")
        .WithSummary("Get users with pagination and filtering")
        .Produces<UserQueries.V1.GetUsers.Response>();

        // Update user
        userGroup.MapPut("/{id}/email", async (
            string id,
            [FromBody] UserCommands.V1.UpdateEmail command,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var updatedCommand = command with { Id = id };
            var result = await userService.UpdateEmailAsync(updatedCommand, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(UserClaims.Update)
        .WithName("UpdateUserEmail")
        .WithSummary("Update user email")
        .Produces<UserResources.V1.User>()
        .ProducesValidationProblem();

        // Delete user
        userGroup.MapDelete("/{id}", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.DeleteUser(id);
            await userService.DeleteUserAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithClaim(UserClaims.Delete)
        .WithName("DeleteUser")
        .WithSummary("Delete user")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // User management endpoints moved from Auth module

        userGroup.MapPost("/invite", async (
            [FromBody] UserCommands.V1.InviteUser command,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.InviteUserAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(UserClaims.Invite)
        .WithName("InviteUser")
        .WithSummary("Invite a new user")
        .Produces<UserResources.V1.InvitationResponse>()
        .ProducesValidationProblem();

        userGroup.MapPut("/profiles/{id}", async (
            string id,
            [FromBody] UserCommands.V1.UpdateUserProfile command,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var updatedCommand = command with { Id = id };
            var result = await userService.UpdateUserProfileAsync(updatedCommand, cancellationToken);
            return Results.Ok(result);
        })
        .WithClaim(UserClaims.Update)
        .WithName("UpdateUserProfile")
        .WithSummary("Update user profile")
        .Produces<UserResources.V1.UserProfile>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem();



        userGroup.MapPost("/{id}/lock", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.LockUser(id);
            await userService.LockUserAsync(command);
            return Results.NoContent();
        })
        .WithClaim(UserClaims.Update)
        .WithName("LockUser")
        .WithSummary("Lock a user account")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        userGroup.MapPost("/{id}/unlock", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.UnlockUser(id);
            await userService.UnlockUserAsync(command);
            return Results.NoContent();
        })
        .WithClaim(UserClaims.Update)
        .WithName("UnlockUser")
        .WithSummary("Unlock a user account")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

 