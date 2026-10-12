using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListProductScreenTests
	{
		private static readonly List<ShoppingListProductListItem> FilterItems =
		[
			new()
			{
				ShoppingListID = 1,
				ProductID = 1,
				ProductName = "Bananas",
				PickedUp = false
			},
			new()
			{
				ShoppingListID = 1,
				ProductID = 2,
				ProductName = "Apple Juice",
				PickedUp = true
			},
			new()
			{
				ShoppingListID = 1,
				ProductID = 3,
				ProductName = "Chocolate",
				PickedUp = false
			}
		];

		[Fact]
		public void Filter_WithEmptySearch_ReturnsAllProductsByName()
		{
			// Opening the screen without a search should show every related
			// product in a predictable alphabetical order.
			var result = ShoppingListProductFilter.Filter(
				FilterItems,
				string.Empty);

			Assert.Equal(
				["Apple Juice", "Bananas", "Chocolate"],
				result.Select(item => item.ProductName));
		}

		[Fact]
		public void Filter_WithSearch_IgnoresCaseAndExtraSpaces()
		{
			// The live search should be forgiving about capitalization and spaces
			// around what the shopper typed.
			var result = ShoppingListProductFilter.Filter(
				FilterItems,
				"  APPLE  ");

			Assert.Single(result);
			Assert.Equal("Apple Juice", result[0].ProductName);
		}

		[Fact]
		public void GetStatusText_WithSearch_ShowsVisibleAndTotalCounts()
		{
			// While searching, the status line should show how many of the
			// shopping-list products are currently visible.
			string result = ShoppingListProductFilter.GetStatusText(
				totalCount: 8,
				visibleCount: 2,
				searchText: "soap");

			Assert.Equal("2 of 8 products", result);
		}

		[Fact]
		public async Task GetShoppingListProductsAsync_ReturnsOnlySelectedListsProducts()
		{
			// Two shopping lists can contain different Products. The detail screen
			// must receive only rows belonging to the list the user tapped.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var firstList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 11));

				var secondList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 12));

				var bananas = await database.AddProductAsync("Bananas");
				var soap = await database.AddProductAsync("Soap");

				await database.AddShoppingListProductAsync(
					firstList.ID,
					bananas.ID,
					1.99m);

				await database.AddShoppingListProductAsync(
					secondList.ID,
					soap.ID,
					4.99m);

				var result = await database.GetShoppingListProductsAsync(
					firstList.ID);

				Assert.Single(result);
				Assert.Equal("Bananas", result[0].ProductName);
				Assert.Equal(firstList.ID, result[0].ShoppingListID);
				Assert.False(result[0].PickedUp);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task SetShoppingListProductPickedUpAsync_SavesCheckedAndUncheckedValues()
		{
			// A checkbox tap must persist true as SQLite 1, and another tap must
			// persist false as SQLite 0 for that exact shopping-list/product row.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var shoppingList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 11));

				var product = await database.AddProductAsync("Milk");

				await database.AddShoppingListProductAsync(
					shoppingList.ID,
					product.ID,
					3.49m);

				await database.SetShoppingListProductPickedUpAsync(
					shoppingList.ID,
					product.ID,
					pickedUp: true);

				var checkedItems = await database.GetShoppingListProductsAsync(
					shoppingList.ID);

				Assert.True(checkedItems.Single().PickedUp);

				// Verify the exact SQLite representation requested by the UI:
				// checked is stored as integer 1.
				var connection = await database.GetConnectionAsync();
				int checkedRawValue = await connection.ExecuteScalarAsync<int>(
					"""
                    SELECT "PickedUp"
                    FROM "ShoppingListProduct"
                    WHERE "ShoppingListID" = ?
                      AND "ProductID" = ?;
                    """,
					shoppingList.ID,
					product.ID);

				Assert.Equal(1, checkedRawValue);

				await database.SetShoppingListProductPickedUpAsync(
					shoppingList.ID,
					product.ID,
					pickedUp: false);

				var uncheckedItems = await database.GetShoppingListProductsAsync(
					shoppingList.ID);

				Assert.False(uncheckedItems.Single().PickedUp);

				// Unchecked is stored as integer 0.
				int uncheckedRawValue = await connection.ExecuteScalarAsync<int>(
					"""
                    SELECT "PickedUp"
                    FROM "ShoppingListProduct"
                    WHERE "ShoppingListID" = ?
                      AND "ProductID" = ?;
                    """,
					shoppingList.ID,
					product.ID);

				Assert.Equal(0, uncheckedRawValue);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task ResetAndSeedAsync_PendingListHasChecklistExamples()
		{
			// Development startup data should populate the pending list with rows
			// so the new checklist screen can be tested without manual SQL setup.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				await TestDataSeeder.ResetAndSeedAsync(database);
				var connection = await database.GetConnectionAsync();

				var pendingList = await connection
					.Table<ShoppingList>()
					.Where(list => list.ShoppingDateStorage == "2026-10-11")
					.FirstAsync();

				var items = await connection
					.Table<ShoppingListProduct>()
					.Where(item => item.ShoppingListID == pendingList.ID)
					.ToListAsync();

				Assert.Equal(8, items.Count);

				// Keep both states in the sample data so checked and unchecked
				// rendering can be verified immediately.
				Assert.Equal(2, items.Count(item => item.PickedUp));
				Assert.Equal(6, items.Count(item => !item.PickedUp));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			// Give each test its own SQLite file so state cannot leak between
			// tests or touch the developer's real database.
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-list-product-screen-tests-{Guid.NewGuid():N}.db3");
		}

		private static async Task CloseAndDeleteDatabaseAsync(
			AppDatabase database)
		{
			try
			{
				var connection = await database.GetConnectionAsync();
				await connection.CloseAsync();
			}
			finally
			{
				// SQLite may leave small sidecar files depending on the platform.
				foreach (string path in new[]
				{
					database.DatabasePath,
					database.DatabasePath + "-shm",
					database.DatabasePath + "-wal"
				})
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			}
		}
	}
}
