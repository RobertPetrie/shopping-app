using shopping_app.Models;

namespace shopping_app.Data
{
	/// <summary>
	/// Adds friendly sample records while the app is being developed.
	/// Existing sample records are skipped so restarting the app is safe.
	/// </summary>
	public static class TestDataSeeder
	{
		public static async Task SeedAsync(AppDatabase database)
		{
			ArgumentNullException.ThrowIfNull(database);

			var connection = await database.GetConnectionAsync();

			System.Diagnostics.Debug.WriteLine(
				$"Database location: {database.DatabasePath}");

			int productCount = await SeedProductsAsync(database, connection);
			int shoppingListCount = await SeedShoppingListsAsync(
				database,
				connection);

			System.Diagnostics.Debug.WriteLine(
				$"Test data: created {productCount} products and "
					+ $"{shoppingListCount} shopping lists.");
		}

		private static async Task<int> SeedProductsAsync(
			AppDatabase database,
			SQLite.SQLiteAsyncConnection connection)
		{
			var testProducts =
				new (
					string Name,
					string Description,
					bool IsTaxable,
					string CategoryName)[]
			{
				("Disco Bananas",
					"Excellent dance partners. Slightly slippery.",
					false,
					"Basic Grocery"),
				("Ninja Noodles",
					"They disappear from your bowl without a trace.",
					false,
					"Basic Grocery"),
				("Dragon Breath Hot Sauce",
					"For tacos that need a dramatic entrance.",
					false,
					"Basic Grocery"),
				("Unicorn Breakfast Cereal",
					"A magical start to an otherwise ordinary Tuesday.",
					false,
					"Basic Grocery"),
				("Emergency Chocolate",
					"Break open in case of Monday.",
					true,
					"Candy / Confectionery"),
				("Space Pirate Popcorn",
					"The official snack of questionable space missions.",
					true,
					"Snack Food"),
				("Sneaky Pickles",
					"Somehow always missing when you want a sandwich.",
					false,
					"Basic Grocery"),
				("Superhero Spinach",
					"Cape sold separately.",
					false,
					"Basic Grocery"),
				("Midnight Taco Shells",
					"Because good ideas happen after bedtime.",
					false,
					"Basic Grocery"),
				("Grumpy Cat Coffee",
					"Do not speak to the mug until it is empty.",
					false,
					"Basic Grocery"),
				("Moon Cheese",
					"Suspiciously delicious. Probably from Earth.",
					false,
					"Basic Grocery"),
				("Rainbow Carrots",
					"A vegetable drawer with main-character energy.",
					false,
					"Basic Grocery"),
				("Zombie Survival Beans",
					"A pantry essential for unexpected visitors.",
					false,
					"Basic Grocery"),
				("Fancy Pants Pasta",
					"Dress code: extra parmesan.",
					false,
					"Basic Grocery"),
				("Happy Camper Marshmallows",
					"Ready for campfires and terrible ghost stories.",
					true,
					"Candy / Confectionery"),
				("Rocket Fuel Granola",
					"Launch your morning one crunchy bite at a time.",
					false,
					"Basic Grocery"),
				("Secret Agent Apples",
					"Their mission: infiltrate your lunchbox.",
					false,
					"Basic Grocery"),
				("Pirate Treasure Cookies",
					"Contains chocolate chips, not actual gold.",
					true,
					"Snack Food"),
				("Royal Couch Potato Chips",
					"Best served during one more episode.",
					true,
					"Snack Food"),
				("Victory Ice Cream",
					"For celebrating achievements of any size.",
					false,
					"Basic Grocery")
			};

			int createdCount = 0;

			foreach (var item in testProducts)
			{
				var category = await connection
					.Table<ProductCategory>()
					.Where(value => value.Name == item.CategoryName)
					.FirstAsync();

				// Skip an existing product with the same name.
				var existingProduct = await connection
					.Table<Product>()
					.Where(product => product.Name == item.Name)
					.FirstOrDefaultAsync();

				if (existingProduct is not null)
				{
					// Backfill tax/category only on untouched sample rows.
					// User-edited samples keep the choices the user made.
					if (existingProduct.ModifiedOn is null
						&& (existingProduct.IsTaxable != item.IsTaxable
							|| existingProduct.ProductCategoryID != category.ID))
					{
						await connection.ExecuteAsync(
							"""
                            UPDATE "Product"
                            SET "IsTaxable" = ?,
                                "ProductCategoryID" = ?
                            WHERE "ID" = ?;
                            """,
							item.IsTaxable,
							category.ID,
							existingProduct.ID);
					}

					continue;
				}

				await database.AddProductAsync(
					item.Name,
					item.Description,
					item.IsTaxable,
					category.ID);

				createdCount++;
			}

			return createdCount;
		}

		private static async Task<int> SeedShoppingListsAsync(
			AppDatabase database,
			SQLite.SQLiteAsyncConnection connection)
		{
			// Mix completed grocery trips with one upcoming list so both
			// "Picked up" and "Pending" states are easy to see on the screen.
			var testShoppingLists =
				new (DateOnly Date, decimal? TotalCost, bool PickedUp)[]
			{
				(new DateOnly(2026, 10, 11), null, false),
				(new DateOnly(2026, 10, 8), 83.47m, true),
				(new DateOnly(2026, 10, 1), 126.18m, true),
				(new DateOnly(2026, 9, 24), 71.62m, true),
				(new DateOnly(2026, 9, 17), 104.39m, true),
				(new DateOnly(2026, 9, 10), 58.94m, true),
				(new DateOnly(2026, 9, 3), 147.21m, true),
				(new DateOnly(2026, 8, 27), 92.16m, true),
				(new DateOnly(2026, 8, 20), 64.75m, true),
				(new DateOnly(2026, 8, 13), 118.53m, true),
				(new DateOnly(2026, 8, 6), 79.88m, true),
				(new DateOnly(2026, 7, 30), 135.04m, true)
			};

			int createdCount = 0;

			foreach (var item in testShoppingLists)
			{
				string dateStorage = item.Date.ToString("yyyy-MM-dd");

				// The sample data uses one list per date. If that date already
				// exists, leave the user's existing record alone.
				var existingList = await connection
					.Table<ShoppingList>()
					.Where(list => list.ShoppingDateStorage == dateStorage)
					.FirstOrDefaultAsync();

				if (existingList is not null)
				{
					continue;
				}

				await database.AddShoppingListAsync(
					item.Date,
					item.TotalCost,
					item.PickedUp);

				createdCount++;
			}

			return createdCount;
		}
	}
}
