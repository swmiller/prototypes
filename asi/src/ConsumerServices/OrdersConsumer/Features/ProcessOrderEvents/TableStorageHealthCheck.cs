using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// Health check for Table Storage connectivity.
/// </summary>
public class TableStorageHealthCheck : IHealthCheck
{
    private readonly IOrderRepository _repository;

    public TableStorageHealthCheck(IOrderRepository repository)
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
                $"Table Storage is accessible. Total orders: {stats.TotalOrders}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Table Storage is not accessible",
                ex);
        }
    }
}
