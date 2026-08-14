using Osb.Lang.Ast;

namespace Osb.Lang.Runtime.Events;

/// <summary>
/// Manages event subscriptions for an object instance, using weak references
/// to avoid memory leaks when subscribers are destroyed.
/// </summary>
public sealed class EventSubscriptionManager
{
    private readonly Dictionary<string, List<WeakReference<Action<IReadOnlyList<OslangValue>>>>> _subscriptions = new();

    public void Subscribe(string eventName, Action<IReadOnlyList<OslangValue>> handler)
    {
        if (!_subscriptions.TryGetValue(eventName, out var handlers))
        {
            handlers = new List<WeakReference<Action<IReadOnlyList<OslangValue>>>>();
            _subscriptions[eventName] = handlers;
        }

        handlers.Add(new WeakReference<Action<IReadOnlyList<OslangValue>>>(handler));
    }

    public void Unsubscribe(string eventName, Action<IReadOnlyList<OslangValue>> handler)
    {
        if (!_subscriptions.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        handlers.RemoveAll(h =>
        {
            if (!h.TryGetTarget(out var target))
            {
                return true;
            }

            return target != handler;
        });
    }

    public void Raise(string eventName, IReadOnlyList<OslangValue> args)
    {
        if (!_subscriptions.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        var toRemove = new List<WeakReference<Action<IReadOnlyList<OslangValue>>>>();

        foreach (var handlerRef in handlers)
        {
            if (handlerRef.TryGetTarget(out var handler))
            {
                try
                {
                    handler(args);
                }
                catch
                {
                    // Ignore handler exceptions
                }
            }
            else
            {
                toRemove.Add(handlerRef);
            }
        }

        foreach (var dead in toRemove)
        {
            handlers.Remove(dead);
        }
    }
}
