using shopping_app.Logic;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListListFilterTests
	{
		private static readonly List<ShoppingList> ShoppingLists =
		[
			new ShoppingList
			{
				ID = 1,
				ShoppingDate = new DateOnly(2026, 9, 3),
				TotalCost = 147.21m,
				PickedUp = true
			},
			new ShoppingList
			{
				ID = 2,
				ShoppingDate = new DateOnly(2026, 10, 11),
				TotalCost = null,
				PickedUp = false
			},
			new ShoppingList
			{
				ID = 3,
				ShoppingDate = new DateOnly(2026, 10, 1),
				TotalCost = 126.18m,
				PickedUp = true
			}
		];

		[Fact]
		public void FilterAndSort_NewestFirst_PutsLatestDateFirst()
		{
			// The default screen order should show the newest trip first.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				string.Empty,
				newestFirst: true);

			Assert.Equal(
				[new DateOnly(2026, 10, 11),
				 new DateOnly(2026, 10, 1),
				 new DateOnly(2026, 9, 3)],
				result.Select(list => list.ShoppingDate));
		}

		[Fact]
		public void FilterAndSort_OldestFirst_PutsEarliestDateFirst()
		{
			// Switching the sort button should reverse the date order.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				string.Empty,
				newestFirst: false);

			Assert.Equal(
				[new DateOnly(2026, 9, 3),
				 new DateOnly(2026, 10, 1),
				 new DateOnly(2026, 10, 11)],
				result.Select(list => list.ShoppingDate));
		}

		[Fact]
		public void FilterAndSort_SearchByIsoDate_FindsTheRightList()
		{
			// Date searches also accept the yyyy-MM-dd value stored in SQLite.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				"2026-10-01",
				newestFirst: true);

			Assert.Single(result);
			Assert.Equal(3, result[0].ID);
		}

		[Fact]
		public void FilterAndSort_SearchPending_FindsUnfinishedLists()
		{
			// Typing "pending" should find lists that are not picked up yet.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				"pending",
				newestFirst: true);

			Assert.Single(result);
			Assert.False(result[0].PickedUp);
		}

		[Fact]
		public void FilterAndSort_SearchPickedUp_FindsCompletedLists()
		{
			// Typing "picked" should find completed shopping trips.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				"picked",
				newestFirst: true);

			Assert.Equal(2, result.Count);
			Assert.All(result, list => Assert.True(list.PickedUp));
		}

		[Fact]
		public void FilterAndSort_SearchWithNoMatch_ReturnsEmptyList()
		{
			// An unrelated search should simply show no matching cards.
			var result = ShoppingListListFilter.FilterAndSort(
				ShoppingLists,
				"definitely not a shopping list",
				newestFirst: true);

			Assert.Empty(result);
		}

		[Fact]
		public void GetStatusText_WithSearch_ShowsVisibleAndTotalCounts()
		{
			// The subtitle should explain how many lists match the search.
			string result = ShoppingListListFilter.GetStatusText(
				totalCount: 12,
				visibleCount: 3,
				searchText: "picked");

			Assert.Equal("3 of 12 shopping lists", result);
		}
	}
}
