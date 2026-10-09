using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Orbiters.Logger.Editor
{
    /// <summary>What a Git command printed and how it ended.</summary>
    internal readonly struct GitResult
    {
        public readonly bool Success;
        public readonly string Output;
        public readonly string Error;

        public GitResult(bool success, string output, string error)
        {
            Success = success;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
        }
    }

    /// <summary>
    /// Runs Git in the project without a window, for the timeline and the project report. Call it from a worker thread:
    /// it waits for Git to finish (or the timeout to pass, then stops it).
    /// </summary>
    internal static class GitCli
    {
        internal static string ProjectRoot
        {
            get
            {
                try
                {
                    return Path.GetFullPath(".");
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Whether <paramref name="root"/> is a Git work tree (a ".git" folder, or a file in a linked worktree).</summary>
        internal static bool IsRepository(string root) =>
            !string.IsNullOrEmpty(root) && (Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git")));

        internal static GitResult Run(string root, string arguments, int timeoutMs = 15000)
        {
            var start = new ProcessStartInfo("git", "-C \"" + root + "\" -c core.quotepath=off " + arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            try
            {
                using (var process = Process.Start(start))
                {
                    if (process == null)
                    {
                        return new GitResult(false, null, "Git could not be started.");
                    }

                    // Both streams read at once: a full stderr pipe would otherwise block Git while stdout is read.
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // Already gone.
                        }

                        return new GitResult(false, null, "Git took longer than " + timeoutMs / 1000 + " s.");
                    }

                    Task.WaitAll(new Task[] { output, error }, 5000);
                    return new GitResult(process.ExitCode == 0, output.IsCompleted ? output.Result : null, error.IsCompleted ? error.Result : null);
                }
            }
            catch (Exception exception)
            {
                return new GitResult(false, null, "Git is not available: " + exception.Message);
            }
        }
    }
}
