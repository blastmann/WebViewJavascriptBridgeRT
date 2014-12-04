using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace WebViewJavascriptBridgeRT
{
	public delegate void WVJBResponseCallBack(object data);
	public delegate void WVJBHandler(object data, WVJBResponseCallBack responseCallBack);
	public sealed class WebViewJavascriptBridge
	{
		private WeakReference<WebView> _webViewReference;
		private WVJBHandler _messageHandler;
		private List<Dictionary<string, string>> _startupMessageQueue;
		private Dictionary<string, WVJBResponseCallBack> _responseCallbacks;
		private Dictionary<string, WVJBHandler> _messageHandlers;
		private long _uniqueId;
		private ulong _numRequestsLoading;

		public WebViewJavascriptBridge(WebView webView, WVJBHandler handler)
		{
			Setup(webView, handler);
		}

		public void Send(object message)
		{
			this.Send(message, null);
		}

		public void Send(object message, WVJBResponseCallBack responseCallBack)
		{

		}

		public void RegisterHandlder(string handlerName, WVJBHandler handler)
		{

		}

		public void callHandler(string handlerName)
		{

		}

		public void callHandler(string handlerName, object data)
		{

		}

		public void callHandler(string handlerName, object data, WVJBResponseCallBack responseCallBack)
		{

		}

		private void Setup(WebView webView, WVJBHandler handler)
		{
			_webViewReference = new WeakReference<WebView>(webView);
			_messageHandler = handler;
		}

		private void SendData(string data, WVJBResponseCallBack responseCallBack, string handlerName)
		{
			var message = new Dictionary<string, string>();
			if (data != null)
			{
				message.Add("data", data);
			}

			if (responseCallBack != null)
			{
				_uniqueId++;
				string callbackId = "cb_" + _uniqueId;
				_responseCallbacks.Add(callbackId, responseCallBack);
				message.Add("callbackId", callbackId);
			}

			if (!string.IsNullOrEmpty(handlerName))
			{
				message.Add("handlerName", handlerName);
			}

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

		private Dictionary<string, string> DeserializeMessage(string message)
		{
			using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(message)))
			{
				var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
				return (Dictionary<string, string>)serializer.ReadObject(ms);
			}
		}
	}
}
