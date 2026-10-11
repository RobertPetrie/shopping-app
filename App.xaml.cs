using shopping_app.Data;

namespace shopping_app
{
	public partial class App : Application
	{
		// Keep this true while developing to start every launch with the same
		// clean sample data. WARNING: when true, all existing app data is erased.
		// Set it to false whenever you want the database to keep real changes.
		private const bool ResetTestDataOnStartup = true;

		private readonly AppDatabase database;

		// Pages wait for this task before loading data so the sample records
		// are guaranteed to be in SQLite before the first query runs.
		private readonly TaskCompletionSource testDataReady =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task TestDataReady => testDataReady.Task;

		public App(AppDatabase database)
		{
			// Start each Debug launch with a clean application log so the file
			// contains only messages from the current manual-testing session.
			DevelopmentDebugArtifacts.ResetLog();

			InitializeComponent();
			this.database = database;

			DevelopmentDebugArtifacts.WriteLine(
				$"Debug folder: {DevelopmentDebugArtifacts.FolderPath}");

			DevelopmentDebugArtifacts.WriteLine(
				$"Database location: {database.DatabasePath}");
		}

		protected override Window CreateWindow(
			IActivationState? activationState)
		{
			var window = new Window(new AppShell());

			if (ResetTestDataOnStartup)
			{
				window.Created += OnWindowCreated;
			}
			else
			{
				// Nothing needs to be seeded, so pages may load immediately.
				testDataReady.TrySetResult();
			}

			return window;
		}

		private async void OnWindowCreated(object? sender, EventArgs e)
		{
			if (sender is not Window window)
			{
				return;
			}

			// Run the sample-data setup only once for this window.
			window.Created -= OnWindowCreated;

			try
			{
				await TestDataSeeder.ResetAndSeedAsync(database);
			}
			catch (Exception ex)
			{
				DevelopmentDebugArtifacts.WriteLine(
					$"Could not reset and create test data: {ex}");

				if (window.Page is Page page)
				{
					await page.DisplayAlertAsync(
						"Test data",
						"Could not reset and create all test data. "
							+ "See the debug output for details.",
						"OK");
				}
			}
			finally
			{
				// Always release waiting pages, even if some sample data failed.
				testDataReady.TrySetResult();
			}
		}
	}
}
