using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListProductTests
	{
		[Fact]
		public void Price_WhenSet_StoresValueAsCents()
		{
			// Arrange
			var shoppingListProduct = new ShoppingListProduct();

			// Act
			shoppingListProduct.Price = 12.34m;

			// Assert
			Assert.Equal(1234L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WhenStoredAsCents_ReturnsDecimalValue()
		{
			// Arrange
			var shoppingListProduct = new ShoppingListProduct
			{
				PriceInCents = 1234
			};

			// Act
			decimal result = shoppingListProduct.Price;

			// Assert
			Assert.Equal(12.34m, result);
		}


		[Fact]
		public void Price_WithWholeDollarAmount_StoresCorrectNumberOfCents()
		{
			// Arrange
			var shoppingListProduct = new ShoppingListProduct();

			// Act
			shoppingListProduct.Price = 25.00m;

			// Assert
			Assert.Equal(2500L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WithZero_StoresZeroCents()
		{
			// Arrange
			var shoppingListProduct = new ShoppingListProduct();

			// Act
			shoppingListProduct.Price = 0m;

			// Assert
			Assert.Equal(0L, shoppingListProduct.PriceInCents);
		}


		[Fact]
		public void Price_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException()
		{
			// Arrange
			var shoppingListProduct = new ShoppingListProduct();

			// Act
			Action act = () => shoppingListProduct.Price = 12.345m;

			// Assert
			Assert.Throws<ArgumentException>(act);
		}
	}
}