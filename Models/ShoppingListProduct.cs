using SQLite;

namespace shopping_app.Models
{
	[Table("ShoppingListProduct")]
	public class ShoppingListProduct
	{
		// Together, these IDs uniquely identify an entry.
		// The composite primary key and foreign keys are defined
		// explicitly in AppDatabase's CREATE TABLE statement.
		[NotNull]
		public int ShoppingListID { get; set; }

		[NotNull]
		public int ProductID { get; set; }

		// SQLite stores whole cents in the column named "Price".
		// Example: $12.34 is stored as 1234.
		[Column("Price"), NotNull]
		public long PriceInCents { get; set; }

		// Use this decimal property in app code.
		// Ignore prevents an additional database column from being created.
		[Ignore]
		public decimal Price
		{
			get => PriceInCents / 100m;

			set
			{
				// Reject fractions smaller than a cent.
				if (decimal.Round(value, 2) != value)
				{
					throw new ArgumentException(
						"Price must have no more than two decimal places.",
						nameof(Price));
				}

				// Reject amounts too large for the storage type.
				PriceInCents = checked((long)(value * 100m));
			}
		}

		// Assigned in UTC by AddShoppingListProductAsync before insertion.
		[NotNull]
		public DateTime CreatedOn { get; set; }
	}
}