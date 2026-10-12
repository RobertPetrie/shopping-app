using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Search rules for the products belonging to one shopping list.
	/// </summary>
	public enum ShoppingListProductSortMode
	{
		NameAscending,
		NameDescending
	}

	public static class ShoppingListProductFilter
	{
		public static List<ShoppingListProductListItem> Filter(
			IEnumerable<ShoppingListProductListItem> items,
			string? searchText,
			ShoppingListProductSortMode sortMode =
				ShoppingListProductSortMode.NameAscending)
		{
			ArgumentNullException.ThrowIfNull(items);

			string trimmedSearch = searchText?.Trim() ?? string.Empty;
			IEnumerable<ShoppingListProductListItem> visibleItems = items;

			if (!string.IsNullOrWhiteSpace(trimmedSearch))
			{
				visibleItems = visibleItems.Where(item =>
					item.ProductName.Contains(
						trimmedSearch,
						StringComparison.CurrentCultureIgnoreCase));
			}

			// This screen only needs the two quick alphabetical directions.
			return sortMode switch
			{
				ShoppingListProductSortMode.NameDescending => visibleItems
					.OrderByDescending(
						item => item.ProductName,
						StringComparer.CurrentCultureIgnoreCase)
					.ThenByDescending(item => item.ProductID)
					.ToList(),

				_ => visibleItems
					.OrderBy(
						item => item.ProductName,
						StringComparer.CurrentCultureIgnoreCase)
					.ThenBy(item => item.ProductID)
					.ToList()
			};
		}

		public static string GetStatusText(
			int totalCount,
			int visibleCount,
			string? searchText)
		{
			return string.IsNullOrWhiteSpace(searchText)
				? $"{totalCount} products"
				: $"{visibleCount} of {totalCount} products";
		}
	}
}
