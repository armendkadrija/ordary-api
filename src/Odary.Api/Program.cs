using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Services;
using Odary.Api.Modules.Auth;
using Odary.Api.Modules.Inventory;
using Odary.Api.Modules.Patient;
using Odary.Api.Modules.Tenant;
using Odary.Api.Modules.User;
using Odary.Api.Extensions;
using Odary.Api.Infrastructure.Database;
using Odary.Api.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddEndpointsApiExplorer()
    .RegisterSwagger()
    .AddHttpContextAccessor()
    .AddScoped<ICurrentUserService, CurrentUserService>()
    .AddScoped<IAuditService, AuditService>();

// Add Redis for distributed caching
services.AddStackExchangeRedisCache(options => { options.Configuration = builder.Configuration.GetConnectionString("Redis"); });

// Add database context
services.AddDbContext<OdaryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
            o => o.MigrationsHistoryTable("__migrations_history"))
        .UseSnakeCaseNamingConvention());

services.RegisterAuthorization(configuration);

// Add modules
services.AddAuthModule()
    .AddEmailModule(builder.Configuration)
    .AddInventoryModule()
    .AddPatientModule()
    .AddTenantModule()
    .AddUserModule();

var app = builder.Build();

// Add exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>()
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .UseSwagger()
    .UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Odary API V1");
        c.RoutePrefix = string.Empty;
    });

// Map module endpoints
app.MapAuthEndpoints()
    .MapInventoryEndpoints()
    .MapPatientEndpoints()
    .MapTenantEndpoints()
    .MapUserEndpoints();

await app.Services.GetRequiredService<IDatabaseSeeder>().SeedAsync();

app.Run();

// Make Program class accessible for testing
public partial class Program;