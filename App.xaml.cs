using shopping_app.Data;

namespace shopping_app
{
	public partial class App : Application
	{
		// Keep this true while developing so the app has useful sample data.
		// Set it to false before shipping a real release.
		private static readonly bool CreateTestData = true;

		private readonly AppDatabase database;

		public App(AppDatabase database)
		{
			InitializeComponent();
			this.database = database;
		}

		protected override Window CreateWindow(
			IActivationState? activationState)
		{
			var window = new Window(new AppShell());

			if (CreateTestData)
			{
				window.Created += OnWindowCreated;
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
				await TestDataSeeder.SeedAsync(database);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Could not create test data: {ex}");

				if (window.Page is Page page)
				{
					await page.DisplayAlertAsync(
						"Test data",
						"Could not create all test data. "
							+ "See the debug output for details.",
						"OK");
				}
			}
		}
	}
}
