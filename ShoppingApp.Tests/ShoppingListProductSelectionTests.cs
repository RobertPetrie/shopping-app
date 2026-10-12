using shopping_app.Data;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ShoppingListProductSelectionTests
	{
		[Fact]
		public async Task GetShoppingListProductIdsAsync_ReturnsOnlySelectedListsProducts()
		{
			// The reused Products screen needs to know which buttons should say
			// "Added" for the particular Shopping List currently being edited.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var firstList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 12));

				var secondList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 13));

				var apples = await database.AddProductAsync("Picker Apples");
				var cereal = await database.AddProductAsync("Picker Cereal");

				await database.AddShoppingListProductAsync(
					firstList.ID,
					apples.ID,
					price: 0m);

				await database.AddShoppingListProductAsync(
					secondList.ID,
					cereal.ID,
					price: 0m);

				var productIds = await database.GetShoppingListProductIdsAsync(
					firstList.ID);

				Assert.Single(productIds);
				Assert.Contains(apples.ID, productIds);
				Assert.DoesNotContain(cereal.ID, productIds);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddShoppingListProductAsync_FromPickerStartsUncheckedAtZeroPrice()
		{
			// Adding a Product from the selector should only put it on the list.
			// Price and PickedUp are filled later when the shopper takes the item.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var shoppingList = await database.AddShoppingListAsync(
					new DateOnly(2026, 10, 12));

				var product = await database.AddProductAsync("Picker Milk");

				await database.AddShoppingListProductAsync(
					shoppingList.ID,
					product.ID,
					price: 0m);

				var connection = await database.GetConnectionAsync();
				var row = await connection
					.Table<ShoppingListProduct>()
					.Where(item =>
						item.ShoppingListID == shoppingList.ID
						&& item.ProductID == product.ID)
					.FirstAsync();

				Assert.Equal(0L, row.PriceInCents);
				Assert.False(row.PickedUp);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			// Keep selection tests isolated from each other and from the app's
			// real development database.
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-list-product-selection-tests-{Guid.NewGuid():N}.db3");
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
