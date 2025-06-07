using FluentValidation;

namespace Odary.Api.Modules.Auth.Validators;

public class ChangePasswordValidator : AbstractValidator<AuthCommands.V1.ChangePassword>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)")
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter, and one number")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must be different from current password");
    }
} 