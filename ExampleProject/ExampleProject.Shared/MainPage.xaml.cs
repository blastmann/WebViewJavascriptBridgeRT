﻿using System;
using System.Collections.ObjectModel;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using WebViewJavascriptBridgeRT;

namespace ExampleProject
{
	public partial class MainPage
	{
		private WebViewJavascriptBridge _bridge;
		private readonly ObservableCollection<string> _outputResults = new ObservableCollection<string>();

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			OutputBox.ItemsSource = _outputResults;

			_bridge = new WebViewJavascriptBridge(TestWebView, (data, callback) =>
			{
				_outputResults.Insert(0, @"Receive message from JS: " + data);
				callback(@"Response for message from C#");
			});

			_bridge.RegisterHandler(@"testCSharpCallback", (data, callback) =>
			{
				_outputResults.Insert(0, @"Receive message from JS: " + data);
				callback(@"Response from testCSharpCallback");
			});

			_bridge.Send(@"A string sent from C# before Webview has loaded.",
				data => _outputResults.Insert(0, @"C# got response! " + data));

			_bridge.CallHandler(@"testJavascriptHandler", @"{ 'foo':'before ready'}");

			TestWebView.Navigate(new Uri("ms-appx-web:///ExampleApp.html"));

			_bridge.Send(@"A string sent from C# after Webview has loaded.");
		}

		private void SendMessage(object sender, RoutedEventArgs e)
		{
			_bridge.Send(@"A string sent from C# to JS", data => _outputResults.Insert(0, @"SendMessage got response: " + data));
		}

		private void CallHandler(object sender, RoutedEventArgs e)
		{
			var json = new
			{
				greetingFromCSharp = "Hi there, JS!"
			};
			_bridge.CallHandler(@"testJavascriptHandler", json,
				s => _outputResults.Insert(0, @"testJavascriptHandler responded: " + s));
		}
	}
}
