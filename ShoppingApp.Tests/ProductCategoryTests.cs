using shopping_app.Models;

namespace ShoppingApp.Tests
{
	public class ProductCategoryTests
	{
		[Theory]
		[InlineData(false, "No")]
		[InlineData(true, "Yes")]
		public void TaxableDisplay_ReflectsTaxSetting(
			bool isTaxable,
			string expected)
		{
			// The Product form shows this display-only value in its read-only field.
			var category = new ProductCategory
			{
				IsTaxable = isTaxable
			};

			Assert.Equal(expected, category.TaxableDisplay);
		}
	}
}
