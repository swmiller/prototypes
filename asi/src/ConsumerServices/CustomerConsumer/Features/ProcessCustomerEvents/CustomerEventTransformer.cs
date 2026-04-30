using Shared.Events;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Transforms CustomerChangedEvent to CustomerDocument.
/// </summary>
public class CustomerEventTransformer : IEventTransformer
{
    private readonly ILogger<CustomerEventTransformer> _logger;

    public CustomerEventTransformer(ILogger<CustomerEventTransformer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CustomerDocument TransformToDocument(CustomerChangedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var document = new CustomerDocument
        {
            // Table Storage keys
            PartitionKey = "customer",
            RowKey = @event.CustomerId,

            // Domain properties from event
            CustomerId = @event.CustomerId,
            CustomerName = @event.CustomerName,
            Email = @event.Email,
            PhoneNumber = @event.PhoneNumber,
            Status = @event.Status,
            ChangeType = @event.ChangeType,
            SourceSystem = @event.SourceSystem,
            SchemaVersion = @event.SchemaVersion,

            // Event tracking for idempotency
            LastEventId = @event.EventId.ToString(),
            LastEventTimestamp = @event.Timestamp,

            // Document metadata (UpdatedAt will be set in repository)
            // CreatedAt will be set in repository if this is a new document
        };

        _logger.LogDebug(
            "Transformed event to document: CustomerId={CustomerId}, ChangeType={ChangeType}, EventId={EventId}",
            document.CustomerId,
            document.ChangeType,
            @event.EventId);

        return document;
    }
}