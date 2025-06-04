# .NET Development Rules

You are a senior .NET backend developer and an expert in C#, ASP.NET Core, and Entity Framework Core.

## Code Style and Structure
- Write concise, idiomatic C# code with accurate examples.
- Follow .NET and ASP.NET Core conventions and best practices.
- Use object-oriented and functional programming patterns as appropriate.
- Prefer LINQ and lambda expressions for collection operations.
- Use descriptive variable and method names (e.g., 'IsUserSignedIn', 'CalculateTotal').
- Structure files according to .NET conventions (Controllers, Models, Services, etc.).

## Project Structure
- API project should be placed under `src/` folder.
- Test projects should be placed under `tests/` folder.
- Follow clean architecture principles with proper separation of concerns.
- **Use modular architecture pattern** - see Modular Architecture section below.

## Modular Architecture Pattern
The project MUST follow a modular architecture where each business domain (User, Auth, Device, etc.) is organized as a self-contained module:

### Module Structure
Each module should be organized under `src/Odary.Api/Modules/{ModuleName}/` with:
- **{ModuleName}Contracts.cs** - All contracts including queries, commands, and resources with versioned nested classes (V1, V2, etc.)
- **{ModuleName}Mappings.cs** - Extension methods for mapping between domain models and contracts
- **{ModuleName}Service.cs** - Business logic implementation (interface should also be included in this file initially)
- **Validators/** - FluentValidation validators for requests/commands (validation logic separate from contracts)
- **{ModuleName}ModuleRegistration.cs** - DI registration and API endpoint mapping

### Contract Structure Pattern
All contracts should be contained in a single `{ModuleName}Contracts.cs` file following this versioned pattern:
```csharp
// UserContracts.cs
public class UserQueries
{
    public class V1
    {
        public record GetUser(string Id)
        {
            public record Response
            {
                public string Id { get; init; }
                public string Email { get; init; }
                public DateTimeOffset CreatedAt { get; init; }
            }
        }

        public class GetUsers : PaginatedRequest
        {
            public string? Email { get; set; }
            
            public class Response : PaginatedResponse<UserResources.V1.User>;
        }
    }
}

public class UserCommands
{
    public class V1
    {
        public record CreateUser(string Email, string Password);
        public record UpdateUser(string Id, string Email);
        public record DeleteUser(string Id);
    }
}

public class UserResources
{
    public class V1
    {
        public record User
        {
            public string Id { get; init; }
            public string Email { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? UpdatedAt { get; init; }
        }
    }
}
```

### Mapping Pattern
Services map from domain models to contracts using extension methods:
```csharp
public static class UserMappings
{
    public static UserResources.V1.User ToContract(this Domain.User user)
    {
        return new UserResources.V1.User
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public static UserQueries.V1.GetUser.Response ToGetUserResponse(this Domain.User user)
    {
        return new UserQueries.V1.GetUser.Response
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };
    }
}
```

### Module Registration Pattern
Each module must implement self-registration:
```csharp
public static class UserModuleRegistration
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        // Register validation service (can be singleton - shared across modules)
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
        // Map all endpoints for this module
        return app;
    }
}
```

### Contract Design Rules
- **No validation logic in contracts** - Contracts are pure data structures
- **Service-level validation** - Validation performed explicitly in service methods using FluentValidation
- **No DTOs needed** - Services map directly from domain models to contracts using mapping extensions
- **Versioned structure** - All contracts organized under V1, V2, etc. nested classes for API versioning
- **Separation of concerns** - Queries, Commands, and Resources are in separate classes within the contracts file
- **Mapping responsibility** - All mapping logic isolated in {ModuleName}Mappings.cs files

### Naming Conventions for Modules
- Use consistent prefixes: `{ModuleName}Contracts`, `{ModuleName}Mappings`, `{ModuleName}Service`, etc.
- API endpoints use kebab-case: `/api/v1/user-profile`, `/api/v1/auth/sign-in`
- Group related endpoints under same route prefix
- Use versioned APIs: `/api/v1/`, `/api/v2/`, etc.
- Nested classes for versioning: `V1`, `V2`, etc.

### Module Dependencies
- Modules should be loosely coupled
- Cross-module dependencies should go through well-defined interfaces
- Shared utilities in `src/Odary.Api/Common/`

### Example Module Structure:
```
src/Odary.Api/Modules/
├── User/
│   ├── UserContracts.cs
│   ├── UserMappings.cs
│   ├── UserService.cs
│   ├── Validators/
│   │   ├── CreateUserCommandValidator.cs
│   │   └── UpdateUserCommandValidator.cs
│   └── UserModuleRegistration.cs
```

## Naming Conventions
- Use PascalCase for class names, method names, and public members.
- Use camelCase for local variables and private fields.
- Use UPPERCASE for constants.
- Prefix interface names with "I" (e.g., 'IUserService').

## C# and .NET Usage
- Use the latest C# syntax and features (C# 12+ when available).
- Leverage modern C# features: record types, pattern matching, null-coalescing assignment, file-scoped namespaces, global using statements.
- Use minimal APIs and top-level programs when appropriate.
- Leverage built-in ASP.NET Core features and middleware.
- Use Entity Framework Core effectively for database operations.
- Prefer primary constructors and init-only properties for immutable objects.

## Database
- Use PostgreSQL as the primary database.
- Use Entity Framework Core with Npgsql provider for PostgreSQL integration.
- Implement database migrations using EF Core migrations.
- Use proper indexing strategies for PostgreSQL.
- Follow PostgreSQL naming conventions (snake_case for table and column names).
- Use connection pooling for optimal database performance.

## Syntax and Formatting
- Follow the C# Coding Conventions (https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use C#'s expressive syntax (e.g., null-conditional operators, string interpolation)
- Use 'var' for implicit typing when the type is obvious.

## Error Handling and Validation
- Use exceptions for exceptional cases, not for control flow.
- Implement proper error logging using built-in .NET logging or a third-party logger.
- **Service-level validation**: Use generic ValidationService to automatically find and execute validators based on request type
- Use custom ValidationException for consistent validation error handling
- Validation exception pattern:
```csharp
public class ValidationException(List<ValidationFailure> errors) : Exception
{
    List<ValidationFailure> Errors { get; } = errors;

    public object GetValidationErrors()
        => Errors.Select(
                error => new {
                    Field = error.PropertyName,
                    Error = error.ErrorMessage
                }
            )
            .ToList();
}
```
- Generic validation service pattern:
```csharp
public interface IValidationService
{
    Task ValidateAsync<T>(T request, CancellationToken cancellationToken = default);
}

public class ValidationService : IValidationService
{
    private readonly IServiceProvider _serviceProvider;
    
    public ValidationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task ValidateAsync<T>(T request, CancellationToken cancellationToken = default)
    {
        var validator = _serviceProvider.GetService<IValidator<T>>();
        if (validator == null) return; // No validator registered, skip validation
        
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid) throw new ValidationException(result.Errors);
    }
}
```
- Service validation usage pattern:
```csharp
public class UserService : IUserService
{
    private readonly IValidationService _validationService;
    private readonly IUserRepository _userRepository;
    
    public UserService(IValidationService validationService, IUserRepository userRepository)
    {
        _validationService = validationService;
        _userRepository = userRepository;
    }

    public async Task<UserResources.V1.User> CreateUserAsync(UserCommands.V1.CreateUser command, CancellationToken token)
    {
        await _validationService.ValidateAsync(command, token);
        
        // Business logic continues...
        var user = new Domain.User(command.Email, command.Password);
        await _userRepository.AddAsync(user, token);
        return user.ToContract();
    }
}
```
- Implement global exception handling middleware to catch ValidationException and return appropriate responses.
- Return appropriate HTTP status codes and consistent error responses.

## API Design
- Follow RESTful API design principles.
- Use attribute routing in controllers.
- Implement versioning for your API.
- Use action filters for cross-cutting concerns.

## Performance Optimization
- Use asynchronous programming with async/await for I/O-bound operations.
- Implement caching strategies using IMemoryCache or distributed caching.
- Use efficient LINQ queries and avoid N+1 query problems.
- Implement pagination for large data sets.

## Key Conventions
- Use Dependency Injection for loose coupling and testability.
- Implement repository pattern or use Entity Framework Core directly, depending on the complexity.
- Use manual mapping with extension methods in {ModuleName}Mappings.cs files (no AutoMapper dependency).
- Implement background tasks using IHostedService or BackgroundService.

## Testing
- Write unit tests using xUnit.
- Use NSubstitute for mocking dependencies.
- Implement integration tests for API endpoints.
- Use test containers for database integration tests with PostgreSQL.

## Security
- Use Authentication and Authorization middleware.
- Implement JWT authentication for stateless API authentication.
- Use HTTPS and enforce SSL.
- Implement proper CORS policies.

## CI/CD and Deployment
- Use Docker for containerization of the application.
- Create multi-stage Dockerfile for optimized builds.
- Use GitHub Actions for CI/CD pipeline automation.
- Pipeline should include the following stages:
  - Build: Restore dependencies and compile the application
  - Test: Run unit tests and integration tests
  - Deploy: Build Docker image and deploy to target environment
- Use Docker Compose for local development with PostgreSQL.
- Implement proper environment-specific configuration management.
- Use secrets management for sensitive configuration data.

## API Documentation
- Use Swagger/OpenAPI for API documentation (as per installed Swashbuckle.AspNetCore package).
- Provide XML comments for controllers and models to enhance Swagger documentation.

Follow the official Microsoft documentation and ASP.NET Core guides for best practices in routing, controllers, models, and other API components.

## Explicit Requirements - MUST FOLLOW EXACTLY

### Required Technologies (ONLY THESE):
- .NET 9 (latest)
- Minimal APIs (NOT Controllers)
- Entity Framework Core
- PostgreSQL with Npgsql
- JWT Authentication
- Swagger/OpenAPI
- xUnit for testing
- Docker & Docker Compose
- GitHub Actions CI/CD

### Architecture Constraints:
- Use Minimal APIs with endpoint mapping
- No controller classes
- Keep dependencies minimal and only use what's explicitly requested

## Implementation Rules
- Only implement features and packages explicitly mentioned in requirements
- **For any additional packages/features:** ASK FIRST and EXPLAIN WHY it would be beneficial
- Ask for clarification if requirements are unclear
- Prefer simpler solutions over feature-rich ones when not specified
- Follow the EXACT technology stack specified

### Examples of when to ask:
- "Should I add FluentValidation for request validation? It provides more flexible validation than Data Annotations but is an additional package."
- "Should I add Serilog for structured logging? It offers better logging capabilities than the built-in logger but adds complexity."
- "Should I add MediatR for CQRS pattern implementation? It would provide better separation of concerns but adds a dependency."

## Step-by-Step Guide for Adding New Routes

When adding a new endpoint to an existing module, follow these steps in order:

### 1. **Add Contract Definitions**
In `{ModuleName}Contracts.cs`, add the appropriate contract:

**For Commands (POST/PUT/DELETE):**
```csharp
public class UserCommands
{
    public class V1
    {
        // Add your new command
        public record ArchiveUser(string Id, string Reason);
    }
}
```

**For Queries (GET):**
```csharp
public class UserQueries
{
    public class V1
    {
        // Add your new query
        public record GetArchivedUsers(int Page = 1, int PageSize = 20)
        {
            public record Response(List<UserResources.V1.User> Users, int TotalCount);
        }
    }
}
```

**For Resources (if new data structures needed):**
```csharp
public class UserResources
{
    public class V1
    {
        // Add new resource models if needed
        public record ArchivedUser
        {
            public string Id { get; init; }
            public string Reason { get; init; }
            public DateTimeOffset ArchivedAt { get; init; }
        }
    }
}
```

### 2. **Add Validation (if required)**
In `Validators/` folder, create validator for commands/queries that need validation:

```csharp
// Validators/ArchiveUserValidator.cs
public class ArchiveUserValidator : AbstractValidator<UserCommands.V1.ArchiveUser>
{
    public ArchiveUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("User ID is required");
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).WithMessage("Archive reason must be at least 10 characters");
    }
}
```

### 3. **Add Mapping Methods (if needed)**
In `{ModuleName}Mappings.cs`, add extension methods for new mappings:

```csharp
public static class UserMappings
{
    // Add new mapping methods
    public static UserResources.V1.ArchivedUser ToArchivedUserContract(this Domain.User user, string reason)
    {
        return new UserResources.V1.ArchivedUser
        {
            Id = user.Id,
            Reason = reason,
            ArchivedAt = DateTimeOffset.UtcNow
        };
    }
}
```

### 4. **Add Service Method**
In `{ModuleName}Service.cs`, implement the business logic:

```csharp
public class UserService : IUserService
{
    private readonly IValidationService _validationService;
    private readonly IUserRepository _userRepository;

    // Add new service method
    public async Task<UserResources.V1.ArchivedUser> ArchiveUserAsync(
        UserCommands.V1.ArchiveUser command, 
        CancellationToken cancellationToken = default)
    {
        await _validationService.ValidateAsync(command, cancellationToken);
        
        var user = await _userRepository.GetByIdAsync(command.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"User with ID {command.Id} not found");
        
        user.Archive(command.Reason);
        await _userRepository.SaveChangesAsync(cancellationToken);
        
        return user.ToArchivedUserContract(command.Reason);
    }
}

// Don't forget to add method to interface
public interface IUserService
{
    Task<UserResources.V1.ArchivedUser> ArchiveUserAsync(UserCommands.V1.ArchiveUser command, CancellationToken cancellationToken = default);
}
```

### 5. **Register Endpoint**
In `{ModuleName}ModuleRegistration.cs`, map the new endpoint:

```csharp
public static WebApplication MapUserEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/api/v1/users").WithTags("Users");
    
    // Add new endpoint mapping
    group.MapPost("/{id}/archive", async (
        string id,
        [FromBody] ArchiveUserRequest request,
        IUserService userService,
        CancellationToken cancellationToken) =>
    {
        var command = new UserCommands.V1.ArchiveUser(id, request.Reason);
        var result = await userService.ArchiveUserAsync(command, cancellationToken);
        return Results.Ok(result);
    })
    .WithName("ArchiveUser")
    .WithSummary("Archive a user")
    .Produces<UserResources.V1.ArchivedUser>();
    
    return app;
}

// Simple request model for minimal API binding
public record ArchiveUserRequest(string Reason);
```

### 6. **Register Dependencies**
In `{ModuleName}ModuleRegistration.cs`, register any new validators:

```csharp
public static IServiceCollection AddUserModule(this IServiceCollection services)
{
    // Register validation service
    services.AddSingleton<IValidationService, ValidationService>();
    
    // Register existing validators
    services.AddScoped<IValidator<UserCommands.V1.CreateUser>, CreateUserValidator>();
    
    // Add new validator registration
    services.AddScoped<IValidator<UserCommands.V1.ArchiveUser>, ArchiveUserValidator>();
    
    // Register services
    services.AddScoped<IUserService, UserService>();
    
    return services;
}
```

### 7. **Test the Implementation**
Create unit tests for:
- Validator (if created)
- Service method
- Endpoint mapping (integration test)

### **Checklist for New Routes:**
- [ ] Contract defined in `{ModuleName}Contracts.cs`
- [ ] Validator created and registered (if validation needed)
- [ ] Mapping methods added (if new mappings needed)
- [ ] Service method implemented in `{ModuleName}Service.cs`
- [ ] Interface updated with new method signature
- [ ] Endpoint mapped in `{ModuleName}ModuleRegistration.cs`
- [ ] Dependencies registered in DI container
- [ ] Tests written for new functionality

### **Naming Conventions:**
- **Endpoints**: Use kebab-case `/api/v1/users/{id}/archive`
- **Methods**: Use descriptive names `ArchiveUserAsync`
- **Contracts**: Follow pattern `{Action}{Entity}` like `ArchiveUser`
- **Validators**: Follow pattern `{ContractName}Validator` like `ArchiveUserValidator`
