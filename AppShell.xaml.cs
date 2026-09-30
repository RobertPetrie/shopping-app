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
		}
	}
}