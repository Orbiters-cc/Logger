#if LOGGER_TOOLKIT
using UnityEditor;

namespace Orbiters.Logger.Editor.Toolkit
{
    /// <summary>
    /// With Orbiters Toolkit installed, the Logger asks the server Orbiters tools use (production or the development
    /// one) for its explanations, as the signed-in member, and asks again when the account or the server changes.
    /// </summary>
    [InitializeOnLoad]
    internal static class OrbitersAccountLink
    {
        static OrbitersAccountLink()
        {
            RemoteExplanations.Endpoint = () => OrbitersEnvironment.ApiUrl("logger/explanations");
            RemoteExplanations.Token = () => AuthenticationService.GetAuth()?.token;
            AuthenticationService.Changed += () => RemoteExplanations.Refresh(force: true);
            OrbitersEnvironment.Changed += () => RemoteExplanations.Refresh(force: true);
        }
    }
}
#endif
