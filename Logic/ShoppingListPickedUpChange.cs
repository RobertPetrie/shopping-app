namespace shopping_app.Logic
{
	/// <summary>
	/// Distinguishes a real checkbox change from a UI binding that is only
	/// displaying the PickedUp value already stored on the row.
	/// </summary>
	public static class ShoppingListPickedUpChange
	{
		public static bool ShouldHandle(
			bool storedPickedUp,
			bool checkboxValue)
		{
			// Matching values mean MAUI is loading or recycling the row. A different
			// value means the user actually changed the checkbox.
			return storedPickedUp != checkboxValue;
		}
	}
}
