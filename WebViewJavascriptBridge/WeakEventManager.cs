using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Implements a weak event listener that allows the owner to be garbage
/// collected if its only remaining link is an event handler.
/// </summary>
public static class WeakEventManager
{
	private readonly static List<WeakEvent> registeredEvents = new List<WeakEvent>();

	/// <summary>
	/// Adds the specified event handler to the specified event.
	/// </summary>
	/// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
	/// <param name="sourceType">
	/// The type of the class that contains the specified event.
	/// </param>
	/// <param name="eventName">The name of the event to subscribe to.</param>
	/// <param name="handler">The delegate that handles the event.</param>
	public static void AddHandler<TEventArgs>
		(Type sourceType, string eventName, EventHandler<TEventArgs> handler)
	{
		EventInfo eventInfo = sourceType.GetRuntimeEvent(eventName);
		registeredEvents.Add(
			new WeakEvent(null, eventInfo, handler));
	}

	/// <summary>
	/// Adds the specified event handler to the specified event.
	/// </summary>
	/// <typeparam name="TEventSource">The type that raises the event.</typeparam>
	/// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
	/// <param name="source">
	/// The source object that raises the specified event.
	/// </param>
	/// <param name="eventName">The name of the event to subscribe to.</param>
	/// <param name="handler">The delegate that handles the event.</param>
	public static void AddHandler<TEventSource, TEventArgs>
		(TEventSource source, string eventName, EventHandler<TEventArgs> handler)
	{
		EventInfo eventInfo = typeof(TEventSource).GetRuntimeEvent(eventName);
		registeredEvents.Add(
			new WeakEvent(source, eventInfo, handler));
	}

	/// <summary>
	/// Removes the specified event handler from the specified event.
	/// </summary>
	/// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
	/// <param name="sourceType">
	/// The type of the class that contains the specified event.
	/// </param>
	/// <param name="eventName">
	/// The name of the event to remove the handler from.
	/// </param>
	/// <param name="handler">The delegate to remove.</param>
	public static void RemoveHandler<TEventArgs>
		(Type sourceType, string eventName, EventHandler<TEventArgs> handler)
	{
		EventInfo eventInfo = sourceType.GetRuntimeEvent(eventName);
		foreach (WeakEvent weakEvent in registeredEvents)
		{
			if (weakEvent.IsEqualTo(null, eventInfo, handler))
			{
				weakEvent.Detach();
				break;
			}
		}
	}

	/// <summary>
	/// Removes the specified event handler from the specified event.
	/// </summary>
	/// <typeparam name="TEventSource">The type that raises the event.</typeparam>
	/// <typeparam name="TEventArgs">The type that holds the event data.</typeparam>
	/// <param name="source">
	/// The source object that raises the specified event, or null if it's
	/// a static event.
	/// </param>
	/// <param name="eventName">
	/// The name of the event to remove the handler from.
	/// </param>
	/// <param name="handler">The delegate to remove.</param>
	public static void RemoveHandler<TEventSource, TEventArgs>
		(TEventSource source, string eventName, EventHandler<TEventArgs> handler)
	{
		EventInfo eventInfo = typeof(TEventSource).GetRuntimeEvent(eventName);
		foreach (WeakEvent weakEvent in registeredEvents)
		{
			if (weakEvent.IsEqualTo(source, eventInfo, handler))
			{
				weakEvent.Detach();
				break;
			}
		}
	}

	private class WeakEvent
	{
		private static readonly MethodInfo onEventInfo =
			typeof(WeakEvent).GetTypeInfo().GetDeclaredMethod("OnEvent");

		private EventInfo eventInfo;
		private object eventRegistration;
		private MethodInfo handlerMethod;
		private WeakReference<object> handlerTarget;
		private object source;

		public WeakEvent(object source, EventInfo eventInfo, Delegate handler)
		{
			this.source = source;
			this.eventInfo = eventInfo;

			// We can't store a reference to the handler (as that will keep
			// the target alive) but we also can't store a WeakReference to
			// handler, as that could be GC'd before its target.
			this.handlerMethod = handler.GetMethodInfo();
			this.handlerTarget = new WeakReference<object>(handler.Target);

			object onEventHandler = this.CreateHandler();
			this.eventRegistration = eventInfo.AddMethod.Invoke(
				source,
				new object[] { onEventHandler });

			// If the AddMethod returned null then it was void - to
			// unregister we simply need to pass in the same delegate.
			if (this.eventRegistration == null)
			{
				this.eventRegistration = onEventHandler;
			}
		}

		public void Detach()
		{
			if (this.eventInfo != null)
			{
				WeakEventManager.registeredEvents.Remove(this);

				this.eventInfo.RemoveMethod.Invoke(
					this.source,
					new object[] { eventRegistration });

				this.eventInfo = null;
				this.eventRegistration = null;
			}
		}

		public bool IsEqualTo(object source, EventInfo eventInfo, Delegate handler)
		{
			if ((source == this.source) && (eventInfo == this.eventInfo))
			{
				object target;
				if (this.handlerTarget.TryGetTarget(out target))
				{
					return (handler.Target == target) &&
							(handler.GetMethodInfo() == this.handlerMethod);
				}
			}

			return false;
		}

		public void OnEvent<T>(object sender, T args)
		{
			object instance;
			if (this.handlerTarget.TryGetTarget(out instance))
			{
				this.handlerMethod.Invoke(instance, new object[] { sender, args });
			}
			else
			{
				this.Detach();
			}
		}

		private object CreateHandler()
		{
			Type eventType = this.eventInfo.EventHandlerType;
			ParameterInfo[] parameters = eventType.GetTypeInfo()
													.GetDeclaredMethod("Invoke")
													.GetParameters();

			return onEventInfo.MakeGenericMethod(parameters[1].ParameterType)
								.CreateDelegate(eventType, this);
		}
	}
}