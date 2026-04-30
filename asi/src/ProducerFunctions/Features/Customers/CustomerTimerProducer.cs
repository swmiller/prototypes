using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProducerFunctions.Shared.Configuration;
using ProducerFunctions.Shared.Services;
using Shared.Events;

namespace ProducerFunctions.Features.Customers;

/// <summary>
/// Timer-triggered Azure Function that generates mock customer change events
/// and publishes them to Service Bus.
/// Runs every 2 minutes for demonstration purposes.
/// </summary>
public class CustomerTimerProducer
{
    private readonly IServiceBusPublisher _publisher;
    private readonly ILogger<CustomerTimerProducer> _logger;
    private readonly ProducerOptions _producerOptions;
    private readonly string _topicName;

    public CustomerTimerProducer(
        IServiceBusPublisher publisher,
        ILogger<CustomerTimerProducer> logger,
        IOptions<ProducerOptions> producerOptions)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerOptions = producerOptions?.Value ?? throw new ArgumentNullException(nameof(producerOptions));
        _topicName = _producerOptions.Topics.CustomerChanged;
    }

    [Function("CustomerTimerProducer")]
    public async Task Run(
        [TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CustomerTimerProducer triggered at {Timestamp}. Next run: {NextRun}",
            DateTime.UtcNow,
            timerInfo.ScheduleStatus?.Next);

        if (!_producerOptions.EnableMockData)
        {
            _logger.LogInformation("Mock data generation is disabled");
            return;
        }

        try
        {
            // Generate mock customer events
            var events = GenerateMockCustomerEvents(_producerOptions.EventsPerBatch);

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
                "Successfully published {Count} customer events to topic {TopicName}",
                events.Count,
                _topicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate or publish customer events");
            throw;
        }
    }

    private List<CustomerChangedEvent> GenerateMockCustomerEvents(int count)
    {
        var events = new List<CustomerChangedEvent>();
        var random = new Random();
        var changeTypes = new[] { "Created", "Updated", "StatusChanged" };
        var statuses = new[] { "Active", "Inactive", "Suspended" };

        for (int i = 0; i < count; i++)
        {
            var customerId = $"CUST-{random.Next(1000, 9999)}";
            var changeType = changeTypes[random.Next(changeTypes.Length)];
            var status = statuses[random.Next(statuses.Length)];

            var customerEvent = new CustomerChangedEvent
            {
                EventId = Guid.NewGuid(),
                CustomerId = customerId,
                CustomerName = $"Customer {customerId}",
                Email = $"customer{random.Next(1000, 9999)}@example.com",
                PhoneNumber = $"+1-555-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                Status = status,
                ChangeType = changeType,
                SourceSystem = "customer-sync-service",
                Timestamp = DateTimeOffset.UtcNow,
                SchemaVersion = "v1.0"
            };

            events.Add(customerEvent);
        }

        _logger.LogDebug("Generated {Count} mock customer events", events.Count);
        return events;
    }
}