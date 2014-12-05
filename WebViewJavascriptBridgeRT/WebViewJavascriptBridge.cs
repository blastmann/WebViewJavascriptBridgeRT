using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WebViewJavascriptBridgeRT
{
	public delegate void WVJBResponseCallback(string data);
	public delegate void WVJBHandler(string data, WVJBResponseCallback responseCallback);
	public sealed class WebViewJavascriptBridge
	{
		private const string KCustomProtocolScheme = "wvjbscheme";
		private const string KQueueHasMessage = "__WVJB_QUEUE_MESSAGE__";

		private WeakReference<WebView> _webViewReference;
		private WVJBHandler _messageHandler;
		private List<Dictionary<string, string>> _startupMessageQueue;
		private Dictionary<string, WVJBResponseCallback> _responseCallbacks;
		private Dictionary<string, WVJBHandler> _messageHandlers;
		private long _uniqueId;
		private ulong _numRequestsLoading;

		public WebViewJavascriptBridge(WebView webView, WVJBHandler handler)
		{
			Setup(webView, handler);
		}

		public void Send(string message)
		{
			this.Send(message, null);
		}

		public void Send(string message, WVJBResponseCallback responseCallback)
		{
			SendData(message, responseCallback, null);
		}

		public void CallHandler(string handlerName)
		{
			CallHandler(handlerName, null);
		}

		public void CallHandler(string handlerName, string data)
		{
			CallHandler(handlerName, data, null);
		}

		public void CallHandler(string handlerName, string data, WVJBResponseCallback responseCallback)
		{
			SendData(data, responseCallback, handlerName);
		}

		public void RegisterHandlder(string handlerName, WVJBHandler handler)
		{
			_messageHandlers[handlerName] = handler;
		}

		public void Destroy()
		{
			WebView webView;
			if (_webViewReference.TryGetTarget(out webView))
			{
				webView.NavigationStarting -= this.WebViewOnNavigationStarting;
				webView.NavigationFailed -= this.WebViewOnNavigationFailed;
				webView.NavigationCompleted -= this.WebViewOnNavigationCompleted;
				webView.ScriptNotify -= this.WebViewOnScriptNotify;
			}

			_startupMessageQueue.Clear();
			_startupMessageQueue = null;
			_responseCallbacks.Clear();
			_responseCallbacks = null;

			_messageHandler = null;
			_messageHandlers = null;
		}

		private void Setup(WebView webView, WVJBHandler handler)
		{
			_startupMessageQueue = new List<Dictionary<string, string>>();
			_responseCallbacks = new Dictionary<string, WVJBResponseCallback>();
			_uniqueId = 0;

			webView.ScriptNotify += WebViewOnScriptNotify;
			webView.NavigationStarting += WebViewOnNavigationStarting;
			webView.NavigationFailed += WebViewOnNavigationFailed;
			webView.NavigationCompleted += WebViewOnNavigationCompleted;

			_webViewReference = new WeakReference<WebView>(webView);
			_messageHandler = handler;
		}

		private async void WebViewOnNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
		{
			_numRequestsLoading--;

			if (_numRequestsLoading == 0 && await sender.InvokeScriptAsync("eval", new[] { @"typof WebViewJavascriptBridge == 'object'" }) == "true")
			{
				// todo: load js from file
				await sender.InvokeScriptAsync("eval", new[] { "" });
			}

			var startupMessageQueue = _startupMessageQueue;
			_startupMessageQueue = null;

			if (startupMessageQueue != null)
			{
				foreach (var message in startupMessageQueue)
				{
					DispatchMessage(message);
				}
			}
		}

		private void WebViewOnNavigationFailed(object sender, WebViewNavigationFailedEventArgs webViewNavigationFailedEventArgs)
		{
			_numRequestsLoading--;
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

			var url = new Uri(notifyMessage);
			if (url.Scheme != KCustomProtocolScheme) return;

			if (url.Host == KQueueHasMessage)
			{
				FlushMessage();
			}
			else
			{
				Debug.WriteLine("WebViewJavascriptBridge: WARNING: Received unknown WebViewJavascriptBridge command {0}://{1}", KCustomProtocolScheme, url.PathAndQuery);
			}
		}

		private void SendData(string data, WVJBResponseCallback responseCallback, string handlerName)
		{
			var message = new Dictionary<string, string>();
			if (data != null)
			{
				message["data"] = data;
			}

			if (responseCallback != null)
			{
				_uniqueId++;
				string callbackId = "cb_" + _uniqueId;
				_responseCallbacks[callbackId] = responseCallback;
				message["callbackId"] = callbackId;
			}

			if (!string.IsNullOrEmpty(handlerName))
			{
				message["handlerName"] = handlerName;
			}
			QueueMessage(message);
		}

		private void QueueMessage(Dictionary<string, string> message)
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
			if (!_webViewReference.TryGetTarget(out webView))
				return;

			var messageQueueString = await webView.InvokeScriptAsync("WebViewJavascriptBridge._fetchQueue();", null);
			if (string.IsNullOrEmpty(messageQueueString))
				return;

			var messages = DeserializeMessage(messageQueueString);
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

							var msg = new Dictionary<string, string>
							{
								{"responseId", callbackId},
								{"responseData", responseData}
							};
							QueueMessage(msg);
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

		private async void DispatchMessage(Dictionary<string, string> message)
		{
			var messageJSON = SerializedMessage(message);
			if (!string.IsNullOrEmpty(messageJSON))
			{
				messageJSON = messageJSON.Replace("\\", "\\\\");
				messageJSON = messageJSON.Replace("\"", "\\\"");
				messageJSON = messageJSON.Replace("\'", "\\\'");
				messageJSON = messageJSON.Replace("\n", "\\n");
				messageJSON = messageJSON.Replace("\r", "\\r");
				messageJSON = messageJSON.Replace("\f", "\\f");
				messageJSON = messageJSON.Replace("\u2028", "\\u2028");
				messageJSON = messageJSON.Replace("\u2029", "\\u2029");

				var jsCommand = string.Format("WebViewJavascriptBridge._handleMessageFromNative('{0}');", messageJSON);
				if (Window.Current.Dispatcher.HasThreadAccess)
				{
					await TryExecuteJsCommand(jsCommand, null);
				}
				else
				{
					Window.Current.Dispatcher.RunIdleAsync(async args =>
					{
						await TryExecuteJsCommand(jsCommand, null);
					});
				}
			}
		}

		private async Task<object> TryExecuteJsCommand(string jsCommand, IEnumerable<string> arguments)
		{
			WebView webView;
			_webViewReference.TryGetTarget(out webView);
			if (webView != null)
			{
				return await webView.InvokeScriptAsync(jsCommand, arguments);
			}
			return null;
		}

		private string SerializedMessage(Dictionary<string, string> message)
		{
			using (var ms = new MemoryStream())
			{
				var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
				serializer.WriteObject(ms, message);
				var resultByte = ms.ToArray();
				return Encoding.UTF8.GetString(resultByte, 0, resultByte.Length);
			}
		}

		private IEnumerable<Dictionary<string, string>> DeserializeMessage(string message)
		{
			using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(message)))
			{
				var serializer = new DataContractJsonSerializer(typeof(IEnumerable<Dictionary<string, string>>));
				return serializer.ReadObject(ms) as IEnumerable<Dictionary<string, string>>;
			}
		}
	}
}
