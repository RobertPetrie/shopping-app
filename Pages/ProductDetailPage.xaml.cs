using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Models;
using SQLite;

namespace shopping_app.Pages
{
	/// <summary>
	/// Adds new products or displays an existing product for editing.
	/// Saving changes to existing products will be added next.
	/// </summary>
	public partial class ProductDetailPage : ContentPage, IQueryAttributable
	{
		private bool isBusy;

		// Null means Add mode; otherwise identifies the product being edited.
		private int? editingProductId;

		// Remember a successful insertion if navigation back fails.
		// Retrying navigation must not insert the same product again.
		private int? savedProductId;

		public ProductDetailPage()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Populate the form when opened from a product row.
		/// Copy values into controls so Cancel cannot modify the list item.
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

				// Updating existing records is a separate commit.
				SaveProductButton.IsEnabled = false;

				ToolTipProperties.SetText(
					SaveProductButton,
					"Saving edits is not available yet");

				SemanticProperties.SetDescription(
					CancelButton,
					"Cancel editing product");
			}
		}

		/// <summary>
		/// Validate and save a new product, then return to the list.
		/// Edit mode cannot run this insertion logic.
		/// </summary>
		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			// Block repeat operations and prevent insertion in Edit mode.
			if (isBusy || editingProductId.HasValue)
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
				// Skip insertion if saving already succeeded but
				// navigation back failed on the previous attempt.
				if (savedProductId is null)
				{
					var services = Handler?.MauiContext?.Services
						?? throw new InvalidOperationException(
							"The page's app services are unavailable.");

					var database =
						services.GetRequiredService<AppDatabase>();

					string? description =
						ProductDescriptionEditor.Text?.Trim();

					if (string.IsNullOrWhiteSpace(description))
					{
						description = null;
					}

					// The database method assigns ID and CreatedOn.
					var product = await database.AddProductAsync(
						name,
						description);

					savedProductId = product.ID;
				}

				await ReturnToProductsAsync();
			}
			catch (SQLiteException ex)
				when (ex.Result == SQLite3.Result.Constraint
					  && savedProductId is null)
			{
				// The Product table requires a unique name.
				SetBusy(false);
				ShowNameError("A product with this name already exists.");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Could not complete product save: {ex}");

				await DisplayAlertAsync(
					"Add Product",
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
		/// Return without saving form changes.
		/// If an insertion already succeeded, still return its saved ID.
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
		/// Pop this page and return a saved ID only after an actual save.
		/// Opening an existing product does not count as saving it.
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

		// Clear previous feedback while the user corrects the name.
		private void OnNameChanged(object? sender, TextChangedEventArgs e)
		{
			ClearNameError();
		}

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
		/// Prevent repeated operations while saving or navigating.
		/// Keep Save disabled in Edit mode until update logic is added.
		/// </summary>
		private void SetBusy(bool busy)
		{
			isBusy = busy;

			SaveProductButton.IsEnabled =
				!busy && editingProductId is null;

			CancelButton.IsEnabled = !busy;

			// After a successful insertion, lock the fields if
			// navigation back needs to be retried.
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