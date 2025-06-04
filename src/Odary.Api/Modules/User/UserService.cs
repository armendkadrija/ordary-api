using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;

namespace Odary.Api.Modules.User;

public interface IUserService
{
    Task<UserResources.V1.User> CreateUserAsync(UserCommands.V1.CreateUser command, CancellationToken cancellationToken = default);
    Task<UserQueries.V1.GetUser.Response> GetUserAsync(UserQueries.V1.GetUser query, CancellationToken cancellationToken = default);
    Task<UserQueries.V1.GetUsers.Response> GetUsersAsync(UserQueries.V1.GetUsers query, CancellationToken cancellationToken = default);
    Task<UserResources.V1.User> UpdateUserAsync(UserCommands.V1.UpdateUser command, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserCommands.V1.DeleteUser command, CancellationToken cancellationToken = default);
}

public class UserService(
    IValidationService validationService,
    OdaryDbContext dbContext,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserResources.V1.User> CreateUserAsync(
        UserCommands.V1.CreateUser command, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        // Check if user already exists
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("User with this email already exists");

        // Hash password (simplified - in production use proper password hashing)
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        // For now, create users without tenant constraint - this will be updated when auth is integrated with multi-tenancy
        // TODO: Extract tenantId from authenticated user context
        var tempTenantId = Guid.NewGuid().ToString("N"); // Temporary solution - in production get from auth context
        
        // Create user with minimum required data
        var user = new Domain.User(
            tempTenantId, 
            command.Email, 
            passwordHash,
            command.Email.Split('@')[0], // Default first name from email prefix
            "",                         // Default empty last name
            "User"                      // Default role
        );
        
        // Apply business logic - set default active state
        user.IsActive = true;
        
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User created successfully with ID: {UserId}", user.Id);
        return user.ToContract();
    }

    public async Task<UserQueries.V1.GetUser.Response> GetUserAsync(
        UserQueries.V1.GetUser query, 
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == query.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {query.Id} not found");

        return user.ToGetUserResponse();
    }

    public async Task<UserQueries.V1.GetUsers.Response> GetUsersAsync(
        UserQueries.V1.GetUsers query, 
        CancellationToken cancellationToken = default)
    {
        var usersQuery = dbContext.Users.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(query.Email))
        {
            usersQuery = usersQuery.Where(u => u.Email.Contains(query.Email));
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var users = await usersQuery
            .Skip(query.Skip)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return new UserQueries.V1.GetUsers.Response
        {
            Items = users.Select(u => u.ToContract()).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<UserResources.V1.User> UpdateUserAsync(
        UserCommands.V1.UpdateUser command, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found");

        // Check if email is already taken by another user
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.Id != command.Id, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("Email is already taken by another user");

        user.Email = command.Email;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User updated successfully with ID: {UserId}", user.Id);
        return user.ToContract();
    }

    public async Task DeleteUserAsync(
        UserCommands.V1.DeleteUser command, 
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found");

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User deleted successfully with ID: {UserId}", user.Id);
    }
} 