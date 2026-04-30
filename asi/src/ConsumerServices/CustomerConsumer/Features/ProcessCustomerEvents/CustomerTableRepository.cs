using Azure;
using Azure.Data.Tables;
using CustomerConsumer.Shared.Configuration;
using Microsoft.Extensions.Options;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Azure Table Storage implementation of customer repository.
/// </summary>
public class CustomerTableRepository : ICustomerRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<CustomerTableRepository> _logger;
    private readonly string _tableName;

    public CustomerTableRepository(
        IOptions<TableStorageOptions> tableStorageOptions,
        ILogger<CustomerTableRepository> logger)
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

    public async Task UpsertCustomerAsync(CustomerDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            // Set RowKey to CustomerId for easy lookups
            document.RowKey = document.CustomerId;
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
                "Upserted customer document: CustomerId={CustomerId}, ChangeType={ChangeType}",
                document.CustomerId,
                document.ChangeType);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Failed to upsert customer document: CustomerId={CustomerId}, StatusCode={StatusCode}",
                document.CustomerId,
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
            var query = _tableClient.QueryAsync<CustomerDocument>(
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
                TotalCustomers = count,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to get processing statistics");
            return new ProcessingStats
            {
                TotalCustomers = -1,
                LastChecked = DateTimeOffset.UtcNow
            };
        }
    }
}