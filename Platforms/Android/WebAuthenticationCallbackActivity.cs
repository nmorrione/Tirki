using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace Tirki;

// NOTE: DataScheme va aggiornato con lo scheme reale una volta noto il Client ID Google
// (per un client OAuth "App per Desktop" è "com.googleusercontent.apps.<CLIENT_ID>").
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.TODO_INSERIRE_CLIENT_ID")]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
