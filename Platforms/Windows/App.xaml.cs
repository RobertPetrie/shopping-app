using Microsoft.UI.Xaml;
using shopping_app.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace shopping_app.WinUI
{
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public partial class App : MauiWinUIApplication
	{
		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			// Register before InitializeComponent so the real exception is written
			// to debug.log before Visual Studio's generated handler breaks in App.g.i.cs.
			UnhandledException += OnWinUiUnhandledException;

			this.InitializeComponent();
		}

		private static void OnWinUiUnhandledException(
			object sender,
			Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
		{
			DevelopmentDebugArtifacts.WriteLine(
				$"[WINUI UNHANDLED] Message={e.Message}");

			DevelopmentDebugArtifacts.WriteLine(
				e.Exception?.ToString()
					?? "[WINUI UNHANDLED] Exception object was null.");

			try
			{
				var shell = Microsoft.Maui.Controls.Shell.Current;

				string stack = shell is null
					? "(Shell unavailable)"
					: string.Join(
						" -> ",
						shell.Navigation.NavigationStack.Select(
							page => page is null
								? "(null page)"
								: $"{page.GetType().Name}(Title=\"{page.Title}\")"));

				DevelopmentDebugArtifacts.WriteLine(
					$"[WINUI UNHANDLED] Shell={shell?.CurrentState?.Location}; "
					+ $"Stack=[{stack}]");
			}
			catch (Exception logException)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"[WINUI UNHANDLED] Could not capture navigation state: "
					+ logException);
			}

			// Do not mark the exception handled. We want normal debugger/crash
			// behavior after the diagnostic information has been captured.
		}

		protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
	}

}
