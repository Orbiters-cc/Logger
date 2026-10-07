using NUnit.Framework;

namespace Orbiters.Logger.Editor.Tests
{
    public class LogExplanationTests
    {
        private static string IdOf(string condition, string stack = "", LogLevel level = LogLevel.Error)
        {
            int index = LogExplanations.Match(condition, stack, level);
            return index >= 0 ? LogExplanations.Get(index).Id : null;
        }

        [Test]
        public void CommonUnityMessages()
        {
            Assert.AreEqual("csharp.null-reference", IdOf("NullReferenceException: Object reference not set to an instance of an object"));
            Assert.AreEqual("unity.imgui-layout", IdOf("ArgumentException: Getting control 1's position in a group with only 1 controls when doing repaint"));
            Assert.AreEqual("unity.tls-allocator", IdOf("TLS Allocator ALLOC_TEMP_TLS, underlying allocator ALLOC_TEMP_MAIN has unfreed allocations, size 37", level: LogLevel.Warning));
            Assert.AreEqual("net.insecure", IdOf("Non-secure network connections disabled in Player Settings", level: LogLevel.Warning));
            Assert.AreEqual("asset.material-property", IdOf("Material 'Body' with Shader 'Standard' doesn't have a texture property '_EmissionMap'"));
            Assert.IsNull(IdOf("Everything is fine, nothing to see"));
        }

        [Test]
        public void SpecificBeforeGeneral()
        {
            const string destroyed = "MissingReferenceException: The object of type 'VRCAvatarDescriptor' has been destroyed but you are still trying to access it.";
            Assert.AreEqual("vrchat.sdk-panel-destroyed-avatar", IdOf(destroyed, "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder.get_SelectedAvatar ()"));
            Assert.AreEqual("unity.destroyed", IdOf(destroyed, "Some.Other:Code ()"));
            Assert.AreEqual("compiler.cs0246", IdOf("Library/PackageCache/com.foo@1.0.0/Editor/A.cs(3,7): error CS0246: The type or namespace name 'VRC' could not be found"));
            Assert.AreEqual("compiler.package", IdOf("Library/PackageCache/com.foo@1.0.0/Editor/A.cs(3,7): error CS1002: ; expected"));
            Assert.AreEqual("compiler.error", IdOf("Assets/A.cs(3,7): error CS1002: ; expected"));
        }

        [Test]
        public void PlaceholdersComeFromTheMessage()
        {
            const string missing = "The referenced script (Unknown) on this Behaviour (Game Object 'Body') is missing!";
            int index = LogExplanations.Match(missing, string.Empty, LogLevel.Warning);
            var explained = LogExplanations.Explain(index, missing);
            Assert.AreEqual("Missing script on 'Body'", explained.Title);

            const string compile = "Assets/A.cs(1,1): error CS0246: The type or namespace name 'VRCFury' could not be found (are you missing a using directive?)";
            explained = LogExplanations.Explain(LogExplanations.Match(compile, string.Empty, LogLevel.Error), compile);
            Assert.AreEqual("Unknown type or namespace 'VRCFury'", explained.Title);
            StringAssert.Contains("'VRCFury'", explained.Fixes[0]);
        }

        [Test]
        public void RegisteredExplanationsComeFirst()
        {
            LogExplanations.Register(new LogExplanation("tests.custom-null", "NullReferenceException", "Custom")
            {
                StackNeedle = "MyTool.",
                Summary = "From a test."
            });
            try
            {
                Assert.AreEqual("tests.custom-null", IdOf("NullReferenceException: x", "MyTool.Run ()"));
                Assert.AreEqual("csharp.null-reference", IdOf("NullReferenceException: x", "Other.Run ()"));
            }
            finally
            {
                // Replace it with an entry that can never match, so other tests see the built-ins only.
                LogExplanations.Register(new LogExplanation("tests.custom-null", "\u0001never\u0001", "Unused"));
            }
        }

        [Test]
        public void LevelsRestrictMatches()
        {
            Assert.AreEqual("vrchat.vrcfury-error", IdOf("VRCFury failed to build the avatar", level: LogLevel.Error));
            Assert.AreNotEqual("vrchat.vrcfury-error", IdOf("VRCFury failed to build the avatar", level: LogLevel.Info));
            Assert.IsNull(IdOf("Something unknown is building", level: LogLevel.Info));
        }

        [Test]
        public void TheSdkOwnershipErrorIsExplained()
        {
            Assert.AreEqual("vrchat.avatar-not-yours",
                IdOf("Failed to load avatar data: Avatar's current ID belongs to a different user, assign a different ID"));
        }

        [Test]
        public void ServerEntriesReplaceTheirLocalCopyAndPriorityOrdersThem()
        {
            var entries = KnownLogs.Parse(@"{ ""explanations"": [
                { ""id"": ""tests.broad"", ""needle"": ""ZZ-probe"", ""priority"": -10, ""severity"": ""info"", ""source"": ""Tests"", ""title"": ""Broad"" },
                { ""id"": ""tests.exact"", ""needle"": ""ZZ-probe exact"", ""severity"": ""problem"", ""source"": ""Tests"", ""title"": ""Exact {what}"",
                  ""pattern"": ""ZZ-probe exact (?<what>\\w+)"" },
                { ""id"": ""csharp.null-reference"", ""needle"": ""NullReferenceException"", ""severity"": ""warning"", ""source"": ""Tests"", ""title"": ""From the server"" }
            ] }");
            Assert.AreEqual(3, entries.Count);
            try
            {
                LogExplanations.SetRemote(entries);
                Assert.AreEqual("tests.exact", IdOf("ZZ-probe exact thing"));
                Assert.AreEqual("tests.broad", IdOf("ZZ-probe other"));
                Assert.AreEqual("Exact thing", LogExplanations.Explain(LogExplanations.Match("ZZ-probe exact thing", "", LogLevel.Error), "ZZ-probe exact thing").Title);
                Assert.AreEqual("From the server", LogExplanations.Get(LogExplanations.Match("NullReferenceException: x", "", LogLevel.Error)).Title);
            }
            finally
            {
                LogExplanations.SetRemote(null);
            }

            Assert.AreEqual("csharp.null-reference", IdOf("NullReferenceException: x"));
            Assert.AreNotEqual("From the server", LogExplanations.Get(LogExplanations.Match("NullReferenceException: x", "", LogLevel.Error)).Title);
        }

        [Test]
        public void AnInvalidPatternNeverMatches()
        {
            var entries = KnownLogs.Parse(@"{ ""explanations"": [
                { ""id"": ""tests.invalid"", ""needle"": ""ZZ-invalid"", ""pattern"": ""(unclosed"", ""severity"": ""info"", ""source"": ""Tests"", ""title"": ""Invalid"" } ] }");
            try
            {
                LogExplanations.SetRemote(entries);
                Assert.IsNull(IdOf("ZZ-invalid here"));
            }
            finally
            {
                LogExplanations.SetRemote(null);
            }
        }
    }
}
