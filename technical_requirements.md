# .NET Development Guide - Odary API

You are working with an existing .NET 9 boilerplate API that follows clean architecture principles with a modular design.

## Tech Stack (Already Implemented)
- ✅ .NET 9 with Minimal APIs
- ✅ Entity Framework Core with PostgreSQL
- ✅ JWT Authentication
- ✅ FluentValidation with generic ValidationService
- ✅ Swagger/OpenAPI documentation
- ✅ Docker & Docker Compose
- ✅ xUnit testing with NSubstitute
- ✅ GitHub Actions CI/CD

## Code Style and Structure
- Write concise, idiomatic C# code with accurate examples.
- Follow .NET and ASP.NET Core conventions and best practices.
- Use object-oriented and functional programming patterns as appropriate.
- Prefer LINQ and lambda expressions for collection operations.
- Use descriptive variable and method names (e.g., 'IsUserSignedIn', 'CalculateTotal').
- Follow the established modular architecture pattern.
- **Use primary constructors** - Prefer primary constructors over traditional constructors for cleaner, more concise code
- **Use modern C# features** - Leverage record types, pattern matching, null-coalescing assignment, file-scoped namespaces, and init-only properties

## Modular Architecture Pattern
The project follows a modular architecture where each business domain is organized as a self-contained module:

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
        // Register validation service (shared across modules)
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
- **Service-level validation** - Validation performed using the existing ValidationService
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

## Validation Pattern (Already Implemented)
The project uses a generic ValidationService that automatically finds validators:

```csharp
// In your service methods, use the existing ValidationService
public async Task<UserResources.V1.User> CreateUserAsync(UserCommands.V1.CreateUser command, CancellationToken token)
{
    await _validationService.ValidateAsync(command, token);
    
    // Business logic continues...
}
```

### ValidationException Pattern
```csharp
public class ValidationException(List<ValidationFailure> errors) : Exception
{
    public List<ValidationFailure> Errors { get; } = errors;

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

## Database Conventions (Already Configured)
- PostgreSQL database with EF Core
- Snake_case naming convention for tables and columns (auto-configured)
- Connection pooling enabled
- Migrations using EF Core

## Authentication (Already Implemented)
- JWT authentication with proper middleware setup
- Token generation in AuthService
- Authorization attributes available for endpoints

## Performance & Best Practices
- Use asynchronous programming with async/await for I/O-bound operations.
- Use the existing pagination classes: `PaginatedRequest` and `PaginatedResponse<T>`
- Use efficient LINQ queries and avoid N+1 query problems.
- Follow the existing exception handling patterns.

## Testing Patterns (Already Set Up)
- Use xUnit for unit tests
- Use NSubstitute for mocking (see existing UserServiceTests example)
- Use the in-memory database for service testing
- Integration tests available with Testcontainers

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
public class UserService(IValidationService validationService, OdaryDbContext dbContext) : IUserService
{
    // Add new service method
    public async Task<UserResources.V1.ArchivedUser> ArchiveUserAsync(
        UserCommands.V1.ArchiveUser command, 
        CancellationToken cancellationToken = default)
    {
        await validationService.ValidateAsync(command, cancellationToken);
        
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == command.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"User with ID {command.Id} not found");
        
        user.Archive(command.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        
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
    // Register validation service (already exists)
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
Create unit tests following the existing patterns in `tests/Odary.Api.Tests/Unit/`

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
