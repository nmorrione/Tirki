using Microsoft.Extensions.DependencyInjection;
using Tirki.Services;

namespace Tirki;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		var savedTheme = Preferences.Default.Get(AppPreferenceKeys.Theme, nameof(AppTheme.Unspecified));
		UserAppTheme = Enum.Parse<AppTheme>(savedTheme);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}