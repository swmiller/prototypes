using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProducerFunctions.Shared.Configuration;
using ProducerFunctions.Shared.Services;

namespace ProducerFunctions.Features.Inventory;

/// <summary>
/// Timer-triggered Azure Function that generates mock inventory update events
/// and publishes them to Service Bus.
/// Runs every 2 minutes for demonstration purposes.
/// </summary>
public class InventoryTimerProducer
{
    private readonly IServiceBusPublisher _publisher;
    private readonly ILogger<InventoryTimerProducer> _logger;
    private readonly ProducerOptions _producerOptions;
    private readonly string _topicName;

    public InventoryTimerProducer(
        IServiceBusPublisher publisher,
        ILogger<InventoryTimerProducer> logger,
        IOptions<ProducerOptions> producerOptions)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerOptions = producerOptions?.Value ?? throw new ArgumentNullException(nameof(producerOptions));
        _topicName = _producerOptions.Topics.InventoryUpdated;
    }

    [Function("InventoryTimerProducer")]
    public async Task Run(
        [TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "InventoryTimerProducer triggered at {Timestamp}. Next run: {NextRun}",
            DateTime.UtcNow,
            timerInfo.ScheduleStatus?.Next);

        if (!_producerOptions.EnableMockData)
        {
            _logger.LogInformation("Mock data generation is disabled");
            return;
        }

        try
        {
            // Generate mock inventory events
            var events = GenerateMockInventoryEvents(_producerOptions.EventsPerBatch);

            // Prepare batch with metadata
            var eventBatch = events.Select(evt => (
                Event: evt,
                MessageId: evt.EventId.ToString(),
                SessionId: (string?)evt.ProductSku, // Enable ordered processing per product
                CorrelationId: (string?)null
            ));

            // Publish batch to Service Bus
            await _publisher.PublishBatchAsync(_topicName, eventBatch, cancellationToken);

            _logger.LogInformation(
                "Successfully published {Count} inventory events to topic {TopicName}",
                events.Count,
                _topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate or publish inventory events");
            throw;
        }
    }

    private List<InventoryUpdatedEvent> GenerateMockInventoryEvents(int count)
    {
        var events = new List<InventoryUpdatedEvent>();
        var random = new Random();
        var changeTypes = new[] { "Received", "Adjusted", "Reserved", "Shipped" };
        var warehouses = new[] { "WH-EAST", "WH-WEST", "WH-CENTRAL", "WH-SOUTH" };
        var productCategories = new[] { "ELEC", "APPL", "FURN", "TOOL" };

        for (int i = 0; i < count; i++)
        {
            var productSku = $"{productCategories[random.Next(productCategories.Length)]}-{random.Next(1000, 9999)}";
            var changeType = changeTypes[random.Next(changeTypes.Length)];
            var warehouse = warehouses[random.Next(warehouses.Length)];
            var quantity = random.Next(0, 500);

            var inventoryEvent = new InventoryUpdatedEvent
            {
                EventId = Guid.NewGuid(),
                InventoryId = $"INV-{Guid.NewGuid().ToString()[..8]}",
                ProductSku = productSku,
                ProductName = $"Product {productSku}",
                Quantity = quantity,
                WarehouseLocation = warehouse,
                ChangeType = changeType,
                SourceSystem = "inventory-management-service",
                Timestamp = DateTimeOffset.UtcNow,
                SchemaVersion = "v1.0"
            };

            events.Add(inventoryEvent);
        }

        _logger.LogDebug("Generated {Count} mock inventory events", events.Count);
        return events;
    }
}
