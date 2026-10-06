using System.Collections.Generic;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// Built-in explanations: the Unity, C# compiler and VRChat SDK messages VRChat creators meet most. Specific entries
    /// (bound to a tool's stack trace) are tried before general ones.
    /// </summary>
    internal static class KnownLogs
    {
        private const LogExplanationSeverity Harmless = LogExplanationSeverity.Harmless;
        private const LogExplanationSeverity Info = LogExplanationSeverity.Info;
        private const LogExplanationSeverity Warning = LogExplanationSeverity.Warning;
        private const LogExplanationSeverity Problem = LogExplanationSeverity.Problem;
        private const LogExplanationSeverity Blocking = LogExplanationSeverity.Blocking;

        private const string Vcc = "https://vcc.docs.vrchat.com/";
        private const string VrcCreators = "https://creators.vrchat.com/";

        public static IEnumerable<LogExplanation> All()
        {
            // ---- VRChat SDK -------------------------------------------------------------------------------------

            yield return E("vrchat.sdk-panel-destroyed-avatar", "The object of type 'VRCAvatarDescriptor' has been destroyed",
                    "The VRChat SDK panel still points to a deleted avatar", Warning, "VRChat SDK",
                    "The SDK panel's Builder tab was showing an avatar that no longer exists: a build or test copy, or an avatar you deleted. " +
                    "Something asks the panel for that avatar on every editor update, so the error repeats very fast. Your project is not damaged.",
                    "Select another avatar in the Builder tab of the VRChat SDK panel, or close and reopen the panel.",
                    "If My Avatar appears in the stack trace, update it to 0.9.1 or newer: it no longer asks the panel on every update.",
                    "Clear the log once it stops: millions of repeats slow Unity's own Console down.")
                .Stack("VRCSdkControlPanelAvatarBuilder");

            yield return E("vrchat.spine-hierarchy", "Spine hierarchy missing elements",
                    "The humanoid rig is missing spine bones", Blocking, "VRChat SDK",
                    "VRChat needs a humanoid avatar with a full chain from Hips through Spine and Chest to Neck and Head. The model's humanoid mapping leaves some of them out.",
                    "Select the avatar's model (the FBX), open Rig › Configure… and map the missing bones (often Chest or Upper Chest).",
                    "Apply, then select the avatar again in the VRChat SDK panel.")
                .Link(VrcCreators + "avatars/", "VRChat avatar docs");

            yield return E("vrchat.blueprint", "lueprint",
                    "The avatar's blueprint ID can't be used", Problem, "VRChat SDK",
                    "The blueprint ID on the avatar's Pipeline Manager belongs to another account or to an avatar that no longer exists, so the SDK can't update it.",
                    "Select the avatar root, find the Pipeline Manager component and click Detach to clear the blueprint ID.",
                    "Upload again: the avatar is published as a new one.")
                .Pattern(@"(?i)blueprint ?id.*(belong|another user|not own|not found|doesn't exist|does not exist|invalid)");

            yield return E("vrchat.uncompressed-size", "ncompressed size",
                    "The avatar is too big once loaded", Blocking, "VRChat SDK",
                    "VRChat limits how much memory an avatar takes once downloaded and unpacked (textures and meshes count most).",
                    "Lower texture sizes or use crunch/compressed formats (My Avatar's texture optimization does this in one click).",
                    "Remove unused meshes and blendshapes, and check accessories you added for very large textures.")
                .Pattern(@"(?i)(avatar|upload).*uncompressed size|uncompressed size.*(avatar|limit|too large|exceed)");

            yield return E("vrchat.download-size", "ownload size",
                    "The avatar download is too big", Blocking, "VRChat SDK",
                    "VRChat limits the compressed download size of an avatar.",
                    "Lower texture resolutions and remove unused assets from the avatar.",
                    "Accessories and clothes often bring 4K textures: 1K or 2K is usually enough.")
                .Pattern(@"(?i)(avatar|bundle).*download size.*(too|exceed|limit|larger)|download size.*(too large|exceeds)");

            yield return E("vrchat.vrcfury-error", "VRCFury",
                    "VRCFury couldn't finish its build step", Problem, "VRCFury",
                    "VRCFury applies its toggles, armature links and other features while the avatar builds. One of them failed; the message names the component and object.",
                    "Read the object path in the message: a link target or a toggle often points to an object that was renamed or deleted.",
                    "Update VRCFury through the VRChat Creator Companion.",
                    "If it started after adding an accessory, remove that accessory's VRCFury components and add it again.")
                .Pattern(@"^(?![^\n]*\): (?:error|warning) CS\d+)[\s\S]*VRCFury")
                .Levels("error");

            yield return E("vrchat.udonsharp", "[UdonSharp]",
                    "UdonSharp script error", Problem, "UdonSharp",
                    "An UdonSharp script failed to compile or to build its Udon program. UdonSharp only supports part of C#.",
                    "Double-click to open the script at the failing line.",
                    "Check that the feature you use is supported by UdonSharp (no generics on your own types, no interfaces, limited LINQ).")
                .Levels("error");

            // ---- C# compiler ------------------------------------------------------------------------------------

            yield return E("compiler.cs0246", "error CS0246",
                    "Unknown type or namespace '{name}'", Blocking, "C# compiler",
                    "Code uses '{name}', but no installed package or script defines it. Usually a tool needs another package (VRChat SDK, VRCFury, Newtonsoft JSON…) that is missing or has another version.",
                    "Look at the file path in the message: the package or folder it is in needs '{name}'.",
                    "Install or update the missing dependency (VRChat Creator Companion: Manage Project).",
                    "If the file is under Assets, an old copy of a tool may clash with its package version: delete the old folder.")
                .Pattern(@"error CS0246: The type or namespace name '(?<name>[^']+)'")
                .Link("https://learn.microsoft.com/search/?terms=CS0246", "CS0246 on Microsoft Learn");

            yield return E("compiler.cs0234", "error CS0234",
                    "'{ns}' has no '{name}'", Blocking, "C# compiler",
                    "The installed version of the package that provides '{ns}' doesn't have '{name}'. Two tools expect different versions of it.",
                    "Update both the package named in the file path and the one providing '{ns}'.",
                    "If one of them was installed by hand in Assets, remove that copy and install it through the Creator Companion.")
                .Pattern(@"error CS0234: The type or namespace name '(?<name>[^']+)' does not exist in the namespace '(?<ns>[^']+)'")
                .Link("https://learn.microsoft.com/search/?terms=CS0234", "CS0234 on Microsoft Learn");

            yield return E("compiler.cs0101", "error CS0101",
                    "'{name}' is defined twice", Blocking, "C# compiler",
                    "The same script exists twice in the project, usually because a tool is installed both in Assets and as a package, or two versions were imported.",
                    "Search the Project window for '{name}' and delete the older copy of the tool (often a folder under Assets).",
                    "Old VRChat projects: delete Assets/VRCSDK and Assets/Udon once the SDK is installed as a package.")
                .Pattern(@"error CS0101: The namespace '(?<ns>[^']*)' already contains a definition for '(?<name>[^']+)'");

            yield return E("compiler.cs0433", "error CS0433",
                    "'{name}' comes from two assemblies", Blocking, "C# compiler",
                    "Two DLLs or assemblies define '{name}'. A library was imported twice by different tools.",
                    "Find the two assemblies named in the message and keep only one (prefer the one installed as a package).")
                .Pattern(@"error CS0433: The type '(?<name>[^']+)' exists in both");

            yield return E("compiler.cs1061", "error CS1061",
                    "'{type}' has no member '{member}'", Blocking, "C# compiler",
                    "Code calls '{member}', which the installed version of '{type}' doesn't have: it was written for another version of a package or of Unity.",
                    "Update the package the file belongs to, and the package that provides '{type}'.")
                .Pattern(@"error CS1061: '(?<type>[^']+)' does not contain a definition for '(?<member>[^']+)'");

            yield return E("compiler.cs0117", "error CS0117",
                    "'{type}' has no member '{member}'", Blocking, "C# compiler",
                    "Code uses '{member}', which the installed version of '{type}' doesn't have: a package version mismatch.",
                    "Update the package the file belongs to, and the package that provides '{type}'.")
                .Pattern(@"error CS0117: '(?<type>[^']+)' does not contain a definition for '(?<member>[^']+)'");

            yield return E("compiler.cs0103", "error CS0103",
                    "'{name}' doesn't exist here", Blocking, "C# compiler",
                    "The code refers to '{name}', which isn't defined: a typo, a removed member, or a missing package.",
                    "Open the file at the line given and check the spelling, or install what defines '{name}'.")
                .Pattern(@"error CS0103: The name '(?<name>[^']+)' does not exist");

            yield return E("compiler.obsolete", "warning CS0618",
                    "Obsolete API in use", Harmless, "C# compiler",
                    "Code uses something marked obsolete. It still works; the tool's author should update it someday.",
                    "Nothing to do unless you wrote this code.");

            yield return E("compiler.unused", "warning CS0",
                    "Unused code warning", Harmless, "C# compiler",
                    "The compiler noticed a field or variable that is never used or assigned. It doesn't change how anything works.",
                    "Nothing to do unless you wrote this code.")
                .Pattern(@"warning CS0(108|114|162|168|169|219|414|649|67)\b");

            yield return E("compiler.package", "Library/PackageCache/",
                    "A package doesn't compile", Blocking, "C# compiler",
                    "Code inside an installed package fails to compile. Usually two packages need different versions of each other, or a package was made for another Unity version.",
                    "Update the package and what it depends on (VRChat Creator Companion: Manage Project).",
                    "Check the package supports Unity 2022.3, the version VRChat uses.",
                    "While any compile error remains Unity can't enter Play Mode, build or upload, and script changes are not loaded.")
                .Pattern(@"Library/PackageCache/[^:]*\(\d+,\d+\): error CS\d+")
                .Link(Vcc, "VRChat Creator Companion");

            yield return E("compiler.error", "): error CS",
                    "Scripts don't compile", Blocking, "C# compiler",
                    "A script has a compile error. While any compile error remains, Unity can't enter Play Mode, build or upload avatars, and recent script changes aren't loaded.",
                    "Double-click the error to open the file at the line.",
                    "If the file belongs to a tool, update or reinstall that tool rather than editing it.")
                .Link("https://learn.microsoft.com/dotnet/csharp/language-reference/compiler-messages/", "C# compiler messages");

            yield return E("compiler.duplicate-dll", "Multiple precompiled assemblies with the same name",
                    "The same DLL is in the project twice ({name})", Blocking, "Unity",
                    "Two copies of {name} were found, typically Newtonsoft.Json brought by two tools. Unity loads neither, so everything that depends on it fails.",
                    "Search the project for {name} and delete the older copy, keeping the one the package system provides.",
                    "Most VRChat tools use Unity's com.unity.nuget.newtonsoft-json package: copies inside Assets/Plugins can usually go.")
                .Pattern(@"with the same name (?<name>\S+) included");

            yield return E("compiler.assembly-not-loaded", "will not be loaded due to errors",
                    "A DLL can't be loaded", Problem, "Unity",
                    "{path} depends on another assembly that is missing or has a different version.",
                    "Read the reason under the message, then install or update the assembly it names.")
                .Pattern(@"Assembly '(?<path>[^']+)' will not be loaded");

            yield return E("compiler.unresolved-reference", "Unable to resolve reference",
                    "A DLL dependency is missing", Problem, "Unity",
                    "A precompiled assembly refers to '{name}', which isn't in the project or doesn't support the Editor platform.",
                    "Install the package that provides '{name}', or remove the DLL that needs it.")
                .Pattern(@"Unable to resolve reference '(?<name>[^']+)'");

            // ---- Unity: scripts and objects ---------------------------------------------------------------------

            yield return E("unity.missing-script", "The referenced script",
                    "Missing script on '{object}'", Warning, "Unity",
                    "A component uses a script Unity can't find: the tool it came from isn't installed, its scripts don't compile, or the script's .meta file changed. VRChat may refuse to upload avatars with missing scripts.",
                    "Install the tool the asset was made with (VRCFury, Modular Avatar…); its readme or store page says which.",
                    "Fix compile errors first: scripts that don't compile show as missing.",
                    "If the tool isn't needed, remove the missing component (Inspector › ⋮ › Remove Component).")
                .Pattern(@"The referenced script (?:\((?<script>[^)]*)\) )?on this Behaviour(?: \(Game Object '(?<object>[^']*)'\))? is missing");

            yield return E("unity.script-not-loaded", "The associated script can not be loaded",
                    "A component's script can't be loaded", Warning, "Unity",
                    "The script exists but doesn't compile, so Unity can't use the component.",
                    "Fix the compile errors in red first; this goes away with them.");

            yield return E("unity.destroyed", "has been destroyed but you are still trying to access it",
                    "A destroyed {type} is still used", Warning, "C#",
                    "Code kept a reference to a {type} after it was deleted (or after a scene change, leaving Play Mode, or a build copy being cleaned up) and used it again.",
                    "If it repeats every frame, the tool holds an outdated reference: close and reopen its window or select something else.",
                    "Entering and leaving Play Mode, or reloading the scene, usually clears it.",
                    "In your own scripts, check for null first: destroyed Unity objects compare equal to null.")
                .Pattern(@"The object of type '(?<type>[^']+)' has been destroyed");

            yield return E("unity.unassigned", "UnassignedReferenceException",
                    "{type}.{field} is empty", Problem, "Unity",
                    "A field of the {type} component was left empty in the Inspector and its script used it.",
                    "Select the object with the {type} component and fill in {field} in the Inspector.")
                .Pattern(@"The variable (?<field>\S+) of (?<type>\S+) has not been assigned");

            yield return E("unity.missing-component", "MissingComponentException",
                    "'{object}' has no {component}", Problem, "Unity",
                    "A script expected a {component} on '{object}' and didn't find one.",
                    "Add a {component} to '{object}', or check the script looks at the right object.")
                .Pattern(@"There is no '(?<component>[^']+)' attached to the ""(?<object>[^""]+)"" game object");

            yield return E("csharp.null-reference", "NullReferenceException",
                    "Something was null", Problem, "C#",
                    "Code used an object that doesn't exist: an empty field, a missing or deleted object, or something a tool expected to find on your avatar. The first stack frame with a file is where it happened.",
                    "Double-click to open the line that failed.",
                    "If it is inside a tool (VRChat SDK, VRCFury…), look at what it was working on: a missing reference or an empty slot on a component is the usual cause.",
                    "If it started after updating a tool, update what it depends on too, then restart Unity.");

            yield return E("csharp.index-range", "IndexOutOfRangeException",
                    "Read past the end of a list", Problem, "C#",
                    "Code asked for an item past the end of an array or list: usually data shorter than a tool expected (fewer materials, bones or blendshapes).",
                    "Look at the first frame with a file to see what was being read.",
                    "If it's a tool, check the object it worked on matches what it expects (same mesh, same armature).");

            yield return E("csharp.argument-range", "ArgumentOutOfRangeException",
                    "A value was out of range", Problem, "C#",
                    "Code passed an index or value outside what was allowed, usually a list shorter than expected.",
                    "Look at the first frame with a file to see which value was wrong.");

            yield return E("csharp.key-not-found", "KeyNotFoundException",
                    "A lookup found nothing", Problem, "C#",
                    "Code looked something up by name or id and it wasn't there: a renamed object, a missing setting or a stale cache.",
                    "Restart the tool or Unity if it follows a rename or an update.");

            yield return E("csharp.collection-modified", "Collection was modified",
                    "A list changed while being read", Warning, "C#",
                    "A tool changed a list while it was looping over it. It's a bug in that tool; trying again usually works.",
                    "Retry the action. If it keeps happening, report it to the tool's author with the stack trace.");

            yield return E("csharp.type-initializer", "The type initializer for",
                    "A class failed to start", Problem, "C#",
                    "A class's static setup threw an exception, so the class can't be used until scripts reload. The cause is the inner exception below it.",
                    "Read the inner exception (in the stack trace) and fix that, then let scripts recompile.");

            yield return E("csharp.stack-overflow", "StackOverflowException",
                    "Endless recursion", Problem, "C#",
                    "A method kept calling itself until the stack ran out, often two properties or events triggering each other.",
                    "Look for the same frames repeating in the stack trace: that loop is the cause.");

            yield return E("csharp.thread-abort", "ThreadAbortException",
                    "A background task was stopped", Harmless, "C#",
                    "Unity stopped a running thread when scripts reloaded. Harmless.");

            yield return E("csharp.main-thread", "can only be called from the main thread",
                    "Unity API used from a background thread", Problem, "Unity",
                    "Most Unity functions only work on the main thread. A tool called one from a background task.",
                    "Report it to the tool's author with the stack trace.");

            yield return E("csharp.dll-not-found", "DllNotFoundException",
                    "A native plugin is missing", Problem, "C#",
                    "A tool needs a native library (.dll) that is missing, blocked by antivirus, or built for another platform.",
                    "Reinstall the tool. Check your antivirus didn't quarantine the DLL.");

            // ---- Unity: editor UI -------------------------------------------------------------------------------

            yield return E("unity.imgui-layout", "position in a group with only",
                    "Editor layout glitch", Harmless, "Unity",
                    "An editor window drew a different number of controls between two passes, usually because something changed while it was drawing. It is a display bug of that window.",
                    "Ignore it if it happens once.",
                    "If a window keeps repeating it, close and reopen that window or reset the layout (Window › Layouts › Default).");

            yield return E("unity.imgui-begin-end", "BeginLayoutGroup must be called first",
                    "Editor layout error (follow-up)", Harmless, "Unity",
                    "An editor window stopped drawing halfway, usually because an earlier error interrupted it. The real cause is the error just before this one.",
                    "Look at the error logged just before this one.");

            yield return E("unity.guiclips", "You are pushing more GUIClips than you are popping",
                    "Editor layout error (follow-up)", Harmless, "Unity",
                    "An editor window stopped drawing halfway, usually because an earlier error interrupted it. The real cause is the error just before this one.",
                    "Look at the error logged just before this one.");

            yield return E("unity.invalid-guilayout", "Invalid GUILayout state",
                    "Editor layout error (follow-up)", Harmless, "Unity",
                    "An editor window stopped drawing halfway, usually because an earlier error interrupted it.",
                    "Look at the error logged just before this one.");

            yield return E("unity.exit-gui", "ExitGUIException",
                    "Editor drawing interrupted on purpose", Harmless, "Unity",
                    "Unity uses this exception to stop drawing a window after a dialog or a change. Harmless.");

            yield return E("unity.missing-style", "Unable to find style",
                    "Editor style not found", Harmless, "Unity",
                    "An editor tool asked for a style that this Unity version doesn't have. Only that window's look is affected.");

            yield return E("unity.invalid-window", "Invalid editor window",
                    "A saved window belongs to a removed tool", Harmless, "Unity",
                    "The window layout refers to a window whose tool was removed or failed to compile.",
                    "Reset the layout (Window › Layouts › Default) if a blank tab stays.");

            yield return E("unity.uitk-layout", "Layout update is struggling to process current layout",
                    "Editor window resizes in a loop", Harmless, "Unity",
                    "A UI Toolkit window keeps changing its own size. Only that window is affected.",
                    "Resize or close and reopen the window.");

            // ---- Unity: internal warnings -----------------------------------------------------------------------

            yield return E("unity.tls-allocator", "has unfreed allocations",
                    "Unity temporary memory notice", Harmless, "Unity",
                    "Unity's internal temporary memory reports leftovers, typically after heavy editor work or imports. It doesn't affect your project.");

            yield return E("unity.job-temp-alloc", "JobTempAlloc has allocations",
                    "Unity temporary memory notice", Harmless, "Unity",
                    "Unity's job system kept temporary memory longer than expected, typically during heavy editor work. It doesn't affect your project.");

            yield return E("unity.assertion", "Assertion failed on expression",
                    "Unity internal check failed", Info, "Unity",
                    "Unity's own code found an unexpected state. Harmless when it happens once; if it repeats or comes with a crash, restart Unity.");

            yield return E("unity.render-texture-active", "Releasing render texture that is set to be RenderTexture.active",
                    "A render texture was released while in use", Harmless, "Unity",
                    "A tool (often a thumbnail or preview) released a render texture it was still drawing into. Harmless.");

            yield return E("unity.licensing", "[Licensing::",
                    "Unity licensing message", Harmless, "Unity",
                    "Messages from Unity's license client. Ignore them unless Unity says your license is invalid.");

            yield return E("unity.profile-frame", "Skipping profile frame",
                    "Profiler can't keep up", Harmless, "Unity",
                    "The Profiler receives more data than it can process. Only profiling is affected.");

            yield return E("unity.burst-entry-points", "Failed to find entry-points",
                    "Burst couldn't compile a package's jobs", Harmless, "Unity",
                    "Burst failed to compile some jobs and falls back to slower code in the editor. Avatar uploads are not affected.",
                    "Restart Unity; update the package named in the message.");

            yield return E("unity.line-endings", "inconsistent line endings",
                    "Mixed line endings in a script", Harmless, "Unity",
                    "The script mixes Windows and Unix line endings. It compiles fine.");

            yield return E("unity.admin", "running with Administrator privileges",
                    "Unity runs as administrator", Warning, "Unity",
                    "Files Unity creates as administrator can later be locked for your normal account.",
                    "Start Unity (and the Creator Companion) as your normal user.");

            yield return E("unity.inconsistent-import", "generated inconsistent result for asset",
                    "An import gave two different results", Harmless, "Unity",
                    "Importing the same file twice produced different data. Usually harmless.");

            yield return E("unity.fallback-library", "Fallback handler could not load library",
                    "Native library lookup", Harmless, "Unity",
                    "Mono looked for a native library in several places before finding it. Harmless.");

            // ---- Unity: assets ----------------------------------------------------------------------------------

            yield return E("asset.orphan-meta", "exists but its asset",
                    "Leftover .meta file", Harmless, "Unity",
                    "'{path}.meta' is left from a file or folder that was deleted outside Unity. Unity ignores it.",
                    "Delete the leftover .meta file, or restore the missing file. With Git, check it wasn't left out of a commit.")
                .Pattern(@"its asset '(?<path>[^']+)' can't be found");

            yield return E("asset.immutable-meta", "but it's in an immutable folder",
                    "Package file without a .meta file", Warning, "Unity",
                    "A file inside a package has no .meta file, so Unity ignores it: the package was copied incompletely.",
                    "Reinstall or update the package (VRChat Creator Companion: Manage Project).")
                .Link(Vcc, "VRChat Creator Companion");

            yield return E("asset.guid-conflict", "conflicts with",
                    "Two assets share the same id", Warning, "Unity",
                    "Two files have the same GUID in their .meta files, usually because a folder was duplicated outside Unity. Unity gave '{path}' a new id, so references to it may now point to the other file.",
                    "Check that materials, prefabs and scenes still reference the right asset.",
                    "Duplicate assets inside Unity (Ctrl+D) rather than in the file explorer.")
                .Pattern(@"GUID \[(?<guid>[0-9a-fA-F]+)\] for asset '(?<path>[^']+)' conflicts with");

            yield return E("asset.yaml-guid", "Could not extract GUID in text file",
                    "Broken asset file", Problem, "Unity",
                    "A scene, prefab or material file has invalid content, often merge-conflict markers (<<<<<<<) left by Git or a file cut short.",
                    "Open the file in a text editor at the line given and look for <<<<<<< or >>>>>>> markers.",
                    "Or restore the file from your last commit or backup (Unit Git: Log or Backups).");

            yield return E("asset.yaml-parse", "cannot be extracted by the YAML Parser",
                    "Broken asset file", Problem, "Unity",
                    "A Unity text asset can't be read, often because of merge-conflict markers or a file cut short.",
                    "Restore the file from your last commit or backup (Unit Git: Log or Backups).");

            yield return E("asset.serialization-layout", "has a different serialization layout when loading",
                    "Saved data doesn't match the script", Warning, "Unity",
                    "An asset was saved with another version of a script than the one installed, so Unity skipped part of its data. Common after updating or downgrading the VRChat SDK or a tool.",
                    "Make sure every package has the version the project expects (VRChat Creator Companion: Manage Project).",
                    "Reimport the asset (right-click › Reimport); re-save it with the current tools if it persists.");

            yield return E("asset.newer-version", "serialized with a newer version of Unity",
                    "Asset from a newer Unity version", Problem, "Unity",
                    "The file was saved by a newer Unity than 2022.3, the version VRChat uses, or it is damaged.",
                    "Get a version of the asset made for Unity 2022.3 from its creator.",
                    "Or restore it from a backup.");

            yield return E("asset.missing-prefab", "Missing Prefab",
                    "A prefab reference is broken", Warning, "Unity",
                    "A prefab instance or nested prefab points to a prefab that is no longer in the project.",
                    "Reimport the package the prefab came from.",
                    "Or unpack the broken instance (right-click › Prefab › Unpack) and fix it by hand.");

            yield return E("asset.prefab-import-problem", "Problem detected while importing the Prefab file",
                    "A prefab has problems", Warning, "Unity",
                    "The prefab refers to missing scripts or nested prefabs. The lines below the message list them.",
                    "Install what the prefab needs, or open it and remove the missing parts.");

            yield return E("asset.prefab-parent", "which resides in a Prefab Asset is disabled",
                    "A tool tried to edit a prefab asset directly", Warning, "Unity",
                    "A script tried to move an object inside a prefab asset, which Unity forbids to avoid corrupting it.",
                    "Open the prefab (double-click) before editing it, or report it to the tool's author.");

            yield return E("asset.read-failed", "File could not be read",
                    "A file can't be imported", Warning, "Unity",
                    "Unity couldn't read the file: it is damaged, locked by another program, or not a format Unity supports.",
                    "Close programs that may lock it (image editors, sync tools) and reimport, or replace the file.");

            yield return E("asset.mesh-readable", "isReadable is false",
                    "The mesh isn't readable", Problem, "Unity",
                    "A tool needs the vertices of a mesh whose import settings don't keep them in memory.",
                    "Select the model, enable Read/Write in the Model tab of its import settings and click Apply.");

            yield return E("asset.texture-readable", "is not readable, the texture memory can not be accessed",
                    "The texture isn't readable", Problem, "Unity",
                    "A tool needs the pixels of a texture whose import settings don't keep them in memory.",
                    "Select the texture, enable Read/Write in its import settings and click Apply.");

            yield return E("asset.abnormal-bounds", "abnormal mesh bounds",
                    "Mesh with broken vertices", Warning, "Unity",
                    "The mesh contains vertices at infinity or NaN, usually from an export problem. It may render wrongly or disappear.",
                    "In Blender: apply transforms, merge by distance and delete loose geometry (Mesh › Clean Up), then export again.");

            yield return E("asset.physx-clean", "cleaning the mesh failed",
                    "A mesh collider couldn't be built", Harmless, "Unity",
                    "The mesh used by a Mesh Collider has no valid triangles for physics (flat or degenerate). The collider does nothing.",
                    "Use a primitive collider, or a simpler mesh for collisions.");

            yield return E("asset.convex-limit", "Couldn't create a Convex Mesh",
                    "Convex collider too detailed", Info, "Unity",
                    "Convex Mesh Colliders are limited to 255 polygons; Unity simplified it.",
                    "Use a simpler mesh or primitive colliders.");

            yield return E("asset.material-property", "doesn't have a",
                    "The material has no '{property}' property", Harmless, "Unity",
                    "A script or tool read or set '{property}', which the material's shader doesn't have. Usually it expected another shader; nothing visible changes.",
                    "Nothing to do unless something looks wrong. Otherwise check the material uses the shader the tool expects.")
                .Pattern(@"doesn't have an? (?:texture|float|color|vector|float or range|integer|int|matrix) property '(?<property>[^']+)'");

            yield return E("asset.shader-error-poiyomi", "Shader error in '",
                    "Poiyomi shader doesn't compile", Problem, "Poiyomi",
                    "A Poiyomi shader (often a locked copy) fails to compile; materials using it render pink.",
                    "Unlock the material (Poiyomi/Thry inspector › Unlock), update Poiyomi, then lock it again.",
                    "Locked shaders made with an older Poiyomi break after an update: unlock and lock them again.")
                .Pattern(@"(?i)Shader error in '[^']*poi");

            yield return E("asset.shader-error", "Shader error in '",
                    "Shader '{shader}' doesn't compile", Problem, "Unity",
                    "Materials using '{shader}' render pink until it compiles.",
                    "Update the shader package (Poiyomi, lilToon…) to its latest version.",
                    "Shaders made for URP or HDRP don't work in VRChat, which uses the built-in render pipeline.")
                .Pattern(@"Shader error in '(?<shader>[^']+)'");

            yield return E("asset.shader-warning", "Shader warning in '",
                    "Shader compiler warning", Harmless, "Unity",
                    "The shader compiles; the compiler only noted something its author could tidy up.");

            yield return E("asset.instantiate-material", "Instantiating material due to calling renderer.material during edit mode",
                    "A script copied a material in the editor", Warning, "Unity",
                    "A script used renderer.material outside Play Mode, which creates material copies saved into the scene.",
                    "In your own scripts use renderer.sharedMaterial in the editor; otherwise report it to the tool's author.");

            yield return E("asset.destroy-asset", "Destroying assets is not permitted to avoid data loss",
                    "A tool tried to delete an asset", Warning, "Unity",
                    "A script called Destroy on an asset instead of a scene object. Unity refused, so nothing was lost.",
                    "Report it to the tool's author with the stack trace.");

            // ---- Unity: scene and animation ---------------------------------------------------------------------

            yield return E("scene.audio-listeners", "audio listeners in the scene",
                    "More than one Audio Listener", Harmless, "Unity",
                    "Several cameras or objects have an Audio Listener, so Unity only uses one. Avatars and test tools often bring their own camera.",
                    "Keep a single Audio Listener in test scenes.");

            yield return E("scene.event-systems", "event systems in the scene",
                    "More than one Event System", Harmless, "Unity",
                    "Several UI Event Systems are in the scene; only one is used.",
                    "Keep a single Event System in the scene.");

            yield return E("scene.negative-scale", "does not support negative scale or size",
                    "Collider with negative scale", Warning, "Unity",
                    "A collider is on an object mirrored with a negative scale, which physics doesn't support.",
                    "Use positive scales on objects with colliders; mirror the mesh itself instead.");

            yield return E("anim.no-controller", "Animator is not playing an AnimatorController",
                    "The Animator has no controller", Harmless, "Unity",
                    "A script talked to an Animator without a controller. On VRChat avatars the controllers are set in the avatar descriptor, so this is normal outside Play Mode testing.");

            yield return E("anim.parameter-missing", "does not exist",
                    "Animator parameter '{param}' doesn't exist", Warning, "Unity",
                    "Something set '{param}', which the current Animator Controller doesn't have (names are case-sensitive).",
                    "Add '{param}' to the controller's parameters or fix its spelling.",
                    "On VRChat avatars, check the parameter also exists in the Expression Parameters asset.")
                .Pattern(@"^Parameter '(?<param>[^']+)' does not exist");

            yield return E("anim.invalid-controller", "you have used is not valid. Animations will not play",
                    "The Animator Controller is invalid", Problem, "Unity",
                    "The controller can't be used, often a layer without a default state or a damaged asset.",
                    "Open the controller and make sure each layer has a default (orange) state.");

            yield return E("anim.rig-mismatch", "Bone length in copied configuration does not match position in animation",
                    "Copied humanoid setup doesn't fit", Info, "Unity",
                    "The model's humanoid Avatar was copied from a model with other proportions. Usually harmless.",
                    "If poses look wrong, set the Rig to Create From This Model instead of copying.");

            yield return E("anim.human-description", "not found in HumanDescription",
                    "Humanoid mapping points to a missing bone", Problem, "Unity",
                    "The humanoid setup maps a bone the model no longer has (renamed or removed in the FBX).",
                    "Select the model, open Rig › Configure… and map the bones again.");

            yield return E("anim.look-rotation", "Look rotation viewing vector is zero",
                    "Rotation toward an empty direction", Harmless, "Unity",
                    "A script asked an object to look toward its own position. Nothing changes.");

            yield return E("scene.send-message", "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate",
                    "SendMessage during validation", Harmless, "Unity",
                    "A component called SendMessage while Unity was validating it. Harmless, and usually from a package rather than your project.");

            yield return E("tmp.missing-glyph", "was not found in the [",
                    "Missing character in a font", Harmless, "TextMesh Pro",
                    "A TextMesh Pro text uses a character its font asset doesn't have; a fallback or a square is shown.",
                    "Add a fallback font asset that contains the character.")
                .Pattern(@"The character with Unicode value \\u[0-9A-Fa-f]+ was not found");

            // ---- Network ----------------------------------------------------------------------------------------

            yield return E("net.insecure", "Non-secure network connections disabled in Player Settings",
                    "An http:// request was blocked", Warning, "Unity",
                    "A tool tried to use an http:// address. Unity blocks plain HTTP unless the project allows it, so that request failed.",
                    "The tool should use https:// — report it to its author.",
                    "Only if you trust it: Edit › Project Settings › Player › Other Settings › Allow downloads over HTTP.");

            yield return E("net.curl", "Curl error",
                    "Network request failed (curl {code})", Info, "Network",
                    "Unity or a tool couldn't reach a server: offline, blocked by a firewall, antivirus or proxy, or the server is down. Most tools try again later.",
                    "Check your connection; allow Unity through your firewall or VPN if needed.")
                .Pattern(@"Curl error (?<code>\d+)");

            yield return E("net.unreachable", "Cannot resolve destination host",
                    "No internet connection", Info, "Network",
                    "A request couldn't find the server's address: you are offline or DNS is blocked.",
                    "Check your connection and try again.");

            yield return E("net.cannot-connect", "Cannot connect to destination host",
                    "Server unreachable", Info, "Network",
                    "A request couldn't connect: the server is down, or a firewall or VPN blocks it.",
                    "Try again later, or check firewall and VPN settings.");

            yield return E("net.rate-limit", "429",
                    "Too many requests", Info, "Network",
                    "The server asked to slow down (rate limit).",
                    "Wait a minute before trying again.")
                .Pattern(@"\b429\b.*(?i:too many|rate)|(?i:too many requests)");

            yield return E("net.unauthorized", "401",
                    "Not signed in", Warning, "Network",
                    "A server refused the request because the session expired or the account isn't signed in.",
                    "Sign in again in the tool (VRChat SDK › Authentication, Orbiters account…).")
                .Pattern(@"\b401\b.*(?i:unauthori[sz]ed)|(?i:unauthori[sz]ed).*\b401\b");

            // ---- Files, memory and graphics ---------------------------------------------------------------------

            yield return E("io.access-denied", "Access to the path",
                    "A file is locked or read-only", Problem, "System",
                    "Another program holds the file (OneDrive or Dropbox sync, antivirus, an open preview) or it is read-only.",
                    "Close programs that may use the file and try again.",
                    "Keep Unity projects out of synced folders (OneDrive, Desktop, Documents) and exclude them from antivirus scans.");

            yield return E("io.sharing-violation", "Sharing violation on path",
                    "A file is in use by another program", Problem, "System",
                    "Another program (sync tool, antivirus, image editor) had the file open while Unity wrote it.",
                    "Close that program and try again; keep the project out of synced folders.");

            yield return E("io.in-use", "because it is being used by another process",
                    "A file is in use by another program", Problem, "System",
                    "Another program had the file open while Unity or a tool needed it.",
                    "Close that program and try again; keep the project out of synced folders.");

            yield return E("io.path-too-long", "PathTooLongException",
                    "The file path is too long", Problem, "System",
                    "Windows limits paths to 260 characters by default and the project's folders are nested too deep.",
                    "Move the project to a short path, such as C:\\Unity\\MyAvatar.");

            yield return E("io.disk-full", "not enough space on the disk",
                    "The disk is full", Blocking, "System",
                    "Unity couldn't write a file because the drive is full. Imports and saves fail until space is freed.",
                    "Free space on the project's drive (the Library folder can be deleted while Unity is closed; it is rebuilt).");

            yield return E("mem.out-of-memory", "System out of memory",
                    "Out of memory", Blocking, "Unity",
                    "Unity ran out of memory, usually while importing very large textures or meshes.",
                    "Close other programs, lower texture import sizes, and restart Unity.");

            yield return E("mem.oom-exception", "OutOfMemoryException",
                    "Out of memory", Problem, "C#",
                    "A tool tried to allocate more memory than available.",
                    "Restart Unity; if it repeats, the operation (a huge import, a long history) is too big for the memory left.");

            yield return E("gpu.d3d11", "d3d11: failed to create",
                    "The graphics card ran out of memory", Problem, "Unity",
                    "Unity couldn't create a texture or buffer on the GPU, usually because video memory is full.",
                    "Close other GPU-heavy programs, lower texture sizes, update your graphics driver.");

            // ---- Package management -----------------------------------------------------------------------------

            yield return E("pkg.resolve", "An error occurred while resolving packages",
                    "Packages couldn't be resolved", Blocking, "Package Manager",
                    "A package listed in Packages/manifest.json can't be found or downloaded.",
                    "Open the project in the VRChat Creator Companion and let it resolve the packages.",
                    "Check your internet connection; a package from a repository you removed can't be downloaded.")
                .Link(Vcc, "VRChat Creator Companion");
        }

        private static LogExplanation E(string id, string needle, string title, LogExplanationSeverity severity, string source,
            string summary, params string[] fixes)
        {
            return new LogExplanation(id, needle, title)
            {
                Severity = severity,
                Source = source,
                Summary = summary,
                Fixes = fixes ?? new string[0]
            };
        }

        private static LogExplanation Pattern(this LogExplanation explanation, string pattern)
        {
            explanation.Pattern = pattern;
            return explanation;
        }

        private static LogExplanation Stack(this LogExplanation explanation, string needle)
        {
            explanation.StackNeedle = needle;
            return explanation;
        }

        private static LogExplanation Levels(this LogExplanation explanation, params string[] levels)
        {
            explanation.Levels = levels;
            return explanation;
        }

        private static LogExplanation Link(this LogExplanation explanation, string url, string label)
        {
            explanation.Link = url;
            explanation.LinkLabel = label;
            return explanation;
        }
    }
}
