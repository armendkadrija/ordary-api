using FluentValidation;

namespace Odary.Api.Modules.User.Validators;

public class UpdateUserValidator : AbstractValidator<UserCommands.V1.UpdateUser>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email format is required")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");
    }
} 