using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Contains the product-list rules that do not depend on the screen.
	/// Keeping these rules here makes the live search and sorting easy to test.
	/// </summary>
	public static class ProductListFilter
	{
		/// <summary>
		/// Return the products that match the search text in the requested order.
		/// Searching ignores capitalization and extra spaces around the search.
		/// </summary>
		public static List<Product> FilterAndSort(
			IEnumerable<Product> products,
			string? searchText,
			bool sortAscending)
		{
			ArgumentNullException.ThrowIfNull(products);

			string trimmedSearch = searchText?.Trim() ?? string.Empty;

			// Start with every product and narrow the list only when the
			// user has typed something meaningful into the search box.
			IEnumerable<Product> visibleProducts = products;

			if (!string.IsNullOrWhiteSpace(trimmedSearch))
			{
				visibleProducts = visibleProducts.Where(product =>
					product.Name.Contains(
						trimmedSearch,
						StringComparison.CurrentCultureIgnoreCase));
			}

			// Sort by the product name. ID is used only as a stable tie-breaker.
			var sortedProducts = sortAscending
				? visibleProducts.OrderBy(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase)
				: visibleProducts.OrderByDescending(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase);

			return sortedProducts
				.ThenBy(product => product.ID)
				.ToList();
		}

		/// <summary>
		/// Build the small count message shown underneath the Products heading.
		/// </summary>
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
