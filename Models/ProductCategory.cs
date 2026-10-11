using SQLite;

namespace shopping_app.Models
{
	[Table("ProductCategory")]
	public class ProductCategory
	{
		// SQLite generates a stable ID that Product records can reference.
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		// Category names are unique so the built-in lookup data stays tidy.
		[NotNull, Unique]
		public string Name { get; set; } = string.Empty;

		// Keep the picker in a predictable, human-friendly order.
		[NotNull]
		public int SortOrder { get; set; }

		// Every product in this category shares this tax setting.
		[NotNull]
		public bool IsTaxable { get; set; }

		// Picker controls display the category name automatically.
		public override string ToString() => Name;
	}
}
