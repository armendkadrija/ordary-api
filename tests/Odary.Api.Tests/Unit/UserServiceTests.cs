using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;
using Odary.Api.Common.Validation;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Modules.Email;
using Odary.Api.Modules.User;
using Xunit;

namespace Odary.Api.Tests.Unit;

public class UserServiceTests : IDisposable
{
    private readonly IValidationService _validationService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<UserService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly OdaryDbContext _dbContext;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<OdaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OdaryDbContext(options);

        _validationService = Substitute.For<IValidationService>();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _logger = Substitute.For<ILogger<UserService>>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _emailService = Substitute.For<IEmailService>();

        // Use the real database context instead of a mock since we need actual database operations
        _userService = new UserService(
            _validationService,
            _userManager,
            _dbContext,
            _logger,
            _currentUserService,
            _emailService);
    }

    #region InviteUser Tests

    [Fact]
    public async Task InviteUserAsync_ValidInput_CreatesInvitationSuccessfully()
    {
        // Arrange
        await _dbContext.Database.EnsureCreatedAsync();
        var tenant = new Tenant("Test Tenant", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UserCommands.V1.InviteUser("invite@example.com", "John", "Doe", Roles.DENTIST, tenant.Id);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenant.Id);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.GeneratePasswordResetTokenAsync(Arg.Any<User>()).Returns("test-token-123");

        _emailService.SendUserInvitationAsync(Arg.Any<EmailCommands.V1.SendUserInvitation>(), Arg.Any<CancellationToken>())
            .Returns(new EmailResources.V1.EmailSent { To = command.Email, Subject = "Invitation", SentAt = DateTimeOffset.UtcNow });

        // Act
        var result = await _userService.InviteUserAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Email, result.Email);
        Assert.Contains("test-token-123", result.InvitationToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        
        await _userManager.Received(1).CreateAsync(Arg.Is<User>(u => u.Email == command.Email && !u.IsActive));
        await _userManager.Received(1).GeneratePasswordResetTokenAsync(Arg.Any<User>());
        await _emailService.Received(1).SendUserInvitationAsync(Arg.Any<EmailCommands.V1.SendUserInvitation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteUserAsync_AdminInvitingToOtherTenant_ThrowsBusinessException()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var otherTenantId = Guid.NewGuid().ToString();
        var command = new UserCommands.V1.InviteUser("invite@example.com", "John", "Doe", Roles.DENTIST, otherTenantId);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _userService.InviteUserAsync(command));
    }

    [Fact]
    public async Task InviteUserAsync_NonExistentTenant_ThrowsBusinessException()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var command = new UserCommands.V1.InviteUser("invite@example.com", "John", "Doe", Roles.DENTIST, tenantId);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _userService.InviteUserAsync(command));
    }

    [Fact]
    public async Task InviteUserAsync_ExistingEmail_ThrowsBusinessException()
    {
        // Arrange
        await _dbContext.Database.EnsureCreatedAsync();
        var tenant = new Tenant("Test Tenant", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UserCommands.V1.InviteUser("existing@example.com", "John", "Doe", Roles.DENTIST, tenant.Id);
        var existingUser = new User(tenant.Id, "existing@example.com", "Existing", "User", Roles.DENTIST);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenant.Id);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        _userManager.FindByEmailAsync(command.Email).Returns(existingUser);

        // Act & Assert
        await Assert.ThrowsAsync<BusinessException>(() => _userService.InviteUserAsync(command));
    }

    [Fact]
    public async Task InviteUserAsync_EmailServiceFails_UserStillCreated()
    {
        // Arrange
        await _dbContext.Database.EnsureCreatedAsync();
        var tenant = new Tenant("Test Tenant", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UserCommands.V1.InviteUser("invite@example.com", "John", "Doe", Roles.DENTIST, tenant.Id);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenant.Id);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.GeneratePasswordResetTokenAsync(Arg.Any<User>()).Returns("test-token-123");

        _emailService.SendUserInvitationAsync(Arg.Any<EmailCommands.V1.SendUserInvitation>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<EmailResources.V1.EmailSent>(new Exception("Email service unavailable")));

        // Act
        var result = await _userService.InviteUserAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Email, result.Email);
        await _userManager.Received(1).CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task InviteUserAsync_SuperAdminCanInviteToAnyTenant_Succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var otherTenantId = Guid.NewGuid().ToString();
        
        await _dbContext.Database.EnsureCreatedAsync();
        var tenant = new Tenant("Other Tenant", "US", "UTC");
        await _dbContext.Tenants.AddAsync(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new UserCommands.V1.InviteUser("invite@example.com", "John", "Doe", Roles.DENTIST, tenant.Id);
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.SUPER_ADMIN);
        _currentUserService.Email.Returns("superadmin@example.com");
        _currentUserService.IsAdmin.Returns(false);
        _currentUserService.IsSuperAdmin.Returns(true);

        _userManager.FindByEmailAsync(command.Email).Returns((User?)null);
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.GeneratePasswordResetTokenAsync(Arg.Any<User>()).Returns("test-token-123");

        _emailService.SendUserInvitationAsync(Arg.Any<EmailCommands.V1.SendUserInvitation>(), Arg.Any<CancellationToken>())
            .Returns(new EmailResources.V1.EmailSent { To = command.Email, Subject = "Invitation", SentAt = DateTimeOffset.UtcNow });

        // Act
        var result = await _userService.InviteUserAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(command.Email, result.Email);
        await _userManager.Received(1).CreateAsync(Arg.Any<User>());
    }

    #endregion

    #region GetUsers Tests

    [Fact]
    public async Task GetUsersAsync_WithTenantIsolation_ReturnsOnlyTenantUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var otherTenantId = Guid.NewGuid().ToString();
        
        var user1 = new User(tenantId, "user1@example.com", "User", "One", Roles.DENTIST);
        var user2 = new User(tenantId, "user2@example.com", "User", "Two", Roles.ASSISTANT);
        var user3 = new User(otherTenantId, "user3@example.com", "User", "Three", Roles.DENTIST);
        
        await _dbContext.Users.AddRangeAsync(user1, user2, user3);
        await _dbContext.SaveChangesAsync();

        var query = new UserQueries.V1.GetUsers { Page = 1, PageSize = 10 };
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        // Act
        var result = await _userService.GetUsersAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, u => Assert.True(u.Email == "user1@example.com" || u.Email == "user2@example.com"));
    }

    [Fact]
    public async Task GetUsersAsync_WithEmailFilter_ReturnsFilteredUsers()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        
        var user1 = new User(tenantId, "john@example.com", "John", "Doe", Roles.DENTIST);
        var user2 = new User(tenantId, "jane@example.com", "Jane", "Smith", Roles.ASSISTANT);
        
        await _dbContext.Users.AddRangeAsync(user1, user2);
        await _dbContext.SaveChangesAsync();

        var query = new UserQueries.V1.GetUsers { Page = 1, PageSize = 10, Email = "john" };
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        // Act
        var result = await _userService.GetUsersAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("john@example.com", result.Items.First().Email);
    }

    [Fact]
    public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        
        for (int i = 1; i <= 5; i++)
        {
            var user = new User(tenantId, $"user{i}@example.com", $"User{i}", "Test", Roles.DENTIST);
            await _dbContext.Users.AddAsync(user);
        }
        await _dbContext.SaveChangesAsync();

        var query = new UserQueries.V1.GetUsers { Page = 2, PageSize = 2 };
        
        _currentUserService.UserId.Returns(Guid.NewGuid().ToString());
        _currentUserService.TenantId.Returns(tenantId);
        _currentUserService.Role.Returns(Roles.ADMIN);
        _currentUserService.Email.Returns("admin@example.com");
        _currentUserService.IsAdmin.Returns(true);

        // Act
        var result = await _userService.GetUsersAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Dispose();
    }
} 