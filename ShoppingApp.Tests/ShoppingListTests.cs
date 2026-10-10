using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListTests
	{
		[Fact]
		public void ShoppingDate_WhenSet_StoresDateAsIsoString()
		{
			// Arrange
			var shoppingList = new ShoppingList();
			var date = new DateOnly(2026, 10, 3);

			// Act
			shoppingList.ShoppingDate = date;

			// Assert
			Assert.Equal("2026-10-03", shoppingList.ShoppingDateStorage);
		}


		[Fact]
		public void ShoppingDate_WhenSet_CanBeReadBack()
		{
			// Arrange
			var shoppingList = new ShoppingList
			{
				ShoppingDateStorage = "2026-10-03"
			};

			// Act
			DateOnly? result = shoppingList.ShoppingDate;

			// Assert
			Assert.Equal(new DateOnly(2026, 10, 3), result);
		}


		[Fact]
		public void ShoppingDate_WhenNull_StoresNull()
		{
			// Arrange
			var shoppingList = new ShoppingList();

			// Act
			shoppingList.ShoppingDate = null;

			// Assert
			Assert.Null(shoppingList.ShoppingDateStorage);
			Assert.Null(shoppingList.ShoppingDate);
		}


		[Fact]
		public void TotalCost_WhenSet_StoresValueAsCents()
		{
			// Arrange
			var shoppingList = new ShoppingList();

			// Act
			shoppingList.TotalCost = 12.34m;

			// Assert
			Assert.Equal(1234L, shoppingList.TotalCostInCents);
		}


		[Fact]
		public void TotalCost_WhenStoredAsCents_ReturnsDecimalValue()
		{
			// Arrange
			var shoppingList = new ShoppingList
			{
				TotalCostInCents = 1234
			};

			// Act
			decimal? result = shoppingList.TotalCost;

			// Assert
			Assert.Equal(12.34m, result);
		}


		[Fact]
		public void TotalCost_WhenNull_StoresNull()
		{
			// Arrange
			var shoppingList = new ShoppingList();

			// Act
			shoppingList.TotalCost = null;

			// Assert
			Assert.Null(shoppingList.TotalCostInCents);
			Assert.Null(shoppingList.TotalCost);
		}


		[Fact]
		public void TotalCost_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException()
		{
			// Arrange
			var shoppingList = new ShoppingList();

			// Act
			Action act = () => shoppingList.TotalCost = 12.345m;

			// Assert
			Assert.Throws<ArgumentException>(act);
		}


		[Fact]
		public void PickedUp_WhenShoppingListCreated_DefaultsToFalse()
		{
			// Arrange / Act
			var shoppingList = new ShoppingList();

			// Assert
			Assert.False(shoppingList.PickedUp);
		}

		[Fact]
		public void DisplayDate_WhenDateIsMissing_ShowsFriendlyMessage()
		{
			// A list without a shopping date should still show useful text.
			var shoppingList = new ShoppingList();

			Assert.Equal("No shopping date", shoppingList.DisplayDate);
		}


		[Fact]
		public void DisplayTotalCost_WhenTotalIsMissing_ShowsFriendlyMessage()
		{
			// A pending list may not have a final total yet.
			var shoppingList = new ShoppingList();

			Assert.Equal("Total not entered", shoppingList.DisplayTotalCost);
		}


		[Theory]
		[InlineData(false, "Pending")]
		[InlineData(true, "Picked up")]
		public void PickupStatus_ReflectsPickedUpState(
			bool pickedUp,
			string expectedStatus)
		{
			// The card text should match the stored picked-up flag.
			var shoppingList = new ShoppingList
			{
				PickedUp = pickedUp
			};

			Assert.Equal(expectedStatus, shoppingList.PickupStatus);
		}
	}
}