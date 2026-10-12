using System.Globalization;
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
		private bool isPricePopupOpen;
		private TaskCompletionSource<decimal?>? pricePopupResultSource;

		// Keep this checklist sorter simple: A-Z, Z-A, or pickup status.
		private ShoppingListProductSortMode sortMode =
			ShoppingListProductSortMode.NameAscending;

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

			if (isPricePopupOpen)
			{
				CompletePricePopup(null);
			}
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
			SortShoppingListProductsButton.IsEnabled = false;
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
				SortShoppingListProductsButton.IsEnabled = items.Count > 0;
			}
		}

		private int ApplyFilter()
		{
			string searchText = ProductSearchBar.Text ?? string.Empty;

			var displayedItems = ShoppingListProductFilter.Filter(
				items,
				searchText,
				sortMode);

			ProductsView.ItemsSource = displayedItems;

			SortShoppingListProductsButton.Text = sortMode switch
			{
				ShoppingListProductSortMode.NameDescending => "Z → A",
				ShoppingListProductSortMode.PickedUpStatus => "✓",
				_ => "A → Z"
			};

			string sortDescription = sortMode switch
			{
				ShoppingListProductSortMode.NameDescending =>
					"Sorted Z to A. Tap to sort by pickup status.",

				ShoppingListProductSortMode.PickedUpStatus =>
					"Sorted by pickup status with unpicked products first. Tap to sort A to Z.",

				_ =>
					"Sorted A to Z. Tap to sort Z to A."
			};

			SemanticProperties.SetDescription(
				SortShoppingListProductsButton,
				sortDescription);

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

		private void OnSortProductsClicked(object? sender, EventArgs e)
		{
			// Match the other list screens by cycling through each available mode.
			sortMode = sortMode switch
			{
				ShoppingListProductSortMode.NameAscending =>
					ShoppingListProductSortMode.NameDescending,

				ShoppingListProductSortMode.NameDescending =>
					ShoppingListProductSortMode.PickedUpStatus,

				_ => ShoppingListProductSortMode.NameAscending
			};

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
				|| database is null
				|| !ShoppingListPickedUpChange.ShouldHandle(
					item.PickedUp,
					e.Value))
			{
				return;
			}

			// The binding is one-way, so the model still holds the saved value
			// until this user-triggered change has been successfully persisted.
			bool previousValue = item.PickedUp;

			try
			{
				if (e.Value)
				{
					// Picking an item up requires the price paid. Use an overlay on
					// this page instead of Shell modal navigation, which can fail on Windows.
					decimal? existingPrice = item.PriceInCents > 0
						? item.Price
						: null;

					decimal? price = await ShowPricePopupAsync(
						item.ProductName,
						existingPrice);

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

					if (sortMode == ShoppingListProductSortMode.PickedUpStatus)
					{
						ApplyFilter();
					}

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

					if (sortMode == ShoppingListProductSortMode.PickedUpStatus)
					{
						ApplyFilter();
					}

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

		private Task<decimal?> ShowPricePopupAsync(
			string productName,
			decimal? existingPrice)
		{
			if (isPricePopupOpen)
			{
				throw new InvalidOperationException(
					"The price popup is already open.");
			}

			isPricePopupOpen = true;
			pricePopupResultSource = new TaskCompletionSource<decimal?>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			PricePopupProductNameLabel.Text = productName;
			PricePopupEntry.Text = existingPrice?.ToString(
				"0.00",
				CultureInfo.CurrentCulture) ?? string.Empty;

			PricePopupValidationLabel.Text = string.Empty;
			PricePopupValidationLabel.IsVisible = false;
			PricePopupOverlay.IsVisible = true;

			// Focus after the overlay enters the visual tree so Android can show
			// its numeric keypad and Windows can place the caret in the field.
			Dispatcher.Dispatch(() => PricePopupEntry.Focus());

			return pricePopupResultSource.Task;
		}

		private void OnPricePopupTextChanged(
			object? sender,
			TextChangedEventArgs e)
		{
			// Clear stale validation as soon as the shopper changes the value.
			PricePopupValidationLabel.Text = string.Empty;
			PricePopupValidationLabel.IsVisible = false;
		}

		private async void OnPricePopupCompleted(object? sender, EventArgs e)
		{
			await SavePricePopupAsync();
		}

		private async void OnPricePopupSaveClicked(object? sender, EventArgs e)
		{
			await SavePricePopupAsync();
		}

		private Task SavePricePopupAsync()
		{
			if (!ShoppingListPriceInput.TryParse(
				PricePopupEntry.Text,
				CultureInfo.CurrentCulture,
				out decimal price,
				out string validationMessage))
			{
				PricePopupValidationLabel.Text = validationMessage;
				PricePopupValidationLabel.IsVisible = true;
				PricePopupEntry.Focus();
				return Task.CompletedTask;
			}

			CompletePricePopup(price);
			return Task.CompletedTask;
		}

		private void OnPricePopupCancelClicked(object? sender, EventArgs e)
		{
			CompletePricePopup(null);
		}

		private void CompletePricePopup(decimal? result)
		{
			if (!isPricePopupOpen)
			{
				return;
			}

			var resultSource = pricePopupResultSource;

			isPricePopupOpen = false;
			pricePopupResultSource = null;
			PricePopupOverlay.IsVisible = false;
			PricePopupEntry.Unfocus();
			PricePopupValidationLabel.Text = string.Empty;
			PricePopupValidationLabel.IsVisible = false;

			resultSource?.TrySetResult(result);
		}

		protected override bool OnBackButtonPressed()
		{
			if (isPricePopupOpen)
			{
				// Treat Android/Windows back as Cancel while the price card is open.
				CompletePricePopup(null);
				return true;
			}

			return base.OnBackButtonPressed();
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
