using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Models;

namespace shopping_app
{
	/// <summary>
	/// Displays products, opens their detail form, and highlights saved items.
	/// </summary>
	public partial class MainPage : ContentPage, IQueryAttributable
	{
		private const string HighlightAnimation = "SavedProductHighlight";

		private bool isLoading;
		private bool isOpeningForm;
		private bool isPageActive;
		private bool needsRefresh = true;

		// Preserve the chosen order while navigating to the form and back.
		private bool sortAscending = true;

		private List<Product> products = new();

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

		/// <summary>
		/// Receive the ID returned after a successful save.
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
			StopHighlights();
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
				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				var database = services.GetRequiredService<AppDatabase>();
				var connection = await database.GetConnectionAsync();

				products = await connection.Table<Product>().ToListAsync();

				StopHighlights();

				// Set the target before replacing ItemsSource because
				// replacement can immediately create new row controls.
				highlightProductId = savedProductId;
				scrollRequested = false;

				ApplySort();
				StatusLabel.Text = $"{products.Count} products";

				var savedProduct = products.FirstOrDefault(
					product => product.ID == savedProductId);

				savedProductId = null;

				if (savedProduct is not null)
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

				System.Diagnostics.Debug.WriteLine(
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
		/// Sort the displayed records without changing the database.
		/// Scrolling is handled separately by the calling action.
		/// </summary>
		private void ApplySort()
		{
			var sortedProducts = sortAscending
				? products.OrderBy(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase)
				: products.OrderByDescending(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase);

			ProductsView.ItemsSource = sortedProducts
				.ThenBy(product => product.ID)
				.ToList();

			SortProductsButton.Text = sortAscending ? "A|Z" : "Z|A";

			SemanticProperties.SetDescription(
				SortProductsButton,
				sortAscending
					? "Sorted A to Z. Tap to sort Z to A."
					: "Sorted Z to A. Tap to sort A to Z.");
		}

		private void OnSortProductsClicked(object? sender, EventArgs e)
		{
			StopHighlights();
			sortAscending = !sortAscending;
			ApplySort();

			// An explicit sort change starts at the beginning.
			// Returning from Save instead scrolls to the saved product.
			if (products.Count > 0)
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
			if (isLoading || isOpeningForm)
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
				System.Diagnostics.Debug.WriteLine(ex);

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
				System.Diagnostics.Debug.WriteLine(
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

		// Disable actions while loading or opening another page.
		private void UpdateButtons()
		{
			AddProductButton.IsEnabled = !isLoading && !isOpeningForm;

			SortProductsButton.IsEnabled =
				!isLoading && !isOpeningForm && products.Count > 0;
		}

		// Register rows when they enter the visual tree.
		private void OnProductRowLoaded(object? sender, EventArgs e)
		{
			if (sender is Grid row)
			{
				loadedRows.Add(row);
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

			foreach (var row in loadedRows)
			{
				ResetRow(row);
			}
		}
	}
}