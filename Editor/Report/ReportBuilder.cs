using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// Makes one project report: each part is a step (the scene's in the editor, files and Git on worker threads, so
    /// the editor keeps drawing), the AI diagnosis runs while the scene is exported, and everything is packed in one
    /// zip file. A part that fails is marked and the report is still made; only packing can fail the report.
    /// </summary>
    internal sealed class ReportBuilder
    {
        private readonly ReportContext context;
        private readonly string destination;

        /// <summary>Where the report will be saved.</summary>
        public string Destination => destination;
        // The steps as nested coroutines: a step yielding another one runs it to its end first.
        private readonly Stack<IEnumerator> routine = new Stack<IEnumerator>();
        private readonly ReportStep project;
        private readonly ReportStep logs;
        private readonly ReportStep git;
        private readonly ReportStep scene;
        private readonly ReportStep export;
        private readonly ReportStep diagnosis;
        private readonly ReportStep packing;
        private DiagnosisClient client;
        private string failure;

        public readonly List<ReportStep> Steps = new List<ReportStep>();

        /// <summary>From 0 to 1, for the progress bar.</summary>
        public float Progress { get; private set; }

        public bool Finished { get; private set; }

        /// <summary>When it finished (editor time).</summary>
        public double FinishedAt { get; private set; }
        public bool Cancelled { get; private set; }

        /// <summary>The report file once made.</summary>
        public string Result { get; private set; }

        public long ResultBytes { get; private set; }

        /// <summary>Why the report could not be made.</summary>
        public string Error { get; private set; }

        public Diagnosis Diagnosis => context.Diagnosis;
        public string DiagnosisError => context.DiagnosisError;
        public IReadOnlyList<LogDigestGroup> Groups => context.Logs?.Groups;
        public ReportContext Context => context;

        /// <summary>The diagnosis is being asked again (after a failure), outside the report file.</summary>
        public bool AskingAgain => retry != null;

        private DiagnosisClient retry;

        /// <summary>
        /// Asks for the diagnosis again from what the report found (signed in since, or Orbiters was busy). The answer
        /// shows in the window; the report file keeps what it had.
        /// </summary>
        public void AskAgain()
        {
            if (retry != null || !Finished)
            {
                return;
            }

            context.Diagnosis = null;
            context.DiagnosisError = null;
            DiagnosisInFile = false;
            retry = DiagnosisClient.Start(context);
            EditorApplication.update += PollRetry;
            Changed?.Invoke();
        }

        private void PollRetry()
        {
            if (retry == null || !retry.Poll())
            {
                return;
            }

            EditorApplication.update -= PollRetry;
            context.Diagnosis = retry.Error == null ? retry.Result : null;
            context.DiagnosisError = retry.Error;
            retry = null;
            if (context.Diagnosis != null)
            {
                Complete(diagnosis, LoggerUi.Plural(context.Diagnosis.causes?.Length ?? 0, "likely cause"));
                AddDiagnosisToFile();
            }

            Changed?.Invoke();
        }

        private const long MaxRewrittenZip = 256L * 1024 * 1024;

        /// <summary>Whether the report file holds the diagnosis shown (false while it is being added).</summary>
        public bool DiagnosisInFile { get; private set; }

        // The diagnosis asked again goes into the report file too: diagnosis.md and a README that includes it. Updating a
        // zip rewrites it, so a big one (a scene export) keeps what it had.
        private void AddDiagnosisToFile()
        {
            if (Result == null || ResultBytes > MaxRewrittenZip)
            {
                return;
            }

            const string name = "diagnosis.md";
            if (!context.Entries.Any(e => e.Name == name))
            {
                context.Add(name, "What Orbiters' AI thinks is wrong, and how to fix it");
            }

            string markdown = context.Privacy.Mask(DiagnosisClient.Markdown(context.Diagnosis, context.Logs?.Groups));
            string readme = context.Privacy.Mask(ReportReadme.Write(context, Steps));
            string path = Result;
            var update = Task.Run(() =>
            {
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    Replace(zip, name, markdown);
                    Replace(zip, "README.md", readme);
                }

                return new FileInfo(path).Length;
            });
            void Wait()
            {
                if (!update.IsCompleted)
                {
                    return;
                }

                EditorApplication.update -= Wait;
                if (update.Status == TaskStatus.RanToCompletion)
                {
                    ResultBytes = update.Result;
                    DiagnosisInFile = true;
                    Changed?.Invoke();
                }
            }

            EditorApplication.update += Wait;
        }

        private static void Replace(ZipArchive zip, string name, string text)
        {
            zip.GetEntry(name)?.Delete();
            using (var writer = new StreamWriter(zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal).Open(), new UTF8Encoding(false)))
            {
                writer.Write(text);
            }
        }

        public event Action Changed;

        private ReportBuilder(ReportContext context, string destination)
        {
            this.context = context;
            this.destination = destination;
            project = Add("Project details", LoggerGlyph.Info, true);
            logs = Add("Logs", LoggerGlyph.List, context.Has(ReportPart.Logs));
            git = Add("Git history", LoggerGlyph.Branch, context.Has(ReportPart.Git));
            scene = Add("Scene description", LoggerGlyph.Hierarchy, context.Has(ReportPart.Scene));
            diagnosis = Add("AI diagnosis", LoggerGlyph.Sparkle, context.Has(ReportPart.Diagnosis));
            export = Add("Scene export", LoggerGlyph.Package, context.Has(ReportPart.SceneExport));
            packing = Add("Packing the report", LoggerGlyph.Folder, true);
            routine.Push(Run());
        }

        private ReportStep Add(string title, LoggerGlyph glyph, bool included)
        {
            var step = new ReportStep(title, glyph);
            if (!included)
            {
                step.State = ReportStepState.Skipped;
                step.Detail = "Not included";
            }

            Steps.Add(step);
            return step;
        }

        /// <summary>Starts a report of <paramref name="parts"/> saved in <paramref name="folder"/>.</summary>
        internal static ReportBuilder Start(IEnumerable<ReportPart> parts, string note, string folder)
        {
            string staging = Path.Combine(Path.GetFullPath("Temp"), "OrbitersReport-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(staging);
            var context = new ReportContext(staging, ReportPrivacy.ForThisComputer(), note, parts);
            var builder = new ReportBuilder(context, ReportOptions.FreePath(folder, context.CreatedLocal));
            EditorApplication.update += builder.Tick;
            AssemblyReloadEvents.beforeAssemblyReload += builder.Cancel;
            return builder;
        }

        public void Cancel()
        {
            if (Finished)
            {
                return;
            }

            Cancelled = true;
            Error = "Cancelled";
            End();
        }

        private void Tick()
        {
            if (Finished)
            {
                return;
            }

            try
            {
                var current = routine.Peek();
                if (current.MoveNext())
                {
                    if (current.Current is IEnumerator inner)
                    {
                        routine.Push(inner);
                    }
                }
                else
                {
                    routine.Pop();
                    if (routine.Count == 0)
                    {
                        End();
                    }
                }
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                Fail(Steps.FirstOrDefault(s => s.State == ReportStepState.Running) ?? packing, exception.Message);
                End();
            }

            Changed?.Invoke();
        }

        private void End()
        {
            if (Finished)
            {
                return;
            }

            Finished = true;
            FinishedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            client?.Dispose();
            foreach (var step in Steps.Where(s => s.State == ReportStepState.Running || s.State == ReportStepState.Waiting))
            {
                step.State = ReportStepState.Skipped;
                step.Detail = Cancelled ? "Cancelled" : step.Detail;
            }

            Task.Run(() => Delete(context.Staging));
            Changed?.Invoke();
        }

        private IEnumerator Run()
        {
            // Project details (main thread: Unity's settings).
            Begin(project, 0.02f);
            yield return null;
            context.Project = ProjectFacts.Gather();
            yield return Work(project, () =>
            {
                context.Project.Write(context);
                return "Unity " + context.Project.Unity + " · " + LoggerUi.Plural(context.Project.Packages.Count, "package");
            }, 0.02f, 0.06f);

            // Logs: the digest is always made, the README and the AI use it.
            context.Logs = LogReport.Digest(LogCapture.Store);
            if (context.Has(ReportPart.Logs))
            {
                Begin(logs, 0.06f);
                var snapshot = LogReport.Take(LogCapture.Store);
                yield return Work(logs, () => LogReport.Write(context, snapshot, p => Progress = Mathf.Lerp(0.06f, 0.36f, p)), 0.06f, 0.36f);
            }

            // Git (worker: Git commands).
            if (context.Has(ReportPart.Git))
            {
                Begin(git, 0.36f);
                string root = GitCli.ProjectRoot;
                var extras = ReportOptions.GitExtras.SelectMany(extra =>
                {
                    try
                    {
                        return extra(root) ?? Enumerable.Empty<ReportExtra>();
                    }
                    catch (Exception)
                    {
                        return Enumerable.Empty<ReportExtra>();
                    }
                }).ToList();
                yield return Work(git, () => GitReport.Write(context, root, extras, p => Progress = Mathf.Lerp(0.36f, 0.5f, p)), 0.36f, 0.5f);
            }

            // Scene description: the hierarchy on the main thread, the file tree on a worker.
            if (context.Has(ReportPart.Scene))
            {
                Begin(scene, 0.5f);
                yield return null;
                string summary = Guard(scene, () => SceneReport.WriteHierarchy(context));
                if (summary != null)
                {
                    scene.Detail = summary + " · reading the files it uses…";
                    Changed?.Invoke();
                    yield return null;
                    var files = SceneReport.Dependencies(context.Scene);
                    yield return Work(scene, () =>
                    {
                        SceneReport.Measure(files);
                        SceneReport.WriteFiles(context, files);
                        return summary + " · " + LoggerUi.Plural(files.Paths.Length, "file") + " used";
                    }, 0.5f, 0.6f);
                }
            }
            else if (context.Has(ReportPart.SceneExport))
            {
                // The export needs the scene paths, without the hierarchy file.
                context.Scene = SceneReport.Look();
            }

            // The AI reads what the steps found while the scene is exported.
            if (context.Has(ReportPart.Diagnosis))
            {
                Begin(diagnosis, 0.6f);
                diagnosis.Detail = "Orbiters is reading the report…";
                client = DiagnosisClient.Start(context);
            }

            if (context.Has(ReportPart.SceneExport))
            {
                Begin(export, 0.62f);
                export.Detail = "Exporting with every file it uses…";
                Changed?.Invoke();
                // Two frames: the step shows as running before Unity's export holds the editor.
                yield return null;
                yield return null;
                string done = Guard(export, () => SceneReport.Export(context, context.Scene ?? SceneReport.Look()));
                if (done != null)
                {
                    Complete(export, done);
                }
            }

            if (client != null)
            {
                Progress = Mathf.Max(Progress, 0.85f);
                double started = EditorApplication.timeSinceStartup;
                while (!client.Poll())
                {
                    diagnosis.Detail = "Orbiters is reading the report… " + (int)(EditorApplication.timeSinceStartup - started) + " s";
                    yield return null;
                }

                context.Diagnosis = client.Result;
                context.DiagnosisError = client.Error;
                if (client.Result != null && client.Error == null)
                {
                    context.WriteText("diagnosis.md", DiagnosisClient.Markdown(client.Result, context.Logs?.Groups), "What Orbiters' AI thinks is wrong, and how to fix it");
                    DiagnosisInFile = true;
                    Complete(diagnosis, LoggerUi.Plural(client.Result.causes?.Length ?? 0, "likely cause"));
                }
                else
                {
                    Fail(diagnosis, client.Error);
                }
            }

            // Packing: the README, then the zip (worker).
            Begin(packing, 0.9f);
            yield return Work(packing, () =>
            {
                context.WriteText("README.md", ReportReadme.Write(context, Steps), "This file");
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                ZipFile.CreateFromDirectory(context.Staging, destination, System.IO.Compression.CompressionLevel.Optimal, false);
                ResultBytes = new FileInfo(destination).Length;
                return SceneReport.Size(ResultBytes);
            }, 0.9f, 1f);

            if (packing.State == ReportStepState.Failed)
            {
                Error = failure;
                yield break;
            }

            Result = destination;
            Progress = 1f;
        }

        private void Begin(ReportStep step, float progress)
        {
            step.State = ReportStepState.Running;
            Progress = Mathf.Max(Progress, progress);
            Changed?.Invoke();
        }

        private void Complete(ReportStep step, string detail)
        {
            step.State = ReportStepState.Done;
            step.Detail = detail ?? string.Empty;
            Changed?.Invoke();
        }

        private void Fail(ReportStep step, string error)
        {
            step.State = ReportStepState.Failed;
            step.Detail = string.IsNullOrEmpty(error) ? "Failed" : error;
            failure = step.Detail;
            Changed?.Invoke();
        }

        // Runs the main-thread part of a step; a failure marks the step and returns null.
        private string Guard(ReportStep step, Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception exception)
            {
                Fail(step, exception.Message);
                return null;
            }
        }

        // Runs a step's work on a worker thread, waiting a frame at a time.
        private IEnumerator Work(ReportStep step, Func<string> work, float from, float to)
        {
            var task = Task.Run(work);
            while (!task.IsCompleted)
            {
                if (Cancelled)
                {
                    yield break;
                }

                yield return null;
            }

            if (task.IsFaulted)
            {
                var error = task.Exception?.GetBaseException();
                Fail(step, error?.Message ?? "Failed");
            }
            else
            {
                Complete(step, task.Result);
            }

            Progress = Mathf.Max(Progress, to);
        }

        private static void Delete(string folder)
        {
            try
            {
                if (Directory.Exists(folder) && folder.Contains("OrbitersReport-"))
                {
                    Directory.Delete(folder, true);
                }
            }
            catch (Exception)
            {
                // Unity empties Temp when it closes.
            }
        }
    }
}
