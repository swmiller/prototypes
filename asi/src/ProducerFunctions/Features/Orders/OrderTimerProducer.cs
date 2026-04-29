using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProducerFunctions.Shared.Configuration;
using ProducerFunctions.Shared.Services;

namespace ProducerFunctions.Features.Orders;

/// <summary>
/// Timer-triggered Azure Function that generates mock order created events
/// and publishes them to Service Bus.
/// Runs every 2 minutes for demonstration purposes.
/// </summary>
public class OrderTimerProducer
{
    private readonly IServiceBusPublisher _publisher;
    private readonly ILogger<OrderTimerProducer> _logger;
    private readonly ProducerOptions _producerOptions;
    private readonly string _topicName;

    public OrderTimerProducer(
        IServiceBusPublisher publisher,
        ILogger<OrderTimerProducer> logger,
        IOptions<ProducerOptions> producerOptions)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerOptions = producerOptions?.Value ?? throw new ArgumentNullException(nameof(producerOptions));
        _topicName = _producerOptions.Topics.OrdersCreated;
    }

    [Function("OrderTimerProducer")]
    public async Task Run(
        [TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "OrderTimerProducer triggered at {Timestamp}. Next run: {NextRun}",
            DateTime.UtcNow,
            timerInfo.ScheduleStatus?.Next);

        if (!_producerOptions.EnableMockData)
        {
            _logger.LogInformation("Mock data generation is disabled");
            return;
        }

        try
        {
            // Generate mock order events
            var events = GenerateMockOrderEvents(_producerOptions.EventsPerBatch);

            // Prepare batch with metadata
            var eventBatch = events.Select(evt => (
                Event: evt,
                MessageId: evt.EventId.ToString(),
                SessionId: (string?)evt.CustomerId, // Enable ordered processing per customer
                CorrelationId: (string?)null
            ));

            // Publish batch to Service Bus
            await _publisher.PublishBatchAsync(_topicName, eventBatch, cancellationToken);

            _logger.LogInformation(
                "Successfully published {Count} order events to topic {TopicName}",
                events.Count,
                _topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate or publish order events");
            throw;
        }
    }

    private List<OrderCreatedEvent> GenerateMockOrderEvents(int count)
    {
        var events = new List<OrderCreatedEvent>();
        var random = new Random();
        var statuses = new[] { "Pending", "Confirmed", "Processing" };
        var priorities = new[] { "Standard", "Express", "Overnight" };
        var cities = new[] { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia" };

        for (int i = 0; i < count; i++)
        {
            var customerId = $"CUST-{random.Next(1000, 9999)}";
            var orderId = $"ORD-{random.Next(10000, 99999)}";
            var status = statuses[random.Next(statuses.Length)];
            var priority = priorities[random.Next(priorities.Length)];
            var city = cities[random.Next(cities.Length)];
            var itemCount = random.Next(1, 10);
            var totalAmount = Math.Round((decimal)(random.NextDouble() * 1000 + 10), 2);

            var orderEvent = new OrderCreatedEvent
            {
                EventId = Guid.NewGuid(),
                OrderId = orderId,
                CustomerId = customerId,
                TotalAmount = totalAmount,
                Status = status,
                ItemCount = itemCount,
                ShippingCity = city,
                Priority = priority,
                SourceSystem = "order-management-service",
                Timestamp = DateTimeOffset.UtcNow,
                SchemaVersion = "v1.0"
            };

            events.Add(orderEvent);
        }

        _logger.LogDebug("Generated {Count} mock order events", events.Count);
        return events;
    }
}
