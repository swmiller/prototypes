namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Repository interface for inventory document persistence.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// Upserts an inventory document to Table Storage.
    /// Idempotent operation - safe to call multiple times with the same data.
    /// </summary>
    /// <param name="document">Inventory document to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertInventoryAsync(InventoryDocument document, CancellationToken cancellationToken = default);

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
    public long TotalInventoryItems { get; init; }
    public DateTimeOffset LastChecked { get; init; }
}
