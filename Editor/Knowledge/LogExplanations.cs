using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Orbiters.Logger.Editor
{
    /// <summary>How much a known log matters.</summary>
    public enum LogExplanationSeverity
    {
        /// <summary>Nothing to do: the editor or a tool reports something that does not affect the project.</summary>
        Harmless,
        /// <summary>Worth knowing; usually nothing to do.</summary>
        Info,
        /// <summary>Something is off and may cause problems later.</summary>
        Warning,
        /// <summary>Something is broken and needs fixing.</summary>
        Problem,
        /// <summary>Blocks Play Mode, builds or uploads until fixed.</summary>
        Blocking
    }

    /// <summary>
    /// What a known log means and how to fix it. A log matches when its text contains <see cref="Needle"/>, matches
    /// <see cref="Pattern"/> (if set) and its stack trace contains <see cref="StackNeedle"/> (if set). Named groups of
    /// <see cref="Pattern"/> fill <c>{name}</c> placeholders in the title, summary and fixes.
    /// </summary>
    public sealed class LogExplanation
    {
        public LogExplanation(string id, string needle, string title)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Needle = needle ?? string.Empty;
            Title = title ?? string.Empty;
        }

        public string Id { get; }
        /// <summary>Text the message must contain (case-sensitive), checked before anything else.</summary>
        public string Needle { get; }
        public string Title { get; }
        /// <summary>A regular expression the message must match, with named groups for the placeholders.</summary>
        public string Pattern { get; set; }
        /// <summary>Text the stack trace must contain, for explanations specific to one tool.</summary>
        public string StackNeedle { get; set; }
        /// <summary>Only logs of these levels match (all when null): "error", "warning", "log".</summary>
        public string[] Levels { get; set; }
        /// <summary>Higher is tried first among explanations of the same kind; broad catch-alls use negative values.</summary>
        public int Priority { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string[] Fixes { get; set; } = Array.Empty<string>();
        /// <summary>Where the log comes from, as people know it ("VRChat SDK", "Unity", "C# compiler").</summary>
        public string Source { get; set; } = string.Empty;
        public LogExplanationSeverity Severity { get; set; } = LogExplanationSeverity.Info;
        public string Link { get; set; }
        public string LinkLabel { get; set; }

        internal ExplanationOrigin Origin;
        private Regex regex;
        private bool patternFailed;

        /// <summary>The compiled pattern, built at its first use; null without a pattern or when it doesn't compile.</summary>
        internal Regex Regex
        {
            get
            {
                if (regex == null && !patternFailed && !string.IsNullOrEmpty(Pattern))
                {
                    try
                    {
                        regex = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                    }
                    catch (ArgumentException)
                    {
                        patternFailed = true;
                    }
                }

                return regex;
            }
        }

        /// <summary>A pattern that doesn't compile: the entry never matches.</summary>
        internal bool Broken => !string.IsNullOrEmpty(Pattern) && Regex == null;

        internal bool LevelMatches(LogLevel level)
        {
            if (Levels == null || Levels.Length == 0)
            {
                return true;
            }

            string name = level == LogLevel.Error ? "error" : level == LogLevel.Warning ? "warning" : "log";
            return Array.IndexOf(Levels, name) >= 0;
        }
    }

    /// <summary>Where an explanation comes from: the Logger's own file, the Orbiters server, or a package's code.</summary>
    internal enum ExplanationOrigin : byte
    {
        Local,
        Remote,
        Custom
    }

    /// <summary>A matched explanation with its placeholders filled from the log.</summary>
    internal sealed class ExplainedLog
    {
        public LogExplanation Explanation;
        public string Title;
        public string Summary;
        public string[] Fixes;
    }

    /// <summary>
    /// Explanations for known logs: the Logger's own list (shipped with it, see <see cref="KnownLogs"/>), the newer list
    /// from the Orbiters server when it could be downloaded (it replaces entries with the same id and adds others), and
    /// what packages add with <see cref="Register"/>, which comes first.
    /// </summary>
    public static class LogExplanations
    {
        private static readonly List<LogExplanation> entries = new List<LogExplanation>();
        private static readonly List<LogExplanation> custom = new List<LogExplanation>();
        private static List<LogExplanation> local;
        private static List<LogExplanation> remote = new List<LogExplanation>();
        private static readonly object gate = new object();

        /// <summary>Bumps when explanations change: cached matches are looked up again.</summary>
        internal static int Version { get; private set; }

        /// <summary>Adds or replaces (same <see cref="LogExplanation.Id"/>) an explanation.</summary>
        public static void Register(LogExplanation explanation)
        {
            if (explanation == null)
            {
                throw new ArgumentNullException(nameof(explanation));
            }

            explanation.Origin = ExplanationOrigin.Custom;
            lock (gate)
            {
                custom.RemoveAll(e => e.Id == explanation.Id);
                custom.Add(explanation);
                Rebuild();
            }
        }

        /// <summary>Replaces the entries from the Orbiters server.</summary>
        internal static void SetRemote(IEnumerable<LogExplanation> explanations)
        {
            var list = new List<LogExplanation>(explanations ?? Array.Empty<LogExplanation>());
            foreach (var explanation in list)
            {
                explanation.Origin = ExplanationOrigin.Remote;
            }

            lock (gate)
            {
                remote = list;
                Rebuild();
            }
        }

        internal static int Count
        {
            get
            {
                lock (gate)
                {
                    EnsureLoaded();
                    return entries.Count;
                }
            }
        }

        internal static int RemoteCount
        {
            get
            {
                lock (gate)
                {
                    return remote.Count;
                }
            }
        }

        internal static LogExplanation Get(int index)
        {
            lock (gate)
            {
                EnsureLoaded();
                return index >= 0 && index < entries.Count ? entries[index] : null;
            }
        }

        /// <summary>The index of the explanation for this log, or -1.</summary>
        internal static int Match(string condition, string stack, LogLevel level)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return -1;
            }

            lock (gate)
            {
                EnsureLoaded();
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.Needle.Length > 0 && condition.IndexOf(entry.Needle, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (!entry.LevelMatches(level))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(entry.StackNeedle) && (stack == null || stack.IndexOf(entry.StackNeedle, StringComparison.Ordinal) < 0))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(entry.Pattern))
                    {
                        var regex = entry.Regex;
                        if (regex == null || !SafeMatch(regex, condition).Success)
                        {
                            continue;
                        }
                    }

                    return i;
                }
            }

            return -1;
        }

        /// <summary>The explanation at <paramref name="index"/> with its placeholders filled from <paramref name="condition"/>.</summary>
        internal static ExplainedLog Explain(int index, string condition)
        {
            var entry = Get(index);
            if (entry == null)
            {
                return null;
            }

            var regex = entry.Regex;
            Match match = regex != null ? SafeMatch(regex, condition ?? string.Empty) : null;
            string Fill(string text)
            {
                if (string.IsNullOrEmpty(text) || match == null || !match.Success || text.IndexOf('{') < 0)
                {
                    return text ?? string.Empty;
                }

                foreach (string name in regex.GetGroupNames())
                {
                    if (char.IsDigit(name[0]))
                    {
                        continue;
                    }

                    var group = match.Groups[name];
                    text = text.Replace("{" + name + "}", group.Success && group.Value.Length > 0 ? group.Value : "…");
                }

                return text;
            }

            var fixes = new string[entry.Fixes?.Length ?? 0];
            for (int i = 0; i < fixes.Length; i++)
            {
                fixes[i] = Fill(entry.Fixes[i]);
            }

            return new ExplainedLog { Explanation = entry, Title = Fill(entry.Title), Summary = Fill(entry.Summary), Fixes = fixes };
        }

        private static Match SafeMatch(Regex regex, string text)
        {
            try
            {
                return regex.Match(text.Length > 4000 ? text.Substring(0, 4000) : text);
            }
            catch (RegexMatchTimeoutException)
            {
                return System.Text.RegularExpressions.Match.Empty;
            }
        }

        private static void EnsureLoaded()
        {
            if (local != null)
            {
                return;
            }

            local = new List<LogExplanation>(KnownLogs.Load());
            Rebuild();
        }

        // Package explanations first, then the ones bound to a tool's stack trace, then by priority, each group in the
        // order of its list (the server's order before the Logger's own).
        private static void Rebuild()
        {
            if (local == null)
            {
                local = new List<LogExplanation>(KnownLogs.Load());
            }

            var merged = new List<LogExplanation>(custom.Count + remote.Count + local.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var list in new[] { custom, remote, local })
            {
                foreach (var entry in list)
                {
                    if (ids.Add(entry.Id))
                    {
                        merged.Add(entry);
                    }
                }
            }

            var order = new Dictionary<LogExplanation, int>(merged.Count);
            for (int i = 0; i < merged.Count; i++)
            {
                order[merged[i]] = i;
            }

            merged.Sort((a, b) =>
            {
                int rank = Rank(a).CompareTo(Rank(b));
                if (rank != 0)
                {
                    return rank;
                }

                int priority = b.Priority.CompareTo(a.Priority);
                return priority != 0 ? priority : order[a].CompareTo(order[b]);
            });

            entries.Clear();
            entries.AddRange(merged);
            Version++;
        }

        private static int Rank(LogExplanation explanation) =>
            (explanation.Origin == ExplanationOrigin.Custom ? 0 : 2) + (string.IsNullOrEmpty(explanation.StackNeedle) ? 1 : 0);
    }
}
