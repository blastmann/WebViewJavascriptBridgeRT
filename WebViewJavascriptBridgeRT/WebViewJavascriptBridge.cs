using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace WebViewJavascriptBridgeRT
{
	public delegate void WVJBResponseCallback(string data);
	public delegate void WVJBHandler(string data, WVJBResponseCallback responseCallback);

	public sealed class WebViewJavascriptBridge
	{
		private const string KCustomProtocolScheme = "wvjbscheme";
		private const string KQueueHasMessage = "__WVJB_QUEUE_MESSAGE__";

		private WeakReference<WebView> _webViewReference;

		/// <summary>
		/// Current message handler
		/// </summary>
		private WVJBHandler _messageHandler;

		/// <summary>
		/// MessageQueue in native codes
		/// </summary>
		private List<BridgeMessage> _startupMessageQueue;

		/// <summary>
		/// Callbacks
		/// </summary>
		private Dictionary<string, WVJBResponseCallback> _responseCallbacks;

		/// <summary>
		/// Message Handlers from outside
		/// </summary>
		private Dictionary<string, WVJBHandler> _messageHandlers;

		private long _uniqueId;
		private ulong _numRequestsLoading;

		public WebViewJavascriptBridge(WebView webView, WVJBHandler handler)
		{
			Setup(webView, handler);
		}

		/// <summary>
		/// Send message to JS
		/// </summary>
		/// <param name="message"></param>
		public void Send(string message)
		{
			this.Send(message, null);
		}

		/// <summary>
		/// Send message to JS with native callback
		/// </summary>
		/// <param name="message"></param>
		/// <param name="responseCallback"></param>
		public void Send(string message, WVJBResponseCallback responseCallback)
		{
			SendData(message, responseCallback, null);
		}

		/// <summary>
		/// Call handler registered in native code.
		/// </summary>
		/// <param name="handlerName"></param>
		public void CallHandler(string handlerName)
		{
			CallHandler(handlerName, null);
		}

		/// <summary>
		/// Call handler with stringify data
		/// </summary>
		/// <param name="handlerName"></param>
		/// <param name="data"></param>
		public void CallHandler(string handlerName, string data)
		{
			CallHandler(handlerName, data, null);
		}

		/// <summary>
		/// Call handler with data and response callback
		/// </summary>
		/// <param name="handlerName"></param>
		/// <param name="data"></param>
		/// <param name="responseCallback"></param>
		public void CallHandler(string handlerName, object data, WVJBResponseCallback responseCallback)
		{
			SendData(data, responseCallback, handlerName);
		}

		/// <summary>
		/// Register a js handler
		/// </summary>
		/// <param name="handlerName"></param>
		/// <param name="handler"></param>
		public void RegisterHandlder(string handlerName, WVJBHandler handler)
		{
			_messageHandlers[handlerName] = handler;
		}

		private void Setup(WebView webView, WVJBHandler handler)
		{
			_startupMessageQueue = new List<BridgeMessage>();

			_responseCallbacks = new Dictionary<string, WVJBResponseCallback>();
			_uniqueId = 0;

			_webViewReference = new WeakReference<WebView>(webView);
			_messageHandler = handler;
			_messageHandlers = new Dictionary<string, WVJBHandler>();

			WeakEventManager.AddHandler(webView, "ScriptNotify", this.WebViewOnScriptNotify);
			WeakEventManager.AddHandler<WebView, WebViewNavigationStartingEventArgs>(webView, "NavigationStarting", this.WebViewOnNavigationStarting);
			WeakEventManager.AddHandler<WebView, WebViewNavigationCompletedEventArgs>(webView, "NavigationCompleted", this.WebViewOnNavigationCompleted);
		}

		private async void WebViewOnNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
		{
			_numRequestsLoading--;
			if (!args.IsSuccess)
				return;

			if (_numRequestsLoading == 0)
			{
				var result = await sender.EvalScript("typeof WebViewJavascriptBridge == 'object'");
				if (result == "true")
					return;

				var folder = await Package.Current.InstalledLocation.GetFolderAsync("WebViewJavascriptBridgeRT");
				if (folder == null)
					return;

				var file = await folder.GetFileAsync("WebViewJavascriptBridge.js");
				if (file == null)
					return;

				var js = await FileIO.ReadTextAsync(file);
				await sender.EvalScript(js);
			}

			var startupMessageQueue = _startupMessageQueue;
			_startupMessageQueue = null;

			if (startupMessageQueue == null) return;

			foreach (var message in startupMessageQueue)
			{
				DispatchMessage(message);
			}
		}

		private void WebViewOnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
		{
			_numRequestsLoading++;
		}

		private void WebViewOnScriptNotify(object sender, NotifyEventArgs notifyEventArgs)
		{
			var notifyMessage = notifyEventArgs.Value;
			if (string.IsNullOrEmpty(notifyMessage))
				return;

			if (!notifyMessage.StartsWith(KCustomProtocolScheme, StringComparison.Ordinal))
				return;

			if (notifyMessage.IndexOf(KQueueHasMessage, StringComparison.Ordinal) >= 0)
			{
				FlushMessage();
			}
			else
			{
				Debug.WriteLine("WebViewJavascriptBridge: WARNING: Received unknown WebViewJavascriptBridge command: " + notifyMessage);
			}
		}

		private void SendData(object data, WVJBResponseCallback responseCallback, string handlerName)
		{
			var message = new BridgeMessage();
			if (data != null)
			{
				message.data = data;
			}

			if (responseCallback != null)
			{
				_uniqueId++;
				string callbackId = "cb_" + _uniqueId;
				_responseCallbacks[callbackId] = responseCallback;
				message.callbackId = callbackId;
			}

			if (!string.IsNullOrEmpty(handlerName))
			{
				message.handlerName = handlerName;
			}

			QueueMessage(message);
		}

		private void QueueMessage(BridgeMessage message)
		{
			if (_startupMessageQueue != null)
			{
				_startupMessageQueue.Add(message);
			}
			else
			{
				DispatchMessage(message);
			}
		}

		private async void FlushMessage()
		{
			WebView webView;
			_webViewReference.TryGetTarget(out webView);
			if (webView == null)
				return;

			var messageQueueString = await webView.EvalScript("WebViewJavascriptBridge._fetchQueue();");
			if (string.IsNullOrEmpty(messageQueueString))
				return;

			var messages = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(messageQueueString);
			if (messages == null)
			{
				Debug.WriteLine("WebViewJavascriptBridge: WARNING: Invalid received");
				return;
			}

			foreach (var message in messages)
			{
				string responseId;
				string data;
				WVJBResponseCallback responseCallback;
				if (message.TryGetValue("responseId", out responseId) && message.TryGetValue("responseData", out data))
				{
					if (_responseCallbacks.TryGetValue(responseId, out responseCallback))
					{
						responseCallback(data);
						_responseCallbacks.Remove(responseId);
					}
				}
				else
				{
					string callbackId;
					if (message.TryGetValue("callbackId", out callbackId))
					{
						responseCallback = delegate(string responseData)
						{
							if (string.IsNullOrEmpty(responseData))
							{
								responseData = string.Empty;
							}

							QueueMessage(new BridgeMessage { responseId = callbackId, responseData = responseData });
						};
					}
					else
					{
						responseCallback = s => Debug.WriteLine("Empty callback");
					}

					WVJBHandler handler;
					string handlerName;
					if (message.TryGetValue("handlerName", out handlerName))
					{
						_messageHandlers.TryGetValue(handlerName, out handler);
					}
					else
					{
						handler = _messageHandler;
					}

					if (handler == null)
						throw new Exception("No handler for message from JS:" + message);

					string messageData;
					if (message.TryGetValue("data", out messageData))
					{
						handler(messageData, responseCallback);
					}
				}
			}
		}

		private void DispatchMessage(BridgeMessage message)
		{
			var messageJSON = JsonConvert.SerializeObject(message, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
			if (!string.IsNullOrEmpty(messageJSON))
			{
				var jsCommand = string.Format("WebViewJavascriptBridge._handleMessageFromNative('{0}');", messageJSON);
				if (Window.Current.Dispatcher.HasThreadAccess)
				{
					TryExecuteJsCommand(jsCommand);
				}
				else
				{
					Window.Current.Dispatcher.RunIdleAsync(args => TryExecuteJsCommand(jsCommand));
				}
			}
		}

		private void TryExecuteJsCommand(string jsCommand)
		{
			try
			{
				WebView webView;
				_webViewReference.TryGetTarget(out webView);
				if (webView != null)
				{
					webView.EvalScript(jsCommand);
				}
			}
			catch (Exception exception)
			{
				Debug.WriteLine(jsCommand);
				Debug.WriteLine(exception.Message);
			}
		}
	}

	/// <summary>
	/// BridgeMessage Class for passing message to WebView
	/// </summary>
	public class BridgeMessage
	{
		public string handlerName { get; set; }
		public object data { get; set; }
		public string callbackId { get; set; }
		public string responseId { get; set; }
		public string responseData { get; set; }
	}

	public static class WebViewExtensions
	{
		public static IAsyncOperation<string> EvalScript(this WebView webView, string script)
		{
			return webView.InvokeScriptAsync("eval", new[] { script });
		}
	}
}
