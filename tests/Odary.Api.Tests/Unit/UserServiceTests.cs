using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;
using Odary.Api.Domain;
using Odary.Api.Modules.User;
using Xunit;

namespace Odary.Api.Tests.Unit;

public class UserServiceTests : IDisposable
{
    private readonly IValidationService _validationService;
    private readonly OdaryDbContext _dbContext;
    private readonly ILogger<UserService> _logger;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _validationService = Substitute.For<IValidationService>();
        _logger = Substitute.For<ILogger<UserService>>();
        
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<OdaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new OdaryDbContext(options);
        
        _userService = new UserService(_validationService, _dbContext, _logger);
    }

    [Fact]
    public async Task CreateUserAsync_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        var command = new UserCommands.V1.CreateUser("test@example.com", "Password123!");
        
        // Act
        var result = await _userService.CreateUserAsync(command);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("test@example.com", result.Email);
        Assert.NotEmpty(result.Id);
        
        // Verify validation was called
        await _validationService.Received(1).ValidateAsync(command, Arg.Any<CancellationToken>());
        
        // Verify user was saved to database
        var userInDb = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(userInDb);
        Assert.Equal("test@example.com", userInDb.Email);
    }

    [Fact]
    public async Task CreateUserAsync_WithExistingEmail_ShouldThrowBusinessException()
    {
        // Arrange
        var existingUser = new User("temp-tenant-id", "test@example.com", "hashedpassword", "Test", "User", "User");
        existingUser.IsActive = true; // Set default active state
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();
        
        var command = new UserCommands.V1.CreateUser("test@example.com", "Password123!");
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _userService.CreateUserAsync(command));
        
        Assert.Equal("User with this email already exists", exception.Message);
    }

    [Fact]
    public async Task GetUserAsync_WithValidId_ShouldReturnUser()
    {
        // Arrange
        var user = new User("temp-tenant-id", "test@example.com", "hashedpassword", "Test", "User", "User");
        user.IsActive = true; // Set default active state
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        var query = new UserQueries.V1.GetUser(user.Id);
        
        // Act
        var result = await _userService.GetUserAsync(query);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetUserAsync_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new UserQueries.V1.GetUser("nonexistent-id");
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _userService.GetUserAsync(query));
        
        Assert.Contains("not found", exception.Message);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
} 