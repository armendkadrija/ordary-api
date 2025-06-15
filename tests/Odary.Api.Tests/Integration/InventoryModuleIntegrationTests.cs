using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Odary.Api.Constants;
using Odary.Api.Domain;
using Odary.Api.Domain.Enums;
using Odary.Api.Infrastructure.Database;
using Odary.Api.Modules.Inventory;
using Odary.Api.Modules.Auth;

namespace Odary.Api.Tests.Integration;

[Collection("IntegrationTests")]
public class InventoryModuleIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InventoryModuleIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateInventoryItem_WithValidData_ShouldReturnCreated()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var command = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Filtek Z250 Composite",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Syringe,
            UnitSize: 4.0m,
            Quantity: 10.0m,
            MinThreshold: 2.0m,
            ExpiryDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            BatchNumber: "BATCH123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryResources.V1.CreateInventoryItemResponse>();
        Assert.NotNull(result);
        Assert.Equal(command.Name, result.Name);
        Assert.Equal(command.Category, result.Category);
        Assert.Equal(command.UnitType, result.UnitType);
        Assert.Equal(command.UnitSize, result.UnitSize);
        Assert.Equal(command.Quantity, result.Quantity);
        Assert.Equal(command.MinThreshold, result.MinThreshold);
        Assert.Equal(command.ExpiryDate, result.ExpiryDate);
        Assert.Equal(command.BatchNumber, result.BatchNumber);
        Assert.False(result.IsLowStock);
        Assert.False(result.IsExpired);
        Assert.False(result.IsExpiringSoon);
        Assert.Equal(2.5m, result.CompleteUnitsAvailable); // 10.0 / 4.0 = 2.5
        Assert.Equal(2.0m, result.PartialUnitRemaining); // 10.0 % 4.0 = 2.0
    }

    [Fact]
    public async Task CreateInventoryItem_WithDuplicateName_ShouldReturnBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var command1 = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Duplicate Item",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Syringe,
            UnitSize: 4.0m,
            Quantity: 10.0m,
            MinThreshold: 2.0m);

        var command2 = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Duplicate Item",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Tube,
            UnitSize: 5.0m,
            Quantity: 15.0m,
            MinThreshold: 3.0m);

        // Act
        await _client.PostAsJsonAsync("/api/v1/inventory", command1);
        var response = await _client.PostAsJsonAsync("/api/v1/inventory", command2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetInventoryItems_ShouldReturnPaginatedResults()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var items = new[]
        {
            new InventoryCommands.V1.CreateInventoryItem(
                Name: "Item A",
                Category: InventoryCategory.Composite,
                UnitType: InventoryUnitType.Syringe,
                UnitSize: 4.0m,
                Quantity: 10.0m,
                MinThreshold: 2.0m),
            new InventoryCommands.V1.CreateInventoryItem(
                Name: "Item B",
                Category: InventoryCategory.Anesthetic,
                UnitType: InventoryUnitType.Carpule,
                UnitSize: 1.8m,
                Quantity: 20.0m,
                MinThreshold: 5.0m),
            new InventoryCommands.V1.CreateInventoryItem(
                Name: "Item C",
                Category: InventoryCategory.GIC,
                UnitType: InventoryUnitType.Capsule,
                UnitSize: 1.0m,
                Quantity: 50.0m,
                MinThreshold: 10.0m)
        };

        foreach (var item in items)
        {
            await _client.PostAsJsonAsync("/api/v1/inventory", item);
        }

        // Act
        var response = await _client.GetAsync("/api/v1/inventory?page=1&pageSize=2");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryQueries.V1.GetInventoryItems.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetInventoryItems_WithCategoryFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var compositeItem = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Composite Item",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Syringe,
            UnitSize: 4.0m,
            Quantity: 10.0m,
            MinThreshold: 2.0m);

        var anestheticItem = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Anesthetic Item",
            Category: InventoryCategory.Anesthetic,
            UnitType: InventoryUnitType.Carpule,
            UnitSize: 1.8m,
            Quantity: 20.0m,
            MinThreshold: 5.0m);

        await _client.PostAsJsonAsync("/api/v1/inventory", compositeItem);
        await _client.PostAsJsonAsync("/api/v1/inventory", anestheticItem);

        // Act
        var response = await _client.GetAsync($"/api/v1/inventory?page=1&pageSize=10&category={InventoryCategory.Composite}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryQueries.V1.GetInventoryItems.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(InventoryCategory.Composite, result.Items[0].Category);
        Assert.Equal("Composite Item", result.Items[0].Name);
    }

    [Fact]
    public async Task GetInventoryItems_WithLowStockFilter_ShouldReturnLowStockItems()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var lowStockItem = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Low Stock Item",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Syringe,
            UnitSize: 4.0m,
            Quantity: 1.0m, // Below threshold
            MinThreshold: 2.0m);

        var normalStockItem = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Normal Stock Item",
            Category: InventoryCategory.Anesthetic,
            UnitType: InventoryUnitType.Carpule,
            UnitSize: 1.8m,
            Quantity: 20.0m, // Above threshold
            MinThreshold: 5.0m);

        await _client.PostAsJsonAsync("/api/v1/inventory", lowStockItem);
        await _client.PostAsJsonAsync("/api/v1/inventory", normalStockItem);

        // Act
        var response = await _client.GetAsync("/api/v1/inventory?page=1&pageSize=10&lowStock=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryQueries.V1.GetInventoryItems.Response>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Low Stock Item", result.Items[0].Name);
        Assert.True(result.Items[0].IsLowStock);
    }

    [Fact]
    public async Task DeductStockByUnits_WithValidUnits_ShouldReduceStockCorrectly()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        await AuthenticateAsAdminAsync();
        
        var createCommand = new InventoryCommands.V1.CreateInventoryItem(
            Name: "Unit Deduct Item",
            Category: InventoryCategory.Composite,
            UnitType: InventoryUnitType.Syringe,
            UnitSize: 4.0m,
            Quantity: 20.0m,
            MinThreshold: 2.0m);

        var createResponse = await _client.PostAsJsonAsync("/api/v1/inventory", createCommand);
        var createdItem = await createResponse.Content.ReadFromJsonAsync<InventoryResources.V1.CreateInventoryItemResponse>();

        var deductUnitsCommand = new InventoryCommands.V1.DeductStockByUnits(
            Id: createdItem!.Id,
            UnitsUsed: 1.5m); // 1.5 syringes = 1.5 * 4.0 = 6.0 base units

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/v1/inventory/{createdItem.Id}/deduct-units", deductUnitsCommand);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryResources.V1.InventoryItem>();
        Assert.NotNull(result);
        Assert.Equal(14.0m, result.Quantity); // 20.0 - (1.5 * 4.0) = 14.0
        Assert.Equal(3.5m, result.CompleteUnitsAvailable); // 14.0 / 4.0 = 3.5
        Assert.Equal(2.0m, result.PartialUnitRemaining); // 14.0 % 4.0 = 2.0
    }

    [Fact]
    public async Task GetCategories_ShouldReturnAllCategories()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        
        // Act
        var response = await _client.GetAsync("/api/v1/inventory/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryQueries.V1.GetCategories.Response>();
        Assert.NotNull(result);
        Assert.Equal(16, result.Categories.Count); // All enum values
        
        var compositeCategory = result.Categories.FirstOrDefault(c => c.Value == InventoryCategory.Composite);
        Assert.NotNull(compositeCategory);
        Assert.Equal("Composite Resins", compositeCategory.DisplayName);
    }

    [Fact]
    public async Task GetUnitTypes_ShouldReturnAllUnitTypes()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        
        // Act
        var response = await _client.GetAsync("/api/v1/inventory/unit-types");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InventoryQueries.V1.GetUnitTypes.Response>();
        Assert.NotNull(result);
        Assert.Equal(17, result.UnitTypes.Count); // All enum values
        
        var syringeUnitType = result.UnitTypes.FirstOrDefault(ut => ut.Value == InventoryUnitType.Syringe);
        Assert.NotNull(syringeUnitType);
        Assert.Equal("Syringe", syringeUnitType.DisplayName);
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var email = $"admin{Guid.NewGuid():N}@test.com"; // Unique email for each test
        await CreateTestUserAsync(email, "TestPassword123", Roles.ADMIN);
        
        var signInCommand = new AuthCommands.V1.SignIn(email, "TestPassword123", false);
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signin", signInCommand);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<AuthResources.V1.TokenResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.AccessToken);
    }

    private async Task<User> CreateTestUserAsync(string email, string password, string role, string firstName = "Test", string lastName = "User")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        // Create test tenant first
        var tenant = new Tenant("Test Clinic", "test-clinic", "US", "America/New_York");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var user = new User(tenant.Id, email, firstName, lastName, role)
        {
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user;
    }
} 