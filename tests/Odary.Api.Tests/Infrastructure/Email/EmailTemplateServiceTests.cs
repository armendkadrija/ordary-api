using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Odary.Api.Infrastructure.Email;
using System.IO;
using Xunit;

namespace Odary.Api.Tests.Infrastructure.Email;

public class EmailTemplateServiceTests : IDisposable
{
    private readonly IWebHostEnvironment _mockEnvironment;
    private readonly ILogger<EmailTemplateService> _mockLogger;
    private readonly string _tempDirectory;
    private readonly string _templatesDirectory;
    private readonly EmailTemplateService _service;

    public EmailTemplateServiceTests()
    {
        _mockEnvironment = Substitute.For<IWebHostEnvironment>();
        _mockLogger = Substitute.For<ILogger<EmailTemplateService>>();

        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _templatesDirectory = Path.Combine(_tempDirectory, "Templates", "Email");
        Directory.CreateDirectory(_templatesDirectory);

        _mockEnvironment.ContentRootPath.Returns(_tempDirectory);

        _service = new EmailTemplateService(_mockEnvironment, _mockLogger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public async Task RenderTemplateAsync_WithValidTemplate_RendersCorrectly()
    {
        // Arrange
        var templateContent = @"
            Hello {{ first_name }} {{ last_name }}!

            Your email is: {{ email }}
            Current year: {{ current_year }}
            ";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "test-template.scriban"), templateContent);

        var model = new
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
        };

        // Act
        var result = await _service.RenderTemplateAsync("test-template", model);

        // Assert
        Assert.Contains("Hello John Doe!", result);
        Assert.Contains("Your email is: john.doe@example.com", result);
        Assert.Contains($"Current year: {DateTime.UtcNow.Year}", result);
    }

    [Fact]
    public void RenderTemplate_WithValidTemplate_RendersCorrectly()
    {
        // Arrange
        var templateContent = @"
            Welcome {{ first_name }}!

            {{ if is_admin }}
            You have admin privileges.
            {{ else }}
            You are a regular user.
            {{ end }}
            ";
        File.WriteAllText(Path.Combine(_templatesDirectory, "welcome-template.scriban"), templateContent);

        var adminModel = new { FirstName = "Admin", IsAdmin = true };
        var userModel = new { FirstName = "User", IsAdmin = false };

        // Act
        var adminResult = _service.RenderTemplate("welcome-template", adminModel);
        var userResult = _service.RenderTemplate("welcome-template", userModel);

        // Assert
        Assert.Contains("Welcome Admin!", adminResult);
        Assert.Contains("You have admin privileges.", adminResult);

        Assert.Contains("Welcome User!", userResult);
        Assert.Contains("You are a regular user.", userResult);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithPreFormattedData_RendersCorrectly()
    {
        // Arrange - Pre-format complex data in C# instead of using template functions
        var templateContent = @"
            Date: {{ formatted_date }}
            URL: {{ encoded_url }}
            Current Year: {{ current_year }}
            Name: {{ first_name }} {{ last_name }}
            ";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "preformatted-template.scriban"), templateContent);

        var birthDate = new DateTime(1990, 5, 15);
        var searchTerm = "hello world & more";

        var model = new
        {
            FirstName = "John",
            LastName = "Doe",
            FormattedDate = birthDate.ToString("yyyy-MM-dd"), // Pre-format in C#
            EncodedUrl = System.Web.HttpUtility.UrlEncode(searchTerm) // Pre-encode in C#
        };

        // Act
        var result = await _service.RenderTemplateAsync("preformatted-template", model);

        // Assert
        Assert.Contains("Date: 1990-05-15", result);
        Assert.Contains("URL: hello+world+%26+more", result);
        Assert.Contains($"Current Year: {DateTime.UtcNow.Year}", result);
        Assert.Contains("Name: John Doe", result);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithNonExistentTemplate_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RenderTemplateAsync("non-existent", new { }));

        Assert.Contains("Failed to render email template 'non-existent'", exception.Message);
        Assert.Contains("Email template not found: non-existent", exception.Message);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithInvalidTemplate_HandlesGracefully()
    {
        // Arrange - Create template with syntax error that Scriban will catch
        var invalidTemplateContent = "{{ for item in }}{{ end }}"; // Invalid for loop syntax
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "invalid-template.scriban"), invalidTemplateContent);

        // Act & Assert - This should either throw an exception or handle gracefully
        try
        {
            var result = await _service.RenderTemplateAsync("invalid-template", new { });
            // If no exception is thrown, the template was handled gracefully
            Assert.NotNull(result);
        }
        catch (InvalidOperationException ex)
        {
            // If an exception is thrown, verify it's the expected one
            Assert.Contains("Failed to render email template 'invalid-template'", ex.Message);
        }
    }

    [Fact]
    public async Task RenderTemplateAsync_WithMultipleTemplates_LoadsAllCorrectly()
    {
        // Arrange
        var template1 = "Template 1: {{ name }}";
        var template2 = "Template 2: {{ title }}";
        var template3 = "Template 3: {{ message }}";

        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "template1.scriban"), template1);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "template2.scriban"), template2);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "template3.scriban"), template3);

        // Act
        var result1 = await _service.RenderTemplateAsync("template1", new { Name = "Test1" });
        var result2 = await _service.RenderTemplateAsync("template2", new { Title = "Test2" });
        var result3 = await _service.RenderTemplateAsync("template3", new { Message = "Test3" });

        // Assert
        Assert.Equal("Template 1: Test1", result1);
        Assert.Equal("Template 2: Test2", result2);
        Assert.Equal("Template 3: Test3", result3);
    }

    [Fact]
    public async Task RenderTemplateAsync_ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var templateContent = "Hello {{ name }} - {{ thread_id }}";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "concurrent-template.scriban"), templateContent);

        var tasks = new List<Task<string>>();

        // Act - Create multiple concurrent requests
        for (int i = 0; i < 10; i++)
        {
            var threadId = i;
            tasks.Add(_service.RenderTemplateAsync("concurrent-template", new { Name = $"User{threadId}", ThreadId = threadId }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.Contains($"Hello User{i} - {i}", results[i]);
        }
    }

    [Fact]
    public async Task RenderTemplateAsync_WithNullModel_HandlesGracefully()
    {
        // Arrange
        var templateContent = "Static content without variables";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "static-template.scriban"), templateContent);

        // Act
        var result = await _service.RenderTemplateAsync("static-template", null!);

        // Assert
        Assert.Equal("Static content without variables", result);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithComplexModel_RendersCorrectly()
    {
        // Arrange
        var templateContent = @"
            User: {{ user.first_name }} {{ user.last_name }}
            Email: {{ user.email }}
            Roles: {{ for role in user.roles }}{{ role }}{{ if !for.last }}, {{ end }}{{ end }}
            Settings:
              - Language: {{ user.settings.language }}
              - Timezone: {{ user.settings.timezone }}
            ";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "complex-template.scriban"), templateContent);

        var model = new
        {
            User = new
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Roles = new[] { "Admin", "User", "Editor" },
                Settings = new
                {
                    Language = "en-US",
                    Timezone = "UTC"
                }
            }
        };

        // Act
        var result = await _service.RenderTemplateAsync("complex-template", model);

        // Assert
        Assert.Contains("User: Jane Smith", result);
        Assert.Contains("Email: jane.smith@example.com", result);
        Assert.Contains("Roles: Admin, User, Editor", result);
        Assert.Contains("Language: en-US", result);
        Assert.Contains("Timezone: UTC", result);
    }

    [Fact]
    public async Task ReloadTemplatesAsync_AfterTemplateChange_LoadsNewContent()
    {
        // Arrange
        var originalContent = "Original: {{ name }}";
        var templatePath = Path.Combine(_templatesDirectory, "reload-template.scriban");
        await File.WriteAllTextAsync(templatePath, originalContent);

        // Act - First render
        var originalResult = await _service.RenderTemplateAsync("reload-template", new { Name = "Test" });

        // Change template content
        var newContent = "Updated: {{ name }}";
        await File.WriteAllTextAsync(templatePath, newContent);

        // Reload templates
        await _service.ReloadTemplatesAsync();

        // Render again
        var newResult = await _service.RenderTemplateAsync("reload-template", new { Name = "Test" });

        // Assert
        Assert.Equal("Original: Test", originalResult);
        Assert.Equal("Updated: Test", newResult);
    }

    [Fact]
    public async Task RenderTemplateAsync_WithMissingTemplatesDirectory_HandlesGracefully()
    {
        // Arrange - Point to non-existent directory
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var mockEnv = Substitute.For<IWebHostEnvironment>();
        var mockLogger = Substitute.For<ILogger<EmailTemplateService>>();
        mockEnv.ContentRootPath.Returns(nonExistentPath);

        var serviceWithMissingDir = new EmailTemplateService(mockEnv, mockLogger);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => serviceWithMissingDir.RenderTemplateAsync("any-template", new { }));

        Assert.Contains("Failed to render email template 'any-template'", exception.Message);
    }

    [Fact]
    public async Task RenderTemplateAsync_PropertyNameConversion_ConvertsToSnakeCase()
    {
        // Arrange
        var templateContent = @"
            First Name: {{ first_name }}
            Last Name: {{ last_name }}
            Email Address: {{ email_address }}
            Is Admin: {{ is_admin }}
            User ID: {{ user_id }}
            ";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "snake-case-template.scriban"), templateContent);

        var model = new
        {
            FirstName = "John",
            LastName = "Doe",
            EmailAddress = "john.doe@example.com",
            IsAdmin = true,
            UserId = 12345
        };

        // Act
        var result = await _service.RenderTemplateAsync("snake-case-template", model);

        // Assert
        Assert.Contains("First Name: John", result);
        Assert.Contains("Last Name: Doe", result);
        Assert.Contains("Email Address: john.doe@example.com", result);
        Assert.Contains("Is Admin: true", result);
        Assert.Contains("User ID: 12345", result);
    }

    [Fact]
    public async Task RenderTemplateAsync_LazyLoading_InitializesOnFirstCall()
    {
        // Arrange
        var templateContent = "Lazy loaded: {{ message }}";
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "lazy-template.scriban"), templateContent);

        // Act - First call should trigger initialization
        var result = await _service.RenderTemplateAsync("lazy-template", new { Message = "Success" });

        // Assert
        Assert.Equal("Lazy loaded: Success", result);
    }
}