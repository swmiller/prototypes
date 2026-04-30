using System.ComponentModel.DataAnnotations;

namespace InventoryConsumer.Shared.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus connectivity (consumer).
/// </summary>
public class ServiceBusOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Service Bus namespace (e.g., "asi-servicebus-dev").
    /// Stored in appsettings.json (non-sensitive).
    /// </summary>
    [Required]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Service Bus topic name to consume from.
    /// </summary>
    [Required]
    public string TopicName { get; set; } = "inventory-changed";

    /// <summary>
    /// Service Bus subscription name for this consumer.
    /// </summary>
    [Required]
    public string SubscriptionName { get; set; } = "nosql-writer-inventory";

    /// <summary>
    /// Service Bus connection string.
    /// Stored in dotnet user-secrets (sensitive).
    /// Only used for local development; production uses Managed Identity.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets the fully qualified namespace for Service Bus client.
    /// </summary>
    public string FullyQualifiedNamespace => $"{Namespace}.servicebus.windows.net";
}
