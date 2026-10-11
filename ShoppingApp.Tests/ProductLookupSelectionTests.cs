using shopping_app.Logic;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ProductLookupSelectionTests
	{
		[Fact]
		public void FindCategoryIndex_WithSavedId_ReturnsMatchingPickerPosition()
		{
			// The edit form restores a saved category by ID, so the helper should
			// return the exact position of that category in the picker list.
			var categories = new List<ProductCategory>
			{
				new() { ID = 4, Name = "Basic Grocery" },
				new() { ID = 9, Name = "Household / Non-Food" }
			};

			int result = ProductLookupSelection.FindCategoryIndex(
				categories,
				categoryId: 9);

			Assert.Equal(1, result);
		}

		[Fact]
		public void FindStorageLocationIndex_WithSavedId_ReturnsMatchingPickerPosition()
		{
			// The edit form restores a saved storage location by ID, so the helper
			// should return the exact position of that location in the picker list.
			var locations = new List<StorageLocation>
			{
				new() { ID = 3, Name = "Basement" },
				new() { ID = 8, Name = "Bathroom" }
			};

			int result = ProductLookupSelection.FindStorageLocationIndex(
				locations,
				storageLocationId: 8);

			Assert.Equal(1, result);
		}

		[Fact]
		public void FindLookupIndexes_WithoutSavedIds_LeavePickersUnselected()
		{
			// Add mode has no saved lookup IDs yet, so both picker helpers should
			// return -1, which is MAUI Picker's unselected position.
			var categories = new List<ProductCategory>
			{
				new() { ID = 1, Name = "Basic Grocery" }
			};

			var locations = new List<StorageLocation>
			{
				new() { ID = 1, Name = "Pantry" }
			};

			Assert.Equal(
				-1,
				ProductLookupSelection.FindCategoryIndex(categories, null));

			Assert.Equal(
				-1,
				ProductLookupSelection.FindStorageLocationIndex(locations, null));
		}
	}
}
