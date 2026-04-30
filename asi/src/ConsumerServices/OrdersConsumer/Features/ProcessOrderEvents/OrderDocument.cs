using Azure;
using Azure.Data.Tables;

namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// Order document stored in Azure Table Storage.
/// Represents the materialized view for CRM integration.
/// </summary>
public class OrderDocument : ITableEntity
{
    /// <summary>
    /// Partition key - using "order" for all orders (simple partitioning).
    /// In production, you might partition by customer, region, or date.
    /// </summary>
    public string PartitionKey { get; set; } = "order";

    /// <summary>
    /// Row key - unique order identifier.
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
    /// Order identifier (same as RowKey for convenience).
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Customer identifier associated with this order.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Total order amount.
    /// </summary>
    public double TotalAmount { get; set; }

    /// <summary>
    /// Order status (Pending, Confirmed, Processing, Shipped, Delivered, Cancelled).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of items in the order.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Shipping address city.
    /// </summary>
    public string ShippingCity { get; set; } = string.Empty;

    /// <summary>
    /// Order priority (Standard, Express, Overnight).
    /// </summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>
    /// Type of last change (Created, Updated, StatusChanged, Cancelled).
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

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
