using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// User-facing sort choices for the Products screen.
	/// </summary>
	public enum ProductSortMode
	{
		NameAscending,
		NameDescending,
		StorageLocation
	}

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
			ProductSortMode sortMode)
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

			IOrderedEnumerable<Product> sortedProducts = sortMode switch
			{
				ProductSortMode.NameDescending =>
					visibleProducts.OrderByDescending(
						product => product.Name,
						StringComparer.CurrentCultureIgnoreCase),

				ProductSortMode.StorageLocation =>
					visibleProducts
						.OrderBy(
							product => product.StorageLocationName,
							StringComparer.CurrentCultureIgnoreCase)
						.ThenBy(
							product => product.Name,
							StringComparer.CurrentCultureIgnoreCase),

				_ => visibleProducts.OrderBy(
					product => product.Name,
					StringComparer.CurrentCultureIgnoreCase)
			};

			// Name modes use ID as their stable tie-breaker. Location mode already
			// uses product name first, then ID keeps duplicate names deterministic.
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
