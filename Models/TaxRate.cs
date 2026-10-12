using SQLite;

namespace shopping_app.Models
{
	[Table("TaxRate")]
	public class TaxRate
	{
		// SQLite generates the row ID. The app currently uses the first row as
		// the active sales-tax setting.
		[PrimaryKey, AutoIncrement]
		public int ID { get; set; }

		// A friendly label such as "Ontario HST".
		[NotNull]
		public string Name { get; set; } = "Sales Tax";

		// Store hundredths of one percentage point as an integer.
		// Example: 13.00% is stored as 1300 basis points.
		[NotNull]
		public int RateBasisPoints { get; set; }

		// App code works with a normal percentage such as 13.00m.
		[Ignore]
		public decimal Percentage
		{
			get => RateBasisPoints / 100m;

			set
			{
				if (value < 0m || value > 100m)
				{
					throw new ArgumentOutOfRangeException(
						nameof(Percentage),
						"Tax rate must be between 0 and 100 percent.");
				}

				if (decimal.Round(value, 2) != value)
				{
					throw new ArgumentException(
						"Tax rate can have no more than two decimal places.",
						nameof(Percentage));
				}

				RateBasisPoints = checked((int)(value * 100m));
			}
		}
	}
}
