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
		// Keep the filename stable so app updates reuse existing data.
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
				// Return the existing connection once initialization succeeds.
				if (connection is not null)
				{
					return connection;
				}

				// Create makes a missing file without replacing existing data.
				// FullMutex enables SQLite's serialized connection access.
				var newConnection = new SQLiteAsyncConnection(
					DatabasePath,
					SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.Create |
					SQLiteOpenFlags.FullMutex);

				try
				{
					// Open the connection and enable enforcement of any
					// foreign-key relationships we define in the future.
					await newConnection.ExecuteAsync(
						"PRAGMA foreign_keys = ON;").ConfigureAwait(false);

					// Create missing tables without deleting existing rows.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);

					await newConnection.CreateTableAsync<ShoppingList>()
						.ConfigureAwait(false);
				}
				catch
				{
					// Leave initialization retryable if opening the database
					// or creating its tables fails.
					await newConnection.CloseAsync().ConfigureAwait(false);
					throw;
				}

				// Publish the connection only after initialization succeeds.
				connection = newConnection;
				return connection;
			}
			finally
			{
				// Release the gate even when initialization throws an error.
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
			// Reject null, empty, and whitespace-only names.
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

			// SQLite assigns ID, and sqlite-net copies it into product.ID.
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
			// The model converts the date and currency to their storage forms.
			// Setting TotalCost also validates its decimal places.
			var shoppingList = new ShoppingList
			{
				ShoppingDate = shoppingDate,
				TotalCost = totalCost,
				PickedUp = pickedUp,
				ModifiedOn = null
			};

			// Ensure both tables exist before inserting the new row.
			var database = await GetConnectionAsync().ConfigureAwait(false);

			// Set the creation timestamp immediately before saving.
			shoppingList.CreatedOn = DateTime.UtcNow;

			// SQLite assigns ID and sqlite-net updates shoppingList.ID.
			await database.InsertAsync(shoppingList).ConfigureAwait(false);

			return shoppingList;
		}
	}
}