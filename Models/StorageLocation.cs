using SQLite;

namespace shopping_app.Models
{
	[Table("StorageLocation")]
	public class StorageLocation
	{
		// SQLite generates a stable ID that Product records can reference.
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		// Names are unique so the picker never shows duplicate locations.
		[NotNull, Unique]
		public string Name { get; set; } = string.Empty;

		// Picker controls display the location name automatically.
		public override string ToString() => Name;
	}
}
