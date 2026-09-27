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

		// Optional product description.
		public string? Description { get; set; }

		// Relative path to one picture in the app's private storage.
		// Example: "products/green-apples.jpg"
		// Null means the product has no picture.
		public string? ImagePath { get; set; }

		// Assigned by AddProductAsync immediately before insertion.
		[NotNull]
		public DateTime CreatedOn { get; set; }

		// Remains null until the product is modified.
		public DateTime? ModifiedOn { get; set; }
	}
}