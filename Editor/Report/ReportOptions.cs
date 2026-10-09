using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Orbiters.Logger.Editor
{
    /// <summary>What a project report can hold, one card each in the report window.</summary>
    internal enum ReportPart
    {
        Diagnosis,
        Logs,
        Git,
        Scene,
        SceneExport
    }

    /// <summary>A file or folder another package adds to the Git part of a report (Unit Git's release notes).</summary>
    internal readonly struct ReportExtra
    {
        public readonly string Path;
        public readonly string Name;
        public readonly string Label;

        /// <param name="path">The file or folder on disk.</param>
        /// <param name="name">Where it goes inside the report ("unitgit/releases.json").</param>
        /// <param name="label">What it is, for the report's contents.</param>
        public ReportExtra(string path, string name, string label)
        {
            Path = path;
            Name = name;
            Label = label;
        }
    }

    /// <summary>
    /// The report's remembered choices (the parts the person keeps on or off, where reports go) and what other
    /// assemblies add to it.
    /// </summary>
    internal static class ReportOptions
    {
        private const string PartPref = "Orbiters.Logger.Report.";
        private const string FolderPref = "Orbiters.Logger.Report.Folder";

        internal static readonly ReportPart[] Parts = (ReportPart[])Enum.GetValues(typeof(ReportPart));

        /// <summary>Files Git-aware packages add to the Git part (called on the main thread with the project folder).</summary>
        internal static readonly List<Func<string, IEnumerable<ReportExtra>>> GitExtras = new List<Func<string, IEnumerable<ReportExtra>>>();

        internal static bool IsOn(ReportPart part) => EditorPrefs.GetBool(PartPref + part, true);

        internal static void Set(ReportPart part, bool on) => EditorPrefs.SetBool(PartPref + part, on);

        /// <summary>Where reports are saved: the Desktop unless the person picked another folder that still exists.</summary>
        internal static string Folder
        {
            get
            {
                string chosen = EditorPrefs.GetString(FolderPref, string.Empty);
                return !string.IsNullOrEmpty(chosen) && Directory.Exists(chosen) ? chosen : DefaultFolder;
            }
            set => EditorPrefs.SetString(FolderPref, value == DefaultFolder ? string.Empty : value ?? string.Empty);
        }

        internal static string DefaultFolder
        {
            get
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                return string.IsNullOrEmpty(desktop) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : desktop;
            }
        }

        /// <summary>The project's folder name ("MCB Test").</summary>
        internal static string ProjectName
        {
            get
            {
                string root = GitCli.ProjectRoot;
                string name = string.IsNullOrEmpty(root) ? null : new DirectoryInfo(root).Name;
                return string.IsNullOrWhiteSpace(name) ? "Unity project" : name;
            }
        }

        /// <summary>"MCB Test report 2026-10-08 14-32.zip": readable, sortable, safe on every system.</summary>
        internal static string FileName(DateTime local)
        {
            string name = ProjectName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name + " report " + local.ToString("yyyy-MM-dd HH-mm", System.Globalization.CultureInfo.InvariantCulture) + ".zip";
        }

        /// <summary>A path in <paramref name="folder"/> no other report uses yet.</summary>
        internal static string FreePath(string folder, DateTime local)
        {
            string file = FileName(local);
            string path = Path.Combine(folder, file);
            for (int i = 2; File.Exists(path); i++)
            {
                path = Path.Combine(folder, Path.GetFileNameWithoutExtension(file) + " (" + i + ").zip");
            }

            return path;
        }
    }
}
