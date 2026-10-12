using System.Globalization;
using shopping_app.Logic;

namespace ShoppingApp.Tests
{
	public class ShoppingListPriceInputTests
	{
		[Fact]
		public void TryParse_WithNormalPrice_ReturnsExactDecimal()
		{
			// A normal two-decimal price should be accepted exactly as typed so
			// the database can later convert it to whole cents without rounding.
			bool valid = ShoppingListPriceInput.TryParse(
				"12.34",
				CultureInfo.InvariantCulture,
				out decimal price,
				out string message);

			Assert.True(valid);
			Assert.Equal(12.34m, price);
			Assert.Equal(string.Empty, message);
		}

		[Fact]
		public void TryParse_WithBlankPrice_RequiresAValue()
		{
			// Checking an item as picked up should not silently save a missing
			// price, because the popup exists specifically to capture that amount.
			bool valid = ShoppingListPriceInput.TryParse(
				" ",
				CultureInfo.InvariantCulture,
				out _,
				out string message);

			Assert.False(valid);
			Assert.Equal("Enter the price paid.", message);
		}

		[Fact]
		public void TryParse_WithNegativePrice_RejectsValue()
		{
			// A purchase price cannot be negative in this shopping-list workflow.
			bool valid = ShoppingListPriceInput.TryParse(
				"-1.25",
				CultureInfo.InvariantCulture,
				out _,
				out string message);

			Assert.False(valid);
			Assert.Equal("Price cannot be negative.", message);
		}

		[Fact]
		public void TryParse_WithFractionOfACent_RejectsValue()
		{
			// SQLite stores whole cents, so the popup must reject values that
			// would otherwise need hidden rounding.
			bool valid = ShoppingListPriceInput.TryParse(
				"1.999",
				CultureInfo.InvariantCulture,
				out _,
				out string message);

			Assert.False(valid);
			Assert.Equal(
				"Price can have no more than two decimal places.",
				message);
		}

		[Fact]
		public void TryParse_UsesTheSelectedCultureDecimalSeparator()
		{
			// Price entry should follow the device culture, including cultures
			// that use a comma rather than a period as the decimal separator.
			var culture = CultureInfo.GetCultureInfo("fr-CA");

			bool valid = ShoppingListPriceInput.TryParse(
				"12,34",
				culture,
				out decimal price,
				out _);

			Assert.True(valid);
			Assert.Equal(12.34m, price);
		}
	}
}
