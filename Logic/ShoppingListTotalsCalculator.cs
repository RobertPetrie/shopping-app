using shopping_app.Models;

namespace shopping_app.Logic
{
	/// <summary>
	/// Calculates the running shopping-list amounts from picked-up items only.
	/// </summary>
	public static class ShoppingListTotalsCalculator
	{
		public static ShoppingListTotals Calculate(
			IEnumerable<ShoppingListProductListItem> items,
			int taxRateBasisPoints)
		{
			ArgumentNullException.ThrowIfNull(items);

			if (taxRateBasisPoints < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(taxRateBasisPoints));
			}

			long subtotalCents = 0;
			long taxableSubtotalCents = 0;

			foreach (var item in items.Where(item => item.PickedUp))
			{
				subtotalCents = checked(subtotalCents + item.PriceInCents);

				if (item.IsTaxable)
				{
					taxableSubtotalCents = checked(
						taxableSubtotalCents + item.PriceInCents);
				}
			}

			// Apply tax to the combined taxable subtotal and round to the nearest
			// cent using normal money-style midpoint rounding.
			long taxCents = checked((long)decimal.Round(
				taxableSubtotalCents * taxRateBasisPoints / 10000m,
				0,
				MidpointRounding.AwayFromZero));

			return new ShoppingListTotals(
				SubtotalCents: subtotalCents,
				TaxCents: taxCents,
				TotalCents: checked(subtotalCents + taxCents));
		}
	}

	public readonly record struct ShoppingListTotals(
		long SubtotalCents,
		long TaxCents,
		long TotalCents)
	{
		public decimal Subtotal => SubtotalCents / 100m;

		public decimal Tax => TaxCents / 100m;

		public decimal Total => TotalCents / 100m;
	}
}
