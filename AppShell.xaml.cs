using Tirki.Views;

namespace Tirki;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TransactionEditPage), typeof(TransactionEditPage));
		Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
		Routing.RegisterRoute(nameof(CategoriesPage), typeof(CategoriesPage));
		Routing.RegisterRoute(nameof(CategoryEditPage), typeof(CategoryEditPage));
	}
}
