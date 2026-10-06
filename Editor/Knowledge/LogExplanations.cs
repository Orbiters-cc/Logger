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
        public string Summary { get; set; } = string.Empty;
        public string[] Fixes { get; set; } = Array.Empty<string>();
        /// <summary>Where the log comes from, as people know it ("VRChat SDK", "Unity", "C# compiler").</summary>
        public string Source { get; set; } = string.Empty;
        public LogExplanationSeverity Severity { get; set; } = LogExplanationSeverity.Info;
        public string Link { get; set; }
        public string LinkLabel { get; set; }

        internal Regex Regex;
        internal bool Custom;

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

    /// <summary>A matched explanation with its placeholders filled from the log.</summary>
    internal sealed class ExplainedLog
    {
        public LogExplanation Explanation;
        public string Title;
        public string Summary;
        public string[] Fixes;
    }

    /// <summary>
    /// Explanations for known logs. The Logger ships explanations for common Unity, C# compiler and VRChat SDK messages;
    /// other packages add their own with <see cref="Register"/>, which take precedence over the built-in ones.
    /// </summary>
    public static class LogExplanations
    {
        private static readonly List<LogExplanation> entries = new List<LogExplanation>();
        private static readonly object gate = new object();
        private static bool builtInsLoaded;

        /// <summary>Bumps when explanations are added: cached matches are looked up again.</summary>
        internal static int Version { get; private set; }

        /// <summary>Adds or replaces (same <see cref="LogExplanation.Id"/>) an explanation.</summary>
        public static void Register(LogExplanation explanation)
        {
            if (explanation == null)
            {
                throw new ArgumentNullException(nameof(explanation));
            }

            explanation.Custom = true;
            lock (gate)
            {
                EnsureBuiltIns();
                Add(explanation);
            }
        }

        internal static int Count
        {
            get
            {
                lock (gate)
                {
                    EnsureBuiltIns();
                    return entries.Count;
                }
            }
        }

        internal static LogExplanation Get(int index)
        {
            lock (gate)
            {
                EnsureBuiltIns();
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
                EnsureBuiltIns();
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

                    if (entry.Regex != null && !SafeMatch(entry.Regex, condition).Success)
                    {
                        continue;
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

            Match match = entry.Regex != null ? SafeMatch(entry.Regex, condition ?? string.Empty) : null;
            string Fill(string text)
            {
                if (string.IsNullOrEmpty(text) || match == null || !match.Success || text.IndexOf('{') < 0)
                {
                    return text ?? string.Empty;
                }

                foreach (string name in entry.Regex.GetGroupNames())
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

        private static void Add(LogExplanation explanation)
        {
            if (!string.IsNullOrEmpty(explanation.Pattern) && explanation.Regex == null)
            {
                explanation.Regex = new Regex(explanation.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            }

            entries.RemoveAll(e => e.Id == explanation.Id);
            // Custom explanations first, then the most specific (bound to a stack trace), then the rest in order.
            int position = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                if (Rank(explanation) < Rank(entries[i]))
                {
                    position = i;
                    break;
                }
            }

            entries.Insert(position, explanation);
            Version++;
        }

        private static int Rank(LogExplanation explanation) =>
            (explanation.Custom ? 0 : 2) + (string.IsNullOrEmpty(explanation.StackNeedle) ? 1 : 0);

        private static void EnsureBuiltIns()
        {
            if (builtInsLoaded)
            {
                return;
            }

            builtInsLoaded = true;
            foreach (var explanation in KnownLogs.All())
            {
                Add(explanation);
            }
        }
    }
}
