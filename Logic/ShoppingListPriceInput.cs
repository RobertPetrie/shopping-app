using System.Globalization;

namespace shopping_app.Logic
{
	/// <summary>
	/// Validates the price typed into the picked-up item popup.
	/// Keeping this logic outside the page makes it easy to test without MAUI.
	/// </summary>
	public static class ShoppingListPriceInput
	{
		public static bool TryParse(
			string? text,
			CultureInfo culture,
			out decimal price,
			out string validationMessage)
		{
			ArgumentNullException.ThrowIfNull(culture);

			price = 0m;
			validationMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(text))
			{
				validationMessage = "Enter the price paid.";
				return false;
			}

			if (!decimal.TryParse(
				text.Trim(),
				NumberStyles.Number,
				culture,
				out price))
			{
				validationMessage = "Enter a valid price.";
				return false;
			}

			if (price < 0m)
			{
				validationMessage = "Price cannot be negative.";
				return false;
			}

			if (decimal.Round(price, 2) != price)
			{
				validationMessage = "Price can have no more than two decimal places.";
				return false;
			}

			if (price > long.MaxValue / 100m)
			{
				validationMessage = "Price is too large.";
				return false;
			}

			return true;
		}
	}
}
