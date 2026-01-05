namespace Shared.Core.Events;

/// <summary>
/// Interface for event bus that handles system-wide events
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="eventData">Event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class, IEvent;
    
    /// <summary>
    /// Subscribes to events of a specific type
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="handler">Event handler</param>
    void Subscribe<T>(IEventHandler<T> handler) where T : class, IEvent;
    
    /// <summary>
    /// Subscribes to events of a specific type with a delegate handler
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="handler">Event handler delegate</param>
    void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent;
    
    /// <summary>
    /// Unsubscribes from events of a specific type
    /// </summary>
    /// <typeparam name="T">Event type</typeparam>
    /// <param name="handler">Event handler to remove</param>
    void Unsubscribe<T>(IEventHandler<T> handler) where T : class, IEvent;
    
    /// <summary>
    /// Gets all active subscriptions
    /// </summary>
    /// <returns>Dictionary of event types and their handler counts</returns>
    Dictionary<Type, int> GetActiveSubscriptions();
}

/// <summary>
/// Base interface for all events
/// </summary>
public interface IEvent
{
    /// <summary>
    /// Unique identifier for the event
    /// </summary>
    Guid EventId { get; }
    
    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// Source of the event (e.g., service name, user ID)
    /// </summary>
    string Source { get; }
    
    /// <summary>
    /// Event version for compatibility
    /// </summary>
    int Version { get; }
}

/// <summary>
/// Interface for event handlers
/// </summary>
/// <typeparam name="T">Event type</typeparam>
public interface IEventHandler<in T> where T : class, IEvent
{
    /// <summary>
    /// Handles the event
    /// </summary>
    /// <param name="eventData">Event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(T eventData, CancellationToken cancellationToken = default);
}