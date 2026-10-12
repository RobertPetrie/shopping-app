using System.Globalization;
using shopping_app.Data;
using shopping_app.Logic;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class TaxRateAndShoppingListTotalsTests
	{
		[Fact]
		public void TaxRate_WithOntarioHst_StoresThirteenPercentAsBasisPoints()
		{
			// Store the percentage as an integer so the database never needs a
			// floating-point tax rate. Ontario HST 13.00% becomes 1300 basis points.
			var taxRate = new TaxRate
			{
				Name = "Ontario HST",
				Percentage = 13.00m
			};

			Assert.Equal(1300, taxRate.RateBasisPoints);
			Assert.Equal(13.00m, taxRate.Percentage);
		}

		[Fact]
		public void Calculate_MixedTaxableItems_AppliesTaxOnlyToTaxablePickedUpItems()
		{
			// The subtotal includes every picked-up item, but HST is applied only
			// to picked-up products whose ProductCategory is marked taxable.
			var items = new[]
			{
				new ShoppingListProductListItem
				{
					PriceInCents = 1000,
					PickedUp = true,
					IsTaxable = false
				},
				new ShoppingListProductListItem
				{
					PriceInCents = 2000,
					PickedUp = true,
					IsTaxable = true
				},
				new ShoppingListProductListItem
				{
					PriceInCents = 5000,
					PickedUp = false,
					IsTaxable = true
				}
			};

			var totals = ShoppingListTotalsCalculator.Calculate(
				items,
				taxRateBasisPoints: 1300);

			Assert.Equal(30.00m, totals.Subtotal);
			Assert.Equal(2.60m, totals.Tax);
			Assert.Equal(32.60m, totals.Total);
		}

		[Fact]
		public void Calculate_WithTaxThatNeedsRounding_RoundsToNearestCent()
		{
			// Sales tax can produce fractions of one cent. The calculator should
			// round the final tax amount to the nearest cent instead of truncating it.
			var items = new[]
			{
				new ShoppingListProductListItem
				{
					PriceInCents = 199,
					PickedUp = true,
					IsTaxable = true
				}
			};

			var totals = ShoppingListTotalsCalculator.Calculate(items, 1300);

			Assert.Equal(0.26m, totals.Tax);
			Assert.Equal(2.25m, totals.Total);
		}

		[Fact]
		public void TaxRateInput_WithThirteenPercent_AcceptsValue()
		{
			// The Settings field should accept the normal Ontario HST percentage.
			bool valid = TaxRateInput.TryParse(
				"13",
				CultureInfo.InvariantCulture,
				out decimal percentage,
				out string message);

			Assert.True(valid);
			Assert.Equal(13m, percentage);
			Assert.Equal(string.Empty, message);
		}

		[Theory]
		[InlineData("")]
		[InlineData("-1")]
		[InlineData("100.01")]
		[InlineData("13.123")]
		public void TaxRateInput_WithInvalidValue_RejectsInput(string value)
		{
			// Blank, out-of-range, and over-precise rates should never be written
			// to the TaxRate table from Settings.
			bool valid = TaxRateInput.TryParse(
				value,
				CultureInfo.InvariantCulture,
				out _,
				out string message);

			Assert.False(valid);
			Assert.NotEmpty(message);
		}

		[Fact]
		public async Task ResetAndSeedAsync_CreatesOntarioHstAndPricedPickedUpItems()
		{
			// Development startup should create one Ontario HST row at 13% and
			// give already-picked sample items non-zero prices for totals testing.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				await TestDataSeeder.ResetAndSeedAsync(database);
				var connection = await database.GetConnectionAsync();

				var taxRates = await connection.Table<TaxRate>().ToListAsync();
				Assert.Single(taxRates);
				Assert.Equal("Ontario HST", taxRates[0].Name);
				Assert.Equal(1300, taxRates[0].RateBasisPoints);

				var pickedUpItems = await connection
					.Table<ShoppingListProduct>()
					.Where(item => item.PickedUp)
					.ToListAsync();

				Assert.NotEmpty(pickedUpItems);
				Assert.All(
					pickedUpItems,
					item => Assert.True(item.PriceInCents > 0));
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		[Fact]
		public async Task SetTaxRateAsync_UpdatesExistingRateInsteadOfAddingAnotherRow()
		{
			// Editing Settings should keep one app-wide tax-rate row rather than
			// creating a new row every time the percentage changes.
			string databasePath = CreateTemporaryDatabasePath();
			var database = new AppDatabase(databasePath);

			try
			{
				await database.SetTaxRateAsync("Ontario HST", 13m);
				await database.SetTaxRateAsync("Ontario HST", 12.5m);

				var connection = await database.GetConnectionAsync();
				var rows = await connection.Table<TaxRate>().ToListAsync();

				Assert.Single(rows);
				Assert.Equal(1250, rows[0].RateBasisPoints);
			}
			finally
			{
				await CloseAndDeleteDatabaseAsync(database);
			}
		}

		private static string CreateTemporaryDatabasePath()
		{
			// Every database test gets its own file so test settings and prices
			// cannot leak into another test or the developer's app database.
			return Path.Combine(
				Path.GetTempPath(),
				$"shopping-app-tax-tests-{Guid.NewGuid():N}.db3");
		}

		private static async Task CloseAndDeleteDatabaseAsync(
			AppDatabase database)
		{
			try
			{
				var connection = await database.GetConnectionAsync();
				await connection.CloseAsync();
			}
			finally
			{
				foreach (string path in new[]
				{
					database.DatabasePath,
					database.DatabasePath + "-shm",
					database.DatabasePath + "-wal"
				})
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			}
		}
	}
}
