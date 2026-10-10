using System.Globalization;
using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Contains the Shopping Lists screen rules without depending on the UI.
	/// This keeps search and sorting easy to unit test.
	/// </summary>
	public static class ShoppingListListFilter
	{
		public static List<ShoppingList> FilterAndSort(
			IEnumerable<ShoppingList> shoppingLists,
			string? searchText,
			bool newestFirst)
		{
			ArgumentNullException.ThrowIfNull(shoppingLists);

			string trimmedSearch = searchText?.Trim() ?? string.Empty;
			IEnumerable<ShoppingList> visibleLists = shoppingLists;

			if (!string.IsNullOrWhiteSpace(trimmedSearch))
			{
				visibleLists = visibleLists.Where(list =>
					MatchesSearch(list, trimmedSearch));
			}

			// Lists without a date are kept at the end in either direction.
			var sortedLists = newestFirst
				? visibleLists
					.OrderByDescending(list => list.ShoppingDate.HasValue)
					.ThenByDescending(list => list.ShoppingDate)
					.ThenByDescending(list => list.ID)
				: visibleLists
					.OrderByDescending(list => list.ShoppingDate.HasValue)
					.ThenBy(list => list.ShoppingDate)
					.ThenBy(list => list.ID);

			return sortedLists.ToList();
		}

		private static bool MatchesSearch(
			ShoppingList shoppingList,
			string searchText)
		{
			string dateText = shoppingList.ShoppingDate?.ToString(
				"MMMM d, yyyy",
				CultureInfo.CurrentCulture) ?? "No shopping date";

			string isoDate = shoppingList.ShoppingDateStorage ?? string.Empty;

			string totalText = shoppingList.TotalCost.HasValue
				? shoppingList.TotalCost.Value.ToString(
					"C",
					CultureInfo.CurrentCulture)
				: "Total not entered";

			// Search the same status text that is actually shown on the card.
			// This keeps "picked" from also matching a Pending list through
			// hidden wording such as "not picked up".
			string statusText = shoppingList.PickupStatus;

			// Match the date, total, or visible status text.
			return Contains(dateText, searchText)
				|| Contains(isoDate, searchText)
				|| Contains(totalText, searchText)
				|| Contains(statusText, searchText);
		}

		private static bool Contains(string value, string searchText)
		{
			return value.Contains(
				searchText,
				StringComparison.CurrentCultureIgnoreCase);
		}

		public static string GetStatusText(
			int totalCount,
			int visibleCount,
			string? searchText)
		{
			return string.IsNullOrWhiteSpace(searchText)
				? $"{totalCount} shopping lists"
				: $"{visibleCount} of {totalCount} shopping lists";
		}
	}
}
