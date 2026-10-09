using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Orbiters.Logger.Editor
{
    /// <summary>Where the project stands in Git, for the README and the AI diagnosis.</summary>
    internal sealed class GitDigest
    {
        public bool Repository;
        public string Branch = string.Empty;
        public string Head = string.Empty;
        public int Commits;
        public int Changed;
        public readonly List<string> Recent = new List<string>();
        public readonly List<string> Extras = new List<string>();
        public string Error;
    }

    /// <summary>
    /// The Git part of a report: the history with the files each commit changed, what is changed now, where HEAD went
    /// (reflog), the remotes without their passwords, and what Git-aware packages add (Unit Git releases).
    /// </summary>
    internal static class GitReport
    {
        private const int MaxOutput = 6 * 1024 * 1024;

        /// <summary>The numbers shown on the card; safe on a worker thread.</summary>
        internal static GitDigest Look(string root)
        {
            var digest = new GitDigest { Repository = GitCli.IsRepository(root) };
            if (!digest.Repository)
            {
                return digest;
            }

            var branch = GitCli.Run(root, "rev-parse --abbrev-ref HEAD");
            if (!branch.Success)
            {
                digest.Error = FirstLine(branch.Error);
                return digest;
            }

            digest.Branch = branch.Output.Trim();
            digest.Head = GitCli.Run(root, "rev-parse --short HEAD").Output.Trim();
            int.TryParse(GitCli.Run(root, "rev-list --count HEAD").Output.Trim(), out digest.Commits);
            digest.Changed = Lines(GitCli.Run(root, "status --porcelain=v1").Output).Count;
            digest.Recent.AddRange(Lines(GitCli.Run(root, "log -n 15 --format=%s").Output));
            return digest;
        }

        /// <summary>Writes the Git files; safe on a worker thread.</summary>
        internal static string Write(ReportContext context, string root, IReadOnlyList<ReportExtra> extras, Action<float> progress)
        {
            var digest = Look(root);
            context.Git = digest;
            if (!digest.Repository)
            {
                return "Not a Git repository";
            }

            if (digest.Error != null)
            {
                throw new InvalidOperationException(digest.Error);
            }

            progress?.Invoke(0.2f);
            var summary = new StringBuilder();
            summary.Append("Branch   ").Append(digest.Branch).Append('\n');
            summary.Append("HEAD     ").Append(GitCli.Run(root, "log -1 --date=iso-local \"--format=%H%n         %s%n         %an, %ad\"").Output.TrimEnd()).Append('\n');
            summary.Append("Commits  ").Append(digest.Commits).Append('\n');
            summary.Append("Changed  ").Append(digest.Changed).Append(" files not committed\n");
            string upstream = GitCli.Run(root, "status -sb --porcelain=v1").Output;
            summary.Append("Tracking ").Append(Lines(upstream).FirstOrDefault()?.TrimStart('#', ' ') ?? string.Empty).Append('\n');
            summary.Append("\nRemotes\n").Append(Indent(GitCli.Run(root, "remote -v").Output));
            summary.Append("\nLarge files (Git LFS)\n").Append(Indent(GitCli.Run(root, "lfs ls-files").Output, "  (none or Git LFS not installed)"));
            context.WriteText("git/summary.txt", summary.ToString(), "Branch, last commit, remotes and Git LFS files");
            progress?.Invoke(0.35f);

            // Without rename detection: it reads every big binary file of each commit (45 s instead of 0.2 s on an avatar project).
            Save(context, root, "log -n 300 --name-status --no-renames --date=iso-local", "git/log.txt", "The last 300 commits with the files each one added, changed or deleted");
            progress?.Invoke(0.6f);
            Save(context, root, "status --porcelain=v1 -uall", "git/status.txt", "Files changed, added or deleted since the last commit");
            Save(context, root, "diff --stat=160 --no-renames HEAD", "git/diff-stat.txt", "How much each uncommitted file changed");
            Save(context, root, "reflog -n 300 --date=iso-local", "git/reflog.txt", "Where the project went: commits, checkouts, pulls and resets");
            Save(context, root, "stash list", "git/stashes.txt", "Changes put aside with git stash");
            progress?.Invoke(0.8f);

            foreach (var extra in extras)
            {
                if (CopyExtra(context, extra))
                {
                    digest.Extras.Add(extra.Label);
                }
            }

            progress?.Invoke(1f);
            return LoggerUi.Plural(digest.Commits, "commit") + " · " + digest.Branch + (digest.Extras.Count > 0 ? " · " + string.Join(", ", digest.Extras) : string.Empty);
        }

        private static void Save(ReportContext context, string root, string arguments, string name, string label)
        {
            var result = GitCli.Run(root, arguments, 30000);
            string text = result.Success ? result.Output : "git " + arguments + " failed:\n" + result.Error;
            if (text.Length > MaxOutput)
            {
                text = text.Substring(0, MaxOutput) + "\n[Cut at " + MaxOutput / (1024 * 1024) + " MB.]\n";
            }

            context.WriteText(name, text, label);
        }

        private static bool CopyExtra(ReportContext context, ReportExtra extra)
        {
            try
            {
                if (File.Exists(extra.Path))
                {
                    CopyFile(context, extra.Path, extra.Name);
                    context.Add(extra.Name, extra.Label);
                    return true;
                }

                if (!Directory.Exists(extra.Path))
                {
                    return false;
                }

                foreach (string file in Directory.GetFiles(extra.Path, "*", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(extra.Path.Length).TrimStart('\\', '/').Replace('\\', '/');
                    CopyFile(context, file, extra.Name + "/" + relative);
                }

                context.Add(extra.Name + "/", extra.Label);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".json", ".txt", ".md", ".yaml", ".yml", ".xml", ".csv", ".log", ".ini"
        };

        // Text files are masked like everything the report writes; pictures and other binaries are copied as they are.
        private static void CopyFile(ReportContext context, string from, string name)
        {
            if (TextExtensions.Contains(Path.GetExtension(from)))
            {
                File.WriteAllText(context.PathOf(name), context.Privacy.Mask(File.ReadAllText(from)), new UTF8Encoding(false));
            }
            else
            {
                File.Copy(from, context.PathOf(name), true);
            }
        }

        internal static List<string> Lines(string text) =>
            (text ?? string.Empty).Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0).ToList();

        private static string FirstLine(string text) => Lines(text).FirstOrDefault() ?? "Git did not answer.";

        private static string Indent(string text, string empty = "  (none)")
        {
            var lines = Lines(text);
            return lines.Count == 0 ? empty + "\n" : string.Concat(lines.Select(line => "  " + line + "\n"));
        }
    }
}
