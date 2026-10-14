using shopping_app.Data;
using shopping_app.Models;

namespace ShoppingApp.Tests
{
    public class ShoppingListCreationTests
    {
        [Fact]
        public async Task FirstProduct_CreatesTheListAndOneUncheckedItem()
        {
            // A list only exists when its first product is selected.
            string path = NewDatabasePath();
            var database = new AppDatabase(path);

            try
            {
                var milk = await database.AddProductAsync("Draft Milk");
                var plannedDate = new DateOnly(2026, 10, 14);

                var list = await database.CreateShoppingListWithFirstProductAsync(
                    plannedDate, milk.ID);

                var connection = await database.GetConnectionAsync();
                Assert.Equal(1, await connection.Table<ShoppingList>().CountAsync());
                Assert.Equal(plannedDate, list.ShoppingDate);
                Assert.Equal(0m, list.TotalCost);
                Assert.False(list.PickedUp);

                var items = await database.GetShoppingListProductsAsync(list.ID);
                var first = Assert.Single(items);
                Assert.Equal(milk.ID, first.ProductID);
                Assert.False(first.PickedUp);
                Assert.Equal(0m, first.Price);
            }
            finally
            {
                await CloseAndDeleteDatabaseAsync(database);
            }
        }

        [Fact]
        public async Task FirstProduct_WhenProductIsMissing_LeavesNoEmptyList()
        {
            // A failed first selection rolls back the shopping-list header too.
            string path = NewDatabasePath();
            var database = new AppDatabase(path);

            try
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(
                    () => database.CreateShoppingListWithFirstProductAsync(
                        new DateOnly(2026, 10, 14), productID: 987654321));

                var connection = await database.GetConnectionAsync();
                Assert.Equal(0, await connection.Table<ShoppingList>().CountAsync());
                Assert.Equal(0, await connection.Table<ShoppingListProduct>().CountAsync());
            }
            finally
            {
                await CloseAndDeleteDatabaseAsync(database);
            }
        }

        [Fact]
        public async Task AddLaterProduct_UsesSameSavedShoppingList()
        {
            // A user can build one saved list over several days.
            string path = NewDatabasePath();
            var database = new AppDatabase(path);

            try
            {
                var milk = await database.AddProductAsync("Weekly Milk");
                var eggs = await database.AddProductAsync("Weekly Eggs");
                var list = await database.CreateShoppingListWithFirstProductAsync(
                    new DateOnly(2026, 10, 16), milk.ID);

                await database.AddShoppingListProductAsync(list.ID, eggs.ID, 0m);

                var items = await database.GetShoppingListProductsAsync(list.ID);
                Assert.Equal(2, items.Count);

                var connection = await database.GetConnectionAsync();
                Assert.Equal(1, await connection.Table<ShoppingList>().CountAsync());
                Assert.All(items, item => Assert.Equal(list.ID, item.ShoppingListID));
            }
            finally
            {
                await CloseAndDeleteDatabaseAsync(database);
            }
        }

        [Fact]
        public async Task EditingDate_DoesNotChangeProductsPricesOrPickupState()
        {
            // Rescheduling is independent of what the shopper already picked up.
            string path = NewDatabasePath();
            var database = new AppDatabase(path);

            try
            {
                var bread = await database.AddProductAsync("Reschedule Bread");
                var list = await database.CreateShoppingListWithFirstProductAsync(
                    new DateOnly(2026, 10, 14), bread.ID);
                await database.PickUpShoppingListProductAsync(list.ID, bread.ID, 3.25m);
                await database.SetShoppingListTotalCostAsync(list.ID, 3.25m);

                await database.SetShoppingListDateAsync(
                    list.ID, new DateOnly(2026, 10, 17));

                var connection = await database.GetConnectionAsync();
                var stored = await connection.Table<ShoppingList>()
                    .Where(item => item.ID == list.ID).FirstAsync();
                Assert.Equal(new DateOnly(2026, 10, 17), stored.ShoppingDate);
                Assert.Equal("10/17/2026", stored.DisplayShortDate);
                Assert.Equal(3.25m, stored.TotalCost);

                var item = Assert.Single(await database.GetShoppingListProductsAsync(list.ID));
                Assert.True(item.PickedUp);
                Assert.Equal(3.25m, item.Price);
                Assert.Equal(bread.ID, item.ProductID);
            }
            finally
            {
                await CloseAndDeleteDatabaseAsync(database);
            }
        }

        private static string NewDatabasePath() => Path.Combine(
            Path.GetTempPath(), $"shopping-list-creation-{Guid.NewGuid():N}.db3");

        private static async Task CloseAndDeleteDatabaseAsync(AppDatabase database)
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
