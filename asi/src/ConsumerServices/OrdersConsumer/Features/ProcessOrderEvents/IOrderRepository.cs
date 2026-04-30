namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// Repository interface for order document persistence.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Upserts an order document to Table Storage.
    /// Idempotent operation - safe to call multiple times with the same data.
    /// </summary>
    /// <param name="document">Order document to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertOrderAsync(OrderDocument document, CancellationToken cancellationToken = default);

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
    public long TotalOrders { get; init; }
    public DateTimeOffset LastChecked { get; init; }
}
