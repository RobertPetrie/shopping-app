using SQLite;

namespace shopping_app.Models
{
	[Table("Product")]
	public class Product
	{
		// SQLite generates a unique ID when the row is inserted.
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		// SQLite rejects null values and duplicate names.
		[NotNull, Unique]
		public string Name { get; set; } = string.Empty;

		// The ? allows the description to be null.
		public string? Description { get; set; }

		// Assigned by AddProductAsync immediately before insertion.
		[NotNull]
		public DateTime CreatedOn { get; set; }

		// Remains null until the product is modified.
		public DateTime? ModifiedOn { get; set; }
	}
}