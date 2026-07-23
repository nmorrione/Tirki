using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Tirki.Services;

/// <summary>
/// Login Google via browser di sistema (WebAuthenticator) con Authorization Code + PKCE.
/// Il refresh token viene salvato in SecureStorage e usato per ottenere nuovi access token
/// senza richiedere login ogni volta.
///
/// NOTE: per un client OAuth di tipo "App per Desktop", Google richiede comunque il
/// client_secret nello scambio token (anche con PKCE) — a differenza dei tipi Android/iOS,
/// che sono client pubblici puri. Non è una credenziale personale dell'utente: è un secondo
/// identificativo dell'app generato da Google insieme al Client ID.
/// </summary>
// Il client secret vive in GoogleAuthService.Secrets.cs, un file locale ignorato da git
// (vedi GoogleAuthService.Secrets.cs.template per come ricrearlo su una macchina nuova).
public partial class GoogleAuthService
{
    private const string ClientId = "827791927659-13jodpkjgofvjuaiqvfb09nc4dmft2qd.apps.googleusercontent.com";
    private const string RedirectUri = "com.googleusercontent.apps.827791927659-13jodpkjgofvjuaiqvfb09nc4dmft2qd:/oauth2redirect";

    private const string Scope = "https://www.googleapis.com/auth/drive.file";
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RefreshTokenKey = "google_refresh_token";

    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc;

    public async Task<bool> IsSignedInAsync()
    {
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        return !string.IsNullOrEmpty(refreshToken);
    }

    public async Task SignInAsync()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        var authUrl =
            $"{AuthorizationEndpoint}" +
            $"?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Scope)}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&access_type=offline" +
            $"&prompt=consent";

        var result = await WebAuthenticator.Default.AuthenticateAsync(
            new WebAuthenticatorOptions
            {
                Url = new Uri(authUrl),
                CallbackUrl = new Uri(RedirectUri),
            });

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
            throw new InvalidOperationException("Google non ha restituito un codice di autorizzazione.");

        await ExchangeAuthorizationCodeAsync(code, codeVerifier);
    }

    public async Task SignOutAsync()
    {
        SecureStorage.Default.Remove(RefreshTokenKey);
        _accessToken = null;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken is not null && DateTime.UtcNow < _accessTokenExpiresAtUtc)
            return _accessToken;

        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken))
            throw new InvalidOperationException("Nessun account Google collegato: effettua il login prima di sincronizzare.");

        using var http = new HttpClient();
        var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }));
        await EnsureSuccessOrThrowAsync(response);

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Risposta token non valida da Google.");

        _accessToken = token.AccessToken;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);
        return _accessToken;
    }

    private async Task ExchangeAuthorizationCodeAsync(string code, string codeVerifier)
    {
        using var http = new HttpClient();
        var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = RedirectUri,
        }));
        await EnsureSuccessOrThrowAsync(response);

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Risposta token non valida da Google.");

        _accessToken = token.AccessToken;
        _accessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);

        if (!string.IsNullOrEmpty(token.RefreshToken))
            await SecureStorage.Default.SetAsync(RefreshTokenKey, token.RefreshToken);
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Google ha rifiutato la richiesta ({(int)response.StatusCode}): {body}");
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}
