using FluentValidation;
using Odary.Api.Constants;

namespace Odary.Api.Modules.User.Validators;

public class CreateUserValidator : AbstractValidator<UserCommands.V1.CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Valid email format is required")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required")
            .Length(32).WithMessage("Tenant ID must be a valid GUID format");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .Must(role => new[] { Roles.SUPER_ADMIN, Roles.ADMIN, Roles.DENTIST, Roles.ASSISTANT }.Contains(role))
            .WithMessage($"Role must be one of: {Roles.SUPER_ADMIN}, {Roles.ADMIN}, {Roles.DENTIST}, {Roles.ASSISTANT}");
    }
} 