using System.Globalization;
using SQLite;

namespace shopping_app.Models
{
	[Table("Shopping_List")]
	public class ShoppingList
	{
		// SQLite generates a unique, non-null ID during insertion.
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		// SQLite stores this as date-only text, such as "2026-09-26".
		// Column keeps the database column name as "ShoppingDate".
		[Column("ShoppingDate")]
		public string? ShoppingDateStorage { get; set; }

		// Use this property in app code.
		// Ignore means it does not create an additional database column.
		[Ignore]
		public DateOnly? ShoppingDate
		{
			get => ShoppingDateStorage is null
				? null
				: DateOnly.ParseExact(
					ShoppingDateStorage,
					"yyyy-MM-dd",
					CultureInfo.InvariantCulture);

			set => ShoppingDateStorage = value?.ToString(
				"yyyy-MM-dd",
				CultureInfo.InvariantCulture);
		}

		// Assigned automatically by AddShoppingListAsync before insertion.
		[NotNull]
		public DateTime CreatedOn { get; set; }

		// Remains null until future update logic sets it.
		public DateTime? ModifiedOn { get; set; }

		// Store money as whole cents to avoid floating-point rounding errors.
		// Example: $12.34 is stored as 1234 in the "TotalCost" column.
		[Column("TotalCost")]
		public long? TotalCostInCents { get; set; }

		// App code uses decimal amounts, such as 12.34m.
		// Only TotalCostInCents is persisted to SQLite.
		[Ignore]
		public decimal? TotalCost
		{
			get => TotalCostInCents.HasValue
				? TotalCostInCents.Value / 100m
				: null;

			set
			{
				if (!value.HasValue)
				{
					TotalCostInCents = null;
					return;
				}

				// Reject fractions smaller than a cent instead of silently
				// rounding the amount supplied by the caller.
				if (decimal.Round(value.Value, 2) != value.Value)
				{
					throw new ArgumentException(
						"Total cost must have no more than two decimal places.",
						nameof(TotalCost));
				}

				// checked rejects amounts too large for the storage type.
				TotalCostInCents = checked((long)(value.Value * 100m));
			}
		}

		// SQLite stores false as 0 and true as 1.
		// This is non-nullable and defaults to false for new objects.
		[NotNull]
		public bool PickedUp { get; set; } = false;
	}
}