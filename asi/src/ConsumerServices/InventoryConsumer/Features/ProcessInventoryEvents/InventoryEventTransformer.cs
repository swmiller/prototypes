using Shared.Events;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Transforms InventoryChangedEvent to InventoryDocument.
/// </summary>
public class InventoryEventTransformer : IEventTransformer
{
    private readonly ILogger<InventoryEventTransformer> _logger;

    public InventoryEventTransformer(ILogger<InventoryEventTransformer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public InventoryDocument TransformToDocument(InventoryChangedEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var document = new InventoryDocument
        {
            // Table Storage keys
            PartitionKey = "inventory",
            RowKey = @event.InventoryItemId,

            // Domain properties from event
            InventoryItemId = @event.InventoryItemId,
            ProductId = @event.ProductId,
            Quantity = @event.Quantity,
            Location = @event.Location,
            Status = @event.Status,
            ChangeType = @event.ChangeType,
            ReservedQuantity = @event.ReservedQuantity ?? 0,
            ReorderPoint = @event.ReorderPoint ?? 0,
            SourceSystem = @event.SourceSystem,
            SchemaVersion = @event.SchemaVersion,

            // Event tracking for idempotency
            LastEventId = @event.EventId.ToString(),
            LastEventTimestamp = @event.Timestamp,

            // Document metadata (UpdatedAt will be set in repository)
            // CreatedAt will be set in repository if this is a new document
        };

        _logger.LogDebug(
            "Transformed event to document: InventoryItemId={InventoryItemId}, ProductId={ProductId}, ChangeType={ChangeType}, EventId={EventId}",
            document.InventoryItemId,
            document.ProductId,
            document.ChangeType,
            @event.EventId);

        return document;
    }
}
