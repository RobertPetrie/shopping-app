using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace shopping_app.Pages
{
	/// <summary>
	/// Home for app-wide settings such as the sales-tax rate.
	/// </summary>
	public partial class SettingsPage : ContentPage
	{
		private AppDatabase? database;
		private TaxRate? currentTaxRate;
		private bool isLoading;

		public SettingsPage()
		{
			InitializeComponent();

			// Shell creates this tab before the platform handler is guaranteed to
			// exist, so wait for Loaded before asking MAUI for app services.
			Loaded += OnPageLoaded;
		}

		protected override void OnNavigatedTo(NavigatedToEventArgs args)
		{
			base.OnNavigatedTo(args);
			QueueLoad();
		}

		private void OnPageLoaded(object? sender, EventArgs e)
		{
			QueueLoad();
		}

		private void QueueLoad()
		{
			Dispatcher.Dispatch(async () => await LoadTaxRateAsync());
		}

		private async Task LoadTaxRateAsync()
		{
			if (!IsLoaded || isLoading)
			{
				return;
			}

			isLoading = true;
			SaveTaxRateButton.IsEnabled = false;
			TaxRateStatusLabel.Text = "Loading tax rate...";

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
				currentTaxRate = await database.GetTaxRateAsync();

				TaxRateNameLabel.Text = currentTaxRate?.Name ?? "Sales Tax";
				TaxRateEntry.Text = (currentTaxRate?.Percentage ?? 0m).ToString(
					"0.##",
					CultureInfo.CurrentCulture);
				TaxRateStatusLabel.Text = string.Empty;
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not load tax rate setting: {ex}");
				TaxRateStatusLabel.Text = "Could not load the tax rate.";
			}
			finally
			{
				isLoading = false;
				SaveTaxRateButton.IsEnabled = true;
			}
		}

		private void OnTaxRateTextChanged(object? sender, TextChangedEventArgs e)
		{
			TaxRateValidationLabel.IsVisible = false;
			TaxRateValidationLabel.Text = string.Empty;
			TaxRateStatusLabel.Text = string.Empty;
		}

		private async void OnTaxRateCompleted(object? sender, EventArgs e)
		{
			await SaveTaxRateAsync();
		}

		private async void OnSaveTaxRateClicked(object? sender, EventArgs e)
		{
			await SaveTaxRateAsync();
		}

		private async Task SaveTaxRateAsync()
		{
			if (database is null || isLoading)
			{
				return;
			}

			if (!TaxRateInput.TryParse(
				TaxRateEntry.Text,
				CultureInfo.CurrentCulture,
				out decimal percentage,
				out string validationMessage))
			{
				TaxRateValidationLabel.Text = validationMessage;
				TaxRateValidationLabel.IsVisible = true;
				TaxRateEntry.Focus();
				return;
			}

			isLoading = true;
			SaveTaxRateButton.IsEnabled = false;

			try
			{
				string name = currentTaxRate?.Name ?? "Sales Tax";
				currentTaxRate = await database.SetTaxRateAsync(name, percentage);

				TaxRateNameLabel.Text = currentTaxRate.Name;
				TaxRateEntry.Text = currentTaxRate.Percentage.ToString(
					"0.##",
					CultureInfo.CurrentCulture);
				TaxRateStatusLabel.Text = "Tax rate saved.";
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not save tax rate setting: {ex}");
				TaxRateStatusLabel.Text = "Could not save the tax rate.";
			}
			finally
			{
				isLoading = false;
				SaveTaxRateButton.IsEnabled = true;
			}
		}
	}
}
