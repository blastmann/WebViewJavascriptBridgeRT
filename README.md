WebViewJavascriptBridgeRT
=========================

A WinRT 8.1 [WebViewJavascriptBridge](https://github.com/marcuswestin/WebViewJavascriptBridge), you can send message between C# and JS with WebView.

HOW TO?
------------

To use a `WebViewJavascriptBridge`, please follow these steps.


## First

Create a bridge instance, pass your target WebView.

``` C#

var bridge = new WebViewJavascriptBridge(TestWebView, (data, callback) =>
{
    Debug.WriteLine(@"Receive message from JS: " + data);
    callback(@"Response for message from C#");
});

```

## Register native function for your JS 


``` C#

bridge.RegisterHandler(@"testCSharpCallback", (data, callback) =>
{
    Debug.WriteLine(@"Receive message from JS: " + data);
    callback(@"Response from testCSharpCallback");
});

```

Then you can call C# code by sending `testCSharpCallback` string to native.



What's next?
------------

- Keep improving performance of sending message from JS.
