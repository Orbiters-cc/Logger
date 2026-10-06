using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// The Logger: Unity's console rebuilt for big sessions. Logs come from <see cref="LogCapture"/> (always running),
    /// filtering from <see cref="FilterRunner"/>, so the window only draws what is on screen. Partial files: toolbar,
    /// list, details, popups.
    /// </summary>
    internal sealed partial class LoggerWindow : EditorWindow, IHasCustomMenu
    {
        private const string StyleSheetPath = "Packages/orbiters.logger/Editor/UI/logger.uss";
        private const float ComfortableRowHeight = 40f;
        private const float CompactRowHeight = 24f;
        private const string CompactPref = "Orbiters.Logger.Compact";
        private const string MonospacePref = "Orbiters.Logger.Monospace";
        private const int TickMs = 40;

        private static int openWindows;

        [SerializeField] private int levelMask = 7;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private bool useRegex;
        [SerializeField] private bool matchCase;
        [SerializeField] private bool searchStack;
        [SerializeField] private bool grouped;
        [SerializeField] private GroupSort sort = GroupSort.FirstSeen;
        [SerializeField] private List<string> hiddenSources = new List<string>();
        [SerializeField] private List<string> muted = new List<string>();
        [SerializeField] private long rangeFrom;
        [SerializeField] private long rangeTo;
        [SerializeField] private float detailsPaneHeight = 320f;

        private readonly FilterRunner runner = new FilterRunner();
        private LogStore boundStore;
        private FilterState state;
        private LogQuery query = LogQuery.Empty;
        private int seenVersion = -1;
        private bool specDirty;
        private double specDueAt;
        private double lastFullPass;
        private int fullPassVersion = -1;
        private double lastSort;
        private double lastChrome;
        private bool chromeDirty = true;
        private bool detailsDirty = true;
        private int newRows;
        private bool built;

        internal static bool IsOpen => openWindows > 0;

        private LogStore Store => LogCapture.Store;
        private bool Compact => EditorPrefs.GetBool(CompactPref, false);
        private float RowHeight => Compact ? CompactRowHeight : ComfortableRowHeight;
        private int ItemCount => state == null ? 0 : grouped ? state.GroupRowCount : state.RowCount;

        [MenuItem(LoggerInfo.MenuPath + " %&l", priority = 2000)]
        public static void Open()
        {
            var window = GetWindow<LoggerWindow>();
            window.Show();
        }

        private void OnEnable()
        {
            openWindows++;
            var icon = EditorGUIUtility.IconContent("UnityEditor.ConsoleWindow");
            titleContent = new GUIContent(LoggerInfo.DisplayName, icon?.image, "Orbiters Logger");
            minSize = new Vector2(520f, 300f);
            AssemblyReloadEvents.beforeAssemblyReload += runner.Cancel;
        }

        private void OnDisable()
        {
            openWindows = Math.Max(0, openWindows - 1);
            AssemblyReloadEvents.beforeAssemblyReload -= runner.Cancel;
            runner.Cancel();
            Unbind();
            DisposeDetails();
        }

        private void Unbind()
        {
            if (boundStore == null)
            {
                return;
            }

            boundStore.Shifted -= OnShifted;
            boundStore.Emptied -= ClearSelection;
            boundStore.Regrouped -= OnRegrouped;
            boundStore = null;
        }

        private void OnRegrouped()
        {
            if (grouped)
            {
                ClearSelection();
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("lg-root");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null && !rootVisualElement.styleSheets.Contains(sheet))
            {
                rootVisualElement.styleSheets.Add(sheet);
            }

            rootVisualElement.EnableInClassList("lg-root--compact", Compact);
            rootVisualElement.EnableInClassList("lg-root--mono", EditorPrefs.GetBool(MonospacePref, false));
            topBar = BuildTopBar();
            rootVisualElement.Add(topBar);
            searchRow = LoggerUi.Box("lg-searchrow");
            searchRow.style.display = DisplayStyle.None;
            rootVisualElement.Add(searchRow);
            rootVisualElement.Add(BuildTimeline());
            rootVisualElement.Add(BuildBody());
            rootVisualElement.Add(BuildFooter());
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
            toasts = new Toasts();
            rootVisualElement.Add(toasts);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            built = true;

            query = new LogQuery(search, useRegex, matchCase, searchStack);
            searchBox.SetError(query.Error);
            if (query.Error != null)
            {
                query = LogQuery.Empty;
            }

            Bind(Store);
            Refilter(allowAsync: false);
            list.ScrollToEnd();
            rootVisualElement.schedule.Execute(Tick).Every(TickMs);
            UpdateChrome(force: true);
        }

        private void Bind(LogStore store)
        {
            if (boundStore == store)
            {
                return;
            }

            Unbind();
            boundStore = store;
            if (boundStore != null)
            {
                boundStore.Shifted += OnShifted;
                boundStore.Emptied += ClearSelection;
                boundStore.Regrouped += OnRegrouped;
            }

            ClearSelection();
            state = null;
        }

        // History merged in front of the logs, or the oldest trimmed: selected logs keep pointing at the same logs.
        private void OnShifted(int offset)
        {
            if (grouped)
            {
                return;
            }

            ShiftSelection(offset);
        }

        private FilterSpec BuildSpec()
        {
            var spec = new FilterSpec
            {
                LevelMask = levelMask,
                Query = query,
                From = rangeFrom,
                To = rangeTo,
                Grouped = grouped,
                Sort = sort
            };
            foreach (string key in hiddenSources)
            {
                spec.HiddenSources.Add(key);
            }

            foreach (string key in muted)
            {
                spec.Muted.Add(key);
            }

            return spec;
        }

        /// <summary>Asks for a new filter pass, a moment later when typing (<paramref name="delay"/> seconds).</summary>
        private void QueueRefilter(double delay = 0d)
        {
            specDirty = true;
            specDueAt = EditorApplication.timeSinceStartup + delay;
        }

        private void Refilter(bool allowAsync)
        {
            specDirty = false;
            var store = Store;
            if (store == null || !built)
            {
                return;
            }

            long now = LogCapture.Now;
            long chartFrom = store.Count > 0 ? store.FirstTime : now - TimeSpan.TicksPerMinute;
            long chartTo = Math.Max(store.LastTime, now);
            if (chartTo - chartFrom < TimeSpan.TicksPerSecond * 30)
            {
                chartTo = chartFrom + TimeSpan.TicksPerSecond * 30;
            }

            int buckets = Mathf.Clamp((int)(timeline.PlotWidth / 4f), 30, 500);
            lastFullPass = EditorApplication.timeSinceStartup;
            fullPassVersion = store.Version;
            var result = runner.Request(store, BuildSpec(), state, chartFrom, chartTo, buckets, allowAsync);
            if (result != null)
            {
                Adopt(result);
            }
        }

        private void Adopt(FilterState result)
        {
            var store = Store;
            if (result == null || store == null)
            {
                return;
            }

            if (!result.Extend(store, store.Sources.All))
            {
                // The store changed shape while the pass ran (cleared, merged): start again.
                Refilter(allowAsync: false);
                return;
            }

            bool atEnd = list == null || list.AtEnd || state == null;
            int cursorRow = -1;
            state = result;
            seenVersion = store.Version;
            newRows = 0;
            if (list != null)
            {
                list.SetItemCount(ItemCount, keepAtEnd: false);
                cursorRow = cursor >= 0 ? RowOf(cursor) : -1;
                if (atEnd)
                {
                    list.ScrollToEnd();
                }
                else if (cursorRow >= 0)
                {
                    list.Reveal(cursorRow);
                }

                list.RefreshRows();
            }

            chromeDirty = true;
            detailsDirty = true;
        }

        private void Tick()
        {
            try
            {
                TickCore();
            }
            catch (Exception exception)
            {
                // A failing refresh must not throw every 40 ms into the log it shows.
                if (EditorApplication.timeSinceStartup - lastTickError > 10d)
                {
                    lastTickError = EditorApplication.timeSinceStartup;
                    Debug.LogWarning("[Logger] " + exception);
                }
            }
        }

        private double lastTickError = -100d;

        private void TickCore()
        {
            var store = Store;
            if (store == null || !built)
            {
                return;
            }

            if (store != boundStore)
            {
                Bind(store);
                Refilter(allowAsync: false);
            }

            var ready = runner.TryTake();
            if (ready != null)
            {
                Adopt(ready);
            }

            double now = EditorApplication.timeSinceStartup;
            if (state == null || state.Epoch != store.Epoch || state.MessagesVersion != store.MessagesVersion || !ReferenceEquals(state.Texts, store.Texts))
            {
                // Row ids no longer match the store: never draw them, recompute now.
                Refilter(allowAsync: false);
            }
            else if (store.Version != seenVersion)
            {
                Catchup(store, now);
            }

            if (specDirty && now >= specDueAt)
            {
                Refilter(allowAsync: true);
            }
            else if (!runner.Busy && timeline != null && timeline.resolvedStyle.display != DisplayStyle.None)
            {
                // The chart keeps up with time: every second while logs arrive, every ten seconds otherwise.
                double age = now - lastFullPass;
                if (age > 1d && store.Version != fullPassVersion || age > 10d)
                {
                    Refilter(allowAsync: true);
                }
            }

            if (chromeDirty && now - lastChrome > 0.1d)
            {
                UpdateChrome(force: false);
            }

            if (detailsDirty)
            {
                UpdateDetails();
            }
        }

        // New logs since the last pass: added to the view in place.
        private void Catchup(LogStore store, double now)
        {
            int before = ItemCount;
            bool atEnd = list.AtEnd;
            if (!state.Extend(store, store.Sources.All))
            {
                Refilter(allowAsync: false);
                return;
            }

            seenVersion = store.Version;
            int after = ItemCount;
            if (grouped && state.GroupOrderDirty && now - lastSort > 0.4d)
            {
                lastSort = now;
                state.SortGroups(store.View());
            }

            if (after != before)
            {
                list.SetItemCount(after, keepAtEnd: false);
                if (atEnd)
                {
                    list.ScrollToEnd();
                }
                else
                {
                    newRows += after - before;
                }
            }

            if (grouped || after != before)
            {
                list.RefreshRows();
            }

            chromeDirty = true;
            if (SelectionTouchesNewData())
            {
                detailsDirty = true;
            }
        }

        // ---- Keyboard -------------------------------------------------------------------------------------------

        private void OnKeyDown(KeyDownEvent evt)
        {
            bool command = evt.ctrlKey || evt.commandKey;
            bool typing = searchBox != null && searchBox.HasFocus;
            if (command && evt.keyCode == KeyCode.F)
            {
                searchBox.FocusField();
                evt.StopPropagation();
                return;
            }

            if (command && evt.keyCode == KeyCode.L)
            {
                ClearLogs();
                evt.StopPropagation();
                return;
            }

            if (typing)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    MoveCursor(-1, evt.shiftKey);
                    break;
                case KeyCode.DownArrow:
                    MoveCursor(1, evt.shiftKey);
                    break;
                case KeyCode.PageUp:
                    MoveCursor(-Math.Max(1, (int)list.VisibleRows - 1), evt.shiftKey);
                    break;
                case KeyCode.PageDown:
                    MoveCursor(Math.Max(1, (int)list.VisibleRows - 1), evt.shiftKey);
                    break;
                case KeyCode.Home:
                    MoveCursorTo(0, evt.shiftKey);
                    break;
                case KeyCode.End:
                    MoveCursorTo(ItemCount - 1, evt.shiftKey);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenCursor();
                    break;
                case KeyCode.Escape:
                    if (HasSelection)
                    {
                        ClearSelection();
                    }
                    else if (rangeTo > 0)
                    {
                        SetRange(0, 0);
                    }
                    else
                    {
                        return;
                    }

                    break;
                case KeyCode.A when command:
                    SelectAll();
                    break;
                case KeyCode.C when command:
                    Copy(withStack: evt.shiftKey);
                    break;
                case KeyCode.F3:
                    JumpToLevel(LogLevel.Error, evt.shiftKey ? -1 : 1);
                    break;
                case KeyCode.Delete:
                    HideSelected();
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
        }

        // ---- Window menu ----------------------------------------------------------------------------------------

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open Unity's Console"), false, () => EditorApplication.ExecuteMenuItem("Window/General/Console"));
            menu.AddItem(new GUIContent("Rebuild from Unity's Console"), false, () =>
            {
                LogCapture.RebuildFromUnityConsole();
                ShowToast("Reading Unity's console again");
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Compact rows"), Compact, () => SetCompact(!Compact));
            menu.AddItem(new GUIContent("Monospace font"), IsMonospace, () => SetMonospace(!IsMonospace));
        }

        internal bool IsCompact => Compact;
        internal bool IsMonospace => EditorPrefs.GetBool(MonospacePref, false);

        internal void SetCompact(bool on)
        {
            EditorPrefs.SetBool(CompactPref, on);
            if (!built)
            {
                return;
            }

            rootVisualElement.EnableInClassList("lg-root--compact", on);
            list.SetRowHeight(RowHeight);
            list.RefreshRows();
        }

        internal void SetMonospace(bool on)
        {
            EditorPrefs.SetBool(MonospacePref, on);
            if (!built)
            {
                return;
            }

            rootVisualElement.EnableInClassList("lg-root--mono", on);
            shownKey = int.MinValue;
            detailsDirty = true;
            list.RefreshRows();
        }

        private void ShowToast(string text, bool error = false) => toasts?.Show(text, error);

        private Toasts toasts;
    }
}
