using System.Globalization;

namespace shopping_app.Logic
{
	/// <summary>
	/// Validates the editable sales-tax percentage shown on the Settings page.
	/// </summary>
	public static class TaxRateInput
	{
		public static bool TryParse(
			string? text,
			CultureInfo culture,
			out decimal percentage,
			out string validationMessage)
		{
			ArgumentNullException.ThrowIfNull(culture);

			percentage = 0m;
			validationMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(text))
			{
				validationMessage = "Enter a tax rate.";
				return false;
			}

			if (!decimal.TryParse(
				text.Trim(),
				NumberStyles.Number,
				culture,
				out percentage))
			{
				validationMessage = "Enter a valid tax rate.";
				return false;
			}

			if (percentage < 0m || percentage > 100m)
			{
				validationMessage = "Tax rate must be between 0 and 100 percent.";
				return false;
			}

			if (decimal.Round(percentage, 2) != percentage)
			{
				validationMessage =
					"Tax rate can have no more than two decimal places.";
				return false;
			}

			return true;
		}
	}
}
