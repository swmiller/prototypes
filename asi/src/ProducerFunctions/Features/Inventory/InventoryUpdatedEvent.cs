namespace ProducerFunctions.Features.Inventory;

/// <summary>
/// Domain event published when inventory data changes in source systems.
/// </summary>
public record InventoryUpdatedEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Used as MessageId for idempotency.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Inventory record identifier from the source system.
    /// </summary>
    public string InventoryId { get; init; } = string.Empty;

    /// <summary>
    /// Product SKU (Stock Keeping Unit).
    /// </summary>
    public string ProductSku { get; init; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>
    /// Current quantity on hand.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Warehouse location code.
    /// </summary>
    public string WarehouseLocation { get; init; } = string.Empty;

    /// <summary>
    /// Type of change (Received, Adjusted, Reserved, Shipped).
    /// </summary>
    public string ChangeType { get; init; } = string.Empty;

    /// <summary>
    /// System that generated this event.
    /// </summary>
    public string SourceSystem { get; init; } = "inventory-management-service";

    /// <summary>
    /// Timestamp when the event was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Schema version for event evolution support.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1.0";
}
