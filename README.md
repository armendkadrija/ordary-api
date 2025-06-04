# Odary API

A modular .NET 9 API built with clean architecture principles, featuring JWT authentication, PostgreSQL integration, and comprehensive validation.

## 🏗️ Architecture

This project follows a **modular architecture** where each business domain is organized as a self-contained module:

### Module Structure
```
src/Odary.Api/Modules/{ModuleName}/
├── {ModuleName}Contracts.cs      # Queries, Commands, and Resources
├── {ModuleName}Mappings.cs       # Extension methods for mapping
├── {ModuleName}Service.cs        # Business logic implementation
├── Validators/                   # FluentValidation validators
└── {ModuleName}ModuleRegistration.cs # DI registration and endpoint mapping
```

### Key Features
- ✅ **Minimal APIs** - No controllers, clean endpoint definitions
- ✅ **Modular Design** - Self-contained business modules
- ✅ **JWT Authentication** - Stateless authentication
- ✅ **FluentValidation** - Service-level validation with ValidationService
- ✅ **PostgreSQL** - Production-ready database with EF Core
- ✅ **Docker Support** - Multi-stage Dockerfile and Docker Compose
- ✅ **CI/CD Pipeline** - GitHub Actions with automated testing
- ✅ **Exception Handling** - Global middleware with consistent error responses
- ✅ **Swagger/OpenAPI** - Auto-generated API documentation

## 🚀 Quick Start

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [PostgreSQL](https://www.postgresql.org/download/) (optional - Docker Compose includes it)

### Using Docker Compose (Recommended)

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd odary-api
   ```

2. **Start the application**
   ```bash
   docker-compose up --build
   ```

3. **Access the API**
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000
   - PostgreSQL: localhost:5432

### Local Development

1. **Start PostgreSQL**
   ```bash
   docker run --name postgres-dev -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=odary_dev -p 5432:5432 -d postgres:16-alpine
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore src/Odary.Api/Odary.Api.csproj
   ```

3. **Update database**
   ```bash
   cd src/Odary.Api
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run --project src/Odary.Api/Odary.Api.csproj
   ```

## 📚 API Documentation

### Authentication

**Sign In**
```http
POST /api/v1/auth/sign-in
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Response**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "guid-string",
  "expiresAt": "2024-01-01T12:00:00Z",
  "tokenType": "Bearer"
}
```

### User Management

**Create User**
```http
POST /api/v1/users
Content-Type: application/json

{
  "email": "newuser@example.com",
  "password": "SecurePassword123!"
}
```

**Get User**
```http
GET /api/v1/users/{id}
Authorization: Bearer {token}
```

**Get Users (with pagination)**
```http
GET /api/v1/users?page=1&pageSize=20&email=search
Authorization: Bearer {token}
```

**Update User**
```http
PUT /api/v1/users/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "email": "updated@example.com"
}
```

**Delete User**
```http
DELETE /api/v1/users/{id}
Authorization: Bearer {token}
```

## 🧪 Testing

### Run Unit Tests
```bash
dotnet test tests/Odary.Api.Tests/
```

### Run with Coverage
```bash
dotnet test tests/Odary.Api.Tests/ --collect:"XPlat Code Coverage"
```

### Integration Tests
The project includes integration tests with Testcontainers for PostgreSQL.

## 🛠️ Development

### Adding a New Module

Follow the [Step-by-Step Guide](technical_requirements.md#step-by-step-guide-for-adding-new-routes) in the technical requirements.

**Quick checklist:**
1. [ ] Create contracts in `{ModuleName}Contracts.cs`
2. [ ] Add validators in `Validators/` folder
3. [ ] Create mapping methods in `{ModuleName}Mappings.cs`
4. [ ] Implement service in `{ModuleName}Service.cs`
5. [ ] Register endpoints in `{ModuleName}ModuleRegistration.cs`
6. [ ] Register dependencies in DI container
7. [ ] Write tests

### Database Migrations

**Add Migration**
```bash
cd src/Odary.Api
dotnet ef migrations add MigrationName
```

**Update Database**
```bash
dotnet ef database update
```

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | See appsettings.json |
| `JwtSettings__SecretKey` | JWT signing key (min 32 chars) | See appsettings.json |
| `JwtSettings__Issuer` | JWT issuer | `Odary.Api` |
| `JwtSettings__Audience` | JWT audience | `Odary.Api.Users` |
| `JwtSettings__ExpiryHours` | Token expiry in hours | `24` |

### Example Environment File
```bash
# .env
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__DefaultConnection=Host=localhost;Database=odary_dev;Username=postgres;Password=postgres;Port=5432
JwtSettings__SecretKey=your-super-secret-key-that-should-be-at-least-32-characters-long
```

## 📦 Project Structure

```
📁 odary-api/
├── 📁 src/
│   └── 📁 Odary.Api/
│       ├── 📁 Common/           # Shared utilities
│       │   ├── 📁 Database/     # DbContext and configurations
│       │   ├── 📁 Exceptions/   # Custom exceptions and middleware
│       │   ├── 📁 Pagination/   # Pagination models
│       │   └── 📁 Validation/   # ValidationService
│       ├── 📁 Domain/           # Domain entities
│       ├── 📁 Modules/          # Business modules
│       │   ├── 📁 Auth/         # Authentication module
│       │   └── 📁 User/         # User management module
│       ├── Program.cs           # Application entry point
│       └── appsettings.json     # Configuration
├── 📁 tests/
│   └── 📁 Odary.Api.Tests/     # Unit and integration tests
├── 📁 .github/workflows/       # CI/CD pipelines
├── Dockerfile                  # Multi-stage Docker build
├── docker-compose.yml          # Local development setup
└── README.md                   # This file
```

## 🚀 Deployment

### Using Docker

**Build Image**
```bash
docker build -t odary-api .
```

**Run Container**
```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=odary;Username=postgres;Password=postgres" \
  -e JwtSettings__SecretKey="your-production-secret-key" \
  odary-api
```

### CI/CD Pipeline

The GitHub Actions workflow automatically:
1. **Tests** - Runs unit and integration tests
2. **Builds** - Creates optimized Docker image
3. **Pushes** - Publishes to GitHub Container Registry
4. **Deploys** - Ready for production deployment

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow the coding standards and add tests
4. Commit your changes (`git commit -m 'Add amazing feature'`)
5. Push to the branch (`git push origin feature/amazing-feature`)
6. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🛡️ Security

- JWT tokens for stateless authentication
- Password hashing with BCrypt
- Input validation with FluentValidation
- Global exception handling
- HTTPS enforcement
- Proper CORS configuration

## 📊 Monitoring

The application includes:
- Structured logging
- Health checks
- Exception tracking
- Performance monitoring endpoints

---

Built with ❤️ using .NET 9, PostgreSQL, and clean architecture principles. 