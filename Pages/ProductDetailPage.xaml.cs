using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
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

		// Null means Add mode; otherwise identifies the existing record.
		private int? editingProductId;

		// Remember a successful save if navigation back fails.
		// Retrying must not insert again or repeat the update.
		private int? savedProductId;

		public ProductDetailPage()
		{
			InitializeComponent();
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

		/// <summary>
		/// Validate the required name and check for duplicates.
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

			ClearNameError();
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

					if (editingProductId is int id)
					{
						// Update the existing record without changing its ID.
						await database.UpdateProductAsync(
							id,
							name,
							description);

						savedProductId = id;
					}
					else
					{
						// Add mode retains the existing insertion behavior.
						var product = await database.AddProductAsync(
							name,
							description);

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