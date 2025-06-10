using FluentValidation;

namespace Odary.Api.Common.Services;

public interface IValidationService
{
    Task ValidateAsync<T>(T request, CancellationToken cancellationToken = default);
} 

public class ValidationService(IServiceProvider serviceProvider) : IValidationService
{
    public async Task ValidateAsync<T>(T request, CancellationToken cancellationToken = default)
    {
        var validator = serviceProvider.GetService<IValidator<T>>();
        if (validator == null) return; // No validator registered, skip validation
        
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid) throw new Exceptions.ValidationException(result.Errors);
    }
} 