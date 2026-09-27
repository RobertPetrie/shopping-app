using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Models;

namespace shopping_app
{
	public partial class MainPage : ContentPage
	{
		private bool isLoading;
		private bool sortAscending = true;
		private List<Product> products = new();

		public MainPage()
		{
			InitializeComponent();

			Loaded += OnPageLoaded;
		}

		private async void OnPageLoaded(object? sender, EventArgs e)
		{
			if (isLoading)
			{
				return;
			}

			isLoading = true;
			SortProductsButton.IsEnabled = false;
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

				products = await connection
					.Table<Product>()
					.ToListAsync();

				ApplySort();

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
				SortProductsButton.IsEnabled = products.Count > 0;
			}
		}

		private void OnSortProductsClicked(object? sender, EventArgs e)
		{
			sortAscending = !sortAscending;
			ApplySort();
		}

		private void ApplySort()
		{
			var sortedProducts = sortAscending
				? products.OrderBy(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase).ToList()
				: products.OrderByDescending(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase).ToList();

			ProductsView.ItemsSource = sortedProducts;

			SortProductsButton.Text = sortAscending ? "A|Z" : "Z|A";

			SemanticProperties.SetDescription(
				SortProductsButton,
				sortAscending
					? "Sorted A to Z. Tap to sort Z to A."
					: "Sorted Z to A. Tap to sort A to Z.");

			// Show the beginning of the newly sorted list.
			if (sortedProducts.Count > 0)
			{
				ProductsView.ScrollTo(
					0,
					position: ScrollToPosition.Start,
					animate: false);
			}
		}
	}
}