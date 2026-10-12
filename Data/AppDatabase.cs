using Microsoft.Maui.Storage;
using shopping_app.Models;
using SQLite;

namespace shopping_app.Data
{
	/// <summary>
	/// Initializes the local database and provides methods for stored data.
	/// Product names are compared without regard to capitalization.
	/// </summary>
	public sealed class AppDatabase
	{
		// Keep the filename stable so app updates reuse existing data.
		public string DatabasePath { get; }

		// Debug builds keep the live database in the repository's DebugData
		// folder so it is easy to inspect while developing. Release builds keep
		// the database in the app's normal private data folder.
		public AppDatabase()
			: this(GetDefaultDatabasePath())
		{
		}

		private static string GetDefaultDatabasePath()
		{
#if DEBUG
			return DevelopmentDebugArtifacts.DatabasePath;
#else
			return Path.Combine(FileSystem.AppDataDirectory, "shopping.db3");
#endif
		}

		// Tests can supply a temporary path so they never touch the real app database.
		public AppDatabase(string databasePath)
		{
			if (string.IsNullOrWhiteSpace(databasePath))
			{
				throw new ArgumentException(
					"A database path is required.",
					nameof(databasePath));
			}

			DatabasePath = databasePath;
		}

		// Prevent simultaneous callers from initializing the database twice.
		private readonly SemaphoreSlim initializationGate = new(1, 1);

		private SQLiteAsyncConnection? connection;

		/// <summary>
		/// Open the database and initialize its tables on first access.
		/// Later calls reuse the same connection.
		/// </summary>
		public async Task<SQLiteAsyncConnection> GetConnectionAsync()
		{
			await initializationGate.WaitAsync().ConfigureAwait(false);

			try
			{
				if (connection is not null)
				{
					return connection;
				}

				// Preserve existing data and store DateTime values as ticks.
				var newConnection = new SQLiteAsyncConnection(
					DatabasePath,
					SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.Create |
					SQLiteOpenFlags.FullMutex,
					storeDateTimeAsTicks: true);

				try
				{
					// Foreign-key enforcement is enabled per connection.
					await newConnection.ExecuteAsync(
						"PRAGMA foreign_keys = ON;").ConfigureAwait(false);

					// Product categories are app reference data, not development-only
					// test data, so every database gets the same seven choices.
					//
					// Important: do not ask sqlite-net to migrate an old category
					// table before our explicit migration runs. IsTaxable is NOT NULL,
					// and sqlite-net cannot add that column to a table that already
					// contains rows unless a SQL default is supplied.
					int categoryTableExists =
						await newConnection.ExecuteScalarAsync<int>(
							"""
                            SELECT COUNT(*)
                            FROM sqlite_master
                            WHERE type = 'table'
                              AND name = 'ProductCategory';
                            """).ConfigureAwait(false);

					if (categoryTableExists == 0)
					{
						// A brand-new database can use the current model directly.
						await newConnection.CreateTableAsync<ProductCategory>()
							.ConfigureAwait(false);
					}
					else
					{
						int categoryTaxColumnExists =
							await newConnection.ExecuteScalarAsync<int>(
								"""
                                SELECT COUNT(*)
                                FROM pragma_table_info('ProductCategory')
                                WHERE name = 'IsTaxable';
                                """).ConfigureAwait(false);

						if (categoryTaxColumnExists == 0)
						{
							// Older databases had categories without a tax flag.
							// Add a SQL default so existing rows receive a valid value,
							// then apply the intended built-in category defaults.
							await newConnection.ExecuteAsync(
								"""
                                ALTER TABLE "ProductCategory"
                                ADD COLUMN "IsTaxable" INTEGER NOT NULL DEFAULT 0;
                                """).ConfigureAwait(false);

							await ApplyBuiltInCategoryTaxDefaultsAsync(newConnection)
								.ConfigureAwait(false);
						}
					}

					await EnsureProductCategoriesAsync(newConnection)
						.ConfigureAwait(false);

					// Storage locations are reference data used by the Product form.
					await newConnection.CreateTableAsync<StorageLocation>()
						.ConfigureAwait(false);

					await EnsureStorageLocationsAsync(newConnection)
						.ConfigureAwait(false);

					// Sales tax is stored separately so the Settings screen can change
					// the percentage without changing Product or shopping-list rows.
					await newConnection.CreateTableAsync<TaxRate>()
						.ConfigureAwait(false);

					// Older app databases may not have the newer Product columns.
					// Add them before sqlite-net compares the Product model so
					// existing rows remain intact.
					int productTableExists =
						await newConnection.ExecuteScalarAsync<int>(
							"""
                            SELECT COUNT(*)
                            FROM sqlite_master
                            WHERE type = 'table'
                              AND name = 'Product';
                            """).ConfigureAwait(false);

					if (productTableExists > 0)
					{
						int legacyTaxColumnExists =
							await newConnection.ExecuteScalarAsync<int>(
								"""
                                SELECT COUNT(*)
                                FROM pragma_table_info('Product')
                                WHERE name = 'IsTaxable';
                                """).ConfigureAwait(false);

						if (legacyTaxColumnExists > 0)
						{
							// Taxability now belongs to ProductCategory.
							// This removes the short-lived Product column while
							// preserving every Product row.
							await newConnection.ExecuteAsync(
								"""
                                ALTER TABLE "Product"
                                DROP COLUMN "IsTaxable";
                                """).ConfigureAwait(false);
						}

						int categoryColumnExists =
							await newConnection.ExecuteScalarAsync<int>(
								"""
                                SELECT COUNT(*)
                                FROM pragma_table_info('Product')
                                WHERE name = 'ProductCategoryID';
                                """).ConfigureAwait(false);

						if (categoryColumnExists == 0)
						{
							// Do not guess categories for existing products.
							await newConnection.ExecuteAsync(
								"""
                                ALTER TABLE "Product"
                                ADD COLUMN "ProductCategoryID" INTEGER;
                                """).ConfigureAwait(false);
						}

						int storageLocationColumnExists =
							await newConnection.ExecuteScalarAsync<int>(
								"""
                                SELECT COUNT(*)
                                FROM pragma_table_info('Product')
                                WHERE name = 'StorageLocationID';
                                """).ConfigureAwait(false);

						if (storageLocationColumnExists == 0)
						{
							// Existing products remain unassigned until edited.
							await newConnection.ExecuteAsync(
								"""
                                ALTER TABLE "Product"
                                ADD COLUMN "StorageLocationID" INTEGER;
                                """).ConfigureAwait(false);
						}
					}

					// Create parent tables before the linking table.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);

					await newConnection.CreateTableAsync<ShoppingList>()
						.ConfigureAwait(false);

					// Support fast product-to-category lookups on new and old databases.
					await newConnection.ExecuteAsync(
						"""
                        CREATE INDEX IF NOT EXISTS
                            "IX_Product_ProductCategoryID"
                        ON "Product" ("ProductCategoryID");
                        """).ConfigureAwait(false);

					// Storage location is another Product lookup.
					await newConnection.ExecuteAsync(
						"""
                        CREATE INDEX IF NOT EXISTS
                            "IX_Product_StorageLocationID"
                        ON "Product" ("StorageLocationID");
                        """).ConfigureAwait(false);

					// Preserve existing tables and records.
					await newConnection.ExecuteAsync(
						"""
                        CREATE TABLE IF NOT EXISTS "ShoppingListProduct"
                        (
                            "ShoppingListID" INTEGER NOT NULL,
                            "ProductID" INTEGER NOT NULL,
                            "Price" INTEGER NOT NULL,
                            "PickedUp" INTEGER NOT NULL DEFAULT 0,
                            "CreatedOn" INTEGER NOT NULL,

                            PRIMARY KEY ("ShoppingListID", "ProductID"),

                            FOREIGN KEY ("ShoppingListID")
                                REFERENCES "Shopping_List" ("ID")
                                ON DELETE RESTRICT
                                ON UPDATE RESTRICT,

                            FOREIGN KEY ("ProductID")
                                REFERENCES "Product" ("ID")
                                ON DELETE RESTRICT
                                ON UPDATE RESTRICT
                        );
                        """).ConfigureAwait(false);

					int shoppingListProductPickedUpColumnExists =
						await newConnection.ExecuteScalarAsync<int>(
							"""
                            SELECT COUNT(*)
                            FROM pragma_table_info('ShoppingListProduct')
                            WHERE name = 'PickedUp';
                            """).ConfigureAwait(false);

					if (shoppingListProductPickedUpColumnExists == 0)
					{
						// Existing shopping-list products were created before per-item
						// pickup tracking existed. Mark them not picked up by default.
						await newConnection.ExecuteAsync(
							"""
                            ALTER TABLE "ShoppingListProduct"
                            ADD COLUMN "PickedUp" INTEGER NOT NULL DEFAULT 0;
                            """).ConfigureAwait(false);
					}

					// Support lookups by ProductID.
					await newConnection.ExecuteAsync(
						"""
                        CREATE INDEX IF NOT EXISTS
                            "IX_ShoppingListProduct_ProductID"
                        ON "ShoppingListProduct" ("ProductID");
                        """).ConfigureAwait(false);
				}
				catch
				{
					// Leave initialization retryable if setup fails.
					await newConnection.CloseAsync().ConfigureAwait(false);
					throw;
				}

				connection = newConnection;
				return connection;
			}
			finally
			{
				initializationGate.Release();
			}
		}

		/// <summary>
		/// Built-in category defaults. Taxability lives here rather than Product.
		/// </summary>
		private static readonly (string Name, int SortOrder, bool IsTaxable)[]
			BuiltInProductCategories =
			[
				("Basic Grocery", 1, false),
				("Snack Food", 2, true),
				("Candy / Confectionery", 3, true),
				("Carbonated Beverage", 4, true),
				("Household / Non-Food", 5, true),
				("Prepared Food", 6, true),
				("Other", 7, false)
			];

		/// <summary>
		/// Apply tax defaults only during the one-time schema migration.
		/// Later category edits will not be overwritten when the app starts.
		/// </summary>
		private static async Task ApplyBuiltInCategoryTaxDefaultsAsync(
			SQLiteAsyncConnection database)
		{
			foreach (var category in BuiltInProductCategories)
			{
				await database.ExecuteAsync(
					"""
                    UPDATE "ProductCategory"
                    SET "IsTaxable" = ?
                    WHERE "Name" = ?;
                    """,
					category.IsTaxable,
					category.Name).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Insert any missing built-in categories without changing existing rows.
		/// </summary>
		private static async Task EnsureProductCategoriesAsync(
			SQLiteAsyncConnection database)
		{
			foreach (var category in BuiltInProductCategories)
			{
				var existing = await database
					.Table<ProductCategory>()
					.Where(item => item.Name == category.Name)
					.FirstOrDefaultAsync()
					.ConfigureAwait(false);

				if (existing is null)
				{
					await database.InsertAsync(new ProductCategory
					{
						Name = category.Name,
						SortOrder = category.SortOrder,
						IsTaxable = category.IsTaxable
					}).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		/// Return categories in the order shown on the product form.
		/// </summary>
		public async Task<List<ProductCategory>> GetProductCategoriesAsync()
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);

			return await database
				.Table<ProductCategory>()
				.OrderBy(category => category.SortOrder)
				.ToListAsync()
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Default home-storage labels. There is intentionally no sort-order column;
		/// the picker sorts these names alphabetically.
		/// </summary>
		private static readonly string[] BuiltInStorageLocations =
		[
			"Pantry",
			"Refrigerator",
			"Freezer",
			"Kitchen Cabinet",
			"Bathroom",
			"Laundry Room",
			"Basement",
			"Garage",
			"Other"
		];

		private static async Task EnsureStorageLocationsAsync(
			SQLiteAsyncConnection database)
		{
			foreach (string name in BuiltInStorageLocations)
			{
				var existing = await database
					.Table<StorageLocation>()
					.Where(location => location.Name == name)
					.FirstOrDefaultAsync()
					.ConfigureAwait(false);

				if (existing is null)
				{
					await database.InsertAsync(new StorageLocation
					{
						Name = name
					}).ConfigureAwait(false);
				}
			}
		}

		/// <summary>
		/// Return storage locations alphabetically for the Product form.
		/// </summary>
		public async Task<List<StorageLocation>> GetStorageLocationsAsync()
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);

			return await database
				.Table<StorageLocation>()
				.OrderBy(location => location.Name)
				.ToListAsync()
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Verify a selected storage location still exists before saving.
		/// Null is allowed because storage location is optional.
		/// </summary>
		private static void EnsureStorageLocationExists(
			SQLiteConnection database,
			int? storageLocationId)
		{
			if (storageLocationId is null)
			{
				return;
			}

			int count = database.ExecuteScalar<int>(
				"""
                SELECT COUNT(*)
                FROM "StorageLocation"
                WHERE "ID" = ?;
                """,
				storageLocationId.Value);

			if (count == 0)
			{
				throw new KeyNotFoundException(
					"The selected storage location no longer exists.");
			}
		}

		/// <summary>
		/// Verify a selected category still exists before saving a product.
		/// </summary>
		private static void EnsureProductCategoryExists(
			SQLiteConnection database,
			int? productCategoryId)
		{
			if (productCategoryId is null)
			{
				return;
			}

			int count = database.ExecuteScalar<int>(
				"""
                SELECT COUNT(*)
                FROM "ProductCategory"
                WHERE "ID" = ?;
                """,
				productCategoryId.Value);

			if (count == 0)
			{
				throw new KeyNotFoundException(
					"The selected product category no longer exists.");
			}
		}

		/// <summary>
		/// Compare names using the same rule everywhere.
		/// Ignore capitalization and surrounding whitespace.
		/// Exclude the current product when checking an edit.
		/// </summary>
		private static bool ContainsDuplicateName(
			IEnumerable<Product> products,
			string name,
			int? excludingProductId)
		{
			string trimmedName = name.Trim();

			return products.Any(product =>
				product.ID != excludingProductId &&
				string.Equals(
					product.Name.Trim(),
					trimmedName,
					StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Load one complete Product row for the edit form.
		/// Returning null lets the caller handle a product deleted after navigation.
		/// </summary>
		public async Task<Product?> GetProductAsync(int productId)
		{
			if (productId <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(productId),
					"Product ID must be greater than zero.");
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);

			return await database
				.Table<Product>()
				.Where(product => product.ID == productId)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Provide the form's duplicate-name check.
		/// Keeping the current product's name or changing only its
		/// capitalization is allowed.
		/// </summary>
		public async Task<bool> ProductNameExistsAsync(
			string name,
			int? excludingProductId = null)
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);

			// Read only the fields required for comparison.
			// Compare in C# so the rule is consistent across platforms.
			var products = await database.QueryAsync<Product>(
				"""
                SELECT "ID", "Name"
                FROM "Product";
                """).ConfigureAwait(false);

			return ContainsDuplicateName(
				products,
				name,
				excludingProductId);
		}

		/// <summary>
		/// Enforce the same name rule inside a write transaction.
		/// This prevents callers from bypassing the form's validation.
		/// </summary>
		private static void EnsureUniqueProductName(
			SQLiteConnection database,
			string name,
			int? excludingProductId = null)
		{
			var products = database.Query<Product>(
				"""
                SELECT "ID", "Name"
                FROM "Product";
                """);

			if (ContainsDuplicateName(products, name, excludingProductId))
			{
				// The existing form already handles this result by showing
				// its duplicate-name message beside the name field.
				throw SQLiteException.New(
					SQLite3.Result.Constraint,
					"A product with this name already exists.");
			}
		}

		/// <summary>
		/// Insert a product after validating its required and unique name.
		/// CreatedOn is UTC; ModifiedOn starts as null.
		/// </summary>
		public async Task<Product> AddProductAsync(
			string name,
			string? description = null,
			int? productCategoryId = null,
			int? storageLocationId = null)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException(
					"A product name is required.", nameof(name));
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);

			var product = new Product
			{
				Name = name.Trim(),
				Description = description,
				ProductCategoryID = productCategoryId,
				StorageLocationID = storageLocationId,
				CreatedOn = DateTime.UtcNow,
				ModifiedOn = null
			};

			// Check and insert together, rather than relying only on the
			// earlier form check or the case-sensitive database index.
			await database.RunInTransactionAsync(transaction =>
			{
				EnsureUniqueProductName(transaction, product.Name);
				EnsureProductCategoryExists(
					transaction,
					product.ProductCategoryID);

				EnsureStorageLocationExists(
					transaction,
					product.StorageLocationID);

				// SQLite assigns the ID and updates product.ID.
				transaction.Insert(product);
			}).ConfigureAwait(false);

			return product;
		}

		/// <summary>
		/// Update the editable fields of an existing product.
		/// Preserve its ID, CreatedOn, ImagePath, and shopping-list links.
		/// </summary>
		public async Task UpdateProductAsync(
			int productId,
			string name,
			string? description = null,
			int? productCategoryId = null,
			int? storageLocationId = null)
		{
			if (productId <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(productId),
					"Product ID must be greater than zero.");
			}

			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException(
					"A product name is required.", nameof(name));
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);
			string trimmedName = name.Trim();

			// Check and update in one transaction.
			await database.RunInTransactionAsync(transaction =>
			{
				// Excluding this ID means a product does not conflict
				// with itself when its name is unchanged.
				EnsureUniqueProductName(
					transaction,
					trimmedName,
					productId);

				EnsureProductCategoryExists(
					transaction,
					productCategoryId);

				EnsureStorageLocationExists(
					transaction,
					storageLocationId);

				// Change only the editable fields and modification time.
				// Parameters keep user-entered text separate from SQL.
				int rowsUpdated = transaction.Execute(
					"""
                    UPDATE "Product"
                    SET "Name" = ?,
                        "Description" = ?,
                        "ProductCategoryID" = ?,
                        "StorageLocationID" = ?,
                        "ModifiedOn" = ?
                    WHERE "ID" = ?;
                    """,
					trimmedName,
					description,
					productCategoryId,
					storageLocationId,
					DateTime.UtcNow.Ticks,
					productId);

				// Do not silently insert a replacement for a missing record.
				if (rowsUpdated == 0)
				{
					throw new KeyNotFoundException(
						"This product no longer exists.");
				}
			}).ConfigureAwait(false);
		}

		/// <summary>
		/// Delete every stored app record, then restore the built-in lookup rows.
		/// This is intentionally destructive and is used only for development test data.
		/// </summary>
		public async Task ResetAllDataForDevelopmentAsync()
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);

			// Delete child rows first so SQLite foreign-key rules are respected.
			await database.RunInTransactionAsync(transaction =>
			{
				transaction.Execute("DELETE FROM \"ShoppingListProduct\";");
				transaction.Execute("DELETE FROM \"Product\";");
				transaction.Execute("DELETE FROM \"Shopping_List\";");
				transaction.Execute("DELETE FROM \"StorageLocation\";");
				transaction.Execute("DELETE FROM \"ProductCategory\";");
				transaction.Execute("DELETE FROM \"TaxRate\";");

				// Reset AUTOINCREMENT counters so each fresh test-data load starts
				// from the same predictable IDs as a brand-new database.
				transaction.Execute(
					"""
                    DELETE FROM "sqlite_sequence"
                    WHERE "name" IN
                    (
                        'Product',
                        'Shopping_List',
                        'StorageLocation',
                        'ProductCategory',
                        'TaxRate'
                    );
                    """);
			}).ConfigureAwait(false);
		}

		/// <summary>
		/// Return the single app-wide sales-tax setting, or null before one has
		/// been configured.
		/// </summary>
		public async Task<TaxRate?> GetTaxRateAsync()
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);

			return await database
				.Table<TaxRate>()
				.OrderBy(rate => rate.ID)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Save the app-wide sales-tax percentage. The first row is reused so
		/// shopping-list calculations always have one unambiguous rate.
		/// </summary>
		public async Task<TaxRate> SetTaxRateAsync(
			string name,
			decimal percentage)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException(
					"A tax-rate name is required.",
					nameof(name));
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);
			var taxRate = await database
				.Table<TaxRate>()
				.OrderBy(rate => rate.ID)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			if (taxRate is null)
			{
				taxRate = new TaxRate
				{
					Name = name.Trim(),
					Percentage = percentage
				};

				await database.InsertAsync(taxRate).ConfigureAwait(false);
			}
			else
			{
				taxRate.Name = name.Trim();
				taxRate.Percentage = percentage;
				await database.UpdateAsync(taxRate).ConfigureAwait(false);
			}

			return taxRate;
		}

		/// <summary>
		/// Save a new shopping list and return its generated ID.
		/// </summary>
		public async Task<ShoppingList> AddShoppingListAsync(
			DateOnly? shoppingDate = null,
			decimal? totalCost = null,
			bool pickedUp = false)
		{
			// The model converts dates and currency to storage values.
			var shoppingList = new ShoppingList
			{
				ShoppingDate = shoppingDate,
				TotalCost = totalCost,
				PickedUp = pickedUp,
				ModifiedOn = null
			};

			var database = await GetConnectionAsync().ConfigureAwait(false);

			shoppingList.CreatedOn = DateTime.UtcNow;

			await database.InsertAsync(shoppingList).ConfigureAwait(false);

			return shoppingList;
		}

		/// <summary>
		/// Return the products assigned to one shopping list, including the
		/// Product name needed by the checklist screen.
		/// </summary>
		public async Task<List<ShoppingListProductListItem>>
			GetShoppingListProductsAsync(int shoppingListID)
		{
			if (shoppingListID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(shoppingListID),
					"Shopping list ID must be greater than zero.");
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);

			return await database.QueryAsync<ShoppingListProductListItem>(
				"""
                SELECT
                    item."ShoppingListID",
                    item."ProductID",
                    product."Name" AS "ProductName",
                    item."Price" AS "PriceInCents",
                    item."PickedUp",
                    COALESCE(category."IsTaxable", 0) AS "IsTaxable"
                FROM "ShoppingListProduct" AS item
                INNER JOIN "Product" AS product
                    ON product."ID" = item."ProductID"
                LEFT JOIN "ProductCategory" AS category
                    ON category."ID" = product."ProductCategoryID"
                WHERE item."ShoppingListID" = ?
                ORDER BY product."Name" COLLATE NOCASE, product."ID";
                """,
				shoppingListID).ConfigureAwait(false);
		}

		/// <summary>
		/// Mark one shopping-list product as picked up and save the price paid.
		/// The price and checkbox state are written together so they cannot drift.
		/// </summary>
		public async Task PickUpShoppingListProductAsync(
			int shoppingListID,
			int productID,
			decimal price)
		{
			if (shoppingListID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(shoppingListID),
					"Shopping list ID must be greater than zero.");
			}

			if (productID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(productID),
					"Product ID must be greater than zero.");
			}

			if (price < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(price),
					"Price cannot be negative.");
			}

			// Reuse the model's currency conversion so fractions smaller than
			// one cent are rejected exactly the same way everywhere.
			var priceValue = new ShoppingListProduct
			{
				Price = price
			};

			var database = await GetConnectionAsync().ConfigureAwait(false);

			int rowsUpdated = await database.ExecuteAsync(
				"""
                UPDATE "ShoppingListProduct"
                SET "Price" = ?,
                    "PickedUp" = 1
                WHERE "ShoppingListID" = ?
                  AND "ProductID" = ?;
                """,
				priceValue.PriceInCents,
				shoppingListID,
				productID).ConfigureAwait(false);

			if (rowsUpdated == 0)
			{
				throw new KeyNotFoundException(
					"This product is no longer on the selected shopping list.");
			}
		}

		/// <summary>
		/// Store the checkbox state for one product on one shopping list.
		/// SQLite stores false as 0 and true as 1.
		/// </summary>
		public async Task SetShoppingListProductPickedUpAsync(
			int shoppingListID,
			int productID,
			bool pickedUp)
		{
			if (shoppingListID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(shoppingListID),
					"Shopping list ID must be greater than zero.");
			}

			if (productID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(productID),
					"Product ID must be greater than zero.");
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);

			int rowsUpdated = await database.ExecuteAsync(
				"""
                UPDATE "ShoppingListProduct"
                SET "PickedUp" = ?
                WHERE "ShoppingListID" = ?
                  AND "ProductID" = ?;
                """,
				pickedUp ? 1 : 0,
				shoppingListID,
				productID).ConfigureAwait(false);

			if (rowsUpdated == 0)
			{
				throw new KeyNotFoundException(
					"This product is no longer on the selected shopping list.");
			}
		}

		/// <summary>
		/// Link an existing product to an existing shopping list.
		/// A product can appear only once in a particular list.
		/// </summary>
		public async Task<ShoppingListProduct> AddShoppingListProductAsync(
			int shoppingListID,
			int productID,
			decimal price)
		{
			if (shoppingListID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(shoppingListID),
					"Shopping list ID must be greater than zero.");
			}

			if (productID <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(productID),
					"Product ID must be greater than zero.");
			}

			// Assigning Price validates decimal places and converts to cents.
			var entry = new ShoppingListProduct
			{
				ShoppingListID = shoppingListID,
				ProductID = productID,
				Price = price,

				// A product starts unchecked until the shopper picks it up.
				PickedUp = false
			};

			var database = await GetConnectionAsync().ConfigureAwait(false);

			entry.CreatedOn = DateTime.UtcNow;

			// SQLite enforces parent references and the composite key.
			await database.InsertAsync(entry).ConfigureAwait(false);

			return entry;
		}
	}
}