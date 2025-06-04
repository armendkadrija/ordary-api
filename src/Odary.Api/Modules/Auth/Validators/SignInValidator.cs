using FluentValidation;

namespace Odary.Api.Modules.Auth.Validators;

public class SignInValidator : AbstractValidator<AuthCommands.V1.SignIn>
{
    public SignInValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email format is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
} 