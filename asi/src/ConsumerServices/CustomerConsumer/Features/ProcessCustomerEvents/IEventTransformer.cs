using Shared.Events;

namespace CustomerConsumer.Features.ProcessCustomerEvents;

/// <summary>
/// Transforms Service Bus events into Table Storage documents.
/// </summary>
public interface IEventTransformer
{
    /// <summary>
    /// Transforms a CustomerChangedEvent into a CustomerDocument for storage.
    /// </summary>
    /// <param name="event">The event from Service Bus.</param>
    /// <returns>Customer document ready for Table Storage.</returns>
    CustomerDocument TransformToDocument(CustomerChangedEvent @event);
}