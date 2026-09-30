using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using SQLite;

namespace shopping_app.Pages
{
	/// <summary>
	/// Creates a product and returns its ID to the products screen.
	/// Editing existing products will be added separately.
	/// </summary>
	public partial class ProductDetailPage : ContentPage
	{
		private bool isBusy;

		// Remember a successful save if returning to the list fails.
		// Retrying navigation must not insert the product again.
		private int? savedProductId;

		public ProductDetailPage()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Validate the form, save once, and return to the products list.
		/// </summary>
		private async void OnSaveClicked(object? sender, EventArgs e)
		{
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
				// Skip insertion if an earlier attempt already saved
				// successfully but navigation back failed.
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

					// The existing database method assigns ID and CreatedOn.
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
		/// Return without inserting anything.
		/// If a save already succeeded, still return its ID to the list.
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
		/// Pop this page and send the saved ID back only when applicable.
		/// Single-use parameters avoid repeating the highlight later.
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

		// Move to the description when the keyboard's Next action is used.
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
		/// Prevent repeated saves and changes during database operations.
		/// After saving, keep the fields locked if navigation needs retrying.
		/// </summary>
		private void SetBusy(bool busy)
		{
			isBusy = busy;

			SaveProductButton.IsEnabled = !busy;
			CancelButton.IsEnabled = !busy;

			ProductNameEntry.IsEnabled = !busy && savedProductId is null;
			ProductDescriptionEditor.IsEnabled =
				!busy && savedProductId is null;

			SavingIndicator.IsVisible = busy;
			SavingIndicator.IsRunning = busy;

			Shell.SetBackButtonBehavior(
				this,
				new BackButtonBehavior { IsEnabled = !busy });
		}

		// Ignore hardware back presses while saving.
		protected override bool OnBackButtonPressed()
		{
			return isBusy || base.OnBackButtonPressed();
		}
	}
}