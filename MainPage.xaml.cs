using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Models;

namespace shopping_app
{
	public partial class MainPage : ContentPage
	{
		private bool isLoading;

		public MainPage()
		{
			InitializeComponent();

			// Wait until the page is ready before accessing its services.
			Loaded += OnPageLoaded;
		}

		private async void OnPageLoaded(object? sender, EventArgs e)
		{
			if (isLoading)
			{
				return;
			}

			isLoading = true;
			StatusLabel.Text = "Loading products...";

			try
			{
				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				var database = services.GetRequiredService<AppDatabase>();

				System.Diagnostics.Debug.WriteLine(
					$"Database location: {database.DatabasePath}");

				var connection = await database.GetConnectionAsync();

				var products = await connection
					.Table<Product>()
					.ToListAsync();

				ProductsView.ItemsSource = products
					.OrderBy(
						product => product.Name,
						StringComparer.CurrentCultureIgnoreCase)
					.ToList();

				StatusLabel.Text = $"{products.Count} products";

				System.Diagnostics.Debug.WriteLine(
					$"Loaded {products.Count} products.");
			}
			catch (Exception ex)
			{
				StatusLabel.Text = "Could not load products.";

				System.Diagnostics.Debug.WriteLine(
					$"Error loading products: {ex}");

				await DisplayAlertAsync(
					"Products",
					ex.Message,
					"OK");
			}
			finally
			{
				isLoading = false;
			}
		}
	}
}