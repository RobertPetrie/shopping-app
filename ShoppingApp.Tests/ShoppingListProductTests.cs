using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListProductTests
	{
		[Fact]
		public void PickedUp_NewEntry_DefaultsToFalse()
		{
			// A product added to a shopping list should begin unchecked so the
			// shopper can mark it picked up only after taking it from the shelf.
			var shoppingListProduct = new ShoppingListProduct();

			Assert.False(shoppingListProduct.PickedUp);
		}


		[Fact]
		public void Price_WhenSet_StoresValueAsCents()
		{
			// Assigning a decimal price should convert dollars and cents into
			// the integer-cent value stored in SQLite.
			var shoppingListProduct = new ShoppingListProduct();
			shoppingListProduct.Price = 12.34m;

			// The database value should be exact and avoid floating-point storage.
			Assert.Equal(1234L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WhenStoredAsCents_ReturnsDecimalValue()
		{
			// A value loaded from SQLite as cents should be exposed to the app
			// as the matching decimal currency amount.
			var shoppingListProduct = new ShoppingListProduct
			{
				PriceInCents = 1234
			};

			decimal result = shoppingListProduct.Price;
			Assert.Equal(12.34m, result);
		}


		[Fact]
		public void Price_WithWholeDollarAmount_StoresCorrectNumberOfCents()
		{
			// Whole-dollar prices still need to be stored as their full number
			// of cents rather than as a decimal value.
			var shoppingListProduct = new ShoppingListProduct();
			shoppingListProduct.Price = 25.00m;
			Assert.Equal(2500L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WithZero_StoresZeroCents()
		{
			// A zero price is valid and should remain exactly zero when converted
			// to the integer-cent storage format.
			var shoppingListProduct = new ShoppingListProduct();
			shoppingListProduct.Price = 0m;
			Assert.Equal(0L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException()
		{
			// Prices with fractions of a cent cannot be represented exactly in
			// the database, so the model should reject them.
			var shoppingListProduct = new ShoppingListProduct();
			Action act = () => shoppingListProduct.Price = 12.345m;
			Assert.Throws<ArgumentException>(act);
		}
	}
}