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
			// A fresh database should always expose the seven built-in categories
			// with the tax settings the Product form depends on.
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
			// Saving a product should keep its chosen category, and editing the
			// product should replace that category without changing the product ID.
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
		public async Task GetProductAsync_ReturnsSavedCategoryAndStorageLocation()
		{
			// The edit form reloads the Product row before selecting its pickers,
			// so this read must return both saved lookup IDs unchanged.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var category = (await database.GetProductCategoriesAsync())
					.Single(item => item.Name == "Household / Non-Food");

				var location = (await database.GetStorageLocationsAsync())
					.Single(item => item.Name == "Bathroom");

				var saved = await database.AddProductAsync(
					"Bathroom Tissue Test",
					productCategoryId: category.ID,
					storageLocationId: location.ID);

				var loaded = await database.GetProductAsync(saved.ID);

				Assert.NotNull(loaded);
				Assert.Equal(category.ID, loaded.ProductCategoryID);
				Assert.Equal(location.ID, loaded.StorageLocationID);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task GetStorageLocationsAsync_ReturnsBuiltInChoicesAlphabetically()
		{
			// The Storage Location picker relies on the built-in names being
			// returned in predictable alphabetical order.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var locations = await database.GetStorageLocationsAsync();

				Assert.Equal(
					[
						"Basement",
						"Bathroom",
						"Freezer",
						"Garage",
						"Kitchen Cabinet",
						"Laundry Room",
						"Other",
						"Pantry",
						"Refrigerator"
					],
					locations.Select(location => location.Name));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddAndUpdateProductAsync_PersistsStorageLocation()
		{
			// Saving and then editing a product should persist the selected
			// home storage location in the Product table.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();
				var grocery = categories.Single(
					category => category.Name == "Basic Grocery");

				var locations = await database.GetStorageLocationsAsync();
				var pantry = locations.Single(
					location => location.Name == "Pantry");
				var refrigerator = locations.Single(
					location => location.Name == "Refrigerator");

				var product = await database.AddProductAsync(
					"Storage Test",
					productCategoryId: grocery.ID,
					storageLocationId: pantry.ID);

				Assert.Equal(pantry.ID, product.StorageLocationID);

				await database.UpdateProductAsync(
					product.ID,
					product.Name,
					product.Description,
					grocery.ID,
					refrigerator.ID);

				var connection = await database.GetConnectionAsync();
				var saved = await connection
					.Table<Product>()
					.Where(item => item.ID == product.ID)
					.FirstAsync();

				Assert.Equal(refrigerator.ID, saved.StorageLocationID);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddProductAsync_WithMissingStorageLocation_RejectsReference()
		{
			// A product must not be saved with a storage-location ID that does
			// not exist in the lookup table.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				await Assert.ThrowsAsync<KeyNotFoundException>(
					() => database.AddProductAsync(
						"Bad Storage Location",
						storageLocationId: 999999));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddProductAsync_WithoutStorageLocation_AllowsNull()
		{
			// Storage location is optional, so a product without one should
			// save successfully and keep a null reference.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				var product = await database.AddProductAsync(
					"Unassigned Storage");

				Assert.Null(product.StorageLocationID);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task AddProductAsync_WithMissingCategory_RejectsReference()
		{
			// A product must not be saved with a category ID that no longer
			// exists in the ProductCategory table.
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
			// Taxability now belongs to ProductCategory, so a brand-new Product
			// table must not recreate the old IsTaxable column.
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
			// Opening an older database should remove the obsolete Product tax
			// column while keeping the existing product and adding new lookup fields.
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

				int storageLocationColumnCount =
					await connection.ExecuteScalarAsync<int>(
						"""
                        SELECT COUNT(*)
                        FROM pragma_table_info('Product')
                        WHERE name = 'StorageLocationID';
                        """);

				var legacyProduct = await connection
					.Table<Product>()
					.Where(item => item.Name == "Legacy Milk")
					.FirstAsync();

				Assert.Equal(0, taxColumnCount);
				Assert.Equal(1, storageLocationColumnCount);
				Assert.Null(legacyProduct.ProductCategoryID);
				Assert.Null(legacyProduct.StorageLocationID);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task GetConnectionAsync_LegacyCategoryTable_AddsTaxColumn()
		{
			// Opening a category table from the older schema should add IsTaxable
			// safely and apply the intended defaults to existing built-in rows.
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
                VALUES ('Basic Grocery', 1), ('Snack Food', 2);
                """);

			await legacyConnection.CloseAsync();

			var database = new AppDatabase(databasePath);

			try
			{
				var categories = await database.GetProductCategoriesAsync();
				var connection = await database.GetConnectionAsync();

				// The explicit migration must add the new NOT NULL column
				// before sqlite-net attempts any model-driven migration.
				int taxColumnCount = await connection.ExecuteScalarAsync<int>(
					"""
                    SELECT COUNT(*)
                    FROM pragma_table_info('ProductCategory')
                    WHERE name = 'IsTaxable';
                    """);

				Assert.Equal(1, taxColumnCount);

				// Existing category rows survive and receive their intended
				// built-in tax settings during the one-time migration.
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

		[Fact]
		public async Task GetConnectionAsync_LegacyShoppingListProduct_AddsPickedUpDefaultFalse()
		{
			// Reproduce the old linking-table schema before per-product pickup
			// tracking existed, including one row that must survive the upgrade.
			string databasePath = CreateTemporaryDatabasePath();

			var legacyConnection = new SQLiteAsyncConnection(
				databasePath,
				SQLiteOpenFlags.ReadWrite |
				SQLiteOpenFlags.Create |
				SQLiteOpenFlags.FullMutex,
				storeDateTimeAsTicks: true);

			await legacyConnection.ExecuteAsync(
				"""
                CREATE TABLE "ShoppingListProduct"
                (
                    "ShoppingListID" INTEGER NOT NULL,
                    "ProductID" INTEGER NOT NULL,
                    "Price" INTEGER NOT NULL,
                    "CreatedOn" INTEGER NOT NULL,
                    PRIMARY KEY ("ShoppingListID", "ProductID")
                );
                """);

			await legacyConnection.ExecuteAsync(
				"""
                INSERT INTO "ShoppingListProduct"
                    ("ShoppingListID", "ProductID", "Price", "CreatedOn")
                VALUES (1, 1, 499, ?);
                """,
				DateTime.UtcNow.Ticks);

			await legacyConnection.CloseAsync();

			var database = new AppDatabase(databasePath);

			try
			{
				var connection = await database.GetConnectionAsync();

				// The explicit migration must add a NOT NULL PickedUp column
				// with a SQL default so old rows remain valid.
				int pickedUpColumnCount =
					await connection.ExecuteScalarAsync<int>(
						"""
                        SELECT COUNT(*)
                        FROM pragma_table_info('ShoppingListProduct')
                        WHERE name = 'PickedUp';
                        """);

				int pickedUpValue =
					await connection.ExecuteScalarAsync<int>(
						"""
                        SELECT "PickedUp"
                        FROM "ShoppingListProduct"
                        WHERE "ShoppingListID" = 1
                          AND "ProductID" = 1;
                        """);

				Assert.Equal(1, pickedUpColumnCount);
				Assert.Equal(0, pickedUpValue);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			// Give every database test its own file so one test cannot leak
			// records or schema changes into another test.
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-product-tests-{Guid.NewGuid():N}.db3");
		}

		private static async Task CloseAndDeleteDatabaseAsync(
			AppDatabase database)
		{
			// Close SQLite first, then remove the database and any temporary
			// sidecar files it created during the test.
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
