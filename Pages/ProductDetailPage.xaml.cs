using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;
using SQLite;

namespace shopping_app.Pages
{
	/// <summary>
	/// Shared form for adding and updating products.
	/// Changes reach the database only when Save succeeds.
	/// </summary>
	public partial class ProductDetailPage : ContentPage, IQueryAttributable
	{
		private bool isBusy;

		// Keep saved lookup IDs until the picker data has loaded.
		private int? editingProductCategoryId;
		private int? editingStorageLocationId;

		// Keep the exact lists assigned to the pickers. Selecting by index is
		// more reliable on Windows than comparing Picker.SelectedItem objects.
		private List<ProductCategory> productCategories = new();
		private List<StorageLocation> storageLocations = new();

		private bool lookupsLoaded;

		// Null means Add mode; otherwise identifies the existing record.
		private int? editingProductId;

		// Remember a successful save if navigation back fails.
		// Retrying must not insert again or repeat the update.
		private int? savedProductId;

		public ProductDetailPage()
		{
			InitializeComponent();
			UpdateTaxableStatus(null);
		}

		/// <summary>
		/// Copy the selected product's values into the form.
		/// Editing controls does not modify the original list object.
		/// </summary>
		public void ApplyQueryAttributes(IDictionary<string, object> query)
		{
			if (query.TryGetValue("ProductToEdit", out var value)
				&& value is Product product)
			{
				editingProductId = product.ID;
				Title = "Edit Product";

				ProductNameEntry.Text = product.Name;
				ProductDescriptionEditor.Text = product.Description;
				editingProductCategoryId = product.ProductCategoryID;
				editingStorageLocationId = product.StorageLocationID;
				SelectEditingLookups();

				ClearNameError();

				// Save now supports both Add and Edit modes.
				SaveProductButton.IsEnabled = !isBusy;

				ToolTipProperties.SetText(
					SaveProductButton,
					"Save changes");

				SemanticProperties.SetDescription(
					SaveProductButton,
					"Save product changes");

				SemanticProperties.SetDescription(
					CancelButton,
					"Cancel editing product");
			}
		}

		protected override async void OnAppearing()
		{
			base.OnAppearing();

			if (lookupsLoaded)
			{
				SelectEditingLookups();
				return;
			}

			try
			{
				// The development build may be replacing all data during startup.
				// Wait for that work before reading the product or either lookup table.
				if (Application.Current is App app)
				{
					await app.TestDataReady;
				}

				var services = Handler?.MauiContext?.Services
					?? throw new InvalidOperationException(
						"The page's app services are unavailable.");

				var database =
					services.GetRequiredService<AppDatabase>();

				if (editingProductId is int productId)
				{
					// Reload the complete row from SQLite. This guarantees the picker
					// selections use the current category and storage-location IDs,
					// even if the navigation object was stale.
					var currentProduct = await database.GetProductAsync(productId)
						?? throw new KeyNotFoundException(
							"This product no longer exists.");

					ProductNameEntry.Text = currentProduct.Name;
					ProductDescriptionEditor.Text = currentProduct.Description;
					editingProductCategoryId = currentProduct.ProductCategoryID;
					editingStorageLocationId = currentProduct.StorageLocationID;
				}

				// Load both lookup lists every time the form is first shown.
				// New products get choices; existing products also get their
				// saved category and storage location selected below.
				productCategories = await database.GetProductCategoriesAsync();
				storageLocations = await database.GetStorageLocationsAsync();

				// Fill the native Picker item lists with plain text. The form keeps
				// the matching database objects in memory so the selected index can
				// still be translated back to the correct foreign-key ID on Save.
				PopulateLookupPickers();

				lookupsLoaded = true;
				SelectEditingLookups();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Could not load product lookups: {ex}");

				await DisplayAlertAsync(
					"Product Lookups",
					"Product categories or storage locations could not be loaded. Please try again.",
					"OK");
			}
		}

		private void PopulateLookupPickers()
		{
			// Clear first so returning to the page can never duplicate choices.
			ProductCategoryPicker.Items.Clear();
			StorageLocationPicker.Items.Clear();

			foreach (var category in productCategories)
			{
				ProductCategoryPicker.Items.Add(category.Name);
			}

			foreach (var location in storageLocations)
			{
				StorageLocationPicker.Items.Add(location.Name);
			}

			System.Diagnostics.Debug.WriteLine(
				$"Product form lookups loaded: {productCategories.Count} categories, "
					+ $"{storageLocations.Count} storage locations.");
		}

		private void SelectEditingLookups()
		{
			// Select by index instead of assigning SelectedItem directly.
			// The Windows Picker can render more reliably when its native control
			// receives an explicit SelectedIndex after ItemsSource is populated.
			ProductCategoryPicker.SelectedIndex =
				ProductLookupSelection.FindCategoryIndex(
					productCategories,
					editingProductCategoryId);

			StorageLocationPicker.SelectedIndex =
				ProductLookupSelection.FindStorageLocationIndex(
					storageLocations,
					editingStorageLocationId);

			// Keep the read-only tax field in sync even if the picker does not
			// raise SelectedIndexChanged while restoring an existing value.
			UpdateTaxableStatus(
				ProductCategoryPicker.SelectedIndex >= 0
					? productCategories[ProductCategoryPicker.SelectedIndex]
					: null);
		}

		/// <summary>
		/// Validate the required name and category, then check for duplicates.
		/// Insert or update, then return the saved ID to the products list.
		/// </summary>
		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			// Prevent double-clicks from starting multiple save operations.
			if (isBusy)
			{
				return;
			}

			string name = ProductNameEntry.Text?.Trim() ?? string.Empty;

			if (savedProductId is null && string.IsNullOrWhiteSpace(name))
			{
				ShowNameError("Enter a product name.");
				return;
			}

			if (savedProductId is null
				&& ProductCategoryPicker.SelectedIndex < 0)
			{
				ShowCategoryError("Choose a product category.");
				return;
			}

			ClearNameError();
			ClearCategoryError();
			SetBusy(true);

			try
			{
				// If an earlier save succeeded, only retry navigation.
				if (savedProductId is null)
				{
					var services = Handler?.MauiContext?.Services
						?? throw new InvalidOperationException(
							"The page's app services are unavailable.");

					var database =
						services.GetRequiredService<AppDatabase>();

					// Exclude this product when editing so its existing
					// name does not count as a duplicate of itself.
					bool duplicateName = await database.ProductNameExistsAsync(
						name,
						editingProductId);

					if (duplicateName)
					{
						// Enable the field before attempting to focus it.
						SetBusy(false);
						ShowNameError(
							"A product with this name already exists.");
						return;
					}

					string? description =
						ProductDescriptionEditor.Text?.Trim();

					if (string.IsNullOrWhiteSpace(description))
					{
						description = null;
					}

					var selectedCategory =
						ProductLookupSelection.GetCategoryAtIndex(
							productCategories,
							ProductCategoryPicker.SelectedIndex)
						?? throw new InvalidOperationException(
							"The selected product category could not be resolved.");

					// Storage location is optional. When selected, translate the
					// visible picker position back to the matching database row ID.
					int? selectedStorageLocationId =
						ProductLookupSelection.GetStorageLocationAtIndex(
							storageLocations,
							StorageLocationPicker.SelectedIndex)?.ID;

					if (editingProductId is int id)
					{
						// Update the existing record without changing its ID.
						await database.UpdateProductAsync(
							id,
							name,
							description,
							selectedCategory.ID,
							selectedStorageLocationId);

						savedProductId = id;
					}
					else
					{
						// Add mode retains the existing insertion behavior.
						var product = await database.AddProductAsync(
							name,
							description,
							selectedCategory.ID,
							selectedStorageLocationId);

						savedProductId = product.ID;
					}
				}

				// The existing main page refreshes, preserves its sort,
				// scrolls to this ID, and highlights the saved product.
				await ReturnToProductsAsync();
			}
			catch (SQLiteException ex)
				when (ex.Result == SQLite3.Result.Constraint
					  && savedProductId is null)
			{
				// The unique constraint catches a duplicate even if it was
				// created after our initial duplicate-name check.
				System.Diagnostics.Debug.WriteLine(ex);

				SetBusy(false);
				ShowNameError(
					"A product with this name already exists.");
			}
			catch (KeyNotFoundException ex)
			{
				// The product may have been deleted since the form opened.
				System.Diagnostics.Debug.WriteLine(ex);

				await DisplayAlertAsync(
					"Product unavailable",
					"This product no longer exists. "
						+ "Cancel and return to the products list.",
					"OK");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Could not complete product save: {ex}");

				await DisplayAlertAsync(
					"Save Product",
					savedProductId.HasValue
						? "The product was saved, but the list could not "
						  + "be opened. Tap Save again to return."
						: "The product could not be saved. Please try again.",
					"OK");
			}
			finally
			{
				SetBusy(false);
			}
		}

		/// <summary>
		/// Discard unsaved form values and return to the products list.
		/// An already successful save cannot be undone by Cancel.
		/// </summary>
		private async void OnCancelClicked(object? sender, EventArgs e)
		{
			if (isBusy)
			{
				return;
			}

			SetBusy(true);

			try
			{
				await ReturnToProductsAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex);

				await DisplayAlertAsync(
					"Products",
					"Could not return to the products list. Please try again.",
					"OK");
			}
			finally
			{
				SetBusy(false);
			}
		}

		/// <summary>
		/// Return the saved ID only after an insert or update succeeds.
		/// Single-use parameters prevent repeated highlighting later.
		/// </summary>
		private Task ReturnToProductsAsync()
		{
			if (savedProductId is int id)
			{
				return Shell.Current.GoToAsync(
					"..",
					new ShellNavigationQueryParameters
					{
						["SavedProductId"] = id
					});
			}

			return Shell.Current.GoToAsync("..");
		}

		// Category selection determines the product's tax treatment through
		// ProductCategory.IsTaxable; the product itself stores only the category ID.
		private void OnCategoryChanged(object? sender, EventArgs e)
		{
			var selectedCategory =
				ProductLookupSelection.GetCategoryAtIndex(
					productCategories,
					ProductCategoryPicker.SelectedIndex);

			UpdateTaxableStatus(selectedCategory);
			ClearCategoryError();
		}

		private void UpdateTaxableStatus(ProductCategory? category)
		{
			// Keep the field read-only and simply reflect the category value.
			TaxableStatusEntry.Text =
				category?.TaxableDisplay ?? "Select a category";

			SemanticProperties.SetDescription(
				TaxableStatusEntry,
				category is null
					? "Taxable status. Select a category first."
					: $"Taxable: {category.TaxableDisplay}, based on the selected category.");
		}

		private void ShowCategoryError(string message)
		{
			CategoryValidationLabel.Text = message;
			CategoryValidationLabel.IsVisible = true;
			ProductCategoryPicker.Focus();
			SemanticScreenReader.Announce(message);
		}

		private void ClearCategoryError()
		{
			CategoryValidationLabel.IsVisible = false;
			CategoryValidationLabel.Text = string.Empty;
		}

		// Move to the description with the keyboard's Next action.
		private void OnNameCompleted(object? sender, EventArgs e)
		{
			ProductDescriptionEditor.Focus();
		}

		// Remove previous feedback while the user corrects the name.
		private void OnNameChanged(object? sender, TextChangedEventArgs e)
		{
			ClearNameError();
		}

		/// <summary>
		/// Explain the error, highlight the field, and move focus to it.
		/// </summary>
		private void ShowNameError(string message)
		{
			ValidationLabel.Text = message;
			ValidationLabel.IsVisible = true;
			NameBorder.Stroke = new SolidColorBrush(Colors.IndianRed);

			ProductNameEntry.Focus();
			SemanticScreenReader.Announce(message);
		}

		private void ClearNameError()
		{
			ValidationLabel.IsVisible = false;
			ValidationLabel.Text = string.Empty;
			NameBorder.Stroke = new SolidColorBrush(Colors.Transparent);
		}

		/// <summary>
		/// Disable actions during saving or navigation.
		/// After saving, lock fields while allowing navigation to be retried.
		/// </summary>
		private void SetBusy(bool busy)
		{
			isBusy = busy;

			SaveProductButton.IsEnabled = !busy;
			CancelButton.IsEnabled = !busy;

			ProductNameEntry.IsEnabled =
				!busy && savedProductId is null;

			ProductDescriptionEditor.IsEnabled =
				!busy && savedProductId is null;

			ProductCategoryPicker.IsEnabled =
				!busy && savedProductId is null;

			StorageLocationPicker.IsEnabled =
				!busy && savedProductId is null;

			SavingIndicator.IsVisible = busy;
			SavingIndicator.IsRunning = busy;

			Shell.SetBackButtonBehavior(
				this,
				new BackButtonBehavior { IsEnabled = !busy });
		}

		// Ignore hardware back presses while an operation is running.
		protected override bool OnBackButtonPressed()
		{
			return isBusy || base.OnBackButtonPressed();
		}
	}
}