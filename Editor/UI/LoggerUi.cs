using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orbiters.Logger.Editor
{
    /// <summary>
    /// The Logger's building blocks, in the style of Unit Git and My Avatar: buttons that answer on press (they scale and
    /// act at once, Unity's click stays as the fallback), icon buttons, pills, chips, a spinner, toasts and a segmented
    /// control with a sliding highlight. The Logger has no package dependency so it keeps working when other packages
    /// don't compile; these mirror Orbiters Toolkit's <c>ButtonInteraction</c> for that reason.
    /// </summary>
    internal static class LoggerUi
    {
        internal const string PressedClass = "lg-pressed";

        /// <summary>Shows the press at once and runs <paramref name="action"/> on press; a click still works (keyboard, assistive input).</summary>
        internal static void Press(VisualElement element, Action action)
        {
            bool handledByPress = false;
            long lastPress = 0;
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !element.enabledInHierarchy)
                {
                    return;
                }

                element.AddToClassList(PressedClass);
                long now = DateTime.UtcNow.Ticks;
                if (now - lastPress < TimeSpan.TicksPerMillisecond * 25)
                {
                    return;
                }

                lastPress = now;
                handledByPress = true;
                element.schedule.Execute(() => handledByPress = false).StartingIn(800);
                // Keep Unity's Clickable from capturing the pointer: a menu or popup opened here takes the pointer-up,
                // the capture would stay, and every later click would land on this button again.
                evt.StopImmediatePropagation();
                evt.PreventDefault();
                action?.Invoke();
            }, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerUpEvent>(_ => element.RemoveFromClassList(PressedClass));
            element.RegisterCallback<PointerLeaveEvent>(_ => element.RemoveFromClassList(PressedClass));
            element.RegisterCallback<PointerCaptureOutEvent>(_ => element.RemoveFromClassList(PressedClass));
            if (element is Button button)
            {
                button.clicked += () =>
                {
                    if (handledByPress)
                    {
                        handledByPress = false;
                        return;
                    }

                    action?.Invoke();
                };
            }

            element.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (element is Button)
                {
                    return;
                }

                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Space)
                {
                    action?.Invoke();
                    evt.StopPropagation();
                }
            });
        }

        internal static Button Icon(LoggerGlyph glyph, string tooltip, Action action, string extraClass = null)
        {
            var button = new Button { tooltip = tooltip };
            button.AddToClassList("lg-icon-button");
            if (!string.IsNullOrEmpty(extraClass))
            {
                button.AddToClassList(extraClass);
            }

            button.Add(new LoggerIcon(glyph));
            Press(button, action);
            return button;
        }

        /// <summary>A rounded button: <c>primary</c> (green), <c>ghost</c> (quiet), <c>danger</c> or the default grey.</summary>
        internal static Button Pill(string text, Action action, string variant = null, LoggerGlyph? glyph = null, string tooltip = null)
        {
            var button = new Button { tooltip = tooltip };
            button.AddToClassList("lg-button");
            if (!string.IsNullOrEmpty(variant))
            {
                button.AddToClassList("lg-button--" + variant);
            }

            if (glyph.HasValue)
            {
                button.AddToClassList("lg-button--with-icon");
                button.Add(new LoggerIcon(glyph.Value));
            }

            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("lg-button__label");
            button.Add(label);
            Press(button, action);
            return button;
        }

        internal static Label Text(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        internal static VisualElement Box(string className, PickingMode picking = PickingMode.Position)
        {
            var element = new VisualElement { pickingMode = picking };
            element.AddToClassList(className);
            return element;
        }

        internal static VisualElement Spacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("lg-spacer");
            return spacer;
        }

        internal static VisualElement Separator()
        {
            var separator = new VisualElement { pickingMode = PickingMode.Ignore };
            separator.AddToClassList("lg-separator");
            return separator;
        }

        internal static VisualElement Spinner()
        {
            var spinner = new VisualElement { pickingMode = PickingMode.Ignore };
            spinner.AddToClassList("lg-spinner");
            var arc = new VisualElement { pickingMode = PickingMode.Ignore };
            arc.AddToClassList("lg-spinner__arc");
            spinner.Add(arc);
            spinner.schedule.Execute(() =>
            {
                if (spinner.resolvedStyle.display == DisplayStyle.None || spinner.panel == null)
                {
                    return;
                }

                float angle = (float)(EditorApplication.timeSinceStartup * 420d % 360d);
                arc.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }).Every(16);
            return spinner;
        }

        /// <summary>Plays a class-driven entrance: the element starts with <paramref name="from"/>, removed a frame later.</summary>
        internal static void Enter(VisualElement element, string from, long delay = 16)
        {
            if (element == null)
            {
                return;
            }

            element.AddToClassList(from);
            element.schedule.Execute(() => element.RemoveFromClassList(from)).StartingIn(delay);
        }

        internal static void Show(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ---- Formatting -------------------------------------------------------------------------------------------

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>1234 → "1,234".</summary>
        internal static string Count(long value) => value.ToString("N0", Culture);

        /// <summary>1234 → "1.2K", 1834438 → "1.8M": for tight places (pills, badges).</summary>
        internal static string Compact(long value)
        {
            if (value < 10_000)
            {
                return value.ToString("N0", Culture);
            }

            if (value < 1_000_000)
            {
                return (value / 1000d).ToString(value < 100_000 ? "0.#" : "0", Culture) + "K";
            }

            return (value / 1_000_000d).ToString(value < 100_000_000 ? "0.#" : "0", Culture) + "M";
        }

        internal static DateTime Local(long utcTicks) => new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();

        internal static string Time(long utcTicks, bool milliseconds)
        {
            if (utcTicks <= 0)
            {
                return "--:--:--";
            }

            return Local(utcTicks).ToString(milliseconds ? "HH:mm:ss.fff" : "HH:mm:ss", Culture);
        }

        /// <summary>A date and time, the date only when it isn't today.</summary>
        internal static string Moment(long utcTicks)
        {
            var local = Local(utcTicks);
            return local.Date == DateTime.Today ? local.ToString("HH:mm:ss", Culture) : local.ToString("MMM d, HH:mm:ss", Culture);
        }

        internal static string Duration(TimeSpan span)
        {
            if (span.TotalSeconds < 1)
            {
                return Math.Max(0, (int)span.TotalMilliseconds) + " ms";
            }

            if (span.TotalMinutes < 1)
            {
                return span.TotalSeconds.ToString(span.TotalSeconds < 10 ? "0.#" : "0", Culture) + " s";
            }

            if (span.TotalHours < 1)
            {
                return (int)span.TotalMinutes + " min " + (span.Seconds > 0 ? span.Seconds + " s" : string.Empty);
            }

            if (span.TotalDays < 1)
            {
                return (int)span.TotalHours + " h " + (span.Minutes > 0 ? span.Minutes + " min" : string.Empty);
            }

            return (int)span.TotalDays + " d " + span.Hours + " h";
        }

        internal static string Ago(long utcTicks)
        {
            var span = TimeSpan.FromTicks(Math.Max(0, DateTime.UtcNow.Ticks - utcTicks));
            if (span.TotalSeconds < 2)
            {
                return "just now";
            }

            return Duration(span).Trim() + " ago";
        }

        internal static string Plural(long count, string singular, string plural = null) =>
            Count(count) + " " + (count == 1 ? singular : plural ?? singular + "s");

        internal static string ColorHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    /// <summary>Two or three options in one rounded track; a highlight slides to the chosen one on press.</summary>
    internal sealed class SegmentedControl : VisualElement
    {
        private readonly VisualElement indicator = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly List<Button> options = new List<Button>();
        private readonly Action<int> changed;
        private int selected = -1;
        private bool placed;

        public SegmentedControl(Action<int> changed)
        {
            this.changed = changed;
            AddToClassList("lg-segmented");
            indicator.AddToClassList("lg-segmented__indicator");
            Add(indicator);
            RegisterCallback<GeometryChangedEvent>(_ => Place(placed));
        }

        public int Selected => selected;

        public Button AddOption(string text, LoggerGlyph glyph, string tooltip)
        {
            int index = options.Count;
            var button = new Button { tooltip = tooltip };
            button.AddToClassList("lg-segmented__option");
            button.Add(new LoggerIcon(glyph));
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("lg-segmented__label");
            button.Add(label);
            LoggerUi.Press(button, () =>
            {
                if (selected == index)
                {
                    return;
                }

                Select(index);
                changed?.Invoke(index);
            });
            button.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                if (index == selected)
                {
                    Place(placed);
                }
            });
            options.Add(button);
            Add(button);
            return button;
        }

        public void Select(int index)
        {
            selected = index;
            for (int i = 0; i < options.Count; i++)
            {
                options[i].EnableInClassList("lg-segmented__option--on", i == index);
            }

            Place(placed);
        }

        private void Place(bool animate)
        {
            if (selected < 0 || selected >= options.Count)
            {
                indicator.style.opacity = 0;
                return;
            }

            var box = options[selected].layout;
            if (float.IsNaN(box.width) || box.width <= 0)
            {
                return;
            }

            indicator.EnableInClassList("lg-segmented__indicator--instant", !animate);
            indicator.style.opacity = 1;
            indicator.style.left = box.x;
            indicator.style.top = box.y;
            indicator.style.width = box.width;
            indicator.style.height = box.height;
            placed = true;
        }
    }

    /// <summary>Short confirmations and errors that slide in at the bottom right and fade away on their own.</summary>
    internal sealed class Toasts : VisualElement
    {
        public Toasts()
        {
            AddToClassList("lg-toasts");
            pickingMode = PickingMode.Ignore;
        }

        public void Show(string text, bool error = false)
        {
            var toast = new VisualElement();
            toast.AddToClassList("lg-toast");
            if (error)
            {
                toast.AddToClassList("lg-toast--error");
            }

            var icon = new LoggerIcon(error ? LoggerGlyph.Warning : LoggerGlyph.Check);
            icon.AddToClassList("lg-toast__icon");
            toast.Add(icon);
            toast.Add(LoggerUi.Text(text, "lg-toast__text"));
            toast.Add(LoggerUi.Icon(LoggerGlyph.Close, "Dismiss", () => Dismiss(toast), "lg-toast__close"));
            while (childCount >= 3)
            {
                RemoveAt(0);
            }

            Add(toast);
            LoggerUi.Enter(toast, "lg-toast--enter", 20);
            toast.schedule.Execute(() => Dismiss(toast)).StartingIn(error ? 7000 : 2600);
        }

        private static void Dismiss(VisualElement toast)
        {
            if (toast.parent == null || toast.ClassListContains("lg-toast--leave"))
            {
                return;
            }

            toast.AddToClassList("lg-toast--leave");
            toast.schedule.Execute(() => toast.RemoveFromHierarchy()).StartingIn(240);
        }
    }

    /// <summary>
    /// The search field: a magnifier, a placeholder, toggles for match case, regular expression and stack traces, and a
    /// clear button. An invalid regular expression turns the field red with the reason in its tooltip.
    /// </summary>
    internal sealed class SearchBox : VisualElement
    {
        private readonly TextField field;
        private readonly Label placeholder;
        private readonly Button clear;
        private readonly Button caseToggle;
        private readonly Button regexToggle;
        private readonly Button stackToggle;

        public event Action<string> TextChanged;
        public event Action OptionsChanged;
        public event Action Submitted;
        public event Action Escaped;
        public event Action DownPressed;

        public SearchBox(string value, bool matchCase, bool regex, bool stack)
        {
            AddToClassList("lg-search");
            var icon = new LoggerIcon(LoggerGlyph.Search);
            icon.AddToClassList("lg-search__icon");
            field = new TextField { value = value ?? string.Empty };
            field.AddToClassList("lg-search__field");
            Add(field);
            Add(icon);
            placeholder = new Label("Search logs") { pickingMode = PickingMode.Ignore };
            placeholder.AddToClassList("lg-search__placeholder");
            var hint = new Label("Ctrl+F") { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("lg-search__hint");
            placeholder.Add(hint);
            Add(placeholder);

            var options = LoggerUi.Box("lg-search__options");
            caseToggle = Toggle("Aa", "Match case", matchCase);
            regexToggle = Toggle(".*", "Regular expression (.NET syntax)", regex);
            stackToggle = new Button { tooltip = "Search stack traces too" };
            stackToggle.AddToClassList("lg-search__toggle");
            stackToggle.Add(new LoggerIcon(LoggerGlyph.Stack));
            stackToggle.EnableInClassList("lg-search__toggle--on", stack);
            LoggerUi.Press(stackToggle, () =>
            {
                stackToggle.ToggleInClassList("lg-search__toggle--on");
                OptionsChanged?.Invoke();
            });
            clear = LoggerUi.Icon(LoggerGlyph.Close, "Clear the search (Esc)", () =>
            {
                field.value = string.Empty;
                field.Q(className: "unity-text-field__input")?.Focus();
            }, "lg-search__clear");
            options.Add(clear);
            options.Add(caseToggle);
            options.Add(regexToggle);
            options.Add(stackToggle);
            Add(options);

            field.RegisterValueChangedCallback(evt =>
            {
                Sync();
                TextChanged?.Invoke(evt.newValue ?? string.Empty);
            });
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Submitted?.Invoke();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    if (!string.IsNullOrEmpty(field.value))
                    {
                        field.value = string.Empty;
                    }
                    else
                    {
                        Escaped?.Invoke();
                    }

                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.DownArrow)
                {
                    DownPressed?.Invoke();
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            Sync();
        }

        public string Value
        {
            get => field.value ?? string.Empty;
            set => field.SetValueWithoutNotify(value ?? string.Empty);
        }

        public bool MatchCase => caseToggle.ClassListContains("lg-search__toggle--on");
        public bool Regex => regexToggle.ClassListContains("lg-search__toggle--on");
        public bool Stack => stackToggle.ClassListContains("lg-search__toggle--on");

        public void FocusField()
        {
            var input = field.Q(className: "unity-text-field__input") ?? field.Q(className: "unity-base-text-field__input");
            (input ?? field).Focus();
            field.SelectAll();
        }

        public bool HasFocus => field.panel != null && field.panel.focusController?.focusedElement is VisualElement focused && (focused == field || field.Contains(focused));

        public void SetError(string error)
        {
            EnableInClassList("lg-search--error", !string.IsNullOrEmpty(error));
            field.tooltip = string.IsNullOrEmpty(error) ? null : "Invalid regular expression: " + error;
        }

        public void Sync()
        {
            bool empty = string.IsNullOrEmpty(field.value);
            placeholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            clear.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            EnableInClassList("lg-search--active", !empty);
        }

        private Button Toggle(string text, string tooltip, bool on)
        {
            var button = new Button { text = text, tooltip = tooltip };
            button.AddToClassList("lg-search__toggle");
            button.AddToClassList("lg-search__toggle--text");
            button.EnableInClassList("lg-search__toggle--on", on);
            LoggerUi.Press(button, () =>
            {
                button.ToggleInClassList("lg-search__toggle--on");
                OptionsChanged?.Invoke();
            });
            return button;
        }
    }
}
