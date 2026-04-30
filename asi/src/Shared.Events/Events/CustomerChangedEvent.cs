namespace Shared.Events;

/// <summary>
/// Domain event published when customer data changes in source systems.
/// </summary>
public record CustomerChangedEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// Used as MessageId for idempotency.
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Customer identifier from the source system.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Customer's full name.
    /// </summary>
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>
    /// Customer's email address.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Customer's phone number.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// Customer status (Active, Inactive, Suspended).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Type of change (Created, Updated, StatusChanged).
    /// </summary>
    public string ChangeType { get; init; } = string.Empty;

    /// <summary>
    /// System that generated this event.
    /// </summary>
    public string SourceSystem { get; init; } = "customer-sync-service";

    /// <summary>
    /// Timestamp when the event was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Schema version for event evolution support.
    /// </summary>
    public string SchemaVersion { get; init; } = "v1.0";
}