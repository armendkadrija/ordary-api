using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Validation;
using Odary.Api.Domain;
using Odary.Api.Modules.Tenant;
using Xunit;

namespace Odary.Api.Tests.Unit;

public class TenantServiceTests : IDisposable
{
    private readonly OdaryDbContext _dbContext;
    private readonly IValidationService _validationService;
    private readonly ILogger<TenantService> _logger;
    private readonly TenantService _service;

    public TenantServiceTests()
    {
        var options = new DbContextOptionsBuilder<OdaryDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new OdaryDbContext(options);
        _validationService = Substitute.For<IValidationService>();
        _logger = Substitute.For<ILogger<TenantService>>();
        _service = new TenantService(_validationService, _dbContext, _logger);
    }

    [Fact]
    public async Task CreateTenantAsync_WithValidCommand_ShouldCreateTenantAndAdminUser()
    {
        // Arrange
        var command = new TenantCommands.V1.CreateTenant(
            "Test Clinic",
            "admin@test.com",
            "Password123",
            "USA",
            "America/New_York"
        );

        // Act
        var result = await _service.CreateTenantAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Clinic", result.Name);
        Assert.Equal("USA", result.Country);
        Assert.Equal("America/New_York", result.Timezone);
        Assert.True(result.IsActive);

        // Verify tenant was created in database
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == result.Id);
        Assert.NotNull(tenant);

        // Verify admin user was created
        var adminUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "admin@test.com");
        Assert.NotNull(adminUser);
        Assert.Equal(result.Id, adminUser.TenantId);

        // Verify tenant settings were created
        var settings = await _dbContext.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == result.Id);
        Assert.NotNull(settings);
    }

    [Fact]
    public async Task CreateTenantAsync_WithDuplicateName_ShouldThrowBusinessException()
    {
        // Arrange
        var existingTenant = new Domain.Tenant("Test Clinic", "USA", "America/New_York");
        existingTenant.IsActive = true; // Set default active state
        _dbContext.Tenants.Add(existingTenant);
        await _dbContext.SaveChangesAsync();

        var command = new TenantCommands.V1.CreateTenant(
            "Test Clinic", // Duplicate name
            "admin@test.com",
            "Password123",
            "USA",
            "America/New_York"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _service.CreateTenantAsync(command));
        
        Assert.Equal("A clinic with this name already exists", exception.Message);
    }

    [Fact]
    public async Task CreateTenantAsync_WithDuplicateEmail_ShouldThrowBusinessException()
    {
        // Arrange
        var existingTenant = new Domain.Tenant("Existing Clinic", "USA", "America/New_York");
        existingTenant.IsActive = true; // Set default active state
        _dbContext.Tenants.Add(existingTenant);
        
        var existingUser = new User(existingTenant.Id, "admin@test.com", "hashedpassword", "Admin", "User", "Admin");
        existingUser.IsActive = true; // Set default active state
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var command = new TenantCommands.V1.CreateTenant(
            "New Clinic",
            "admin@test.com", // Duplicate email
            "Password123",
            "USA",
            "America/New_York"
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => _service.CreateTenantAsync(command));
        
        Assert.Equal("A user with this email already exists", exception.Message);
    }

    [Fact]
    public async Task GetTenantAsync_WithValidId_ShouldReturnTenantWithSettings()
    {
        // Arrange
        var tenant = new Domain.Tenant("Test Clinic", "USA", "America/New_York");
        tenant.IsActive = true; // Set default active state
        _dbContext.Tenants.Add(tenant);
        
        var settings = new Domain.TenantSettings(tenant.Id, "en-US", "USD", "MM/dd/yyyy", "h:mm tt");
        _dbContext.TenantSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var query = new TenantQueries.V1.GetTenant(tenant.Id);

        // Act
        var result = await _service.GetTenantAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(tenant.Id, result.Id);
        Assert.Equal("Test Clinic", result.Name);
        Assert.NotNull(result.Settings);
    }

    [Fact]
    public async Task GetTenantAsync_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new TenantQueries.V1.GetTenant("non-existent-id");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetTenantAsync(query));
        
        Assert.Equal("Tenant with ID non-existent-id not found", exception.Message);
    }

    [Fact]
    public async Task UpdateTenantSettingsAsync_WithValidCommand_ShouldUpdateSettings()
    {
        // Arrange
        var tenant = new Domain.Tenant("Test Clinic", "USA", "America/New_York");
        tenant.IsActive = true; // Set default active state
        _dbContext.Tenants.Add(tenant);
        
        var settings = new Domain.TenantSettings(tenant.Id, "en-US", "USD", "MM/dd/yyyy", "h:mm tt");
        _dbContext.TenantSettings.Add(settings);
        await _dbContext.SaveChangesAsync();

        var command = new TenantCommands.V1.UpdateTenantSettings(
            tenant.Id,
            "es-ES",
            "EUR",
            "dd/MM/yyyy",
            "HH:mm"
        );

        // Act
        var result = await _service.UpdateTenantSettingsAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("es-ES", result.Language);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal("dd/MM/yyyy", result.DateFormat);
        Assert.Equal("HH:mm", result.TimeFormat);
    }

    [Fact]
    public async Task DeactivateTenantAsync_WithValidId_ShouldDeactivateTenant()
    {
        // Arrange
        var tenant = new Domain.Tenant("Test Clinic", "USA", "America/New_York");
        tenant.IsActive = true; // Set default active state
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new TenantCommands.V1.DeactivateTenant(tenant.Id);

        // Act
        await _service.DeactivateTenantAsync(command);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);
        Assert.NotNull(updatedTenant);
        Assert.False(updatedTenant.IsActive);
    }

    [Fact]
    public async Task ActivateTenantAsync_WithValidId_ShouldActivateTenant()
    {
        // Arrange
        var tenant = new Domain.Tenant("Test Clinic", "USA", "America/New_York");
        tenant.IsActive = false; // Start deactivated
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var command = new TenantCommands.V1.ActivateTenant(tenant.Id);

        // Act
        await _service.ActivateTenantAsync(command);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenant.Id);
        Assert.NotNull(updatedTenant);
        Assert.True(updatedTenant.IsActive);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
} 