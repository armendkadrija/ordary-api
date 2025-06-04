using FluentValidation;

namespace Odary.Api.Common.Validation;

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
        if (!result.IsValid) throw new Exceptions.ValidationException(result.Errors);
    }
} 