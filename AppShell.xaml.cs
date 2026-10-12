using shopping_app.Data;
using shopping_app.Pages;

namespace shopping_app
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();

			// Register the detail page as a navigation destination.
			// nameof keeps the route name aligned with the class name.
			Routing.RegisterRoute(
				nameof(ProductDetailPage),
				typeof(ProductDetailPage));

			// A shopping-list card opens its own checklist of related products.
			Routing.RegisterRoute(
				nameof(ShoppingListProductsPage),
				typeof(ShoppingListProductsPage));

			// Reuse MainPage as a pushed product picker when adding Products to a
			// Shopping List. The normal Products tab keeps its original behavior.
			Routing.RegisterRoute(
				MainPage.ShoppingListSelectionRoute,
				typeof(MainPage));

			// Keep a breadcrumb trail of every Shell route change. This is much
			// more useful than logging every mouse click because it shows exactly
			// which page stack led into a navigation crash.
			Navigating += OnShellNavigating;
			Navigated += OnShellNavigated;

			DevelopmentDebugArtifacts.WriteLine(
				$"[NAV] Shell ready. {GetNavigationSnapshot()}");
		}

		private void OnShellNavigating(
			object? sender,
			ShellNavigatingEventArgs e)
		{
			DevelopmentDebugArtifacts.WriteLine(
				$"[NAV] START Source={e.Source}; "
				+ $"Current={DescribeState(e.Current)}; "
				+ $"Target={DescribeState(e.Target)}; "
				+ GetNavigationSnapshot());
		}

		private void OnShellNavigated(
			object? sender,
			ShellNavigatedEventArgs e)
		{
			DevelopmentDebugArtifacts.WriteLine(
				$"[NAV] END Source={e.Source}; "
				+ $"Previous={DescribeState(e.Previous)}; "
				+ $"Current={DescribeState(e.Current)}; "
				+ GetNavigationSnapshot());
		}

		private static string DescribeState(ShellNavigationState? state) =>
			state?.Location?.ToString() ?? "(none)";

		private string GetNavigationSnapshot()
		{
			try
			{
				string stack = string.Join(
					" -> ",
					Navigation.NavigationStack.Select(DescribePage));

				string modals = string.Join(
					" -> ",
					Navigation.ModalStack.Select(DescribePage));

				return $"Stack=[{stack}] Modal=[{modals}]";
			}
			catch (Exception ex)
			{
				return $"Navigation snapshot unavailable: {ex.Message}";
			}
		}

		private static string DescribePage(Page page) =>
			$"{page.GetType().Name}(Title=\"{page.Title}\")";
	}
}