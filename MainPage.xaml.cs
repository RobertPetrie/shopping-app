using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace shopping_app
{
	/// <summary>
	/// Displays products, opens their detail form, and highlights saved items.
	/// </summary>
	public partial class MainPage : ContentPage, IQueryAttributable
	{
		private const string HighlightAnimation = "SavedProductHighlight";

		// Register this route separately from the Products tab so the same page
		// can be pushed on top of a shopping list as a product picker.
		public const string ShoppingListSelectionRoute =
			"SelectProductForShoppingList";

		private AppDatabase? database;
		private bool isLoading;
		private bool isOpeningForm;
		private bool isAddingToShoppingList;
		private bool isPageActive;
		private bool needsRefresh = true;

		// Preserve the chosen order while navigating to the form and back.
		// The button cycles A-to-Z, Z-to-A, then Storage Location.
		private ProductSortMode sortMode = ProductSortMode.NameAscending;

		private List<Product> products = new();
		private readonly HashSet<int> shoppingListProductIds = new();

		private int? shoppingListIdForSelection;
		private int? savedProductId;
		private int? highlightProductId;
		private bool scrollRequested;

		// CollectionView recycles row controls, so track loaded rows
		// rather than assuming every product has a view.
		private readonly HashSet<Grid> loadedRows = new();

		public MainPage()
		{
			InitializeComponent();

			// Wait until platform services are available before loading.
			Loaded += OnPageLoaded;
		}

		private bool IsShoppingListSelectionMode =>
			shoppingListIdForSelection.HasValue;

		/// <summary>
		/// Receive either the ID returned after a product save or the Shopping
		/// List ID that turns this normal Products page into a product picker.
		/// </summary>
		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			if (query.TryGetValue("SavedProductId", out var value)
				&& value is int id)
			{
				savedProductId = id;
				needsRefresh = true;
				QueueRefresh();
			}

			if (query.TryGetValue(
					"ShoppingListForProductSelection",
					out var shoppingListValue)
				&& shoppingListValue is int shoppingListId
				&& shoppingListId > 0)
			{
				shoppingListIdForSelection = shoppingListId;

				// Keep the familiar Product list, but make it clear that this copy
				// is being used to add existing Products to one Shopping List.
				Title = "Add Products";
				PageTitleLabel.Text = "Add Products";
				AddProductButton.IsVisible = false;
				Shell.SetNavBarIsVisible(this, true);
				Shell.SetTabBarIsVisible(this, false);

				needsRefresh = true;
				ConfigureLoadedRows();
				QueueRefresh();
			}
		}

		// Returning to this page may not raise Loaded again.
		protected override void OnNavigatedTo(NavigatedToEventArgs args)
		{
			base.OnNavigatedTo(args);

			isPageActive = true;
			QueueRefresh();
		}

		protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
		{
			base.OnNavigatedFrom(args);

			isPageActive = false;

			try
			{
				// A pushed Product picker is removed from the visual tree when
				// Back is pressed. Its CollectionView rows can unload during this
				// callback, so cleanup must not enumerate the live HashSet directly.
				StopHighlights();
				loadedRows.Clear();
			}
			catch (Exception ex)
			{
				// Highlight cleanup is cosmetic. Never let it crash navigation.
				DevelopmentDebugArtifacts.WriteLine(
					$"Error cleaning up product rows while navigating away: {ex}");
			}
		}

		private void OnPageLoaded(object? sender, EventArgs e)
		{
			QueueRefresh();
		}

		/// <summary>
		/// Schedule loading after navigation and layout events settle.
		/// RefreshProductsAsync guards against duplicate queries.
		/// </summary>
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

			isLoading = true;
			needsRefresh = false;
			UpdateButtons();
			StatusLabel.Text = "Loading products...";

			try
			{
				// Wait for development sample data before reading SQLite.
				// This keeps the first load consistent on a fresh install.
				if (Application.Current is App app)
				{
					await app.TestDataReady;
				}

				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				database ??= services.GetRequiredService<AppDatabase>();
				var connection = await database.GetConnectionAsync();

				products = await connection.Table<Product>().ToListAsync();

				// Product stores only StorageLocationID. Resolve the friendly name
				// once here so every card can display it and the list can sort by it.
				var storageLocations = await database.GetStorageLocationsAsync();
				var storageLocationNames = storageLocations.ToDictionary(
					location => location.ID,
					location => location.Name);

				foreach (var product in products)
				{
					product.StorageLocationName =
						product.StorageLocationID is int storageLocationId
						&& storageLocationNames.TryGetValue(
							storageLocationId,
							out string? storageLocationName)
							? storageLocationName
							: "Unassigned";
				}

				shoppingListProductIds.Clear();

				if (shoppingListIdForSelection is int shoppingListId)
				{
					foreach (int productId in
						await database.GetShoppingListProductIdsAsync(shoppingListId))
					{
						shoppingListProductIds.Add(productId);
					}
				}

				StopHighlights();

				// Set the target before replacing ItemsSource because
				// replacement can immediately create new row controls.
				highlightProductId = savedProductId;
				scrollRequested = false;

				ApplyFilterAndSort();

				var savedProduct = products.FirstOrDefault(
					product => product.ID == savedProductId);

				savedProductId = null;

				if (savedProduct is not null
					&& ProductsView.ItemsSource is IEnumerable<Product> displayedProducts
					&& displayedProducts.Any(product => product.ID == savedProduct.ID))
				{
					// Let CollectionView process its new items before
					// scrolling to the saved product in the current order.
					Dispatcher.Dispatch(() =>
					{
						if (!isPageActive)
						{
							return;
						}

						scrollRequested = true;

						ProductsView.ScrollTo(
							savedProduct,
							position: ScrollToPosition.Center,
							animate: false);

						// Highlight now if the row exists. Otherwise its
						// Loaded event will try again after scrolling.
						Dispatcher.Dispatch(TryHighlightSavedProduct);
					});
				}
			}
			catch (Exception ex)
			{
				// Revisiting the page can retry a failed refresh.
				needsRefresh = true;
				StatusLabel.Text = "Could not load products.";

				DevelopmentDebugArtifacts.WriteLine(
					$"Error loading products: {ex}");

				await DisplayAlertAsync(
					"Products",
					"Could not refresh the products list. "
						+ "Any successfully saved product remains in the database.",
					"OK");
			}
			finally
			{
				isLoading = false;
				UpdateButtons();
			}
		}

		/// <summary>
		/// Filter and sort the products for display without changing the
		/// database. The actual rules live in ProductListFilter so they can
		/// be unit tested without starting the MAUI user interface.
		/// </summary>
		private int ApplyFilterAndSort()
		{
			string searchText = ProductSearchBar.Text ?? string.Empty;

			var displayedProducts = ProductListFilter.FilterAndSort(
				products,
				searchText,
				sortMode);

			ProductsView.ItemsSource = displayedProducts;

			// Tell the user how many products are visible while searching.
			StatusLabel.Text = ProductListFilter.GetStatusText(
				products.Count,
				displayedProducts.Count,
				searchText);

			SortProductsButton.Text = sortMode switch
			{
				ProductSortMode.NameAscending => "A → Z",
				ProductSortMode.NameDescending => "Z → A",
				ProductSortMode.StorageLocation => "Loc",
				_ => "A → Z"
			};

			SemanticProperties.SetDescription(
				SortProductsButton,
				sortMode switch
				{
					ProductSortMode.NameAscending =>
						"Sorted A to Z. Tap to sort Z to A.",
					ProductSortMode.NameDescending =>
						"Sorted Z to A. Tap to sort by storage location.",
					ProductSortMode.StorageLocation =>
						"Sorted by storage location. Tap to sort A to Z.",
					_ => "Sort products."
				});

			return displayedProducts.Count;
		}

		private void OnProductSearchTextChanged(
			object? sender,
			TextChangedEventArgs e)
		{
			// Update the list immediately after each change in the search box.
			StopHighlights();
			int displayedCount = ApplyFilterAndSort();

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
			StopHighlights();

			// Cycle through all three user-facing sort choices.
			sortMode = sortMode switch
			{
				ProductSortMode.NameAscending => ProductSortMode.NameDescending,
				ProductSortMode.NameDescending => ProductSortMode.StorageLocation,
				ProductSortMode.StorageLocation => ProductSortMode.NameAscending,
				_ => ProductSortMode.NameAscending
			};

			int displayedCount = ApplyFilterAndSort();

			// An explicit sort change starts at the beginning of the
			// currently filtered list.
			if (displayedCount > 0)
			{
				Dispatcher.Dispatch(() =>
					ProductsView.ScrollTo(
						0,
						position: ScrollToPosition.Start,
						animate: false));
			}
		}

		/// <summary>
		/// Open an empty detail form for adding a product.
		/// </summary>
		private async void OnAddProductClicked(object? sender, EventArgs e)
		{
			if (IsShoppingListSelectionMode || isLoading || isOpeningForm)
			{
				return;
			}

			isOpeningForm = true;
			UpdateButtons();

			try
			{
				await Shell.Current.GoToAsync(
					nameof(shopping_app.Pages.ProductDetailPage));
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(ex);

				await DisplayAlertAsync(
					"Add Product",
					"Could not open the product form.",
					"OK");
			}
			finally
			{
				isOpeningForm = false;
				UpdateButtons();
			}
		}

		/// <summary>
		/// Open the tapped product in Edit mode.
		/// This step displays its details without updating the database.
		/// </summary>
		private async void OnProductTapped(object? sender, TappedEventArgs e)
		{
			// In shopping-list selection mode, the explicit Add to list button
			// is the action. Card taps must not accidentally open the edit form.
			if (IsShoppingListSelectionMode)
			{
				return;
			}

			// Ignore repeated taps and taps while the list is loading.
			if (isLoading || isOpeningForm || e.Parameter is not Product product)
			{
				return;
			}

			isOpeningForm = true;
			UpdateButtons();

			try
			{
				// Single-use navigation data supplies the selected product.
				// The detail page copies its values into the form controls.
				await Shell.Current.GoToAsync(
					nameof(shopping_app.Pages.ProductDetailPage),
					new ShellNavigationQueryParameters
					{
						["ProductToEdit"] = product
					});
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not open product for editing: {ex}");

				await DisplayAlertAsync(
					"Edit Product",
					"Could not open this product. Please try again.",
					"OK");
			}
			finally
			{
				isOpeningForm = false;
				UpdateButtons();
			}
		}

		// Disable actions while loading, opening a form, or writing a
		// ShoppingListProduct row.
		private void UpdateButtons()
		{
			AddProductButton.IsVisible = !IsShoppingListSelectionMode;
			AddProductButton.IsEnabled =
				!isLoading && !isOpeningForm && !isAddingToShoppingList;

			SortProductsButton.IsEnabled =
				!isLoading
				&& !isOpeningForm
				&& !isAddingToShoppingList
				&& products.Count > 0;

			ConfigureLoadedRows();
		}

		private async void OnAddProductToShoppingListClicked(
			object? sender,
			EventArgs e)
		{
			if (!IsShoppingListSelectionMode
				|| shoppingListIdForSelection is not int shoppingListId
				|| sender is not Button button
				|| button.BindingContext is not Product product
				|| shoppingListProductIds.Contains(product.ID)
				|| isLoading
				|| isAddingToShoppingList)
			{
				return;
			}

			isAddingToShoppingList = true;
			UpdateButtons();

			try
			{
				if (database is null)
				{
					var services = Handler?.MauiContext?.Services
						?? throw new InvalidOperationException(
							"The page's app services are unavailable.");

					database = services.GetRequiredService<AppDatabase>();
				}

				// A newly selected Product belongs to the list immediately, but it
				// begins unchecked with a $0 price until the shopper picks it up.
				await database.AddShoppingListProductAsync(
					shoppingListId,
					product.ID,
					price: 0m);

				shoppingListProductIds.Add(product.ID);
				SemanticScreenReader.Announce(
					$"Added {product.Name} to shopping list.");
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not add product to shopping list: {ex}");

				await DisplayAlertAsync(
					"Add to Shopping List",
					"Could not add this product. Please try again.",
					"OK");
			}
			finally
			{
				isAddingToShoppingList = false;
				UpdateButtons();
			}
		}

		private void ConfigureLoadedRows()
		{
			// Row Loaded/Unloaded events can fire while navigation or recycling is
			// in progress. Work from a snapshot so those events may safely update
			// the live set without invalidating this enumeration.
			foreach (var row in loadedRows.ToArray())
			{
				ConfigureProductRow(row);
			}
		}

		private void ConfigureProductRow(Grid row)
		{
			var addButton = row.FindByName<Button>("AddToShoppingListButton");
			var editChevron = row.FindByName<Label>("EditChevron");

			if (addButton is not null)
			{
				addButton.IsVisible = IsShoppingListSelectionMode;

				bool alreadyAdded =
					row.BindingContext is Product product
					&& shoppingListProductIds.Contains(product.ID);

				addButton.Text = alreadyAdded ? "Added" : "Add to list";
				addButton.IsEnabled =
					IsShoppingListSelectionMode
					&& !alreadyAdded
					&& !isLoading
					&& !isAddingToShoppingList;
			}

			if (editChevron is not null)
			{
				editChevron.IsVisible = !IsShoppingListSelectionMode;
			}
		}

		// Register rows when they enter the visual tree.
		private void OnProductRowLoaded(object? sender, EventArgs e)
		{
			if (sender is Grid row)
			{
				loadedRows.Add(row);
				ConfigureProductRow(row);
				Dispatcher.Dispatch(TryHighlightSavedProduct);
			}
		}

		// Clear animations when a row leaves the visual tree.
		private void OnProductRowUnloaded(object? sender, EventArgs e)
		{
			if (sender is Grid row)
			{
				loadedRows.Remove(row);
				ResetRow(row);
			}
		}

		// A recycled row may now represent a different product.
		private void OnProductRowBindingContextChanged(
			object? sender,
			EventArgs e)
		{
			if (sender is Grid row)
			{
				ResetRow(row);

				if (row.IsLoaded)
				{
					loadedRows.Add(row);
					ConfigureProductRow(row);
					Dispatcher.Dispatch(TryHighlightSavedProduct);
				}
			}
		}

		/// <summary>
		/// Briefly tint the saved product's row, then fade to transparent.
		/// Wait until scrolling has been requested and the row exists.
		/// </summary>
		private void TryHighlightSavedProduct()
		{
			if (!isPageActive || !scrollRequested
				|| highlightProductId is not int id)
			{
				return;
			}

			var row = loadedRows.FirstOrDefault(view =>
				view.IsLoaded
				&& view.BindingContext is Product product
				&& product.ID == id);

			if (row is null)
			{
				return;
			}

			// Consume the request so later scrolling cannot repeat it.
			highlightProductId = null;
			ResetRow(row);

			var animation = new Animation(progress =>
			{
				// Hold the tint briefly before fading it out.
				double fade = Math.Clamp(
					(progress - 0.35) / 0.65, 0, 1);

				row.BackgroundColor = Color.FromRgba(
					124, 92, 210, (int)(100 * (1 - fade)));
			});

			animation.Commit(
				row,
				HighlightAnimation,
				rate: 16,
				length: 1400,
				finished: (_, _) =>
					row.BackgroundColor = Colors.Transparent);

			SemanticScreenReader.Announce(
				$"Product saved: {((Product)row.BindingContext).Name}");
		}

		private static void ResetRow(Grid row)
		{
			row.AbortAnimation(HighlightAnimation);
			row.BackgroundColor = Colors.Transparent;
		}

		private void StopHighlights()
		{
			highlightProductId = null;
			scrollRequested = false;

			// A row can raise Unloaded as navigation tears down this page.
			// Enumerating a snapshot avoids "Collection was modified" crashes.
			foreach (var row in loadedRows.ToArray())
			{
				ResetRow(row);
			}
		}
	}
}