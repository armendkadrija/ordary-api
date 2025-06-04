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

public class UserService : IUserService
{
    private readonly IValidationService _validationService;
    private readonly OdaryDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IValidationService validationService,
        OdaryDbContext dbContext,
        ILogger<UserService> logger)
    {
        _validationService = validationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserResources.V1.User> CreateUserAsync(
        UserCommands.V1.CreateUser command, 
        CancellationToken cancellationToken = default)
    {
        await _validationService.ValidateAsync(command, cancellationToken);

        // Check if user already exists
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("User with this email already exists");

        // Hash password (simplified - in production use proper password hashing)
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        var user = new Domain.User(command.Email, passwordHash);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User created successfully with ID: {UserId}", user.Id);
        return user.ToContract();
    }

    public async Task<UserQueries.V1.GetUser.Response> GetUserAsync(
        UserQueries.V1.GetUser query, 
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == query.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {query.Id} not found");

        return user.ToGetUserResponse();
    }

    public async Task<UserQueries.V1.GetUsers.Response> GetUsersAsync(
        UserQueries.V1.GetUsers query, 
        CancellationToken cancellationToken = default)
    {
        var usersQuery = _dbContext.Users.AsQueryable();

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
        await _validationService.ValidateAsync(command, cancellationToken);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found");

        // Check if email is already taken by another user
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.Id != command.Id, cancellationToken);
        
        if (existingUser != null)
            throw new BusinessException("Email is already taken by another user");

        user.UpdateEmail(command.Email);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User updated successfully with ID: {UserId}", user.Id);
        return user.ToContract();
    }

    public async Task DeleteUserAsync(
        UserCommands.V1.DeleteUser command, 
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);

        if (user == null)
            throw new NotFoundException($"User with ID {command.Id} not found");

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User deleted successfully with ID: {UserId}", user.Id);
    }
} 