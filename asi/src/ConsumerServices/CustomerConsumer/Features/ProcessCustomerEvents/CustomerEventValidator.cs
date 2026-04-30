using FluentValidation;
using Shared.Events;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// FluentValidation validator for CustomerChangedEvent.
/// Validates events before processing.
/// </summary>
public class CustomerEventValidator : AbstractValidator<CustomerChangedEvent>
{
    public CustomerEventValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .WithMessage("EventId is required");

        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("CustomerId is required")
            .MaximumLength(100)
            .WithMessage("CustomerId must not exceed 100 characters");

        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .WithMessage("CustomerName is required")
            .MaximumLength(200)
            .WithMessage("CustomerName must not exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50)
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("PhoneNumber must not exceed 50 characters");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .Must(status => new[] { "Active", "Inactive", "Suspended" }.Contains(status))
            .WithMessage("Status must be one of: Active, Inactive, Suspended");

        RuleFor(x => x.ChangeType)
            .NotEmpty()
            .WithMessage("ChangeType is required")
            .Must(changeType => new[] { "Created", "Updated", "StatusChanged" }.Contains(changeType))
            .WithMessage("ChangeType must be one of: Created, Updated, StatusChanged");

        RuleFor(x => x.SourceSystem)
            .NotEmpty()
            .WithMessage("SourceSystem is required")
            .MaximumLength(100)
            .WithMessage("SourceSystem must not exceed 100 characters");

        RuleFor(x => x.Timestamp)
            .NotEmpty()
            .WithMessage("Timestamp is required")
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Timestamp cannot be more than 5 minutes in the future");

        RuleFor(x => x.SchemaVersion)
            .NotEmpty()
            .WithMessage("SchemaVersion is required")
            .Must(version => version == "v1.0")
            .WithMessage("Unsupported schema version. Expected: v1.0");
    }
}