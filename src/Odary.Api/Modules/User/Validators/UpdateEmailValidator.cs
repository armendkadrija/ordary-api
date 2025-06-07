using FluentValidation;

namespace Odary.Api.Modules.User.Validators;

public class UpdateEmailValidator : AbstractValidator<UserCommands.V1.UpdateEmail>
{
    public UpdateEmailValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email format is required")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");
    }
} 