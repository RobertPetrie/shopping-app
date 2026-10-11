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
		public async Task GetProductCategoriesAsync_ReturnsBuiltInChoicesInOrder()
		{
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();

				Assert.Equal(
					[
						"Basic Grocery",
						"Snack Food",
						"Candy / Confectionery",
						"Carbonated Beverage",
						"Household / Non-Food",
						"Prepared Food",
						"Other"
					],
					categories.Select(category => category.Name));
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
					product.IsTaxable,
					snack.ID);

				var connection = await database.GetConnectionAsync();
				var saved = await connection
					.Table<Product>()
					.Where(item => item.ID == product.ID)
					.FirstAsync();

				Assert.Equal(snack.ID, saved.ProductCategoryID);
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

				int categoryColumnCount =
					await connection.ExecuteScalarAsync<int>(
						"""
                        SELECT COUNT(*)
                        FROM pragma_table_info('Product')
                        WHERE name = 'ProductCategoryID';
                        """);

				var legacyProduct = await connection
					.Table<Product>()
					.Where(item => item.Name == "Legacy Milk")
					.FirstAsync();

				Assert.Equal(1, taxColumnCount);
				Assert.Equal(1, categoryColumnCount);
				Assert.False(legacyProduct.IsTaxable);
				Assert.Null(legacyProduct.ProductCategoryID);
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
