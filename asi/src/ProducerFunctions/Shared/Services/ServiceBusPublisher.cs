using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ProducerFunctions.Shared.Services;

/// <summary>
/// Implementation of Service Bus publisher using Azure SDK.
/// Registered as Singleton for connection reuse.
/// </summary>
public class ServiceBusPublisher : IServiceBusPublisher
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusPublisher> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ServiceBusPublisher(
        ServiceBusClient serviceBusClient,
        ILogger<ServiceBusPublisher> logger)
    {
        _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task PublishEventAsync<TEvent>(
        string topicName,
        TEvent @event,
        string messageId,
        string? sessionId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(topicName);
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        ServiceBusSender sender = _serviceBusClient.CreateSender(topicName);

        try
        {
            var message = CreateServiceBusMessage(@event, messageId, sessionId, correlationId);

            await sender.SendMessageAsync(message, cancellationToken);

            _logger.LogInformation(
                "Published event to topic {TopicName}: MessageId={MessageId}, Type={EventType}",
                topicName,
                messageId,
                typeof(TEvent).Name);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event to topic {TopicName}: MessageId={MessageId}",
                topicName,
                messageId);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    public async Task PublishBatchAsync<TEvent>(
        string topicName,
        IEnumerable<(TEvent Event, string MessageId, string? SessionId, string? CorrelationId)> events,
        CancellationToken cancellationToken = default)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(topicName);
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();
        if (!eventList.Any())
        {
            _logger.LogWarning("No events to publish to topic {TopicName}", topicName);
            return;
        }

        ServiceBusSender sender = _serviceBusClient.CreateSender(topicName);

        try
        {
            using ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);

            foreach (var (evt, messageId, sessionId, correlationId) in eventList)
            {
                var message = CreateServiceBusMessage(evt, messageId, sessionId, correlationId);

                if (!messageBatch.TryAddMessage(message))
                {
                    _logger.LogWarning(
                        "Message {MessageId} too large for batch, sending separately",
                        messageId);
                    
                    await sender.SendMessageAsync(message, cancellationToken);
                }
            }

            if (messageBatch.Count > 0)
            {
                await sender.SendMessagesAsync(messageBatch, cancellationToken);
            }

            _logger.LogInformation(
                "Published batch of {Count} events to topic {TopicName}",
                eventList.Count,
                topicName);
        }
        catch (ServiceBusException ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish batch to topic {TopicName}",
                topicName);
            throw;
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }

    private ServiceBusMessage CreateServiceBusMessage<TEvent>(
        TEvent @event,
        string messageId,
        string? sessionId,
        string? correlationId)
        where TEvent : class
    {
        string messageBody = JsonSerializer.Serialize(@event, _jsonOptions);

        var message = new ServiceBusMessage(messageBody)
        {
            MessageId = messageId,
            ContentType = "application/json",
            Subject = typeof(TEvent).Name.ToLowerInvariant(),
            CorrelationId = correlationId ?? messageId
        };

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            message.SessionId = sessionId;
        }

        // Add application properties for routing and filtering
        message.ApplicationProperties.Add("eventType", typeof(TEvent).Name);
        message.ApplicationProperties.Add("timestamp", DateTimeOffset.UtcNow.ToString("o"));

        // Add schema version if available via reflection
        var schemaVersionProperty = typeof(TEvent).GetProperty("SchemaVersion");
        if (schemaVersionProperty != null)
        {
            var schemaVersion = schemaVersionProperty.GetValue(@event)?.ToString();
            if (!string.IsNullOrEmpty(schemaVersion))
            {
                message.ApplicationProperties.Add("schemaVersion", schemaVersion);
            }
        }

        return message;
    }
}