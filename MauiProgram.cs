using Microsoft.Extensions.Logging;
using Tirki.Services;
using Tirki.ViewModels;
using Tirki.Views;

namespace Tirki;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				// Il filtro nativo di Android per la tastiera numerica accetta solo il punto come
				// separatore decimale, indipendentemente dai tasti mostrati a schermo: su tastiera
				// italiana (virgola) il carattere digitato viene scartato in silenzio. Si estende
				// qui l'elenco dei caratteri accettati a entrambi i separatori.
				Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NumericAllowDecimalComma", (handler, view) =>
				{
					if (view.Keyboard == Keyboard.Numeric)
						handler.PlatformView.KeyListener = Android.Text.Method.DigitsKeyListener.GetInstance("0123456789.,");
				});
#endif
			});

		builder.Services.AddSingleton<LocalDatabaseService>();
		builder.Services.AddSingleton<HistoricalImportService>();
		builder.Services.AddSingleton<GoogleAuthService>();
		builder.Services.AddSingleton<DriveSyncService>();
		builder.Services.AddSingleton<AutoSyncService>();
		builder.Services.AddTransient<TransactionsViewModel>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<TransactionEditViewModel>();
		builder.Services.AddTransient<TransactionEditPage>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
