using Microsoft.Maui.Storage;
using shopping_app.Models;
using SQLite;

namespace shopping_app.Data
{
	/// <summary>
	/// Manages the shared SQLite connection, initializes tables,
	/// and provides methods for accessing stored data.
	/// </summary>
	public sealed class AppDatabase
	{
		// MAUI supplies the private, writable folder for each platform.
		// Keep this filename stable so app updates reuse existing data.
		public string DatabasePath { get; } =
			Path.Combine(FileSystem.AppDataDirectory, "shopping.db3");

		// Prevent concurrent callers from initializing the database twice.
		private readonly SemaphoreSlim initializationGate = new(1, 1);

		private SQLiteAsyncConnection? connection;

		/// <summary>
		/// Opens the database and creates missing tables on first use.
		/// Later calls reuse the initialized connection.
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

				// Create makes a missing file without replacing existing data.
				// FullMutex enables SQLite's serialized connection access.
				// DateTime values are stored as ticks, matching the INTEGER
				// CreatedOn column in the explicit table definition below.
				var newConnection = new SQLiteAsyncConnection(
					DatabasePath,
					SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.Create |
					SQLiteOpenFlags.FullMutex,
					storeDateTimeAsTicks: true);

				try
				{
					// Foreign-key constraints must be enabled per connection.
					await newConnection.ExecuteAsync(
						"PRAGMA foreign_keys = ON;").ConfigureAwait(false);

					// Create parent tables before their linking table.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);

					await newConnection.CreateTableAsync<ShoppingList>()
						.ConfigureAwait(false);

					// Explicit SQL defines the composite primary key and
					// actual foreign-key constraints.
					//
					// RESTRICT prevents deleting or changing a parent's ID
					// while linked entries still reference it.
					//
					// IF NOT EXISTS preserves an existing table and its rows.
					// Future schema changes will need migration logic.
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

					// The composite key already indexes ShoppingListID first.
					// This separate index speeds up lookups by ProductID,
					// including SQLite's foreign-key checks.
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

				// Publish the connection only after initialization succeeds.
				connection = newConnection;
				return connection;
			}
			finally
			{
				// Release the gate even if initialization throws an error.
				initializationGate.Release();
			}
		}

		/// <summary>
		/// Saves a new product and returns it with its generated ID.
		/// CreatedOn is assigned in UTC; ModifiedOn starts as null.
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

			// SQLite assigns ID and sqlite-net updates product.ID.
			// Duplicate names raise a SQLiteException.
			await database.InsertAsync(product).ConfigureAwait(false);

			return product;
		}

		/// <summary>
		/// Saves a new shopping list and returns it with its generated ID.
		/// Date and cost are optional; PickedUp defaults to false.
		/// </summary>
		public async Task<ShoppingList> AddShoppingListAsync(
			DateOnly? shoppingDate = null,
			decimal? totalCost = null,
			bool pickedUp = false)
		{
			// The model converts the date and currency to storage values.
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
		/// Adds an existing product to an existing shopping list.
		/// Each product can appear only once in a given list.
		/// </summary>
		public async Task<ShoppingListProduct> AddShoppingListProductAsync(
			int shoppingListID,
			int productID,
			decimal price)
		{
			// Auto-generated parent IDs start at 1.
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

			// Automatically populate the timestamp immediately before saving.
			entry.CreatedOn = DateTime.UtcNow;

			// SQLite enforces both parent references and the composite key.
			// Missing parents or a duplicate pair raise a SQLiteException.
			await database.InsertAsync(entry).ConfigureAwait(false);

			return entry;
		}
	}
}