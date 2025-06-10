using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Odary.Api.Common.Database;
using Odary.Api.Common.Exceptions;
using Odary.Api.Common.Authorization;
using Odary.Api.Common.Services;
using Odary.Api.Modules.Auth;
using Odary.Api.Modules.Tenant;
using Odary.Api.Modules.User;
using Odary.Api.Extensions;
using Odary.Api.Infrastructure.Database;
using Odary.Api.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// Add services to the container
services.AddEndpointsApiExplorer();
services.RegisterSwagger();
// Add HTTP context accessor for audit logging and current user service
services.AddHttpContextAccessor();

// Add services
services.AddScoped<ICurrentUserService, CurrentUserService>()
    .AddScoped<IAuditService, AuditService>();

// Add Redis for distributed caching
services.AddStackExchangeRedisCache(options => { options.Configuration = builder.Configuration.GetConnectionString("Redis"); });

// Add database context
services.AddDbContext<OdaryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), options => options.MigrationsHistoryTable("__migrations_history"))
        .UseSnakeCaseNamingConvention());

services.RegisterAuthorization(configuration);
services.AddScoped<IAuthorizationHandler, ClaimAuthorizationHandler>();

// Add claims service for role-based claim management
services.AddScoped<IClaimsService, ClaimsService>();

// Add database seeder
services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

// Add modules
services.AddAuthModule()
    .AddEmailModule(builder.Configuration)
    .AddTenantModule()
    .AddUserModule();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Odary API V1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
});

// Add exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Seed database and claims (skip in Testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    var seeder = app.Services.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync();
}

// Map module endpoints
app.MapAuthEndpoints()
    .MapTenantEndpoints()
    .MapUserEndpoints();

app.Run();