namespace ProducerFunctions.Features.Orders;

/// <summary>
/// Domain event published when a new order is created in source systems.
/// </summary>
public record OrderCreatedEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Used as MessageId for idempotency.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Order identifier from the source system.
    /// </summary>
    public string OrderId { get; init; } = string.Empty;

    /// <summary>
    /// Customer identifier associated with this order.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Total order amount.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// Order status (Pending, Confirmed, Processing).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Number of items in the order.
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// Shipping address city.
    /// </summary>
    public string ShippingCity { get; init; } = string.Empty;

    /// <summary>
    /// Order priority (Standard, Express, Overnight).
    /// </summary>
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// System that generated this event.
    /// </summary>
    public string SourceSystem { get; init; } = "order-management-service";

    /// <summary>
    /// Timestamp when the event was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Schema version for event evolution support.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1.0";
}
