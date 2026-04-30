using Shared.Events;

namespace OrdersConsumer.Features.ProcessOrderEvents;

/// <summary>
/// Transforms Service Bus events into Table Storage documents.
/// </summary>
public interface IEventTransformer
{
    /// <summary>
    /// Transforms an OrderChangedEvent into an OrderDocument for storage.
    /// </summary>
    /// <param name="event">The event from Service Bus.</param>
    /// <returns>Order document ready for Table Storage.</returns>
    OrderDocument TransformToDocument(OrderChangedEvent @event);
}
