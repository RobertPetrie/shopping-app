using Microsoft.Maui.Storage;
using SQLite;

namespace shopping_app.Data
{
	/// <summary>
	/// Owns the app's shared SQLite connection. No tables are created here yet.
	/// </summary>
	public sealed class AppDatabase
	{
		// AppDataDirectory selects the correct private, writable folder on each
		// platform. Keep this filename stable so app updates reuse existing data.
		public string DatabasePath { get; } =
			Path.Combine(FileSystem.AppDataDirectory, "shopping.db3");

		// Concurrent callers wait asynchronously until initialization finishes.
		private readonly SemaphoreSlim initializationGate = new(1, 1);
		private SQLiteAsyncConnection? connection;

		/// <summary>
		/// Opens (or creates) the local file on first use, then reuses the connection.
		/// Future data services can receive AppDatabase through their constructors
		/// and await this method before running their queries.
		/// </summary>
		public async Task<SQLiteAsyncConnection> GetConnectionAsync()
		{
			await initializationGate.WaitAsync().ConfigureAwait(false);
			try
			{
				if (connection is not null)
					return connection;

				// Create only creates a missing file; it does not replace existing data.
				// FullMutex enables SQLite's serialized connection access.
				var newConnection = new SQLiteAsyncConnection(DatabasePath,
					SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

				try
				{
					// SQLiteAsyncConnection opens lazily. This command opens the file
					// and enables enforcement of future foreign-key relationships.
					// It does not create any tables or insert any data.
					await newConnection.ExecuteAsync("PRAGMA foreign_keys = ON;").ConfigureAwait(false);
				}
				catch
				{
					// Leave initialization retryable if opening/configuring the file fails.
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
	}
}
