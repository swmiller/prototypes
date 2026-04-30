using Shared.Events;

namespace InventoryConsumer.Features.ProcessInventoryEvents;

/// <summary>
/// Transforms Service Bus events into Table Storage documents.
/// </summary>
public interface IEventTransformer
{
    /// <summary>
    /// Transforms an InventoryChangedEvent into an InventoryDocument for storage.
    /// </summary>
    /// <param name="event">The event from Service Bus.</param>
    /// <returns>Inventory document ready for Table Storage.</returns>
    InventoryDocument TransformToDocument(InventoryChangedEvent @event);
}
