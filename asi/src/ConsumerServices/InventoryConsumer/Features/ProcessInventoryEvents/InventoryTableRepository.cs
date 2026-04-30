using Azure;
using Azure.Data.Tables;
using InventoryConsumer.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Azure Table Storage implementation of inventory repository.
/// </summary>
public class InventoryTableRepository : IInventoryRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<InventoryTableRepository> _logger;
    private readonly string _tableName;

    public InventoryTableRepository(
        IOptions<TableStorageOptions> tableStorageOptions,
        ILogger<InventoryTableRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var options = tableStorageOptions?.Value ?? throw new ArgumentNullException(nameof(tableStorageOptions));
        _tableName = options.TableName;

        // Create TableClient using connection string (local: Azurite, production: Managed Identity)
        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            // Local development with Azurite or connection string
            var serviceClient = new TableServiceClient(options.ConnectionString);
            _tableClient = serviceClient.GetTableClient(_tableName);
        }
        else
        {
            // Production: use Managed Identity (DefaultAzureCredential)
            throw new InvalidOperationException(
                "Managed Identity for Table Storage not yet implemented. Use ConnectionString for now.");
        }

        // Ensure table exists (create if not)
        _tableClient.CreateIfNotExists();
        _logger.LogInformation("Table Storage repository initialized for table: {TableName}", _tableName);
    }

    public async Task UpsertInventoryAsync(InventoryDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            // Set RowKey to InventoryItemId for easy lookups
            document.RowKey = document.InventoryItemId;
            document.UpdatedAt = DateTimeOffset.UtcNow;

            // If CreatedAt is not set, this is a new document
            if (document.CreatedAt == default)
            {
                document.CreatedAt = DateTimeOffset.UtcNow;
            }

            // Upsert (Insert or Replace) - idempotent operation
            await _tableClient.UpsertEntityAsync(
                document,
                TableUpdateMode.Replace, // Replace entire entity
                cancellationToken);

            _logger.LogDebug(
                "Upserted inventory document: InventoryItemId={InventoryItemId}, ProductId={ProductId}, Quantity={Quantity}, ChangeType={ChangeType}",
                document.InventoryItemId,
                document.ProductId,
                document.Quantity,
                document.ChangeType);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to upsert inventory document: InventoryItemId={InventoryItemId}, StatusCode={StatusCode}",
                document.InventoryItemId,
                ex.Status);
            throw;
        }
    }

    public async Task<ProcessingStats> GetProcessingStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Query all entities to get count (simple implementation)
            // In production, you might maintain a separate counter or use Analytics
            var query = _tableClient.QueryAsync<InventoryDocument>(
                filter: string.Empty,
                maxPerPage: 1000,
                cancellationToken: cancellationToken);

            long count = 0;
            await foreach (var page in query.AsPages())
            {
                count += page.Values.Count;
            }

            return new ProcessingStats
            {
                TotalInventoryItems = count,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get processing stats from Table Storage");
            throw;
        }
    }
}
