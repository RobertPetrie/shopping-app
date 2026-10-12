using System.Globalization;
using shopping_app.Logic;

namespace shopping_app.Pages
{
	/// <summary>
	/// Centered modal used when a shopper checks an item as picked up.
	/// It returns either the entered price or null when the user cancels.
	/// </summary>
	public partial class PriceEntryPopupPage : ContentPage
	{
		private readonly TaskCompletionSource<decimal?> resultSource =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private bool isClosing;

		public PriceEntryPopupPage(
			string productName,
			decimal? existingPrice = null)
		{
			InitializeComponent();

			ProductNameLabel.Text = productName;

			// Rechecking an item can show its previous price instead of making the
			// shopper type the same amount again.
			if (existingPrice.HasValue)
			{
				PriceEntry.Text = existingPrice.Value.ToString(
					"0.00",
					CultureInfo.CurrentCulture);
			}
		}

		public Task<decimal?> WaitForResultAsync()
		{
			return resultSource.Task;
		}

		protected override void OnAppearing()
		{
			base.OnAppearing();

			// Put the cursor in the price field immediately, which is especially
			// useful on a phone while walking through a store.
			Dispatcher.Dispatch(() => PriceEntry.Focus());
		}

		private void OnPriceTextChanged(object? sender, TextChangedEventArgs e)
		{
			// Clear an old validation message as soon as the shopper tries again.
			ValidationLabel.IsVisible = false;
			ValidationLabel.Text = string.Empty;
		}

		private async void OnPriceCompleted(object? sender, EventArgs e)
		{
			await SaveAsync();
		}

		private async void OnSaveClicked(object? sender, EventArgs e)
		{
			await SaveAsync();
		}

		private async Task SaveAsync()
		{
			if (!ShoppingListPriceInput.TryParse(
				PriceEntry.Text,
				CultureInfo.CurrentCulture,
				out decimal price,
				out string validationMessage))
			{
				ValidationLabel.Text = validationMessage;
				ValidationLabel.IsVisible = true;
				PriceEntry.Focus();
				return;
			}

			await CloseAsync(price);
		}

		private async void OnCancelClicked(object? sender, EventArgs e)
		{
			await CloseAsync(null);
		}

		private async Task CloseAsync(decimal? result)
		{
			if (isClosing)
			{
				return;
			}

			isClosing = true;

			// Remove the modal before resuming the shopping-list checkbox handler.
			await Navigation.PopModalAsync(animated: false);
			resultSource.TrySetResult(result);
		}

		protected override bool OnBackButtonPressed()
		{
			// Treat the phone/Windows back action exactly like tapping Cancel.
			if (!isClosing)
			{
				_ = CloseAsync(null);
			}

			return true;
		}

		protected override void OnDisappearing()
		{
			base.OnDisappearing();

			// If the operating system dismisses the modal unexpectedly, make sure
			// the caller is never left waiting forever for a result.
			if (!isClosing)
			{
				resultSource.TrySetResult(null);
			}
		}
	}
}
