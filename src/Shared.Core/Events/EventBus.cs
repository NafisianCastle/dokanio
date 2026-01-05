using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Shared.Core.Events;

/// <summary>
/// In-memory implementation of event bus
/// </summary>
public class EventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly ILogger<EventBus> _logger;

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        var eventType = typeof(T);
        
        if (!_handlers.TryGetValue(eventType, out var handlers) || handlers.Count == 0)
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Publishing event {EventType} with ID {EventId} to {HandlerCount} handlers", 
            eventType.Name, eventData.EventId, handlers.Count);

        var tasks = new List<Task>();

        foreach (var handler in handlers.ToList()) // ToList to avoid modification during enumeration
        {
            try
            {
                if (handler is IEventHandler<T> typedHandler)
                {
                    tasks.Add(typedHandler.HandleAsync(eventData, cancellationToken));
                }
                else if (handler is Func<T, CancellationToken, Task> delegateHandler)
                {
                    tasks.Add(delegateHandler(eventData, cancellationToken));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task for event handler of type {HandlerType}", handler.GetType().Name);
            }
        }

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("Successfully published event {EventType} with ID {EventId}", eventType.Name, eventData.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while publishing event {EventType} with ID {EventId}", eventType.Name, eventData.EventId);
            throw;
        }
    }

    public void Subscribe<T>(IEventHandler<T> handler) where T : class, IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        _handlers.AddOrUpdate(eventType, 
            new List<object> { handler }, 
            (key, existing) =>
            {
                existing.Add(handler);
                return existing;
            });

        _logger.LogDebug("Subscribed handler {HandlerType} to event type {EventType}", 
            handler.GetType().Name, eventType.Name);
    }

    public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class, IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        _handlers.AddOrUpdate(eventType, 
            new List<object> { handler }, 
            (key, existing) =>
            {
                existing.Add(handler);
                return existing;
            });

        _logger.LogDebug("Subscribed delegate handler to event type {EventType}", eventType.Name);
    }

    public void Unsubscribe<T>(IEventHandler<T> handler) where T : class, IEvent
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(T);
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            handlers.Remove(handler);
            _logger.LogDebug("Unsubscribed handler {HandlerType} from event type {EventType}", 
                handler.GetType().Name, eventType.Name);
        }
    }

    public Dictionary<Type, int> GetActiveSubscriptions()
    {
        return _handlers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
    }
}