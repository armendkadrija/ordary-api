using FluentValidation;

namespace Odary.Api.Modules.Tenant.Validators;

public class CreateTenantSettingsValidator : AbstractValidator<TenantCommands.V1.CreateTenantSettings>
{
    private static readonly string[] SupportedLanguages = ["en-US", "en-GB", "es-ES", "fr-FR", "de-DE"];
    private static readonly string[] SupportedCurrencies = ["USD", "EUR", "GBP", "CAD"];
    private static readonly string[] SupportedDateFormats = ["MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd"];
    private static readonly string[] SupportedTimeFormats = ["h:mm tt", "HH:mm", "h:mm:ss tt", "HH:mm:ss"];

    public CreateTenantSettingsValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required")
            .Length(32)
            .WithMessage("Tenant ID must be 32 characters");

        RuleFor(x => x.Language)
            .NotEmpty()
            .WithMessage("Language is required")
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage($"Language must be one of: {string.Join(", ", SupportedLanguages)}");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Must(currency => SupportedCurrencies.Contains(currency))
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}");

        RuleFor(x => x.DateFormat)
            .NotEmpty()
            .WithMessage("Date format is required")
            .Must(format => SupportedDateFormats.Contains(format))
            .WithMessage($"Date format must be one of: {string.Join(", ", SupportedDateFormats)}");

        RuleFor(x => x.TimeFormat)
            .NotEmpty()
            .WithMessage("Time format is required")
            .Must(format => SupportedTimeFormats.Contains(format))
            .WithMessage($"Time format must be one of: {string.Join(", ", SupportedTimeFormats)}");
    }
} 