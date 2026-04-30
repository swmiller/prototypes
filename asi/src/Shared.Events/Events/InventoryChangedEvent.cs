namespace Shared.Events;

/// <summary>
/// Domain event published when inventory data changes in source systems.
/// </summary>
public record InventoryChangedEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Used as MessageId for idempotency.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Inventory item identifier from the source system.
    /// </summary>
    public string InventoryItemId { get; init; } = string.Empty;

    /// <summary>
    /// Product identifier that this inventory item represents.
    /// </summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>
    /// Current quantity in stock.
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Location/warehouse where the inventory is stored.
    /// </summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>
    /// Inventory status (Available, Reserved, OutOfStock, Backordered).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Type of change (Created, Updated, Adjusted, Reserved, Released).
    /// </summary>
    public string ChangeType { get; init; } = string.Empty;

    /// <summary>
    /// Optional: Quantity that is reserved (not available for sale).
    /// </summary>
    public int? ReservedQuantity { get; init; }

    /// <summary>
    /// Optional: Reorder point threshold for replenishment.
    /// </summary>
    public int? ReorderPoint { get; init; }

    /// <summary>
    /// System that generated this event.
    /// </summary>
    public string SourceSystem { get; init; } = "inventory-sync-service";

    /// <summary>
    /// Timestamp when the event was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Schema version for event evolution.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1.0";
}
