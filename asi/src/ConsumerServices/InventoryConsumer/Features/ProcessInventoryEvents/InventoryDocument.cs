using Azure;
using Azure.Data.Tables;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Inventory document stored in Azure Table Storage.
/// Represents the materialized view for CRM integration.
/// </summary>
public class InventoryDocument : ITableEntity
{
    /// <summary>
    /// Partition key - using "inventory" for all items (simple partitioning).
    /// In production, you might partition by location, product category, or first letter of ProductId.
    /// </summary>
    public string PartitionKey { get; set; } = "inventory";

    /// <summary>
    /// Row key - unique inventory item identifier.
    /// </summary>
    public string RowKey { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the entity was last modified (managed by Table Storage).
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency (managed by Table Storage).
    /// </summary>
    public ETag ETag { get; set; }

    // ========== Domain Properties ==========

    /// <summary>
    /// Inventory item identifier (same as RowKey for convenience).
    /// </summary>
    public string InventoryItemId { get; set; } = string.Empty;

    /// <summary>
    /// Product identifier that this inventory item represents.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Current quantity in stock.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Location/warehouse where the inventory is stored.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Inventory status (Available, Reserved, OutOfStock, Backordered).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Type of last change (Created, Updated, Adjusted, Reserved, Released).
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// Quantity that is reserved (not available for sale).
    /// </summary>
    public int ReservedQuantity { get; set; }

    /// <summary>
    /// Reorder point threshold for replenishment.
    /// </summary>
    public int ReorderPoint { get; set; }

    /// <summary>
    /// Source system that generated the last change.
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the last event processed.
    /// </summary>
    public DateTimeOffset LastEventTimestamp { get; set; }

    /// <summary>
    /// Event ID of the last processed event (for idempotency tracking).
    /// </summary>
    public string LastEventId { get; set; } = string.Empty;

    /// <summary>
    /// Schema version for evolution tracking.
    /// </summary>
    public string SchemaVersion { get; set; } = "v1.0";

    /// <summary>
    /// When this document was first created in Table Storage.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When this document was last updated in Table Storage.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
