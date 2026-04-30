using Shared.Events;

namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// Transforms OrderChangedEvent to OrderDocument.
/// </summary>
public class OrderEventTransformer : IEventTransformer
{
    private readonly ILogger<OrderEventTransformer> _logger;

    public OrderEventTransformer(ILogger<OrderEventTransformer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public OrderDocument TransformToDocument(OrderChangedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var document = new OrderDocument
        {
            // Table Storage keys
            PartitionKey = "order",
            RowKey = @event.OrderId,

            // Domain properties from event
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            TotalAmount = (double)@event.TotalAmount,
            Status = @event.Status,
            ItemCount = @event.ItemCount,
            ShippingCity = @event.ShippingCity,
            Priority = @event.Priority,
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
            "Transformed event to document: OrderId={OrderId}, CustomerId={CustomerId}, TotalAmount={TotalAmount}, ChangeType={ChangeType}, EventId={EventId}",
            document.OrderId,
            document.CustomerId,
            document.TotalAmount,
            document.ChangeType,
            @event.EventId);

        return document;
    }
}
