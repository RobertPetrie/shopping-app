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

				// UraniumUI 3.0.0 has a known StatefulButtonHandler teardown bug
				// that can throw "PlatformView cannot be null here" when Shell
				// removes a page. Re-register MAUI's stock ButtonHandler after
				// UraniumUI so buttons remain safe during navigation teardown.
				.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler<
						Microsoft.Maui.Controls.Button,
						Microsoft.Maui.Handlers.ButtonHandler>();
				})
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
