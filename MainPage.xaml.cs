using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Models;

namespace shopping_app
{
	/// <summary>
	/// Displays products and receives the saved product ID from the form.
	/// </summary>
	public partial class MainPage : ContentPage, IQueryAttributable
	{
		private const string HighlightAnimation = "SavedProductHighlight";

		private bool isLoading;
		private bool isOpeningForm;
		private bool isPageActive;
		private bool needsRefresh = true;

		// This field belongs to the existing page, so navigation to the
		// form and back does not reset the user's chosen order.
		private bool sortAscending = true;

		private List<Product> products = new();

		private int? savedProductId;
		private int? highlightProductId;
		private bool scrollRequested;

		// CollectionView recycles its row controls.
		// Track loaded rows rather than assuming every product has a view.
		private readonly HashSet<Grid> loadedRows = new();

		public MainPage()
		{
			InitializeComponent();

			// Initial loading waits until the page has platform services.
			Loaded += OnPageLoaded;
		}

		/// <summary>
		/// Receive the ID passed back after a successful save.
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

		// Navigation back may not raise Loaded again, so check here too.
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
		/// The guard in RefreshProductsAsync prevents duplicate queries.
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

				// Set the highlight target before replacing ItemsSource,
				// because replacement can immediately create new rows.
				highlightProductId = savedProductId;
				scrollRequested = false;

				ApplySort();
				StatusLabel.Text = $"{products.Count} products";

				var savedProduct = products.FirstOrDefault(
					product => product.ID == savedProductId);

				savedProductId = null;

				if (savedProduct is not null)
				{
					// Give CollectionView a UI turn to process its new
					// items, then scroll to the item in the current order.
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

						// The row may already exist, or its Loaded event
						// will call this once scrolling creates it.
						Dispatcher.Dispatch(TryHighlightSavedProduct);
					});
				}
			}
			catch (Exception ex)
			{
				// Leave refresh pending so revisiting the page can retry.
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
		/// This method deliberately does not scroll to the top.
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
			// Returning from Save follows a separate scroll-to-product path.
			if (products.Count > 0)
			{
				Dispatcher.Dispatch(() =>
					ProductsView.ScrollTo(
						0,
						position: ScrollToPosition.Start,
						animate: false));
			}
		}

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

		private void UpdateButtons()
		{
			AddProductButton.IsEnabled = !isLoading && !isOpeningForm;

			SortProductsButton.IsEnabled =
				!isLoading && !isOpeningForm && products.Count > 0;
		}

		// Register rows as CollectionView brings them into the visual tree.
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

		// A recycled row may now represent a completely different product.
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
		/// Briefly tint the saved product's row, then fade it to transparent.
		/// Only run after scrolling has been requested and the row exists.
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

			// Consume the request so later scrolling does not repeat it.
			highlightProductId = null;
			ResetRow(row);

			var animation = new Animation(progress =>
			{
				// Hold the tint briefly, then fade it out.
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