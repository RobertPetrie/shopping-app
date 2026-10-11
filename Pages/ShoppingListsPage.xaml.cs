using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace shopping_app.Pages
{
	/// <summary>
	/// Shows saved shopping lists with live searching and date sorting.
	/// </summary>
	public partial class ShoppingListsPage : ContentPage
	{
		private bool isLoading;
		private bool isPageActive;
		private bool needsRefresh = true;

		// Start with the most recent shopping date at the top.
		private bool newestFirst = true;

		private List<ShoppingList> shoppingLists = new();

		public ShoppingListsPage()
		{
			InitializeComponent();

			// Wait until the page has access to the app services and database.
			Loaded += OnPageLoaded;
		}

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
		}

		private void OnPageLoaded(object? sender, EventArgs e)
		{
			QueueRefresh();
		}

		private void QueueRefresh()
		{
			Dispatcher.Dispatch(async () => await RefreshShoppingListsAsync());
		}

		private async Task RefreshShoppingListsAsync()
		{
			if (!isPageActive || !IsLoaded || isLoading || !needsRefresh)
			{
				return;
			}

			isLoading = true;
			needsRefresh = false;
			UpdateButtons();
			StatusLabel.Text = "Loading shopping lists...";

			try
			{
				// The app seeds development data when the window starts.
				// Wait here so the first screen does not query SQLite too early.
				if (Application.Current is App app)
				{
					await app.TestDataReady;
				}

				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				var database = services.GetRequiredService<AppDatabase>();
				var connection = await database.GetConnectionAsync();

				shoppingLists = await connection
					.Table<ShoppingList>()
					.ToListAsync();

				ApplyFilterAndSort();
			}
			catch (Exception ex)
			{
				// Allow another attempt the next time the page is shown.
				needsRefresh = true;
				StatusLabel.Text = "Could not load shopping lists.";

				DevelopmentDebugArtifacts.WriteLine(
					$"Error loading shopping lists: {ex}");

				await DisplayAlertAsync(
					"Shopping Lists",
					"Could not refresh the shopping lists.",
					"OK");
			}
			finally
			{
				isLoading = false;
				UpdateButtons();
			}
		}

		private int ApplyFilterAndSort()
		{
			string searchText = ShoppingListSearchBar.Text ?? string.Empty;

			var displayedLists = ShoppingListListFilter.FilterAndSort(
				shoppingLists,
				searchText,
				newestFirst);

			ShoppingListsView.ItemsSource = displayedLists;

			StatusLabel.Text = ShoppingListListFilter.GetStatusText(
				shoppingLists.Count,
				displayedLists.Count,
				searchText);

			SortShoppingListsButton.Text = newestFirst
				? "Newest"
				: "Oldest";

			SemanticProperties.SetDescription(
				SortShoppingListsButton,
				newestFirst
					? "Newest first. Tap to show oldest first."
					: "Oldest first. Tap to show newest first.");

			return displayedLists.Count;
		}

		private void OnSearchTextChanged(
			object? sender,
			TextChangedEventArgs e)
		{
			// Refresh the visible cards immediately after each keystroke.
			int displayedCount = ApplyFilterAndSort();

			if (displayedCount > 0)
			{
				Dispatcher.Dispatch(() =>
					ShoppingListsView.ScrollTo(
						0,
						position: ScrollToPosition.Start,
						animate: false));
			}
		}

		private void OnSortClicked(object? sender, EventArgs e)
		{
			newestFirst = !newestFirst;
			int displayedCount = ApplyFilterAndSort();

			if (displayedCount > 0)
			{
				Dispatcher.Dispatch(() =>
					ShoppingListsView.ScrollTo(
						0,
						position: ScrollToPosition.Start,
						animate: false));
			}
		}

		private async void OnAddShoppingListClicked(
			object? sender,
			EventArgs e)
		{
			// The list screen is ready now. The editor will be a separate
			// feature so this button does not create an accidental blank record.
			await DisplayAlertAsync(
				"New Shopping List",
				"The shopping list editor is the next piece to build.",
				"OK");
		}

		private void UpdateButtons()
		{
			AddShoppingListButton.IsEnabled = !isLoading;

			SortShoppingListsButton.IsEnabled =
				!isLoading && shoppingLists.Count > 0;
		}
	}
}
