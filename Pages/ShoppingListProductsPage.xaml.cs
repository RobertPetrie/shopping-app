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
		private TaxRate? taxRate;
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
				taxRate = await database.GetTaxRateAsync();
				ApplyFilter();
				await UpdateRunningTotalsAsync();
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

		private async Task UpdateRunningTotalsAsync()
		{
			int rateBasisPoints = taxRate?.RateBasisPoints ?? 0;

			var totals = ShoppingListTotalsCalculator.Calculate(
				items,
				rateBasisPoints);

			SubtotalLabel.Text =
				$"Subtotal: {totals.Subtotal.ToString("C", System.Globalization.CultureInfo.CurrentCulture)}";

			TotalLabel.Text =
				$"Total: {totals.Total.ToString("C", System.Globalization.CultureInfo.CurrentCulture)}";

			if (database is null || shoppingListID <= 0)
			{
				return;
			}

			try
			{
				// Keep the parent Shopping_List row in sync with the same Total the
				// shopper sees in the top-right corner.
				await database.SetShoppingListTotalCostAsync(
					shoppingListID,
					totals.Total);
			}
			catch (Exception ex)
			{
				// The checkbox/price save may already have succeeded, so do not roll
				// it back just because the parent total could not be persisted.
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not save shopping-list total: {ex}");

				await DisplayAlertAsync(
					"Shopping List",
					"The item was updated, but the shopping-list total could not be saved.",
					"OK");
			}
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

			bool previousValue = !e.Value;

			try
			{
				if (e.Value)
				{
					// Picking an item up requires the price paid. Show a centered
					// modal card and do not commit the checkmark until Save is tapped.
					decimal? existingPrice = item.PriceInCents > 0
						? item.Price
						: null;

					var popup = new PriceEntryPopupPage(
						item.ProductName,
						existingPrice);

					await Navigation.PushModalAsync(popup, animated: false);
					decimal? price = await popup.WaitForResultAsync();

					if (!price.HasValue)
					{
						RestoreCheckbox(checkBox, item, previousValue);
						return;
					}

					await database.PickUpShoppingListProductAsync(
						item.ShoppingListID,
						item.ProductID,
						price.Value);

					item.PriceInCents = checked((long)(price.Value * 100m));
					item.PickedUp = true;
					await UpdateRunningTotalsAsync();
				}
				else
				{
					// Unchecking does not ask for a price. Keep the stored price so a
					// mistaken tap does not erase what the shopper previously entered.
					await database.SetShoppingListProductPickedUpAsync(
						item.ShoppingListID,
						item.ProductID,
						pickedUp: false);

					item.PickedUp = false;
					await UpdateRunningTotalsAsync();
				}
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not update picked-up status or price: {ex}");

				RestoreCheckbox(checkBox, item, previousValue);

				await DisplayAlertAsync(
					"Shopping List",
					"Could not update this product. Please try again.",
					"OK");
			}
		}

		private void RestoreCheckbox(
			CheckBox checkBox,
			ShoppingListProductListItem item,
			bool value)
		{
			// Updating IsChecked raises CheckedChanged again, so suppress that
			// event while returning the UI to the value still stored in SQLite.
			suppressPickedUpChange = true;
			item.PickedUp = value;
			checkBox.IsChecked = value;
			suppressPickedUpChange = false;
		}

		private async void OnAddProductClicked(object? sender, EventArgs e)
		{
			if (shoppingListID <= 0)
			{
				return;
			}

			try
			{
				// Reuse the normal Products page in a special selection mode rather
				// than maintaining a second copy of product search/sort UI.
				await Shell.Current.GoToAsync(
					shopping_app.MainPage.ShoppingListSelectionRoute,
					new ShellNavigationQueryParameters
					{
						["ShoppingListForProductSelection"] = shoppingListID
					});
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not open product selection: {ex}");

				await DisplayAlertAsync(
					"Add Product",
					"Could not open the product list. Please try again.",
					"OK");
			}
		}
	}
}
