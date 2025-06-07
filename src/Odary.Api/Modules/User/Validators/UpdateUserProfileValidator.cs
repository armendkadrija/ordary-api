using FluentValidation;
using Odary.Api.Constants;

namespace Odary.Api.Modules.User.Validators;

public class UpdateUserProfileValidator : AbstractValidator<UserCommands.V1.UpdateUserProfile>
{
    public UpdateUserProfileValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .Must(role => new[] { Roles.SUPER_ADMIN, Roles.ADMIN, Roles.DENTIST, Roles.ASSISTANT }.Contains(role))
            .WithMessage($"Role must be one of: {Roles.SUPER_ADMIN}, {Roles.ADMIN}, {Roles.DENTIST}, {Roles.ASSISTANT}");
    }
} 