using FluentValidation;
using Shared.Events;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// FluentValidation validator for InventoryChangedEvent.
/// Validates events before processing.
/// </summary>
public class InventoryEventValidator : AbstractValidator<InventoryChangedEvent>
{
    public InventoryEventValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required");

        RuleFor(x => x.InventoryItemId)
            .NotEmpty()
            .WithMessage("InventoryItemId is required")
            .MaximumLength(100)
            .WithMessage("InventoryItemId must not exceed 100 characters");

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required")
            .MaximumLength(100)
            .WithMessage("ProductId must not exceed 100 characters");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Quantity must be non-negative");

        RuleFor(x => x.Location)
            .NotEmpty()
            .WithMessage("Location is required")
            .MaximumLength(100)
            .WithMessage("Location must not exceed 100 characters");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .Must(status => new[] { "Available", "Reserved", "OutOfStock", "Backordered" }.Contains(status))
            .WithMessage("Status must be one of: Available, Reserved, OutOfStock, Backordered");

        RuleFor(x => x.ChangeType)
            .NotEmpty()
            .WithMessage("ChangeType is required")
            .Must(changeType => new[] { "Created", "Updated", "Adjusted", "Reserved", "Released" }.Contains(changeType))
            .WithMessage("ChangeType must be one of: Created, Updated, Adjusted, Reserved, Released");

        RuleFor(x => x.ReservedQuantity)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ReservedQuantity.HasValue)
            .WithMessage("ReservedQuantity must be non-negative when specified");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ReorderPoint.HasValue)
            .WithMessage("ReorderPoint must be non-negative when specified");

        RuleFor(x => x.SourceSystem)
            .NotEmpty()
            .WithMessage("SourceSystem is required");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future");

        RuleFor(x => x.SchemaVersion)
            .NotEmpty()
            .WithMessage("SchemaVersion is required");
    }
}
