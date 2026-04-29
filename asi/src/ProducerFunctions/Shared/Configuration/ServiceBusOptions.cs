using System.ComponentModel.DataAnnotations;

namespace ProducerFunctions.Shared.Configuration;

/// <summary>
/// Configuration options for Azure Service Bus connectivity.
/// </summary>
public class ServiceBusOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ServiceBus";

    /// <summary>
    /// Service Bus namespace (e.g., "asi-servicebus-dev").
    /// Stored in local.settings.json (non-sensitive).
    /// </summary>
    [Required]
    public string Namespace { get; set; } = string.Empty;

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