using shopping_app.Data;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class TestDataSeederTests
	{
		[Fact]
		public async Task SeedAsync_OnEmptyDatabase_CreatesExpectedSampleData()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				// Seed a brand-new database exactly like the development app does.
				await TestDataSeeder.SeedAsync(database);

				var connection = await database.GetConnectionAsync();

				Assert.Equal(
					20,
					await connection.Table<Product>().CountAsync());

				Assert.Equal(
					7,
					await connection.Table<ProductCategory>().CountAsync());

				// Sample products include both tax values and explicit categories.
				var bananas = await connection
					.Table<Product>()
					.Where(product => product.Name == "Disco Bananas")
					.FirstAsync();

				var chips = await connection
					.Table<Product>()
					.Where(product => product.Name == "Royal Couch Potato Chips")
					.FirstAsync();

				Assert.False(bananas.IsTaxable);
				Assert.True(chips.IsTaxable);

				var basicGrocery = await connection
					.Table<ProductCategory>()
					.Where(category => category.Name == "Basic Grocery")
					.FirstAsync();

				var snackFood = await connection
					.Table<ProductCategory>()
					.Where(category => category.Name == "Snack Food")
					.FirstAsync();

				Assert.Equal(basicGrocery.ID, bananas.ProductCategoryID);
				Assert.Equal(snackFood.ID, chips.ProductCategoryID);

				Assert.Equal(
					12,
					await connection.Table<ShoppingList>().CountAsync());

				// The sample set intentionally includes one upcoming pending trip.
				var pendingLists = await connection
					.Table<ShoppingList>()
					.Where(list => list.PickedUp == false)
					.ToListAsync();

				Assert.Single(pendingLists);
				Assert.Equal(
					"2026-10-11",
					pendingLists[0].ShoppingDateStorage);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task SeedAsync_WhenRunTwice_DoesNotDuplicateSampleData()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				// Running the seeder more than once should be safe.
				await TestDataSeeder.SeedAsync(database);
				await TestDataSeeder.SeedAsync(database);

				var connection = await database.GetConnectionAsync();

				Assert.Equal(
					20,
					await connection.Table<Product>().CountAsync());

				Assert.Equal(
					12,
					await connection.Table<ShoppingList>().CountAsync());
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			// Every test gets its own database so tests cannot affect one another.
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-tests-{Guid.NewGuid():N}.db3");
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
				// SQLite can create small sidecar files depending on the platform.
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
