using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ExampleProject
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private WebViewJavascriptBridgeRT.WebViewJavascriptBridge _bridge;

		public MainPage()
		{
			this.InitializeComponent();
		}
	}
}
