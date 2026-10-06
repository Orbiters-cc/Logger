#if LOGGER_UNITGIT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Orbiters.UnitGit.Editor;
using UnityEditor;

namespace Orbiters.Logger.Editor.UnitGit
{
    /// <summary>
    /// With Unit Git in the project, the Logger's timeline shows its commits and releases. Releases come from Unit
    /// Git's release catalog, commits from <c>git log</c> on a worker thread (the last 30 days; the timeline only shows
    /// what falls inside the logged time). Refreshed when Unit Git records something, and every minute while a
    /// Logger window is open.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnitGitTimeline
    {
        private const string Source = "unitgit";
        private const double RefreshSeconds = 60d;
        private static double nextRefresh;
        private static int running;

        static UnitGitTimeline()
        {
            UnitGitReleases.ChangedExternally += Refresh;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh || !LoggerWindow.IsOpen)
            {
                return;
            }

            Refresh();
        }

        private static void Refresh()
        {
            nextRefresh = EditorApplication.timeSinceStartup + RefreshSeconds;
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            {
                return;
            }

            string root;
            var markers = new List<TimelineMarker>();
            try
            {
                root = Path.GetFullPath(".");
                if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
                {
                    TimelineMarkers.Clear(Source);
                    running = 0;
                    return;
                }

                AddReleases(root, markers);
            }
            catch (Exception)
            {
                running = 0;
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    AddCommits(root, markers);
                    TimelineMarkers.Set(Source, markers);
                }
                catch (Exception)
                {
                    // Without Git history the timeline just has no commits.
                }
                finally
                {
                    running = 0;
                }
            });
        }

        private static void AddReleases(string root, List<TimelineMarker> markers)
        {
            if (!File.Exists(UnitGitReleases.GetReleasesFilePath(root)))
            {
                return;
            }

            var file = UnitGitReleases.Load(root);
            foreach (var release in file.releases)
            {
                if (release == null || file.hiddenReleaseIds.Contains(release.id) ||
                    !DateTime.TryParse(release.date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                {
                    continue;
                }

                string what = string.IsNullOrEmpty(release.name) ? release.title : release.name;
                string title = (string.IsNullOrEmpty(release.tool) ? "Release" : release.tool + " release") + ": " + what +
                               (string.IsNullOrEmpty(release.version) ? string.Empty : " " + release.version);
                markers.Add(new TimelineMarker(DateTime.SpecifyKind(date, DateTimeKind.Utc), TimelineMarkerKind.Release, title, release.scope));
            }
        }

        private static void AddCommits(string root, List<TimelineMarker> markers)
        {
            var start = new ProcessStartInfo("git", "-C \"" + root + "\" log -n 500 --since=30.days --format=%h%x1f%at%x1f%s%x1f%an")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start))
            {
                if (process == null)
                {
                    return;
                }

                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(10000) || process.ExitCode != 0)
                {
                    return;
                }

                foreach (string line in output.Split('\n'))
                {
                    string[] parts = line.TrimEnd('\r').Split('\u001f');
                    if (parts.Length < 4 || !long.TryParse(parts[1], out long seconds))
                    {
                        continue;
                    }

                    var time = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
                    markers.Add(new TimelineMarker(time, TimelineMarkerKind.Commit, parts[2], parts[0] + " · " + parts[3]));
                }
            }
        }
    }
}
#endif
