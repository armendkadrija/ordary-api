using FluentValidation;

namespace Odary.Api.Modules.Tenant.Validators;

public class CreateTenantValidator : AbstractValidator<TenantCommands.V1.CreateTenant>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Clinic name is required")
            .MaximumLength(255)
            .WithMessage("Clinic name cannot exceed 255 characters");

        RuleFor(x => x.Country)
            .NotEmpty()
            .WithMessage("Country is required")
            .MaximumLength(100)
            .WithMessage("Country cannot exceed 100 characters");

        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithMessage("Timezone is required")
            .MaximumLength(100)
            .WithMessage("Timezone cannot exceed 100 characters");

        RuleFor(x => x.LogoUrl)
            .MaximumLength(500)
            .WithMessage("Logo URL cannot exceed 500 characters")
            .Must(BeValidUrl)
            .WithMessage("Logo URL must be a valid URL")
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out var result) 
               && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
    }
} 