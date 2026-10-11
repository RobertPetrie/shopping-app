using Microsoft.Extensions.Logging;

using shopping_app.Data;

using UraniumUI;

namespace shopping_app
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseUraniumUI()
				.UseUraniumUIMaterial()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

			// Reuse one database service. The file opens on the first call to
			// GetConnectionAsync, rather than during app startup.
			builder.Services.AddSingleton<AppDatabase>();

			// Shell creates ProductDetailPage through the service provider so the
			// database is available before OnAppearing runs.
			builder.Services.AddTransient<Pages.ProductDetailPage>();

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}
