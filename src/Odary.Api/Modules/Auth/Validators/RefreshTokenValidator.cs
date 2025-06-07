using FluentValidation;

namespace Odary.Api.Modules.Auth.Validators;

public class RefreshTokenValidator : AbstractValidator<AuthCommands.V1.RefreshToken>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Refresh token is required")
            .Must(token => Guid.TryParse(token, out _))
            .WithMessage("Refresh token must be a valid GUID format");
    }
} 