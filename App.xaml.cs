using Microsoft.Extensions.DependencyInjection;
using Tirki.Services;
using Tirki.Views;

namespace Tirki;

public partial class App : Application
{
	private readonly AutoSyncService _autoSync;
	private readonly IServiceProvider _services;
	private bool _isLockScreenShowing;

	public App(AutoSyncService autoSync, IServiceProvider services)
	{
		InitializeComponent();
		_autoSync = autoSync;
		_services = services;

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
			ShowLockScreenIfNeeded(window);
			_autoSync.TriggerBackgroundSync();
			_autoSync.StartPeriodicSync();
		};
		window.Resumed += (_, _) =>
		{
			ShowLockScreenIfNeeded(window);
			_autoSync.TriggerBackgroundSync();
			_autoSync.StartPeriodicSync();
		};
		window.Stopped += (_, _) => _autoSync.StopPeriodicSync();
		return window;
	}

	/// <summary>
	/// Mostra la schermata di blocco come pagina modale, sempre, ogni volta che l'app torna in
	/// primo piano (nessun periodo di tolleranza: coerente con un'app di gestione finanziaria).
	/// La guardia evita di impilarne più di una se l'evento scatta più volte di fila.
	/// </summary>
	private void ShowLockScreenIfNeeded(Window window)
	{
		if (_isLockScreenShowing) return;
		if (!Preferences.Default.Get(AppPreferenceKeys.BiometricLockEnabled, false)) return;
		if (window.Page is not { } page) return;

		_isLockScreenShowing = true;
		var lockPage = _services.GetRequiredService<LockPage>();
		lockPage.Disappearing += (_, _) => _isLockScreenShowing = false;
		_ = page.Navigation.PushModalAsync(lockPage, animated: false);
	}
}