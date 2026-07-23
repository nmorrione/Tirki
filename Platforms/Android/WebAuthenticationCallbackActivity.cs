using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;

namespace Tirki;

[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.827791927659-13jodpkjgofvjuaiqvfb09nc4dmft2qd")]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
