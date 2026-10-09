using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Orbiters.Logger.Editor.Tests
{
    public class ProjectReportTests
    {
        private const string Home = @"C:\Users\Jane Doe";
        private const string Token = "orbit-0123abcd-4567ef01-89abcdef-08-10-2026";

        private static ReportPrivacy Privacy() => new ReportPrivacy(Home, "s3cret-session-value");

        [Test]
        public void MasksTheUserFolderInEverySpelling()
        {
            var privacy = Privacy();
            Assert.AreEqual(@"%USERPROFILE%\AppData\Local\Unity\Editor.log", privacy.Mask(@"C:\Users\Jane Doe\AppData\Local\Unity\Editor.log"));
            Assert.AreEqual("%USERPROFILE%/Desktop/a.png", privacy.Mask("C:/Users/Jane Doe/Desktop/a.png"));
            Assert.AreEqual("%USERPROFILE%/x", privacy.Mask("c:/users/jane doe/x"));
            Assert.AreEqual("{\"path\":\"%USERPROFILE%\\\\Temp\"}", privacy.Mask("{\"path\":\"C:\\\\Users\\\\Jane Doe\\\\Temp\"}"));
            Assert.AreEqual(@"D:\Projects\Avatar", privacy.Mask(@"D:\Projects\Avatar"));
        }

        [Test]
        public void MasksTokensAndPasswords()
        {
            var privacy = Privacy();
            Assert.AreEqual("token [masked] used", privacy.Mask("token " + Token + " used"));
            Assert.AreEqual("Authorization: Bearer [masked]", privacy.Mask("Authorization: Bearer abcdefghijklmnopqrstuvwxyz0123"));
            Assert.AreEqual("origin https://[masked]@github.com/me/repo.git (fetch)", privacy.Mask("origin https://me:ghp_secret@github.com/me/repo.git (fetch)"));
            Assert.AreEqual("value [masked]!", privacy.Mask("value s3cret-session-value!"));
            Assert.AreEqual("git@github.com:me/repo.git", privacy.Mask("git@github.com:me/repo.git"));
            Assert.AreEqual(string.Empty, privacy.Mask(null));
        }

        [Test]
        public void ShortValuesAreNeverMasked()
        {
            // A user folder like "C:\" or a two-letter secret would mask half of every file.
            var privacy = new ReportPrivacy(@"C:\", "ab");
            Assert.AreEqual(@"C:\Program Files ab", privacy.Mask(@"C:\Program Files ab"));
        }

        [Test]
        public void ReportFileNamesAreSafeAndUnique()
        {
            var when = new DateTime(2026, 10, 8, 14, 32, 0);
            string name = ReportOptions.FileName(when);
            StringAssert.EndsWith(" report 2026-10-08 14-32.zip", name);
            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));

            string folder = Path.Combine(Path.GetTempPath(), "LoggerReportTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, name), string.Empty);
                StringAssert.EndsWith(" report 2026-10-08 14-32 (2).zip", ReportOptions.FreePath(folder, when));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void StackTopsSkipUnityLoggingFrames()
        {
            string stack = "UnityEngine.Debug:LogError (object)\nUnityEngine.Logger:Log ()\nMy.Tool:Run () (at Assets/Tool.cs:12)\n\nMy.Tool:Main ()\n";
            Assert.AreEqual("My.Tool:Run () (at Assets/Tool.cs:12)\nMy.Tool:Main ()", LogReport.TopOfStack(stack));
            Assert.AreEqual(string.Empty, LogReport.TopOfStack("  "));
            var many = string.Join("\n", Enumerable.Range(0, 40).Select(i => "Frame" + i + " ()"));
            Assert.AreEqual(14, LogReport.TopOfStack(many).Split('\n').Length);
        }

        [Test]
        public void SizesReadNaturally()
        {
            Assert.AreEqual("512 B", SceneReport.Size(512));
            Assert.AreEqual("1.5 KB", SceneReport.Size(1536));
            Assert.AreEqual("48 MB", SceneReport.Size(48L * 1024 * 1024));
            Assert.AreEqual("1.0 GB", SceneReport.Size(1024L * 1024 * 1024));
        }

        private static ReportContext Context(string note = "It broke in C:\\Users\\Jane Doe\\Desktop")
        {
            var context = new ReportContext(Path.GetTempPath(), Privacy(), note, new[] { ReportPart.Diagnosis, ReportPart.Logs });
            context.Project = new ProjectFacts { Name = "Avatar", Unity = "2022.3.22f1", Platform = "StandaloneWindows64", OperatingSystem = "Windows 11", RenderPipeline = "Built-in" };
            context.Project.Packages.Add(new System.Collections.Generic.KeyValuePair<string, string>("com.vrchat.avatars", "3.10.3"));
            context.Logs = new LogDigest { Total = 10, Errors = 2, Warnings = 1 };
            context.Logs.Groups.Add(new LogDigestGroup
            {
                Id = "g0", Level = LogLevel.Error, Count = 2, Source = "Compiler", Text = new string('x', 3000), Stack = "at " + Home + "\\A.cs",
                FirstSeen = DateTime.UtcNow.Ticks, LastSeen = DateTime.UtcNow.Ticks
            });
            return context;
        }

        [Test]
        public void DiagnosisRequestsAreMaskedAndFitTheServer()
        {
            var request = DiagnosisClient.BuildRequest(Context());
            Assert.AreEqual("It broke in %USERPROFILE%\\Desktop", request.note);
            Assert.AreEqual(1500, request.logs.groups[0].text.Length);
            StringAssert.Contains("%USERPROFILE%", request.logs.groups[0].stack);
            Assert.AreEqual("error", request.logs.groups[0].level);
            Assert.AreEqual("(not included)", request.git.branch);
            Assert.AreEqual("(not included)", request.scene.name);
            string json = JsonUtility.ToJson(request);
            StringAssert.Contains("\"packages\":[{\"name\":\"com.vrchat.avatars\",\"version\":\"3.10.3\"}]", json);
            StringAssert.DoesNotContain("Jane Doe", json);
        }

        [Test]
        public void DiagnosesReadAsMarkdownWithTheirLogs()
        {
            var context = Context();
            var diagnosis = JsonUtility.FromJson<Diagnosis>("{\"headline\":\"A script does not compile\",\"summary\":\"Fix it.\",\"causes\":[" +
                                                           "{\"title\":\"Foo.cs\",\"likelihood\":\"high\",\"explanation\":\"CS0246\",\"steps\":[\"Open Foo.cs\",\"Add the using\"],\"related\":[\"g0\",\"g7\"]}]}");
            string markdown = DiagnosisClient.Markdown(diagnosis, context.Logs.Groups, 2);
            StringAssert.StartsWith("## AI diagnosis\n\n**A script does not compile**", markdown);
            StringAssert.Contains("### 1. Foo.cs  (high likelihood)", markdown);
            StringAssert.Contains("1. Open Foo.cs\n2. Add the using\n", markdown);
            Assert.AreEqual(1, markdown.Split(new[] { "\n- `" }, StringSplitOptions.None).Length - 1, "only the log this report sent");

            context.Diagnosis = diagnosis;
            string readme = ReportReadme.Write(context, new[] { new ReportStep("Git history", LoggerGlyph.Branch) { State = ReportStepState.Failed, Detail = "Git is not installed" } });
            StringAssert.Contains("## What's going wrong\n\n> It broke in %USERPROFILE%\\Desktop", readme);
            StringAssert.Contains("## AI diagnosis", readme);
            StringAssert.Contains("## The messages that matter most", readme);
            StringAssert.Contains("- Git history: Git is not installed", readme);
        }
    }
}
