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
		// MAUI supplies the correct private, writable folder for each platform.
		// Keep this filename stable so app updates reuse the existing database.
		public string DatabasePath { get; } =
			Path.Combine(FileSystem.AppDataDirectory, "shopping.db3");

		// Prevent simultaneous callers from initializing the database twice.
		// Waiting is asynchronous so it does not block the UI thread.
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
				// Initialization has already completed.
				if (connection is not null)
				{
					return connection;
				}

				// ReadWrite allows reading and saving data.
				// Create creates a missing file without replacing existing data.
				// FullMutex enables SQLite's serialized connection access.
				var newConnection = new SQLiteAsyncConnection(
					DatabasePath,
					SQLiteOpenFlags.ReadWrite |
					SQLiteOpenFlags.Create |
					SQLiteOpenFlags.FullMutex);

				try
				{
					// The connection opens lazily. This command opens the file
					// and enables enforcement of future foreign-key relationships.
					await newConnection.ExecuteAsync(
						"PRAGMA foreign_keys = ON;").ConfigureAwait(false);

					// Create the Product table and its unique name index.
					// Existing rows are preserved when the app starts again.
					await newConnection.CreateTableAsync<Product>()
						.ConfigureAwait(false);
				}
				catch
				{
					// Release the failed connection. A later call can retry.
					await newConnection.CloseAsync().ConfigureAwait(false);
					throw;
				}

				// Publish the connection only after initialization succeeds.
				connection = newConnection;
				return connection;
			}
			finally
			{
				// Always release the gate, including when an exception occurs.
				initializationGate.Release();
			}
		}

		/// <summary>
		/// Saves a new product and returns it with its generated ID.
		/// CreatedOn is set automatically in UTC; ModifiedOn starts as null.
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

			// Ensure the database and table exist before inserting.
			var database = await GetConnectionAsync().ConfigureAwait(false);

			var product = new Product
			{
				Name = name.Trim(),
				Description = description,
				CreatedOn = DateTime.UtcNow,
				ModifiedOn = null
			};

			// SQLite generates the ID, which sqlite-net assigns to product.ID.
			// Duplicate names raise a SQLiteException instead of replacing data.
			await database.InsertAsync(product).ConfigureAwait(false);

			return product;
		}
	}
}