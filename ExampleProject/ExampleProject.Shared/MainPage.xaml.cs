using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using WebViewJavascriptBridgeRT;

namespace ExampleProject
{
	public partial class MainPage
	{
		private WebViewJavascriptBridge _bridge;
		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);

			_bridge = new WebViewJavascriptBridge(TestWebView, (data, callback) =>
			{
				OutputTextBlock.Text = @"Receive message from JS: " + data + "\n" + OutputTextBlock.Text;
				callback(@"Response for message from C#");
			});

			_bridge.RegisterHandlder(@"testCSharpCallback", delegate(string data, WVJBResponseCallback callback)
			{
				OutputTextBlock.Text = @"Receive message from JS: " + data + "\n" + OutputTextBlock.Text;
				callback(@"Response from testCSharpCallback");
			});

			_bridge.Send(@"A string sent from C# before Webview has loaded.", delegate(string data)
			{
				OutputTextBlock.Text = @"C# got response! " + data + "\n" + OutputTextBlock.Text;
			});

			_bridge.CallHandler(@"testJavascriptHandler", @"{ 'foo':'before ready'}");

			TestWebView.Navigate(new Uri("ms-appx-web:///ExampleApp.html"));

			TestWebView.NavigationCompleted += (sender, args) =>
				_bridge.Send(@"A string sent from C# after Webview has loaded.");
		}

		private void SendMessage(object sender, RoutedEventArgs e)
		{
			_bridge.Send(@"A string sent from C# to JS", data =>
			{
				OutputTextBlock.Text = @"SendMessage got response: " + data + "\n" + OutputTextBlock.Text;
			});
		}

		private void CallHandler(object sender, RoutedEventArgs e)
		{
			string data = @"{ 'greetingFromC#': 'Hi there, JS!' }";
			_bridge.CallHandler(@"testJavascriptHandler", data, s =>
			{
				OutputTextBlock.Text = @"testJavascriptHandler responded: " + s + "\n" + OutputTextBlock.Text;

			});
		}
	}
}
