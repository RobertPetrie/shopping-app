using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Finds the picker positions for a product's saved lookup IDs.
	/// Keeping this logic outside the page makes it easy to unit test.
	/// </summary>
	public static class ProductLookupSelection
	{
		public static int FindCategoryIndex(
			IReadOnlyList<ProductCategory> categories,
			int? categoryId)
		{
			ArgumentNullException.ThrowIfNull(categories);

			if (categoryId is null)
			{
				return -1;
			}

			for (int index = 0; index < categories.Count; index++)
			{
				if (categories[index].ID == categoryId.Value)
				{
					return index;
				}
			}

			return -1;
		}

		public static int FindStorageLocationIndex(
			IReadOnlyList<StorageLocation> storageLocations,
			int? storageLocationId)
		{
			ArgumentNullException.ThrowIfNull(storageLocations);

			if (storageLocationId is null)
			{
				return -1;
			}

			for (int index = 0; index < storageLocations.Count; index++)
			{
				if (storageLocations[index].ID == storageLocationId.Value)
				{
					return index;
				}
			}

			return -1;
		}

		public static ProductCategory? GetCategoryAtIndex(
			IReadOnlyList<ProductCategory> categories,
			int selectedIndex)
		{
			ArgumentNullException.ThrowIfNull(categories);

			// -1 means the user has not chosen anything yet.
			return selectedIndex >= 0 && selectedIndex < categories.Count
				? categories[selectedIndex]
				: null;
		}

		public static StorageLocation? GetStorageLocationAtIndex(
			IReadOnlyList<StorageLocation> storageLocations,
			int selectedIndex)
		{
			ArgumentNullException.ThrowIfNull(storageLocations);

			// Storage Location is optional, so no selection returns null.
			return selectedIndex >= 0 && selectedIndex < storageLocations.Count
				? storageLocations[selectedIndex]
				: null;
		}
	}
}
