using System.ComponentModel.DataAnnotations;

namespace CustomerConsumer.Shared.Configuration;

/// <summary>
/// Configuration options for Azure Table Storage.
/// </summary>
public class TableStorageOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TableStorage";

    /// <summary>
    /// Azure Storage account name (e.g., "devstoreaccount1" for Azurite).
    /// Stored in appsettings.json (non-sensitive).
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Table name where customer documents are stored.
    /// </summary>
    [Required]
    public string TableName { get; set; } = "Customers";

    /// <summary>
    /// Connection string for Table Storage.
    /// Stored in dotnet user-secrets (sensitive).
    /// For local development, use "UseDevelopmentStorage=true" for Azurite.
    /// For production, use Managed Identity (leave null).
    /// </summary>
    public string? ConnectionString { get; set; }
}