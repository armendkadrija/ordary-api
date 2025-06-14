using Scriban;
using Scriban.Runtime;
using Scriban.Functions;
using System.Collections.Concurrent;

namespace Odary.Api.Infrastructure.Email;

public interface IEmailTemplateService
{
    Task<string> RenderTemplateAsync(string templateName, object model, CancellationToken cancellationToken = default);
    string RenderTemplate(string templateName, object model);
    Task ReloadTemplatesAsync(); // For development/testing
}

public class EmailTemplateService(IWebHostEnvironment environment, ILogger<EmailTemplateService> logger) : IEmailTemplateService
{
    private readonly string _templatesPath = Path.Combine(environment.ContentRootPath, "Templates", "Email");
    private readonly ConcurrentDictionary<string, Template> _compiledTemplates = new();
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);
    private volatile bool _isInitialized = false;

    public async Task<string> RenderTemplateAsync(string templateName, object model, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();
        return await Task.Run(() => RenderTemplate(templateName, model), cancellationToken);
    }

    public string RenderTemplate(string templateName, object model)
    {
        EnsureInitialized();
        
        try
        {
            var template = GetTemplate(templateName);
            
            // Create template context with the model
            var templateContext = new TemplateContext();
            var scriptObject = new ScriptObject();
            
            // Add model properties to script object
            if (model != null)
            {
                foreach (var property in model.GetType().GetProperties())
                {
                    var value = property.GetValue(model);
                    scriptObject.SetValue(ToSnakeCase(property.Name), value, true);
                }
            }
            
            templateContext.PushGlobal(scriptObject);
            
            // Add helper functions
            AddHelperFunctions(scriptObject);
            
            var result = template.Render(templateContext);
            
            logger.LogDebug("Successfully rendered template: {TemplateName}", templateName);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to render template: {TemplateName}", templateName);
            throw new InvalidOperationException($"Failed to render email template '{templateName}': {ex.Message}", ex);
        }
    }

    public async Task ReloadTemplatesAsync()
    {
        logger.LogInformation("Reloading email templates...");
        
        await _initializationSemaphore.WaitAsync();
        try
        {
            _compiledTemplates.Clear();
            _isInitialized = false;
            await LoadAllTemplatesAsync();
            _isInitialized = true;
            
            logger.LogInformation("Email templates reloaded successfully. Loaded {Count} templates.", _compiledTemplates.Count);
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                await LoadAllTemplatesAsync();
                _isInitialized = true;
                logger.LogInformation("Email templates initialized on first use. Loaded {Count} templates.", _compiledTemplates.Count);
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;

        _initializationSemaphore.Wait();
        try
        {
            if (!_isInitialized)
            {
                LoadAllTemplates();
                _isInitialized = true;
                logger.LogInformation("Email templates initialized on first use. Loaded {Count} templates.", _compiledTemplates.Count);
            }
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    private async Task LoadAllTemplatesAsync()
    {
        if (!Directory.Exists(_templatesPath))
        {
            logger.LogWarning("Email templates directory not found: {Path}", _templatesPath);
            return;
        }

        var templateFiles = Directory.GetFiles(_templatesPath, "*.scriban", SearchOption.TopDirectoryOnly);
        
        var loadTasks = templateFiles.Select(async filePath =>
        {
            var templateName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            
            try
            {
                var templateContent = await File.ReadAllTextAsync(filePath);
                var template = Template.Parse(templateContent);
                
                if (template.HasErrors)
                {
                    var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                    logger.LogError("Template compilation errors in '{TemplateName}': {Errors}", templateName, errors);
                    return;
                }

                _compiledTemplates.TryAdd(templateName, template);
                logger.LogDebug("Loaded template: {TemplateName}", templateName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load template: {TemplateName} from {FilePath}", templateName, filePath);
            }
        });

        await Task.WhenAll(loadTasks);
    }

    private void LoadAllTemplates()
    {
        if (!Directory.Exists(_templatesPath))
        {
            logger.LogWarning("Email templates directory not found: {Path}", _templatesPath);
            return;
        }

        var templateFiles = Directory.GetFiles(_templatesPath, "*.scriban", SearchOption.TopDirectoryOnly);
        
        Parallel.ForEach(templateFiles, filePath =>
        {
            var templateName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            
            try
            {
                var templateContent = File.ReadAllText(filePath);
                var template = Template.Parse(templateContent);
                
                if (template.HasErrors)
                {
                    var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                    logger.LogError("Template compilation errors in '{TemplateName}': {Errors}", templateName, errors);
                    return;
                }

                _compiledTemplates.TryAdd(templateName, template);
                logger.LogDebug("Loaded template: {TemplateName}", templateName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load template: {TemplateName} from {FilePath}", templateName, filePath);
            }
        });
    }

    private Template GetTemplate(string templateName)
    {
        var templateKey = templateName.ToLowerInvariant();
        
        if (_compiledTemplates.TryGetValue(templateKey, out var template))
        {
            return template;
        }

        throw new FileNotFoundException($"Email template not found: {templateName}. Available templates: {string.Join(", ", _compiledTemplates.Keys)}");
    }

    private static void AddHelperFunctions(ScriptObject scriptObject)
    {
        // Add current year helper - simple values work reliably with Scriban
        scriptObject.SetValue("current_year", DateTime.UtcNow.Year, true);
        
        // Note: For complex formatting (dates, URL encoding, etc.), it's more reliable
        // to pre-format the data in C# before passing to the template rather than
        // using Scriban functions which have compatibility issues with .NET delegates
    }
    
    /// <summary>
    /// Converts PascalCase property names to snake_case for template variables
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        
        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(input[i]));
        }
        
        return result.ToString();
    }
} 