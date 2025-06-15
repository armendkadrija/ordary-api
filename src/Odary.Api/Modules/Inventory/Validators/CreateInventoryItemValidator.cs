using FluentValidation;

namespace Odary.Api.Modules.Inventory.Validators;

public class CreateInventoryItemValidator : AbstractValidator<InventoryCommands.V1.CreateInventoryItem>
{
    public CreateInventoryItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid category");

        RuleFor(x => x.UnitType)
            .IsInEnum().WithMessage("Invalid unit type");

        RuleFor(x => x.UnitSize)
            .GreaterThan(0).WithMessage("Unit size must be greater than 0")
            .LessThanOrEqualTo(1000000).WithMessage("Unit size cannot exceed 1,000,000");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative")
            .LessThanOrEqualTo(1000000).WithMessage("Quantity cannot exceed 1,000,000");

        RuleFor(x => x.MinThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum threshold cannot be negative")
            .LessThanOrEqualTo(1000000).WithMessage("Minimum threshold cannot exceed 1,000,000");

        RuleFor(x => x.ExpiryDate)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Expiry date must be in the future")
            .When(x => x.ExpiryDate.HasValue);

        RuleFor(x => x.BatchNumber)
            .MaximumLength(50).WithMessage("Batch number cannot exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.BatchNumber));
    }
} 