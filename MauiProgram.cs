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
			});

		builder.Services.AddSingleton<LocalDatabaseService>();
		builder.Services.AddSingleton<HistoricalImportService>();
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
