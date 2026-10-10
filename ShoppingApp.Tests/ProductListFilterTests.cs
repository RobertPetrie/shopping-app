using shopping_app.Logic;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ProductListFilterTests
	{
		private static readonly List<Product> Products =
		[
			new Product { ID = 1, Name = "Bananas" },
			new Product { ID = 2, Name = "Apple Juice" },
			new Product { ID = 3, Name = "Apples" },
			new Product { ID = 4, Name = "Chocolate" }
		];

		[Fact]
		public void FilterAndSort_WithEmptySearch_ReturnsEveryProductAZ()
		{
			// An empty search should behave like the normal unfiltered list.
			var result = ProductListFilter.FilterAndSort(
				Products,
				string.Empty,
				sortAscending: true);

			Assert.Equal(
				["Apple Juice", "Apples", "Bananas", "Chocolate"],
				result.Select(product => product.Name));
		}

		[Fact]
		public void FilterAndSort_WithSearch_FiltersImmediatelyAndIgnoresCase()
		{
			// Typing "app" should match product names even when their
			// capitalization is different from the search text.
			var result = ProductListFilter.FilterAndSort(
				Products,
				"app",
				sortAscending: true);

			Assert.Equal(
				["Apple Juice", "Apples"],
				result.Select(product => product.Name));
		}

		[Fact]
		public void FilterAndSort_WithSpacesAroundSearch_TrimsTheSearchText()
		{
			// Accidental spaces before or after the search should not stop a match.
			var result = ProductListFilter.FilterAndSort(
				Products,
				"  apple  ",
				sortAscending: true);

			Assert.Equal(
				["Apple Juice", "Apples"],
				result.Select(product => product.Name));
		}

		[Fact]
		public void FilterAndSort_WithNoMatches_ReturnsEmptyList()
		{
			// A search with no matching product should show an empty list.
			var result = ProductListFilter.FilterAndSort(
				Products,
				"oranges",
				sortAscending: true);

			Assert.Empty(result);
		}

		[Fact]
		public void FilterAndSort_Descending_ReversesTheVisibleNameOrder()
		{
			// The Z-to-A button should reverse the order without changing
			// which products are included.
			var result = ProductListFilter.FilterAndSort(
				Products,
				string.Empty,
				sortAscending: false);

			Assert.Equal(
				["Chocolate", "Bananas", "Apples", "Apple Juice"],
				result.Select(product => product.Name));
		}

		[Fact]
		public void FilterAndSort_WithSearchAndDescending_SortsOnlyMatches()
		{
			// Searching and sorting must work together rather than one
			// feature replacing the other.
			var result = ProductListFilter.FilterAndSort(
				Products,
				"app",
				sortAscending: false);

			Assert.Equal(
				["Apples", "Apple Juice"],
				result.Select(product => product.Name));
		}

		[Fact]
		public void GetStatusText_WithoutSearch_ShowsTotalProductCount()
		{
			// Without a search, the heading should show the normal total.
			string result = ProductListFilter.GetStatusText(
				totalCount: 29,
				visibleCount: 29,
				searchText: string.Empty);

			Assert.Equal("29 products", result);
		}

		[Fact]
		public void GetStatusText_WithSearch_ShowsVisibleAndTotalCounts()
		{
			// While searching, make it clear how many of the total products match.
			string result = ProductListFilter.GetStatusText(
				totalCount: 29,
				visibleCount: 4,
				searchText: "apple");

			Assert.Equal("4 of 29 products", result);
		}
	}
}
