namespace shopping_app.Models
{
	/// <summary>
	/// One row on the Shopping List Products screen. The database query joins
	/// ShoppingListProduct to Product so the screen can display the Product name.
	/// </summary>
	public sealed class ShoppingListProductListItem
	{
		public int ShoppingListID { get; set; }

		public int ProductID { get; set; }

		public string ProductName { get; set; } = string.Empty;

		// SQLite stores the item's shopping-trip price as whole cents.
		public long PriceInCents { get; set; }

		public decimal Price => PriceInCents / 100m;

		// Comes from ProductCategory. Uncategorized products are treated as
		// non-taxable by the database query.
		public bool IsTaxable { get; set; }

		// SQLite maps 0 to false and 1 to true.
		public bool PickedUp { get; set; }
	}
}
