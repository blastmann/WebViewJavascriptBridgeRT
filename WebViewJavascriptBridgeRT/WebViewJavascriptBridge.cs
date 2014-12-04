using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		private void DispatchMessage(Dictionary<string, string> message)
		{

		}

		private string SerializedMessage(Dictionary<string, string> message)
		{
			return string.Empty;
		}

		private Dictionary<string, string> DeserializeMessage(string message)
		{
			return null;
		}


		//		- (void)_dispatchMessage:(WVJBMessage*)message {
		//	NSString *messageJSON = [self _serializeMessage:message];
		//	[self _log:@"SEND" json:messageJSON];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\\" withString:@"\\\\"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\"" withString:@"\\\""];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\'" withString:@"\\\'"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\n" withString:@"\\n"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\r" withString:@"\\r"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\f" withString:@"\\f"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\u2028" withString:@"\\u2028"];
		//	messageJSON = [messageJSON stringByReplacingOccurrencesOfString:@"\u2029" withString:@"\\u2029"];

		//	NSString* javascriptCommand = [NSString stringWithFormat:@"WebViewJavascriptBridge._handleMessageFromObjC('%@');", messageJSON];
		//	if ([[NSThread currentThread] isMainThread]) {
		//		[_webView stringByEvaluatingJavaScriptFromString:javascriptCommand];
		//	} else {
		//		__strong WVJB_WEBVIEW_TYPE* strongWebView = _webView;
		//		dispatch_sync(dispatch_get_main_queue(), ^{
		//			[strongWebView stringByEvaluatingJavaScriptFromString:javascriptCommand];
		//		});
		//	}
		//}
	}
}
