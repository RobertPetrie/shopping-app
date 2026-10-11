using shopping_app.Data;
using shopping_app.Models;
using SQLite;

namespace ShoppingApp.Tests
{
	public class ProductDatabaseTests
	{
		[Fact]
		public async Task GetProductCategoriesAsync_ReturnsBuiltInChoicesAndTaxSettings()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();

				Assert.Equal(
					[
						("Basic Grocery", false),
						("Snack Food", true),
						("Candy / Confectionery", true),
						("Carbonated Beverage", true),
						("Household / Non-Food", true),
						("Prepared Food", true),
						("Other", false)
					],
					categories.Select(category =>
						(category.Name, category.IsTaxable)));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddAndUpdateProductAsync_PersistsChosenCategory()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();
				var grocery = categories.Single(
					category => category.Name == "Basic Grocery");
				var snack = categories.Single(
					category => category.Name == "Snack Food");

				var product = await database.AddProductAsync(
					"Category Test",
					productCategoryId: grocery.ID);

				Assert.Equal(grocery.ID, product.ProductCategoryID);

				await database.UpdateProductAsync(
					product.ID,
					product.Name,
					product.Description,
					snack.ID);

				var connection = await database.GetConnectionAsync();
				var saved = await connection
					.Table<Product>()
					.Where(item => item.ID == product.ID)
					.FirstAsync();

				Assert.Equal(snack.ID, saved.ProductCategoryID);
				Assert.NotNull(saved.ModifiedOn);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddProductAsync_WithMissingCategory_RejectsReference()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				await Assert.ThrowsAsync<KeyNotFoundException>(
					() => database.AddProductAsync(
						"Bad Category",
						productCategoryId: 999999));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task NewDatabase_ProductTableDoesNotContainTaxColumn()
		{
			string databasePath = CreateTemporaryDatabasePath();
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

				Assert.Equal(0, taxColumnCount);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task GetConnectionAsync_LegacyProductTable_MovesTaxOutOfProduct()
		{
			string databasePath = CreateTemporaryDatabasePath();

			// This reproduces the short-lived schema where Product owned IsTaxable.
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
                    "ProductCategoryID" INTEGER,
                    "IsTaxable" INTEGER NOT NULL DEFAULT 0,
                    "CreatedOn" BIGINT NOT NULL,
                    "ModifiedOn" BIGINT
                );
                """);

			await legacyConnection.ExecuteAsync(
				"""
                INSERT INTO "Product" ("Name", "IsTaxable", "CreatedOn")
                VALUES (?, ?, ?);
                """,
				"Legacy Milk",
				1,
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

				Assert.Equal(0, taxColumnCount);
				Assert.Null(legacyProduct.ProductCategoryID);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task GetConnectionAsync_LegacyCategoryTable_AddsTaxColumn()
		{
			string databasePath = CreateTemporaryDatabasePath();

			var legacyConnection = new SQLiteAsyncConnection(
				databasePath,
				SQLiteOpenFlags.ReadWrite |
				SQLiteOpenFlags.Create |
				SQLiteOpenFlags.FullMutex,
				storeDateTimeAsTicks: true);

			await legacyConnection.ExecuteAsync(
				"""
                CREATE TABLE "ProductCategory"
                (
                    "ID" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                    "Name" VARCHAR NOT NULL UNIQUE,
                    "SortOrder" INTEGER NOT NULL
                );
                """);

			await legacyConnection.ExecuteAsync(
				"""
                INSERT INTO "ProductCategory" ("Name", "SortOrder")
                VALUES ("Basic Grocery", 1), ("Snack Food", 2);
                """);

			await legacyConnection.CloseAsync();

			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();

				Assert.False(categories.Single(
					category => category.Name == "Basic Grocery").IsTaxable);

				Assert.True(categories.Single(
					category => category.Name == "Snack Food").IsTaxable);
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
