using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Orbiters.Logger.Editor
{
    internal enum ReportStepState
    {
        Waiting,
        Running,
        Done,
        Skipped,
        Failed
    }

    /// <summary>One line of the report's progress: what it does, what it found, how it went.</summary>
    internal sealed class ReportStep
    {
        public readonly string Title;
        public readonly LoggerGlyph Glyph;
        public ReportStepState State;
        public string Detail = string.Empty;

        public ReportStep(string title, LoggerGlyph glyph)
        {
            Title = title;
            Glyph = glyph;
        }
    }

    /// <summary>A file of the finished report, listed in its README.</summary>
    internal readonly struct ReportEntry
    {
        public readonly string Name;
        public readonly string Label;

        public ReportEntry(string name, string label)
        {
            Name = name;
            Label = label;
        }
    }

    /// <summary>
    /// Everything one report run shares: the folder it is written to before being packed, the privacy mask, what each
    /// part found (for the README and the AI) and the files written so far. Writing methods are safe on any thread.
    /// </summary>
    internal sealed class ReportContext
    {
        public readonly string Staging;
        public readonly ReportPrivacy Privacy;
        public readonly string Note;
        public readonly DateTime CreatedLocal;
        public readonly HashSet<ReportPart> Parts;
        public ProjectFacts Project;
        public LogDigest Logs;
        public GitDigest Git;
        public SceneDigest Scene;
        public Diagnosis Diagnosis;
        public string DiagnosisError;
        private readonly List<ReportEntry> entries = new List<ReportEntry>();

        public ReportContext(string staging, ReportPrivacy privacy, string note, IEnumerable<ReportPart> parts)
        {
            Staging = staging;
            Privacy = privacy;
            Note = privacy.Mask((note ?? string.Empty).Trim());
            CreatedLocal = DateTime.Now;
            Parts = new HashSet<ReportPart>(parts);
        }

        public bool Has(ReportPart part) => Parts.Contains(part);

        public IReadOnlyList<ReportEntry> Entries
        {
            get
            {
                lock (entries)
                {
                    return entries.ToArray();
                }
            }
        }

        public string PathOf(string name)
        {
            string path = Path.Combine(Staging, name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return path;
        }

        public void Add(string name, string label)
        {
            lock (entries)
            {
                entries.Add(new ReportEntry(name, label));
            }
        }

        /// <summary>Writes <paramref name="text"/>, masked, as <paramref name="name"/> ("git/log.txt").</summary>
        public void WriteText(string name, string text, string label)
        {
            File.WriteAllText(PathOf(name), Privacy.Mask(text), new UTF8Encoding(false));
            Add(name, label);
        }

        /// <summary>A masked text file written line by line (logs that can be big).</summary>
        public ReportWriter OpenText(string name, string label)
        {
            Add(name, label);
            return new ReportWriter(PathOf(name), Privacy);
        }
    }

    /// <summary>A text file of the report written in pieces, each masked.</summary>
    internal sealed class ReportWriter : IDisposable
    {
        private readonly StreamWriter writer;
        private readonly ReportPrivacy privacy;

        public ReportWriter(string path, ReportPrivacy privacy)
        {
            this.privacy = privacy;
            writer = new StreamWriter(path, false, new UTF8Encoding(false), 1 << 16) { NewLine = "\n" };
        }

        public long Length => writer.BaseStream.Length;

        public void Line(string text = "") => writer.WriteLine(privacy.Mask(text));

        public void Write(string text) => writer.Write(privacy.Mask(text));

        public void Dispose() => writer.Dispose();
    }
}
