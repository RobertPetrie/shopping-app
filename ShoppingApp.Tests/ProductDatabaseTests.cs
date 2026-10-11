using shopping_app.Data;
using shopping_app.Models;
using SQLite;

namespace ShoppingApp.Tests
{
	public class ProductDatabaseTests
	{
		[Fact]
		public async Task AddProductAsync_WhenTaxableIsTrue_StoresTrue()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var product = await database.AddProductAsync(
					"Taxable Snack",
					"A test snack.",
					isTaxable: true);

				var connection = await database.GetConnectionAsync();
				var saved = await connection
					.Table<Product>()
					.Where(item => item.ID == product.ID)
					.FirstAsync();

				Assert.True(saved.IsTaxable);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddProductAsync_WithoutTaxChoice_DefaultsToFalse()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var product = await database.AddProductAsync(
					"Basic Grocery",
					"A test grocery item.");

				Assert.False(product.IsTaxable);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task UpdateProductAsync_ChangesTaxableValue()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var product = await database.AddProductAsync(
					"Changing Product",
					"Starts as not taxable.");

				await database.UpdateProductAsync(
					product.ID,
					product.Name,
					product.Description,
					isTaxable: true);

				var connection = await database.GetConnectionAsync();
				var saved = await connection
					.Table<Product>()
					.Where(item => item.ID == product.ID)
					.FirstAsync();

				Assert.True(saved.IsTaxable);
				Assert.NotNull(saved.ModifiedOn);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task GetConnectionAsync_LegacyProductTable_AddsTaxColumn()
		{
			string databasePath = CreateTemporaryDatabasePath();

			// Build the old Product schema exactly without IsTaxable.
			var legacyConnection = new SQLiteAsyncConnection(
				databasePath,
				SQLiteOpenFlags.ReadWrite |
				SQLiteOpenFlags.Create |
				SQLiteOpenFlags.FullMutex,
				storeDateTimeAsTicks: true);

			await legacyConnection.ExecuteAsync(
				"""
                CREATE TABLE "Product"
                (
                    "ID" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                    "Name" VARCHAR NOT NULL UNIQUE,
                    "Description" VARCHAR,
                    "ImagePath" VARCHAR,
                    "CreatedOn" BIGINT NOT NULL,
                    "ModifiedOn" BIGINT
                );
                """);

			await legacyConnection.ExecuteAsync(
				"""
                INSERT INTO "Product" ("Name", "CreatedOn")
                VALUES (?, ?);
                """,
				"Legacy Milk",
				DateTime.UtcNow.Ticks);

			await legacyConnection.CloseAsync();

			var database = new AppDatabase(databasePath);

			try
			{
				var connection = await database.GetConnectionAsync();

				int taxColumnCount =
					await connection.ExecuteScalarAsync<int>(
						"""
                        SELECT COUNT(*)
                        FROM pragma_table_info('Product')
                        WHERE name = 'IsTaxable';
                        """);

				var legacyProduct = await connection
					.Table<Product>()
					.Where(item => item.Name == "Legacy Milk")
					.FirstAsync();

				Assert.Equal(1, taxColumnCount);
				Assert.False(legacyProduct.IsTaxable);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-product-tests-{Guid.NewGuid():N}.db3");
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
