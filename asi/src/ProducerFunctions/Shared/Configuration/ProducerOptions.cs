using System.ComponentModel.DataAnnotations;

namespace ProducerFunctions.Shared.Configuration;

/// <summary>
/// Configuration options for timer-based producers.
/// </summary>
public class ProducerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Producer";

    /// <summary>
    /// Number of mock events to generate per timer execution.
    /// </summary>
    [Range(1, 100)]
    public int EventsPerBatch { get; set; } = 5;

    /// <summary>
    /// Enable/disable mock data generation.
    /// </summary>
    public bool EnableMockData { get; set; } = true;

    /// <summary>
    /// Topic names for each event type.
    /// </summary>
    public TopicNames Topics { get; set; } = new();
}

/// <summary>
/// Service Bus topic names for each domain event.
/// </summary>
public class TopicNames
{
    public string InventoryUpdated { get; set; } = "inventory-updated";
    public string OrdersCreated { get; set; } = "orders-created";
    public string CustomerChanged { get; set; } = "customer-changed";
}