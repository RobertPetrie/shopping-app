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
		public void GetCategoryAtIndex_ReturnsCategoryUserSelected()
		{
			// The Add Product form displays category names as plain picker text.
			// Saving must translate the selected position back to the same row ID.
			var categories = new List<ProductCategory>
			{
				new() { ID = 1, Name = "Basic Grocery" },
				new() { ID = 5, Name = "Household / Non-Food" }
			};

			var selected = ProductLookupSelection.GetCategoryAtIndex(
				categories,
				selectedIndex: 1);

			Assert.NotNull(selected);
			Assert.Equal(5, selected.ID);
		}

		[Fact]
		public void GetStorageLocationAtIndex_ReturnsLocationUserSelected()
		{
			// The Add Product form displays storage names as plain picker text.
			// Saving must translate the selected position back to the same row ID.
			var locations = new List<StorageLocation>
			{
				new() { ID = 2, Name = "Bathroom" },
				new() { ID = 8, Name = "Garage" }
			};

			var selected = ProductLookupSelection.GetStorageLocationAtIndex(
				locations,
				selectedIndex: 1);

			Assert.NotNull(selected);
			Assert.Equal(8, selected.ID);
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
