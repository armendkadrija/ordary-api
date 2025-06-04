using FluentValidation.Results;

namespace Odary.Api.Common.Exceptions;

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