using Microsoft.Maui.Storage;
using shopping_app.Models;
using SQLite;

namespace shopping_app.Data
{
	/// <summary>
	/// Initializes the local database and provides methods for stored data.
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

				// Create preserves existing data.
				// Store DateTime values as .NET ticks.
				var newConnection = new SQLiteAsyncConnection(
					DatabasePath,
					SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.Create |
					SQLiteOpenFlags.FullMutex,
					storeDateTimeAsTicks: true);

				try
				{
					// Foreign-key enforcement must be enabled per connection.
					await newConnection.ExecuteAsync(
						"PRAGMA foreign_keys = ON;").ConfigureAwait(false);

					// Create parent tables before their linking table.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);

					await newConnection.CreateTableAsync<ShoppingList>()
						.ConfigureAwait(false);

					// Explicit SQL defines the composite key and foreign keys.
					// Existing tables and records are preserved.
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

					// The composite key indexes ShoppingListID first.
					// This index supports lookups by ProductID.
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
		/// Check whether another product already uses the supplied name.
		/// Excluding the current ID allows keeping its name during editing.
		/// Comparison matches the existing case-sensitive unique constraint.
		/// </summary>
		public async Task<bool> ProductNameExistsAsync(
			string name,
			int? excludingProductId = null)
		{
			var database = await GetConnectionAsync().ConfigureAwait(false);
			string trimmedName = name.Trim();

			// Generated IDs start at 1, so 0 excludes nothing in Add mode.
			int excludedId = excludingProductId ?? 0;

			int count = await database.Table<Product>()
				.Where(product =>
					product.Name == trimmedName &&
					product.ID != excludedId)
				.CountAsync()
				.ConfigureAwait(false);

			return count > 0;
		}

		/// <summary>
		/// Save a new product and return it with its generated ID.
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

			// The unique constraint also protects against duplicate names
			// introduced between the form's duplicate check and insertion.
			await database.InsertAsync(product).ConfigureAwait(false);

			return product;
		}

		/// <summary>
		/// Update an existing product's name and description.
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

			// Validate here as well so callers cannot bypass the UI rule.
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException(
					"A product name is required.", nameof(name));
			}

			var database = await GetConnectionAsync().ConfigureAwait(false);

			// Update only the editable columns and modification timestamp.
			// Parameters keep product text separate from the SQL statement.
			// Ticks match this database's DateTime storage format.
			int rowsUpdated = await database.ExecuteAsync(
				"""
                UPDATE "Product"
                SET "Name" = ?,
                    "Description" = ?,
                    "ModifiedOn" = ?
                WHERE "ID" = ?;
                """,
				name.Trim(),
				description,
				DateTime.UtcNow.Ticks,
				productId).ConfigureAwait(false);

			// Never silently insert a replacement for a missing record.
			if (rowsUpdated == 0)
			{
				throw new KeyNotFoundException(
					"This product no longer exists.");
			}
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

			// SQLite enforces both parent references and the composite key.
			await database.InsertAsync(entry).ConfigureAwait(false);

			return entry;
		}
	}
}