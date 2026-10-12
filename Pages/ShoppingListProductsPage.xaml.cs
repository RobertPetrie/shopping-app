using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace shopping_app.Pages
{
	/// <summary>
	/// Shows the products assigned to one shopping list and lets the shopper
	/// check each item off as it is picked up.
	/// </summary>
	public partial class ShoppingListProductsPage : ContentPage, IQueryAttributable
	{
		private AppDatabase? database;
		private int shoppingListID;
		private bool isLoading;
		private bool isPageActive;
		private bool needsRefresh = true;
		private bool suppressPickedUpChange;
		private List<ShoppingListProductListItem> items = new();

		public ShoppingListProductsPage()
		{
			InitializeComponent();

			// Wait until MAUI has attached platform services before opening SQLite.
			Loaded += OnPageLoaded;
		}

		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			if (query.TryGetValue("ShoppingListToOpen", out var value)
				&& value is ShoppingList shoppingList)
			{
				shoppingListID = shoppingList.ID;
				Title = $"Shopping List - {shoppingList.DisplayDate}";
				needsRefresh = true;
				QueueRefresh();
			}
		}

		protected override void OnNavigatedTo(NavigatedToEventArgs args)
		{
			base.OnNavigatedTo(args);

			isPageActive = true;
			needsRefresh = true;
			QueueRefresh();
		}

		protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
		{
			base.OnNavigatedFrom(args);
			isPageActive = false;
		}

		private void OnPageLoaded(object? sender, EventArgs e)
		{
			QueueRefresh();
		}

		private void QueueRefresh()
		{
			Dispatcher.Dispatch(async () => await RefreshProductsAsync());
		}

		private async Task RefreshProductsAsync()
		{
			if (!isPageActive || !IsLoaded || isLoading || !needsRefresh)
			{
				return;
			}

			if (shoppingListID <= 0)
			{
				StatusLabel.Text = "No shopping list selected.";
				return;
			}

			isLoading = true;
			needsRefresh = false;
			AddProductButton.IsEnabled = false;
			StatusLabel.Text = "Loading products...";

			try
			{
				if (Application.Current is App app)
				{
					await app.TestDataReady;
				}

				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				database ??= services.GetRequiredService<AppDatabase>();

				items = await database.GetShoppingListProductsAsync(shoppingListID);
				ApplyFilter();
			}
			catch (Exception ex)
			{
				needsRefresh = true;
				StatusLabel.Text = "Could not load products.";

				DevelopmentDebugArtifacts.WriteLine(
					$"Error loading shopping-list products: {ex}");

				await DisplayAlertAsync(
					"Shopping List",
					"Could not load the products on this shopping list.",
					"OK");
			}
			finally
			{
				isLoading = false;
				AddProductButton.IsEnabled = shoppingListID > 0;
			}
		}

		private int ApplyFilter()
		{
			string searchText = ProductSearchBar.Text ?? string.Empty;

			var displayedItems = ShoppingListProductFilter.Filter(
				items,
				searchText);

			ProductsView.ItemsSource = displayedItems;

			StatusLabel.Text = ShoppingListProductFilter.GetStatusText(
				items.Count,
				displayedItems.Count,
				searchText);

			return displayedItems.Count;
		}

		private void OnSearchTextChanged(
			object? sender,
			TextChangedEventArgs e)
		{
			int displayedCount = ApplyFilter();

			if (displayedCount > 0)
			{
				Dispatcher.Dispatch(() =>
					ProductsView.ScrollTo(
						0,
						position: ScrollToPosition.Start,
						animate: false));
			}
		}

		private async void OnPickedUpChanged(
			object? sender,
			CheckedChangedEventArgs e)
		{
			// Binding can set checkbox values while rows are first being created.
			// Only treat user-time changes as writes after loading is complete.
			if (suppressPickedUpChange || isLoading
				|| sender is not CheckBox checkBox
				|| checkBox.BindingContext is not ShoppingListProductListItem item
				|| database is null)
			{
				return;
			}

			bool previousValue = item.PickedUp;
			item.PickedUp = e.Value;

			try
			{
				await database.SetShoppingListProductPickedUpAsync(
					item.ShoppingListID,
					item.ProductID,
					e.Value);
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not update picked-up status: {ex}");

				// Put the checkbox back to the value that is still stored in SQLite.
				suppressPickedUpChange = true;
				item.PickedUp = previousValue;
				checkBox.IsChecked = previousValue;
				suppressPickedUpChange = false;

				await DisplayAlertAsync(
					"Shopping List",
					"Could not update this product. Please try again.",
					"OK");
			}
		}

		private async void OnAddProductClicked(object? sender, EventArgs e)
		{
			// The screen and relationship are ready. Product selection will be
			// implemented as the next focused shopping-list feature.
			await DisplayAlertAsync(
				"Add Product",
				"Product selection for this shopping list is the next piece to build.",
				"OK");
		}
	}
}
