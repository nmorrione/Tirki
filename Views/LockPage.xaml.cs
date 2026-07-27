using Tirki.Services;

namespace Tirki.Views;

public partial class LockPage : ContentPage
{
    private readonly IBiometricAuthService _biometric;
    private bool _isAuthenticating;

    public LockPage(IBiometricAuthService biometric)
    {
        InitializeComponent();
        _biometric = biometric;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // La pagina modale sta ancora completando la sua transizione quando OnAppearing scatta:
        // mostrare subito il prompt biometrico in questo istante può fallire silenziosamente
        // (l'activity risulta ancora in una transizione di stato). Un breve rinvio lascia che la
        // transizione si assesti, coerente col fix in BiometricAuthService per lo stesso motivo.
        _ = TryUnlockAfterDelayAsync();
    }

    protected override bool OnBackButtonPressed() => true;

    private async void OnUnlockClicked(object? sender, EventArgs e)
    {
        await TryUnlockAsync();
    }

    private async Task TryUnlockAfterDelayAsync()
    {
        await Task.Delay(300);
        await TryUnlockAsync();
    }

    private async Task TryUnlockAsync()
    {
        if (_isAuthenticating) return;
        _isAuthenticating = true;
        try
        {
            var success = await _biometric.AuthenticateAsync("Sblocca Tirki");
            if (success)
                await Navigation.PopModalAsync(animated: false);
        }
        finally
        {
            _isAuthenticating = false;
        }
    }
}
