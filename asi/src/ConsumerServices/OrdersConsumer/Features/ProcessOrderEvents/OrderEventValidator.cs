using FluentValidation;
using Shared.Events;

namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// FluentValidation validator for OrderChangedEvent.
/// Validates events before processing.
/// </summary>
public class OrderEventValidator : AbstractValidator<OrderChangedEvent>
{
    public OrderEventValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required");

        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required")
            .MaximumLength(100)
            .WithMessage("OrderId must not exceed 100 characters");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required")
            .MaximumLength(100)
            .WithMessage("CustomerId must not exceed 100 characters");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TotalAmount must be non-negative");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .Must(status => new[] { "Pending", "Confirmed", "Processing", "Shipped", "Delivered", "Cancelled" }.Contains(status))
            .WithMessage("Status must be one of: Pending, Confirmed, Processing, Shipped, Delivered, Cancelled");

        RuleFor(x => x.ItemCount)
            .GreaterThan(0)
            .WithMessage("ItemCount must be greater than zero");

        RuleFor(x => x.ShippingCity)
            .NotEmpty()
            .WithMessage("ShippingCity is required")
            .MaximumLength(100)
            .WithMessage("ShippingCity must not exceed 100 characters");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .WithMessage("Priority is required")
            .Must(priority => new[] { "Standard", "Express", "Overnight" }.Contains(priority))
            .WithMessage("Priority must be one of: Standard, Express, Overnight");

        RuleFor(x => x.ChangeType)
            .NotEmpty()
            .WithMessage("ChangeType is required")
            .Must(changeType => new[] { "Created", "Updated", "StatusChanged", "Cancelled" }.Contains(changeType))
            .WithMessage("ChangeType must be one of: Created, Updated, StatusChanged, Cancelled");

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
