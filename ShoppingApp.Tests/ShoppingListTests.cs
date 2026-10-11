using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListTests
	{
		[Fact]
		public void ShoppingDate_WhenSet_StoresDateAsIsoString()
		{
			// Setting the friendly DateOnly property should store a stable ISO
			// date string that SQLite can persist consistently.
			var shoppingList = new ShoppingList();
			var date = new DateOnly(2026, 10, 3);
			shoppingList.ShoppingDate = date;
			Assert.Equal("2026-10-03", shoppingList.ShoppingDateStorage);
		}


		[Fact]
		public void ShoppingDate_WhenSet_CanBeReadBack()
		{
			// A date string loaded from SQLite should be converted back into the
			// DateOnly value used by the rest of the app.
			var shoppingList = new ShoppingList
			{
				ShoppingDateStorage = "2026-10-03"
			};

			DateOnly? result = shoppingList.ShoppingDate;
			Assert.Equal(new DateOnly(2026, 10, 3), result);
		}


		[Fact]
		public void ShoppingDate_WhenNull_StoresNull()
		{
			// Shopping date is optional, so assigning null should leave both the
			// public property and its SQLite storage value empty.
			var shoppingList = new ShoppingList();
			shoppingList.ShoppingDate = null;
			Assert.Null(shoppingList.ShoppingDateStorage);
			Assert.Null(shoppingList.ShoppingDate);
		}


		[Fact]
		public void TotalCost_WhenSet_StoresValueAsCents()
		{
			// Totals are stored as integer cents so currency values remain exact
			// when a shopping list is written to SQLite.
			var shoppingList = new ShoppingList();
			shoppingList.TotalCost = 12.34m;
			Assert.Equal(1234L, shoppingList.TotalCostInCents);
		}


		[Fact]
		public void TotalCost_WhenStoredAsCents_ReturnsDecimalValue()
		{
			// A stored integer-cent total should be exposed as the matching
			// decimal currency amount when the app reads it.
			var shoppingList = new ShoppingList
			{
				TotalCostInCents = 1234
			};

			decimal? result = shoppingList.TotalCost;
			Assert.Equal(12.34m, result);
		}


		[Fact]
		public void TotalCost_WhenNull_StoresNull()
		{
			// A list can be unfinished and have no total yet, so null must stay
			// null in both the app-facing property and SQLite storage value.
			var shoppingList = new ShoppingList();
			shoppingList.TotalCost = null;
			Assert.Null(shoppingList.TotalCostInCents);
			Assert.Null(shoppingList.TotalCost);
		}


		[Fact]
		public void TotalCost_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException()
		{
			// Fractions of a cent cannot be stored exactly, so totals with more
			// than two decimal places should be rejected.
			var shoppingList = new ShoppingList();
			Action act = () => shoppingList.TotalCost = 12.345m;
			Assert.Throws<ArgumentException>(act);
		}


		[Fact]
		public void PickedUp_WhenShoppingListCreated_DefaultsToFalse()
		{
			// A newly created shopping list should start pending until the user
			// explicitly marks the trip as picked up.
			var shoppingList = new ShoppingList();
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