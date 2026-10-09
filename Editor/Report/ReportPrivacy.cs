using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// What a project report never contains: the person's user folder (it often holds their real name), Orbiters
    /// tokens, bearer tokens and passwords written in addresses. Every text the report writes or sends goes through
    /// <see cref="Mask"/>; Unity packages exported from the scene are the project's own files and are left as they are.
    /// </summary>
    internal sealed class ReportPrivacy
    {
        internal const string UserFolder = "%USERPROFILE%";
        internal const string Masked = "[masked]";

        private static readonly Regex OrbitersToken = new Regex(@"\borbit-[0-9a-f]{8}-[0-9a-f]{8}-[0-9a-f]{8}-\d{2}-\d{2}-\d{4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Bearer = new Regex(@"(\bBearer\s+)[A-Za-z0-9\-._~+/]{12,}=*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UrlCredentials = new Regex(@"(\b[a-z][a-z0-9+.\-]*://)[^\s/@:]+(:[^\s/@]*)?@", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly List<string> folders = new List<string>();
        private readonly List<string> secrets = new List<string>();

        /// <param name="userFolder">The user folder to hide ("C:\Users\Name"); both slash styles are hidden.</param>
        /// <param name="secrets">Exact values to hide (the signed-in token).</param>
        internal ReportPrivacy(string userFolder, params string[] secrets)
        {
            if (!string.IsNullOrEmpty(userFolder) && userFolder.Trim().Length > 3)
            {
                string trimmed = userFolder.TrimEnd('\\', '/');
                // As written in JSON ("C:\\Users\\Name") first, then with either slash.
                folders.Add(trimmed.Replace('/', '\\').Replace("\\", "\\\\"));
                folders.Add(trimmed.Replace('/', '\\'));
                folders.Add(trimmed.Replace('\\', '/'));
            }

            foreach (string secret in secrets ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(secret) && secret.Length >= 8)
                {
                    this.secrets.Add(secret);
                }
            }
        }

        /// <summary>The privacy of this computer: its user folder and the signed-in Orbiters token.</summary>
        internal static ReportPrivacy ForThisComputer() =>
            new ReportPrivacy(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), OrbitersLink.SafeToken());

        internal string Mask(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            foreach (string secret in secrets)
            {
                if (text.IndexOf(secret, StringComparison.Ordinal) >= 0)
                {
                    text = text.Replace(secret, Masked);
                }
            }

            foreach (string folder in folders)
            {
                text = ReplaceIgnoringCase(text, folder, UserFolder);
            }

            if (text.IndexOf("orbit-", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text = OrbitersToken.Replace(text, Masked);
            }

            if (text.IndexOf("bearer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text = Bearer.Replace(text, "$1" + Masked);
            }

            if (text.IndexOf("://", StringComparison.Ordinal) >= 0 && text.IndexOf('@') >= 0)
            {
                text = UrlCredentials.Replace(text, "$1" + Masked + "@");
            }

            return text;
        }

        private static string ReplaceIgnoringCase(string text, string value, string replacement)
        {
            int at = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return text;
            }

            var builder = new System.Text.StringBuilder(text.Length);
            int from = 0;
            while (at >= 0)
            {
                builder.Append(text, from, at - from).Append(replacement);
                from = at + value.Length;
                at = text.IndexOf(value, from, StringComparison.OrdinalIgnoreCase);
            }

            return builder.Append(text, from, text.Length - from).ToString();
        }
    }
}
