using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Common.Database;
using Odary.Api.Infrastructure.Email;

namespace Odary.Api.Tests.TestFixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string _databaseName = "TestDatabase_" + Guid.NewGuid().ToString("N")[..8];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Create a new service collection to completely rebuild the database configuration
            var servicesToRemove = services.Where(service =>
                service.ServiceType.FullName?.Contains("EntityFramework") == true
                || service.ServiceType == typeof(OdaryDbContext)
                || service.ServiceType == typeof(IDistributedCache) 
                || service.ServiceType == typeof(IEmailService)
            ).ToList();

            foreach (var service in servicesToRemove)
            {
                services.Remove(service);
            }

            // Add in-memory database with static name shared across the test class
            services.AddDbContext<OdaryDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Replace distributed cache with in-memory version
            services.AddSingleton<IDistributedCache, Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache>();

            // Mock the email service
            var mockEmailService = Substitute.For<IEmailService>();
            services.AddSingleton(mockEmailService);

            // Set up logging to capture test output
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddConsole();
                loggingBuilder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in tests
            });
        });

        builder.UseEnvironment("Testing");
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OdaryDbContext>();

        // Clear all data for fresh test state
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}