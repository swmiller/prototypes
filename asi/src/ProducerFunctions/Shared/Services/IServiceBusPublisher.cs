namespace ProducerFunctions.Shared.Services;

/// <summary>
/// Abstraction for publishing events to Azure Service Bus topics.
/// Follows Dependency Inversion Principle (SOLID).
/// </summary>
public interface IServiceBusPublisher
{
    /// <summary>
    /// Publishes an event to a Service Bus topic with proper message properties.
    /// </summary>
    /// <typeparam name="TEvent">Event type to publish.</typeparam>
    /// <param name="topicName">Service Bus topic name.</param>
    /// <param name="event">Event payload to publish.</param>
    /// <param name="messageId">Unique message identifier for idempotency.</param>
    /// <param name="sessionId">Session ID for ordered processing (optional).</param>
    /// <param name="correlationId">Correlation ID for distributed tracing (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishEventAsync<TEvent>(
        string topicName,
        TEvent @event,
        string messageId,
        string? sessionId = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// Publishes a batch of events to a Service Bus topic.
    /// </summary>
    /// <typeparam name="TEvent">Event type to publish.</typeparam>
    /// <param name="topicName">Service Bus topic name.</param>
    /// <param name="events">Collection of events with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishBatchAsync<TEvent>(
        string topicName,
        IEnumerable<(TEvent Event, string MessageId, string? SessionId, string? CorrelationId)> events,
        CancellationToken cancellationToken = default)
        where TEvent : class;
}