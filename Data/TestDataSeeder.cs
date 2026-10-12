using shopping_app.Models;

namespace shopping_app.Data
{
	/// <summary>
	/// Replaces all existing app data with a clean, predictable sample set
	/// while the app is being developed.
	/// </summary>
	public static class TestDataSeeder
	{
		public static async Task ResetAndSeedAsync(AppDatabase database)
		{
			ArgumentNullException.ThrowIfNull(database);

			// The development startup flag intentionally makes each launch
			// deterministic by removing any products, lists, or lookup edits
			// left over from the previous manual-testing session.
			await database.ResetAllDataForDevelopmentAsync();

			var connection = await database.GetConnectionAsync();

			DevelopmentDebugArtifacts.WriteLine(
				$"Database location: {database.DatabasePath}");

			// Products reference both lookup tables, so rebuild those first.
			int categoryCount = await SeedProductCategoriesAsync(connection);
			int storageLocationCount = await SeedStorageLocationsAsync(connection);

			// Product rows can now safely resolve the lookup names to real IDs.
			int productCount = await SeedProductsAsync(database, connection);

			int shoppingListCount = await SeedShoppingListsAsync(
				database,
				connection);

			// Add child rows only after Products and Shopping Lists exist.
			int shoppingListProductCount = await SeedShoppingListProductsAsync(
				database,
				connection);

			DevelopmentDebugArtifacts.WriteLine(
				$"Test data reset: created {categoryCount} categories, "
					+ $"{storageLocationCount} storage locations, "
					+ $"{productCount} products, "
					+ $"{shoppingListCount} shopping lists and "
					+ $"{shoppingListProductCount} shopping-list products.");
		}

		private static async Task<int> SeedProductCategoriesAsync(
			SQLite.SQLiteAsyncConnection connection)
		{
			// Populate ProductCategory before Products because each Product stores
			// the ID of one of these rows.
			var categories = new ProductCategory[]
			{
				new()
				{
					Name = "Basic Grocery",
					SortOrder = 1,
					IsTaxable = false
				},
				new()
				{
					Name = "Snack Food",
					SortOrder = 2,
					IsTaxable = true
				},
				new()
				{
					Name = "Candy / Confectionery",
					SortOrder = 3,
					IsTaxable = true
				},
				new()
				{
					Name = "Carbonated Beverage",
					SortOrder = 4,
					IsTaxable = true
				},
				new()
				{
					Name = "Household / Non-Food",
					SortOrder = 5,
					IsTaxable = true
				},
				new()
				{
					Name = "Prepared Food",
					SortOrder = 6,
					IsTaxable = true
				},
				new()
				{
					Name = "Other",
					SortOrder = 7,
					IsTaxable = false
				}
			};

			foreach (var category in categories)
			{
				await connection.InsertAsync(category);
			}

			return categories.Length;
		}

		private static async Task<int> SeedStorageLocationsAsync(
			SQLite.SQLiteAsyncConnection connection)
		{
			// Populate StorageLocation before Products because each Product can
			// reference one of these rows by ID.
			string[] locationNames =
			[
				"Pantry",
				"Refrigerator",
				"Freezer",
				"Kitchen Cabinet",
				"Bathroom",
				"Laundry Room",
				"Basement",
				"Garage",
				"Other"
			];

			foreach (string name in locationNames)
			{
				await connection.InsertAsync(new StorageLocation
				{
					Name = name
				});
			}

			return locationNames.Length;
		}

		private static async Task<int> SeedProductsAsync(
			AppDatabase database,
			SQLite.SQLiteAsyncConnection connection)
		{
			var testProducts =
				new (
					string Name,
					string Description,
					string CategoryName,
					string StorageLocationName)[]
			{
				("Disco Bananas",
					"Excellent dance partners. Slightly slippery.",
					"Basic Grocery",
					"Pantry"),
				("Ninja Noodles",
					"They disappear from your bowl without a trace.",
					"Basic Grocery",
					"Pantry"),
				("Dragon Breath Hot Sauce",
					"For tacos that need a dramatic entrance.",
					"Basic Grocery",
					"Pantry"),
				("Unicorn Breakfast Cereal",
					"A magical start to an otherwise ordinary Tuesday.",
					"Basic Grocery",
					"Pantry"),
				("Emergency Chocolate",
					"Break open in case of Monday.",
					"Candy / Confectionery",
					"Kitchen Cabinet"),
				("Space Pirate Popcorn",
					"The official snack of questionable space missions.",
					"Snack Food",
					"Pantry"),
				("Sneaky Pickles",
					"Somehow always missing when you want a sandwich.",
					"Basic Grocery",
					"Refrigerator"),
				("Superhero Spinach",
					"Cape sold separately.",
					"Basic Grocery",
					"Refrigerator"),
				("Midnight Taco Shells",
					"Because good ideas happen after bedtime.",
					"Basic Grocery",
					"Pantry"),
				("Grumpy Cat Coffee",
					"Do not speak to the mug until it is empty.",
					"Basic Grocery",
					"Kitchen Cabinet"),
				("Moon Cheese",
					"Suspiciously delicious. Probably from Earth.",
					"Basic Grocery",
					"Refrigerator"),
				("Rainbow Carrots",
					"A vegetable drawer with main-character energy.",
					"Basic Grocery",
					"Refrigerator"),
				("Zombie Survival Beans",
					"A pantry essential for unexpected visitors.",
					"Basic Grocery",
					"Pantry"),
				("Fancy Pants Pasta",
					"Dress code: extra parmesan.",
					"Basic Grocery",
					"Pantry"),
				("Happy Camper Marshmallows",
					"Ready for campfires and terrible ghost stories.",
					"Candy / Confectionery",
					"Pantry"),
				("Rocket Fuel Granola",
					"Launch your morning one crunchy bite at a time.",
					"Basic Grocery",
					"Pantry"),
				("Secret Agent Apples",
					"Their mission: infiltrate your lunchbox.",
					"Basic Grocery",
					"Refrigerator"),
				("Pirate Treasure Cookies",
					"Contains chocolate chips, not actual gold.",
					"Snack Food",
					"Pantry"),
				("Royal Couch Potato Chips",
					"Best served during one more episode.",
					"Snack Food",
					"Pantry"),
				("Victory Ice Cream",
					"For celebrating achievements of any size.",
					"Basic Grocery",
					"Freezer"),
				("Cosmic Cola",
					"Fizzy fuel for intergalactic grocery runs.",
					"Carbonated Beverage",
					"Refrigerator"),
				("Emergency Paper Towels",
					"Because spills never schedule appointments.",
					"Household / Non-Food",
					"Basement"),
				("Grab-and-Go Sandwich",
					"Lunch that understands you are in a hurry.",
					"Prepared Food",
					"Refrigerator"),
				("Mystery Aisle Surprise",
					"For items that refuse to fit anywhere else.",
					"Other",
					"Other"),
				("Bathroom Tissue",
					"The throne-room supply nobody wants to run out of.",
					"Household / Non-Food",
					"Bathroom"),
				("Laundry Detergent",
					"Helps socks return from their adventures.",
					"Household / Non-Food",
					"Laundry Room"),
				("Car Wash Soap",
					"For making the driveway jealous.",
					"Household / Non-Food",
					"Garage"),
				("Frozen Pizza",
					"Emergency dinner with a crispy ending.",
					"Prepared Food",
					"Freezer")
			};

			int createdCount = 0;

			foreach (var item in testProducts)
			{
				var category = await connection
					.Table<ProductCategory>()
					.Where(value => value.Name == item.CategoryName)
					.FirstAsync();

				var storageLocation = await connection
					.Table<StorageLocation>()
					.Where(value => value.Name == item.StorageLocationName)
					.FirstAsync();


				await database.AddProductAsync(
					item.Name,
					item.Description,
					category.ID,
					storageLocation.ID);

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

				await database.AddShoppingListAsync(
					item.Date,
					item.TotalCost,
					item.PickedUp);

				createdCount++;
			}

			return createdCount;
		}

		private static async Task<int> SeedShoppingListProductsAsync(
			AppDatabase database,
			SQLite.SQLiteAsyncConnection connection)
		{
			// Give every sample shopping list a useful product checklist.
			// Completed shopping trips are fully checked, while the pending trip
			// intentionally keeps a mix of checked and unchecked rows.
			var shoppingLists = await connection
				.Table<ShoppingList>()
				.OrderByDescending(list => list.ShoppingDateStorage)
				.ToListAsync();

			var products = await connection
				.Table<Product>()
				.OrderBy(product => product.ID)
				.ToListAsync();

			const int productsPerList = 8;
			int createdCount = 0;

			for (int listIndex = 0; listIndex < shoppingLists.Count; listIndex++)
			{
				var shoppingList = shoppingLists[listIndex];

				// Rotate through the product catalog so the historical sample lists
				// do not all contain the exact same eight products.
				int startingProductIndex = (listIndex * 3) % products.Count;

				for (int itemIndex = 0; itemIndex < productsPerList; itemIndex++)
				{
					var product = products[
						(startingProductIndex + itemIndex) % products.Count];

					await database.AddShoppingListProductAsync(
						shoppingList.ID,
						product.ID,
						price: 0m);

					bool pickedUp = shoppingList.PickedUp
						|| (!shoppingList.PickedUp && itemIndex < 2);

					if (pickedUp)
					{
						await database.SetShoppingListProductPickedUpAsync(
							shoppingList.ID,
							product.ID,
							pickedUp: true);
					}

					createdCount++;
				}
			}

			return createdCount;
		}
	}
}
