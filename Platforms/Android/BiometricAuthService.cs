using Android.OS;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using Microsoft.Maui.ApplicationModel;
using Tirki.Services;

namespace Tirki.Platforms.Android;

public class BiometricAuthService : IBiometricAuthService
{
    private const int AllowedAuthenticators = BiometricManager.Authenticators.BiometricWeak | BiometricManager.Authenticators.DeviceCredential;

    public Task<bool> IsAvailableAsync()
    {
        var context = global::Android.App.Application.Context;
        var manager = BiometricManager.From(context);
        var result = manager.CanAuthenticate(AllowedAuthenticators);
        return Task.FromResult(result == BiometricManager.BiometricSuccess);
    }

    public Task<bool> AuthenticateAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (Platform.CurrentActivity is not FragmentActivity activity)
        {
            tcs.SetResult(false);
            return tcs.Task;
        }

        var executor = ContextCompat.GetMainExecutor(activity)!;
        var callback = new AuthenticationCallback(tcs);
        var prompt = new BiometricPrompt(activity, executor, callback);

        var promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Tirki")
            .SetSubtitle(reason)
            .SetAllowedAuthenticators(AllowedAuthenticators)
            .Build();

        // Un semplice RunOnUiThread non basta: se chiamato in reazione diretta a onResume, la
        // FragmentManager dell'activity può trovarsi ancora in stato "salvato" (state saved) per
        // via della transizione di lifecycle in corso, e mostrare il prompt fallisce con
        // "Called after onSaveInstanceState()". Un vero Handler.Post rimanda l'esecuzione al giro
        // successivo del message loop, quando la transizione è ormai completata.
        new Handler(Looper.MainLooper!).Post(() => prompt.Authenticate(promptInfo));

        return tcs.Task;
    }

    private class AuthenticationCallback : BiometricPrompt.AuthenticationCallback
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public AuthenticationCallback(TaskCompletionSource<bool> tcs)
        {
            _tcs = tcs;
        }

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            base.OnAuthenticationSucceeded(result);
            _tcs.TrySetResult(true);
        }

        public override void OnAuthenticationError(int errorCode, global::Java.Lang.ICharSequence errString)
        {
            base.OnAuthenticationError(errorCode, errString);
            _tcs.TrySetResult(false);
        }

        public override void OnAuthenticationFailed()
        {
            base.OnAuthenticationFailed();
            // Un singolo tentativo di impronta non riconosciuta: l'utente può riprovare, il prompt resta aperto.
        }
    }
}
