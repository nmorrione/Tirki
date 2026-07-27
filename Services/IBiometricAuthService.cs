namespace Tirki.Services;

/// <summary>
/// Autenticazione biometrica (impronta/viso) o, in alternativa, il metodo di sblocco del
/// device (PIN/pattern/password) già configurato dall'utente — è il sistema operativo a
/// decidere quale offrire, l'app chiede solo "conferma che sei tu".
/// </summary>
public interface IBiometricAuthService
{
    /// <summary>True se il device ha almeno un metodo di sblocco configurato (impronta, viso o PIN/pattern).</summary>
    Task<bool> IsAvailableAsync();

    /// <summary>Mostra il prompt nativo. Restituisce true solo se l'utente si autentica con successo.</summary>
    Task<bool> AuthenticateAsync(string reason);
}
