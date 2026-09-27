using shopping_app.Data;
using shopping_app.Models;

namespace shopping_app
{
	public partial class App : Application
	{
		// Set to false after the test products have been created.
		private static readonly bool CreateTestProducts = false;

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

			if (CreateTestProducts)
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

			// Run only once for this window.
			window.Created -= OnWindowCreated;

			try
			{
				await CreateTestProductsAsync();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(
					$"Could not create test products: {ex}");

				if (window.Page is Page page)
				{
					await page.DisplayAlertAsync(
						"Test data",
						"Could not create all test products. "
							+ "See the debug output for details.",
						"OK");
				}
			}
		}

		private async Task CreateTestProductsAsync()
		{
			var testProducts = new (string Name, string Description)[]
			{
				("Disco Bananas",
					"Excellent dance partners. Slightly slippery."),

				("Ninja Noodles",
					"They disappear from your bowl without a trace."),

				("Dragon Breath Hot Sauce",
					"For tacos that need a dramatic entrance."),

				("Unicorn Breakfast Cereal",
					"A magical start to an otherwise ordinary Tuesday."),

				("Emergency Chocolate",
					"Break open in case of Monday."),

				("Space Pirate Popcorn",
					"The official snack of questionable space missions."),

				("Sneaky Pickles",
					"Somehow always missing when you want a sandwich."),

				("Superhero Spinach",
					"Cape sold separately."),

				("Midnight Taco Shells",
					"Because good ideas happen after bedtime."),

				("Grumpy Cat Coffee",
					"Do not speak to the mug until it is empty."),

				("Moon Cheese",
					"Suspiciously delicious. Probably from Earth."),

				("Rainbow Carrots",
					"A vegetable drawer with main-character energy."),

				("Zombie Survival Beans",
					"A pantry essential for unexpected visitors."),

				("Fancy Pants Pasta",
					"Dress code: extra parmesan."),

				("Happy Camper Marshmallows",
					"Ready for campfires and terrible ghost stories."),

				("Rocket Fuel Granola",
					"Launch your morning one crunchy bite at a time."),

				("Secret Agent Apples",
					"Their mission: infiltrate your lunchbox."),

				("Pirate Treasure Cookies",
					"Contains chocolate chips, not actual gold."),

				("Royal Couch Potato Chips",
					"Best served during one more episode."),

				("Victory Ice Cream",
					"For celebrating achievements of any size.")
			};

			var connection = await database.GetConnectionAsync();
			int createdCount = 0;

			foreach (var item in testProducts)
			{
				// Skip existing names so repeated startups are safe.
				var existingProduct = await connection
					.Table<Product>()
					.Where(product => product.Name == item.Name)
					.FirstOrDefaultAsync();

				if (existingProduct is not null)
				{
					continue;
				}

				// Uses the existing method to assign ID and CreatedOn.
				// ImagePath and ModifiedOn remain null.
				await database.AddProductAsync(
					item.Name,
					item.Description);

				createdCount++;
			}

			System.Diagnostics.Debug.WriteLine(
				$"Test data: created {createdCount} products.");
		}
	}
}