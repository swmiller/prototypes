namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Repository interface for customer document persistence.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Upserts a customer document to Table Storage.
    /// Idempotent operation - safe to call multiple times with the same data.
    /// </summary>
    /// <param name="document">Customer document to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertCustomerAsync(CustomerDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processing statistics (for health/admin endpoints).
    /// </summary>
    Task<ProcessingStats> GetProcessingStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Processing statistics for monitoring.
/// </summary>
public record ProcessingStats
{
    public long TotalCustomers { get; init; }
    public DateTimeOffset LastChecked { get; init; }
}