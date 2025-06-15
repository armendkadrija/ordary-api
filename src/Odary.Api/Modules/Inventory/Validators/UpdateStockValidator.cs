using FluentValidation;

namespace Odary.Api.Modules.Inventory.Validators;

public class UpdateStockValidator : AbstractValidator<InventoryCommands.V1.UpdateStock>
{
    public UpdateStockValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.NewQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative")
            .LessThanOrEqualTo(1000000).WithMessage("Quantity cannot exceed 1,000,000");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required for manual stock updates")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters");
    }
}

public class DeductStockValidator : AbstractValidator<InventoryCommands.V1.DeductStock>
{
    public DeductStockValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.QuantityUsed)
            .GreaterThan(0).WithMessage("Quantity used must be greater than 0")
            .LessThanOrEqualTo(1000000).WithMessage("Quantity used cannot exceed 1,000,000");
    }
}

public class DeductStockByUnitsValidator : AbstractValidator<InventoryCommands.V1.DeductStockByUnits>
{
    public DeductStockByUnitsValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.UnitsUsed)
            .GreaterThan(0).WithMessage("Units used must be greater than 0")
            .LessThanOrEqualTo(10000).WithMessage("Units used cannot exceed 10,000");
    }
} 