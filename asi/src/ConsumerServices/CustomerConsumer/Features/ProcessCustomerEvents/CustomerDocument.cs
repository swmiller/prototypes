using Azure;
using Azure.Data.Tables;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Customer document stored in Azure Table Storage.
/// Represents the materialized view for CRM integration.
/// </summary>
public class CustomerDocument : ITableEntity
{
    /// <summary>
    /// Partition key - using "customer" for all customers (simple partitioning).
    /// In production, you might partition by region, tenant, or first letter of CustomerId.
    /// </summary>
    public string PartitionKey { get; set; } = "customer";

    /// <summary>
    /// Row key - unique customer identifier.
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
    /// Customer identifier (same as RowKey for convenience).
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Customer's full name.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Customer's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Customer status (Active, Inactive, Suspended).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Type of last change (Created, Updated, StatusChanged).
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