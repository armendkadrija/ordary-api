using FluentValidation;
using Odary.Api.Constants;

namespace Odary.Api.Modules.User.Validators;

public class InviteUserValidator : AbstractValidator<UserCommands.V1.InviteUser>
{
    public InviteUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

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

        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required")
            .Length(32).WithMessage("Tenant ID must be a valid GUID format");
    }
} 