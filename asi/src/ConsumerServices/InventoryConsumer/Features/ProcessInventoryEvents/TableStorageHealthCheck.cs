using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Health check for Table Storage connectivity.
/// </summary>
public class TableStorageHealthCheck : IHealthCheck
{
    private readonly IInventoryRepository _repository;

    public TableStorageHealthCheck(IInventoryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get stats - this will verify Table Storage connectivity
            var stats = await _repository.GetProcessingStatsAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"Table Storage is accessible. Total inventory items: {stats.TotalInventoryItems}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Table Storage is not accessible",
                ex);
        }
    }
}
