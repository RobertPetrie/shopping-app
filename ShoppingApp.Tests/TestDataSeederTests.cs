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
					28,
					await connection.Table<Product>().CountAsync());

				Assert.Equal(
					7,
					await connection.Table<ProductCategory>().CountAsync());

				Assert.Equal(
					9,
					await connection.Table<StorageLocation>().CountAsync());

				// Taxability belongs to the category, while products store only
				// the category they were assigned.
				var bananas = await connection
					.Table<Product>()
					.Where(product => product.Name == "Disco Bananas")
					.FirstAsync();

				var chips = await connection
					.Table<Product>()
					.Where(product => product.Name == "Royal Couch Potato Chips")
					.FirstAsync();

				var basicGrocery = await connection
					.Table<ProductCategory>()
					.Where(category => category.Name == "Basic Grocery")
					.FirstAsync();

				var snackFood = await connection
					.Table<ProductCategory>()
					.Where(category => category.Name == "Snack Food")
					.FirstAsync();

				Assert.False(basicGrocery.IsTaxable);
				Assert.True(snackFood.IsTaxable);
				Assert.Equal(basicGrocery.ID, bananas.ProductCategoryID);
				Assert.Equal(snackFood.ID, chips.ProductCategoryID);

				// Keep at least one sample product in every category so the
				// Product form can be exercised against every tax status.
				var categories = await connection
					.Table<ProductCategory>()
					.ToListAsync();

				var products = await connection
					.Table<Product>()
					.ToListAsync();

				Assert.All(
					categories,
					category => Assert.Contains(
						products,
						product => product.ProductCategoryID == category.ID));

				// Every built-in storage location has sample data so the picker
				// can be exercised without creating extra records by hand.
				var storageLocations = await connection
					.Table<StorageLocation>()
					.ToListAsync();

				Assert.All(
					storageLocations,
					location => Assert.Contains(
						products,
						product => product.StorageLocationID == location.ID));

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
					28,
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
