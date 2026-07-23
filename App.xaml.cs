using Microsoft.Extensions.DependencyInjection;
using Tirki.Services;

namespace Tirki;

public partial class App : Application
{
	private readonly AutoSyncService _autoSync;

	public App(AutoSyncService autoSync)
	{
		InitializeComponent();
		_autoSync = autoSync;

		var savedTheme = Preferences.Default.Get(AppPreferenceKeys.Theme, nameof(AppTheme.Unspecified));
		UserAppTheme = Enum.Parse<AppTheme>(savedTheme);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		// Il contesto piattaforma (necessario a SecureStorage) non è ancora pronto nel costruttore di App:
		// si aspetta Created, che scatta a finestra nativa creata.
		window.Created += (_, _) =>
		{
			_autoSync.TriggerBackgroundSync();
			_autoSync.StartPeriodicSync();
		};
		window.Resumed += (_, _) =>
		{
			_autoSync.TriggerBackgroundSync();
			_autoSync.StartPeriodicSync();
		};
		window.Stopped += (_, _) => _autoSync.StopPeriodicSync();
		return window;
	}
}