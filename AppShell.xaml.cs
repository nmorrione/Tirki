using Tirki.Views;

namespace Tirki;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(TransactionEditPage), typeof(TransactionEditPage));
	}
}
