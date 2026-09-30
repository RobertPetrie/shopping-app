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
		public string DatabasePath { get; } =
			Path.Combine(FileSystem.AppDataDirectory, "shopping.db3");

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

					// Create parent tables before the linking table.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);

					await newConnection.CreateTableAsync<ShoppingList>()
						.ConfigureAwait(false);

					// Preserve existing tables and records.
					await newConnection.ExecuteAsync(
						"""
                        CREATE TABLE IF NOT EXISTS "ShoppingListProduct"
                        (
                            "ShoppingListID" INTEGER NOT NULL,
                            "ProductID" INTEGER NOT NULL,
                            "Price" INTEGER NOT NULL,
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
			string? description = null)
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
				CreatedOn = DateTime.UtcNow,
				ModifiedOn = null
			};

			// Check and insert together, rather than relying only on the
			// earlier form check or the case-sensitive database index.
			await database.RunInTransactionAsync(transaction =>
			{
				EnsureUniqueProductName(transaction, product.Name);

				// SQLite assigns the ID and updates product.ID.
				transaction.Insert(product);
			}).ConfigureAwait(false);

			return product;
		}

		/// <summary>
		/// Update the name and description of an existing product.
		/// Preserve its ID, CreatedOn, ImagePath, and shopping-list links.
		/// </summary>
		public async Task UpdateProductAsync(
			int productId,
			string name,
			string? description = null)
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

				// Change only the editable fields and modification time.
				// Parameters keep user-entered text separate from SQL.
				int rowsUpdated = transaction.Execute(
					"""
                    UPDATE "Product"
                    SET "Name" = ?,
                        "Description" = ?,
                        "ModifiedOn" = ?
                    WHERE "ID" = ?;
                    """,
					trimmedName,
					description,
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
				Price = price
			};

			var database = await GetConnectionAsync().ConfigureAwait(false);

			entry.CreatedOn = DateTime.UtcNow;

			// SQLite enforces parent references and the composite key.
			await database.InsertAsync(entry).ConfigureAwait(false);

			return entry;
		}
	}
}