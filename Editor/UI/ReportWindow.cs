using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// Get help: makes a project report someone else can read to help. Five cards (AI diagnosis, logs, Git history,
    /// scene description, scene export) show what each part would hold with live numbers and toggle on press; one
    /// button makes the zip on the Desktop. Partial files: the cards (this one), the progress and the result.
    /// </summary>
    internal sealed partial class ReportWindow : EditorWindow
    {
        private const string LoggerSheet = "Packages/orbiters.logger/Editor/UI/logger.uss";
        private const string ReportSheet = "Packages/orbiters.logger/Editor/UI/report.uss";
        private const string NoteKey = "Orbiters.Logger.Report.Note";
        private const float NarrowWidth = 620f;

        // The report being made survives the window: closed and opened again, it shows where it is.
        private static ReportBuilder running;

        private readonly Dictionary<ReportPart, ReportCard> cards = new Dictionary<ReportPart, ReportCard>();
        private OrbitMark orbit;
        private Label subtitle;
        private VisualElement account;
        private Label accountAvatar;
        private Label accountName;
        private ScrollView scroll;
        private VisualElement choosePage;
        private VisualElement shownPage;
        private TextField noteField;
        private Label notePlaceholder;
        private Button destination;
        private Label destinationLabel;
        private Label destinationPath;
        private Label estimate;
        private Button go;
        private Label goLabel;
        private LoggerIcon goIcon;
        private VisualElement goSheen;
        private Toasts toasts;

        private Task<GitDigest> gitTask;
        private GitDigest gitFacts;
        private List<string> gitExtras = new List<string>();
        private SceneDigest sceneFacts;
        private string scenePathsKey;
        private Task<SceneFiles> filesTask;
        private SceneFiles sceneFiles;
        private bool sceneDirty = true;
        private bool signInShown;
        private bool toolkitLinkShown;
        private int logCount;
        private long editorLogBytes;

        [MenuItem("Tools/Orbiters/Get Help (Project Report)", priority = 2001)]
        public static void Open() => Open(null);

        /// <summary>Opens the window, the note started with <paramref name="note"/> when given (a log to ask about).</summary>
        internal static void Open(string note)
        {
            bool existed = HasOpenInstances<ReportWindow>();
            var window = GetWindow<ReportWindow>(false, "Get help", true);
            if (!existed)
            {
                var main = EditorGUIUtility.GetMainWindowPosition();
                var size = new Vector2(Mathf.Min(860f, main.width - 80f), Mathf.Min(780f, main.height - 80f));
                window.position = new Rect(main.center - size * 0.5f, size);
            }

            if (!string.IsNullOrEmpty(note))
            {
                window.AddToNote(note);
            }

            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Get help", EditorGUIUtility.IconContent("_Help").image, "Make a project report to get help");
            minSize = new Vector2(480f, 520f);
            OrbitersLink.Changed += OnAccountChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private void OnDisable()
        {
            OrbitersLink.Changed -= OnAccountChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
            if (running != null)
            {
                running.Changed -= OnBuilderChanged;
            }
        }

        private void OnFocus()
        {
            // Back from elsewhere (a commit made, a scene saved): read Git and the scene again.
            if (choosePage != null && running == null)
            {
                gitTask = null;
                sceneDirty = true;
                RefreshFacts(git: true);
            }
        }

        private void OnAccountChanged() => rootVisualElement.schedule.Execute(() => RefreshFacts(git: false));

        private void OnHierarchyChanged() => sceneDirty = true;

        private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            sceneDirty = true;
            scenePathsKey = null;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("lg-root");
            root.AddToClassList("rp-root");
            foreach (string path in new[] { LoggerSheet, ReportSheet })
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !root.styleSheets.Contains(sheet))
                {
                    root.styleSheets.Add(sheet);
                }
            }

            root.Add(BuildHero());
            UpdateAccount();
            scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("rp-scroll");
            root.Add(scroll);
            root.Add(BuildFooter());
            toasts = new Toasts();
            root.Add(toasts);
            choosePage = BuildChoosePage();
            root.RegisterCallback<GeometryChangedEvent>(evt => root.EnableInClassList("rp-root--narrow", evt.newRect.width < NarrowWidth));
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.schedule.Execute(Poll).Every(120);
            root.schedule.Execute(RunSheen).Every(4200).StartingIn(1600);

            // A report finished while the window was closed shows its result for a quarter of an hour, then the window
            // starts fresh (the file stays in its folder).
            if (running != null && running.Finished && EditorApplication.timeSinceStartup - running.FinishedAt > 900d)
            {
                running = null;
            }

            if (running != null)
            {
                AttachBuilder(running, animate: false);
            }
            else
            {
                ShowPage(choosePage, animate: false);
                PlayCardsEntrance();
                RefreshFacts(git: true);
            }
        }

        // ---- Hero -----------------------------------------------------------------------------------------------

        private VisualElement BuildHero()
        {
            var hero = LoggerUi.Box("rp-hero");
            orbit = new OrbitMark(LoggerGlyph.Lifebuoy);
            hero.Add(orbit);
            var text = LoggerUi.Box("rp-hero__text");
            text.Add(LoggerUi.Text("Get help with this project", "rp-hero__title"));
            subtitle = LoggerUi.Text("Everything someone needs to see what's going on, in one file. Choose what goes in.", "rp-hero__subtitle");
            text.Add(subtitle);
            hero.Add(text);

            account = LoggerUi.Box("rp-account");
            accountAvatar = LoggerUi.Text(string.Empty, "rp-account__avatar");
            account.Add(accountAvatar);
            accountName = LoggerUi.Text(string.Empty, "rp-account__name");
            account.Add(accountName);
            account.Add(LoggerUi.Box("rp-account__dot", PickingMode.Ignore));
            hero.Add(account);
            return hero;
        }

        private void UpdateAccount()
        {
            string user = OrbitersLink.SafeUsername();
            bool signedIn = OrbitersLink.SignedIn;
            account.EnableInClassList("rp-account--off", !signedIn);
            accountName.text = signedIn ? (string.IsNullOrEmpty(user) ? "Connected to Orbiters" : user) : "Not connected";
            accountAvatar.text = signedIn && !string.IsNullOrEmpty(user) ? user.Substring(0, 1).ToUpperInvariant() : "?";
            account.tooltip = signedIn
                ? "Connected to Orbiters: the AI diagnosis is available."
                : OrbitersLink.CanSignIn ? "Connect in the AI diagnosis card to get the AI diagnosis." : "Orbiters Toolkit connects the Logger to your Orbiters account.";
        }

        // ---- Choose page ----------------------------------------------------------------------------------------

        private VisualElement BuildChoosePage()
        {
            var page = LoggerUi.Box("rp-page");

            var note = LoggerUi.Box("rp-note");
            var head = LoggerUi.Box("rp-note__head", PickingMode.Ignore);
            head.Add(new LoggerIcon(LoggerGlyph.Bulb));
            head.Add(LoggerUi.Text("What's going wrong?", "rp-note__title"));
            head.Add(LoggerUi.Text("optional", "rp-note__optional"));
            note.Add(head);
            noteField = new TextField { multiline = true, value = SessionState.GetString(NoteKey, string.Empty) };
            noteField.AddToClassList("rp-note__field");
            notePlaceholder = LoggerUi.Text("A sentence helps a lot: what you did, what you expected, what happened instead. " +
                                            "For example: “My avatar won't upload: the SDK panel says it has errors.”", "rp-note__placeholder");
            notePlaceholder.pickingMode = PickingMode.Ignore;
            noteField.Add(notePlaceholder);
            noteField.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetString(NoteKey, evt.newValue ?? string.Empty);
                LoggerUi.Show(notePlaceholder, string.IsNullOrEmpty(evt.newValue));
            });
            noteField.RegisterCallback<FocusInEvent>(_ => note.AddToClassList("rp-note--focus"));
            noteField.RegisterCallback<FocusOutEvent>(_ => note.RemoveFromClassList("rp-note--focus"));
            LoggerUi.Show(notePlaceholder, string.IsNullOrEmpty(noteField.value));
            note.Add(noteField);
            // A click anywhere in the box writes in it.
            note.RegisterCallback<PointerDownEvent>(_ => noteField.Q(className: "unity-text-field__input")?.Focus());
            page.Add(note);

            var section = LoggerUi.Box("rp-section-title");
            section.Add(LoggerUi.Text("WHAT GOES IN THE REPORT", "rp-section-title__text"));
            var all = new Button { text = "Everything" };
            all.AddToClassList("rp-section-title__action");
            LoggerUi.Press(all, SelectEverything);
            section.Add(all);
            page.Add(section);

            var grid = LoggerUi.Box("rp-cards");
            AddCard(grid, ReportPart.Diagnosis, LoggerGlyph.Sparkle, "AI diagnosis", "ORBITERS AI",
                "Orbiters' AI reads your errors, warnings and project setup, then tells you what is most likely wrong and how to fix it.");
            AddCard(grid, ReportPart.Logs, LoggerGlyph.List, "Logs", null,
                "Every log of this session with its stack trace, every distinct message with how often it happened, and Unity's own Editor.log.");
            AddCard(grid, ReportPart.Git, LoggerGlyph.Branch, "Git history", null,
                "The last commits with the files they changed, what is changed now, where the project went, and Unit Git's releases when it is used.");
            AddCard(grid, ReportPart.Scene, LoggerGlyph.Hierarchy, "Scene description", null,
                "Every object of the open scenes with its components, missing scripts flagged, and the project files the scenes use as a folder tree.");
            var export = AddCard(grid, ReportPart.SceneExport, LoggerGlyph.Package, "Scene export", "UNITY PACKAGE",
                "The open scenes with every file they depend on, as a .unitypackage someone can import to see exactly what you see.");
            export.AddToClassList("rp-card--wide");
            page.Add(grid);

            var privacy = LoggerUi.Box("rp-privacy", PickingMode.Ignore);
            privacy.Add(new LoggerIcon(LoggerGlyph.Shield));
            privacy.Add(LoggerUi.Text("Your user folder becomes " + ReportPrivacy.UserFolder + ", and Orbiters tokens and passwords in addresses are masked in every text " +
                                      "of the report. Unity version, platform and package list are always included.", "rp-privacy__text"));
            page.Add(privacy);
            return page;
        }

        private ReportCard AddCard(VisualElement grid, ReportPart part, LoggerGlyph glyph, string title, string tag, string description)
        {
            var card = new ReportCard(part, glyph, title, tag, description, on =>
            {
                ReportOptions.Set(part, on);
                cards[part].SetOn(on);
                UpdateEstimate();
            });
            card.SetOn(ReportOptions.IsOn(part));
            cards[part] = card;
            grid.Add(card);
            return card;
        }

        private void PlayCardsEntrance()
        {
            int i = 0;
            foreach (var card in cards.Values)
            {
                LoggerUi.Enter(card, "rp-card--enter", 40 + i++ * 60);
            }
        }

        private void SelectEverything()
        {
            foreach (var pair in cards)
            {
                ReportOptions.Set(pair.Key, true);
                pair.Value.SetOn(true);
            }

            UpdateEstimate();
        }

        // A log to ask about goes under what is already written, once.
        private void AddToNote(string text)
        {
            string current = noteField != null ? noteField.value ?? string.Empty : SessionState.GetString(NoteKey, string.Empty);
            if (current.Contains(text))
            {
                return;
            }

            string next = string.IsNullOrWhiteSpace(current) ? text : current.TrimEnd() + "\n" + text;
            SessionState.SetString(NoteKey, next);
            if (noteField != null)
            {
                noteField.value = next;
            }
        }

        // ---- Facts on the cards ---------------------------------------------------------------------------------

        private void Poll()
        {
            if (running != null || shownPage != choosePage)
            {
                return;
            }

            if (gitTask != null && gitTask.IsCompleted)
            {
                gitFacts = gitTask.Status == TaskStatus.RanToCompletion ? gitTask.Result : new GitDigest { Repository = true, Error = "Git did not answer." };
                gitTask = null;
                ShowGit();
            }

            if (filesTask != null && filesTask.IsCompleted)
            {
                sceneFiles = filesTask.Status == TaskStatus.RanToCompletion ? filesTask.Result : null;
                filesTask = null;
                ShowExport();
                UpdateEstimate();
            }

            // The rest is cheap: about once a second.
            if (EditorApplication.timeSinceStartup - lastFacts > 1d)
            {
                RefreshFacts(git: false);
            }
        }

        private double lastFacts;

        private void RefreshFacts(bool git)
        {
            if (choosePage == null)
            {
                return;
            }

            lastFacts = EditorApplication.timeSinceStartup;
            UpdateAccount();
            ShowLogs();
            ShowDiagnosis();
            if (sceneDirty)
            {
                sceneDirty = false;
                sceneFacts = SceneReport.Look();
                ShowScene();
                string key = string.Join("|", sceneFacts.Paths);
                if (key != scenePathsKey)
                {
                    scenePathsKey = key;
                    sceneFiles = null;
                    var files = SceneReport.Dependencies(sceneFacts);
                    filesTask = Task.Run(() =>
                    {
                        SceneReport.Measure(files);
                        return files;
                    });
                }

                ShowExport();
            }

            if (git && gitTask == null)
            {
                string root = GitCli.ProjectRoot;
                gitExtras = ReportOptions.GitExtras.SelectMany(extra =>
                {
                    try
                    {
                        return extra(root) ?? Enumerable.Empty<ReportExtra>();
                    }
                    catch (Exception)
                    {
                        return Enumerable.Empty<ReportExtra>();
                    }
                }).Where(e => File.Exists(e.Path) || Directory.Exists(e.Path)).Select(e => e.Label).ToList();
                gitTask = Task.Run(() => GitReport.Look(root));
                if (gitFacts == null)
                {
                    cards[ReportPart.Git].Loading();
                }
            }

            UpdateEstimate();
        }

        private void ShowLogs()
        {
            LogReport.Count(LogCapture.Store, out int errors, out int warnings, out int _);
            logCount = LogCapture.Store?.Count ?? 0;
            editorLogBytes = FileSize(ReloadTimings.LogPath);
            var stats = new List<ReportStat>
            {
                ReportStat.Counted(logCount, n => LoggerUi.Plural(n, "log"), LoggerGlyph.List),
                ReportStat.Counted(errors, n => LoggerUi.Plural(n, "error"), LoggerGlyph.Error, errors > 0 ? "error" : null),
                ReportStat.Counted(warnings, n => LoggerUi.Plural(n, "warning"), LoggerGlyph.Warning, warnings > 0 ? "warning" : null)
            };
            if (editorLogBytes > 0)
            {
                stats.Add(ReportStat.Plain("+ Editor.log, " + SceneReport.Size(Math.Min(editorLogBytes, 32L * 1024 * 1024)), null, "muted"));
            }

            cards[ReportPart.Logs].SetStats(stats);
        }

        private void ShowDiagnosis()
        {
            var card = cards[ReportPart.Diagnosis];
            bool signedIn = OrbitersLink.SignedIn;
            string user = OrbitersLink.SafeUsername() ?? string.Empty;
            LogReport.Count(LogCapture.Store, out int errors, out int warnings, out int _);
            card.SetAvailable(signedIn);
            if (signedIn)
            {
                if (signInShown)
                {
                    // Just connected: the card switches itself on.
                    signInShown = false;
                    card.Extra.Clear();
                    LoggerUi.Show(card.Extra, false);
                    ReportOptions.Set(ReportPart.Diagnosis, true);
                    card.SetOn(true);
                    ShowToast("Connected" + (user.Length > 0 ? " as " + user : string.Empty) + ": the AI diagnosis is on");
                }

                card.SetNote(null);
                card.SetStats(new List<ReportStat>
                {
                    ReportStat.Plain(user.Length > 0 ? "Connected as " + user : "Connected", LoggerGlyph.Check),
                    ReportStat.Plain(errors + warnings == 0 ? "Nothing wrong logged yet" : "Reads " + LoggerUi.Plural(Math.Min(errors, 999999), "error") + " and " +
                                     LoggerUi.Plural(warnings, "warning"), null, "muted")
                });
            }
            else if (OrbitersLink.CanSignIn)
            {
                card.SetStats(new List<ReportStat> { ReportStat.Plain("Connect your Orbiters account to use it", null, "muted") });
                if (!signInShown)
                {
                    signInShown = true;
                    card.Extra.Clear();
                    var signIn = OrbitersLink.SignInElement("The AI diagnosis is free for Orbiters members.", () => RefreshFacts(git: false));
                    if (signIn != null)
                    {
                        card.Extra.Add(signIn);
                        LoggerUi.Show(card.Extra, true);
                    }
                }
            }
            else
            {
                card.SetStats(new List<ReportStat> { ReportStat.Plain("Needs Orbiters Toolkit and an Orbiters account", null, "muted") });
                if (!toolkitLinkShown)
                {
                    toolkitLinkShown = true;
                    card.Extra.Clear();
                    card.Extra.Add(LoggerUi.Pill("Get Orbiters Toolkit", () => Application.OpenURL("https://orbiters.cc/"), "ghost", LoggerGlyph.External));
                    card.Extra.Q<Button>()?.AddToClassList("rp-card__link");
                    LoggerUi.Show(card.Extra, true);
                }
            }
        }

        private void ShowGit()
        {
            var card = cards[ReportPart.Git];
            var facts = gitFacts;
            if (facts == null)
            {
                return;
            }

            if (!facts.Repository)
            {
                card.SetAvailable(false);
                card.SetNote(null);
                card.SetStats(new List<ReportStat> { ReportStat.Plain("This project doesn't use Git", null, "muted") });
                return;
            }

            card.SetAvailable(true);
            if (facts.Error != null)
            {
                card.SetStats(new List<ReportStat> { ReportStat.Plain("Git is not available", LoggerGlyph.Warning, "warning") });
                card.SetNote(facts.Error);
                return;
            }

            card.SetNote(null);
            var stats = new List<ReportStat>
            {
                ReportStat.Plain(facts.Branch, LoggerGlyph.Branch),
                ReportStat.Counted(facts.Commits, n => LoggerUi.Plural(n, "commit"), LoggerGlyph.Commit),
                facts.Changed > 0
                    ? ReportStat.Counted(facts.Changed, n => LoggerUi.Plural(n, "uncommitted file"), LoggerGlyph.Warning, "warning")
                    : ReportStat.Plain("Nothing uncommitted", LoggerGlyph.Check)
            };
            foreach (string extra in gitExtras)
            {
                stats.Add(ReportStat.Plain("+ " + extra, null, "muted"));
            }

            card.SetStats(stats);
        }

        private void ShowScene()
        {
            var card = cards[ReportPart.Scene];
            var facts = sceneFacts;
            card.SetAvailable(facts.Names.Count > 0);
            var stats = new List<ReportStat>
            {
                ReportStat.Plain(facts.Title, LoggerGlyph.Hierarchy),
                ReportStat.Counted(facts.Objects, n => LoggerUi.Plural(n, "object"))
            };
            if (facts.MissingScripts > 0)
            {
                stats.Add(ReportStat.Counted(facts.MissingScripts, n => LoggerUi.Plural(n, "missing script"), LoggerGlyph.Warning, "error"));
            }

            card.SetStats(stats);
        }

        private void ShowExport()
        {
            var card = cards[ReportPart.SceneExport];
            var facts = sceneFacts;
            if (facts == null)
            {
                return;
            }

            if (facts.Paths.Count == 0)
            {
                card.SetAvailable(false);
                card.SetStats(new List<ReportStat> { ReportStat.Plain("Nothing to export", null, "muted") });
                card.SetNote("Save the scene first (Ctrl+S): an untitled scene has no file to export.");
                return;
            }

            card.SetAvailable(true);
            card.SetNote(facts.Unsaved ? "The scene has unsaved changes: the export takes the saved scene. Save it first (Ctrl+S) to include them." : null);
            if (sceneFiles == null)
            {
                card.Loading();
                return;
            }

            var stats = new List<ReportStat>
            {
                ReportStat.Counted(sceneFiles.Paths.Length, n => LoggerUi.Plural(n, "file"), LoggerGlyph.Package),
                ReportStat.Plain(SceneReport.Size(sceneFiles.Bytes) + " before compression", null, sceneFiles.Bytes > 1024L * 1024 * 1024 ? "warning" : null)
            };
            card.SetStats(stats);
            if (!facts.Unsaved && sceneFiles.Bytes > 1024L * 1024 * 1024)
            {
                card.SetNote("Big export: it may take a few minutes and make a large file.");
            }
        }

        private static long FileSize(string path)
        {
            try
            {
                return !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0L;
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        // ---- Footer ---------------------------------------------------------------------------------------------

        private VisualElement BuildFooter()
        {
            var footer = LoggerUi.Box("rp-footer");
            destination = new Button { tooltip = "Where the report is saved. Click to choose another folder." };
            destination.AddToClassList("rp-destination");
            destination.Add(new LoggerIcon(LoggerGlyph.Folder));
            var texts = LoggerUi.Box("rp-destination__texts", PickingMode.Ignore);
            destinationLabel = LoggerUi.Text("SAVED TO", "rp-destination__label");
            texts.Add(destinationLabel);
            destinationPath = LoggerUi.Text(string.Empty, "rp-destination__path");
            texts.Add(destinationPath);
            destination.Add(texts);
            LoggerUi.Press(destination, OnDestination);
            footer.Add(destination);
            footer.Add(LoggerUi.Spacer());
            estimate = LoggerUi.Text(string.Empty, "rp-estimate");
            footer.Add(estimate);

            go = new Button { tooltip = "Make the report (Ctrl+Enter)" };
            go.AddToClassList("rp-go");
            goSheen = LoggerUi.Box("rp-go__sheen", PickingMode.Ignore);
            go.Add(goSheen);
            goIcon = new LoggerIcon(LoggerGlyph.Package);
            go.Add(goIcon);
            goLabel = LoggerUi.Text("Create report", "rp-go__label");
            goLabel.pickingMode = PickingMode.Ignore;
            go.Add(goLabel);
            LoggerUi.Press(go, OnGo);
            footer.Add(go);
            UpdateDestination();
            return footer;
        }

        private void UpdateDestination()
        {
            if (destinationPath == null)
            {
                return;
            }

            if (running != null)
            {
                string file = running.Result ?? running.Destination;
                destinationLabel.text = running.Result != null ? "SAVED AS" : "SAVING AS";
                destinationPath.text = Path.GetFileName(file);
                destination.tooltip = file + (running.Result != null ? "\nClick to show it in " + FileBrowserName + "." : string.Empty);
                return;
            }

            destinationLabel.text = "SAVED TO";

            string folder = ReportOptions.Folder;
            destinationPath.text = folder == ReportOptions.DefaultFolder ? "Desktop" : ShortPath(folder);
            destination.tooltip = folder + "\nClick to choose another folder.";
        }

        internal static string FileBrowserName => Application.platform == RuntimePlatform.OSXEditor ? "Finder" : "Explorer";

        private static string ShortPath(string path)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return !string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.OrdinalIgnoreCase) ? "~" + path.Substring(home.Length) : path;
        }

        private void OnDestination()
        {
            if (running != null)
            {
                if (running.Result != null)
                {
                    EditorUtility.RevealInFinder(running.Result);
                }

                return;
            }

            // The folder picker is modal: opened after this press is done.
            rootVisualElement.schedule.Execute(() =>
            {
                string chosen = EditorUtility.OpenFolderPanel("Save reports in", ReportOptions.Folder, string.Empty);
                if (!string.IsNullOrEmpty(chosen))
                {
                    ReportOptions.Folder = chosen;
                    UpdateDestination();
                    ShowToast("Reports go to " + ShortPath(chosen));
                }
            });
        }

        private void UpdateEstimate()
        {
            if (estimate == null || running != null)
            {
                return;
            }

            // Text squeezes about six times in the zip; a Unity package is already compressed.
            long bytes = 64 * 1024;
            if (IsChosen(ReportPart.Logs))
            {
                bytes += (logCount * 140L + Math.Min(editorLogBytes, 32L * 1024 * 1024)) / 6;
            }

            if (IsChosen(ReportPart.Git))
            {
                bytes += 400 * 1024;
            }

            if (IsChosen(ReportPart.SceneExport) && sceneFiles != null)
            {
                bytes += (long)(sceneFiles.Bytes * 0.6);
            }

            estimate.text = "about " + SceneReport.Size(bytes);
        }

        private bool IsChosen(ReportPart part) => cards.TryGetValue(part, out var card) && card.On && !card.ClassListContains("rp-card--disabled");

        private void RunSheen()
        {
            if (goSheen == null || go == null || !go.enabledInHierarchy || go.ClassListContains("rp-go--quiet"))
            {
                return;
            }

            goSheen.AddToClassList("rp-go__sheen--run");
            goSheen.schedule.Execute(() => goSheen.RemoveFromClassList("rp-go__sheen--run")).StartingIn(1000);
        }

        private void SetGo(string text, LoggerGlyph glyph, bool quiet)
        {
            goLabel.text = text;
            goIcon.Glyph = glyph;
            go.EnableInClassList("rp-go--quiet", quiet);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && evt.actionKey && running == null)
            {
                OnGo();
                evt.StopPropagation();
            }
        }

        // ---- Pages ----------------------------------------------------------------------------------------------

        /// <summary>Slides the old page out to the left and the new one in from the right.</summary>
        private void ShowPage(VisualElement page, bool animate)
        {
            if (shownPage == page)
            {
                return;
            }

            var previous = shownPage;
            shownPage = page;
            if (previous != null)
            {
                previous.RemoveFromHierarchy();
            }

            page.RemoveFromClassList("rp-page--leave");
            scroll.Add(page);
            scroll.scrollOffset = Vector2.zero;
            if (animate)
            {
                LoggerUi.Enter(page, "rp-page--enter", 20);
            }
        }

        private void ShowToast(string text, bool error = false) => toasts?.Show(text, error);
    }
}
