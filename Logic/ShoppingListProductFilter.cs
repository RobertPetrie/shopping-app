using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Search rules for the products belonging to one shopping list.
	/// </summary>
	public static class ShoppingListProductFilter
	{
		public static List<ShoppingListProductListItem> Filter(
			IEnumerable<ShoppingListProductListItem> items,
			string? searchText)
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

			// Product name is the natural order for this checklist-style screen.
			return visibleItems
				.OrderBy(
					item => item.ProductName,
					StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(item => item.ProductID)
				.ToList();
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
