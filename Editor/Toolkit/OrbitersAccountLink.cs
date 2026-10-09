#if LOGGER_TOOLKIT
using UnityEditor;

namespace Orbiters.Logger.Editor.Toolkit
{
    /// <summary>
    /// With Orbiters Toolkit installed, the Logger talks to the server Orbiters tools use (production or the development
    /// one) as the signed-in member: explanations for members, the AI diagnosis of project reports, and the Toolkit's
    /// sign-in buttons where the Logger asks to connect.
    /// </summary>
    [InitializeOnLoad]
    internal static class OrbitersAccountLink
    {
        static OrbitersAccountLink()
        {
            OrbitersLink.ApiUrl = path => OrbitersEnvironment.ApiUrl(path);
            OrbitersLink.Token = () => AuthenticationService.GetAuth()?.token;
            OrbitersLink.Username = () =>
            {
                var auth = AuthenticationService.GetAuth();
                return auth == null ? null : string.IsNullOrEmpty(auth.username) ? auth.user : auth.username;
            };
            OrbitersLink.SignInElement = (reason, connected) => new OrbitersSignInElement(connected, reason, "logger");
            AuthenticationService.Changed += OrbitersLink.RaiseChanged;
            OrbitersEnvironment.Changed += OrbitersLink.RaiseChanged;
        }
    }
}
#endif
