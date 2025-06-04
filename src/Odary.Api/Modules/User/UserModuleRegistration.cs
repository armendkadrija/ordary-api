using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Odary.Api.Common.Validation;
using Odary.Api.Modules.User.Validators;

namespace Odary.Api.Modules.User;

public static class UserModuleRegistration
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        // Register validation service (shared across modules)
        services.AddSingleton<IValidationService, ValidationService>();

        // Register validators
        services.AddScoped<IValidator<UserCommands.V1.CreateUser>, CreateUserValidator>();
        services.AddScoped<IValidator<UserCommands.V1.UpdateUser>, UpdateUserValidator>();

        // Register services
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        // Create user
        group.MapPost("/", async (
            [FromBody] CreateUserRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.CreateUser(request.Email, request.Password);
            var result = await userService.CreateUserAsync(command, cancellationToken);
            return Results.Created($"/api/v1/users/{result.Id}", result);
        })
        .WithName("CreateUser")
        .WithSummary("Create a new user")
        .Produces<UserResources.V1.User>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // Get user by ID
        group.MapGet("/{id}", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var query = new UserQueries.V1.GetUser(id);
            var result = await userService.GetUserAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUser")
        .WithSummary("Get user by ID")
        .Produces<UserQueries.V1.GetUser.Response>();

        // Get users with pagination and filtering
        group.MapGet("/", async (
            [AsParameters] GetUsersRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var query = new UserQueries.V1.GetUsers
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Email = request.Email
            };
            var result = await userService.GetUsersAsync(query, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("GetUsers")
        .WithSummary("Get users with pagination and filtering")
        .Produces<UserQueries.V1.GetUsers.Response>();

        // Update user
        group.MapPut("/{id}", async (
            string id,
            [FromBody] UpdateUserRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.UpdateUser(id, request.Email);
            var result = await userService.UpdateUserAsync(command, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("UpdateUser")
        .WithSummary("Update user")
        .Produces<UserResources.V1.User>()
        .ProducesValidationProblem();

        // Delete user
        group.MapDelete("/{id}", async (
            string id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var command = new UserCommands.V1.DeleteUser(id);
            await userService.DeleteUserAsync(command, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithSummary("Delete user")
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}

// Request models for minimal API binding
public record CreateUserRequest(string Email, string Password);
public record UpdateUserRequest(string Email);
public record GetUsersRequest(int Page = 1, int PageSize = 20, string? Email = null); 