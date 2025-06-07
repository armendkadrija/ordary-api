using FluentValidation;

namespace Odary.Api.Modules.Auth.Validators;

public class ForgotPasswordValidator : AbstractValidator<AuthCommands.V1.ForgotPassword>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Invalid email format");
    }
} 