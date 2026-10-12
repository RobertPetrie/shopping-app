using shopping_app.Logic;

namespace ShoppingApp.Tests
{
	public class ShoppingListPickedUpChangeTests
	{
		[Theory]
		[InlineData(false, false)]
		[InlineData(true, true)]
		public void ShouldHandle_WhenCheckboxMatchesStoredValue_IgnoresBindingEvent(
			bool storedValue,
			bool checkboxValue)
		{
			// Loading or recycling a row can raise CheckedChanged even though the
			// user did nothing. Matching values must never open the price popup.
			bool shouldHandle = ShoppingListPickedUpChange.ShouldHandle(
				storedValue,
				checkboxValue);

			Assert.False(shouldHandle);
		}

		[Theory]
		[InlineData(false, true)]
		[InlineData(true, false)]
		public void ShouldHandle_WhenCheckboxDiffersFromStoredValue_HandlesUserChange(
			bool storedValue,
			bool checkboxValue)
		{
			// A genuine tap changes the visual checkbox away from the value still
			// stored on the model, so the app should process that transition.
			bool shouldHandle = ShoppingListPickedUpChange.ShouldHandle(
				storedValue,
				checkboxValue);

			Assert.True(shouldHandle);
		}
	}
}
