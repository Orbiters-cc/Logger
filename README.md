# Logger

## Unreleased

- With Unit Git in the project, the timeline shows its commits (blue) and releases (gold) inside the logged time;
  hover lists them. Other tools can add markers with `TimelineMarkers.Set`.
- Source chips carry the MCB, My Avatar and ReFit logos.
- The time of a row is centred on the row; stack-trace file links line up in one column.

## 0.1.0 — 2026-10-06

- First release: a log hub for the Unity editor that replaces the Console.
- Captures every log of the editor session from any thread, with its time to the millisecond, whether the window is open or not. Logs Unity's console already held when the Logger started are read in too, a slice at a time, so a console with millions of entries never freezes the editor.
- Search as you type, as text or as a .NET regular expression, with match case and stack-trace search; matches are highlighted.
- **Groups**: logs with the same text as one row, with how many times it happened, a sparkline of when, and an order (first seen, last seen, most frequent, errors first). An option groups messages that only differ by numbers, ids or hashes.
- **Sources**: where each log comes from (VRChat SDK, VRCFury, Modular Avatar, Poiyomi, MCB, Unity, the compiler, an Assets folder…), with a filter and per-source counts.
- **Timeline**: every visible log over the session, stacked by level, with Play Mode, compile and reload markers. Click to jump there, drag to keep only that time range.
- **Explanations** for about a hundred common Unity, C# compiler and VRChat SDK messages: what they mean, whether they matter, and how to fix them. Other tools add their own with `LogExplanations.Register`.
- Multi-select with click, Shift and Ctrl, copy the text or the text with stack traces, hide messages, open the source file, select the object a log is about.
- Clear on Play, Build and Recompile and Error Pause are shared with Unity's console.

Unity 2022.3. No dependency: the Logger keeps working when other packages don't compile.

## Get started

Install **Logger** from the Orbiters VPM repository, then open **Tools › Orbiters › Logger** (Ctrl+Alt+L) and dock it
where the Console was. It shows everything logged since Unity started, including what was logged before it was
installed.

## The window

- **Levels**: the three pills show errors, warnings and logs that match the other filters. Click one to show or hide
  it; Alt+click to show only it. The error count flashes when new errors arrive.
- **Search** (Ctrl+F): text anywhere in the message, case-insensitive. **Aa** matches case, **.\*** reads a .NET
  regular expression (an invalid one turns the field red with the reason, and the last results stay), the stack
  button searches stack traces too. Enter applies at once, Escape clears.
- **Sources**: show or hide where logs come from, or show only one ("Only" on hover). Counts follow the search and
  levels.
- **List / Groups**: every log in time order, or one row per text with its count and activity.
- **Timeline**: hover reads the time and counts; click scrolls the list there; drag selects a time range (shown in
  a chip with ✕); double-click or Escape shows the whole session again.
- **Rows** show the time (to the millisecond; to the second for logs read from Unity's console), the source, the
  message and, under it, the method and file that logged it (behind logger wrappers such as `MCBLogger.Log`). A
  bulb means the Logger knows this message; a target means it is about an object in the scene.
- **New logs** follow the end of the list while you are there. Scrolled up, a "N new" pill brings you back.
- **Details** (bottom pane): the full message, the stack trace with links to each file and line (logging frames
  such as `UnityEngine.Debug:Log` folded away), the explanation, and how often the message happens: a chart over
  the session, the count, first and last time, peak per second and average interval. Select several logs to see a
  summary and copy them together.

### Keyboard

| Keys | Action |
| --- | --- |
| ↑ ↓, Page Up/Down, Home/End | Move; with Shift, select a range |
| Ctrl+click, Shift+click | Add to the selection, select a range |
| Ctrl+A | Select every shown log |
| Ctrl+C / Ctrl+Shift+C | Copy the text / the text with stack traces |
| Enter, double-click | Open the source file at the line |
| F3 / Shift+F3 | Next / previous error |
| Delete | Hide messages like the selected ones |
| Ctrl+F | Search |
| Ctrl+L | Clear |
| Escape | Clear the selection, then the time range |

Right-click a log for the same actions plus "Show only / Hide" its source and "Search for this text". Hidden
messages are listed (and shown again) from the chip in the list header.

### Settings

The sliders button opens the settings:

- **Clear on Play, Clear on Build, Clear on Recompile, Error Pause** are Unity's console settings: changing them
  here changes them in the Console too.
- **Group similar messages**: "Loaded 12 assets in 3.4 ms" and "Loaded 7 assets in 1.2 ms" share a group.
- **Compact rows**: one line per log. **Monospace font** for messages.
- **Memory**: how many logs to keep (250K to 6M, 3M by default); the oldest fifth goes when the limit is reached.
- **Unity's console**: open it, or read it again from scratch.

**Clear** (Ctrl+L) empties the Logger and Unity's console. Compile errors that still stand stay, as in Unity.
Clearing Unity's console from its own window doesn't clear the Logger.

## How it works

- **Capture.** `Application.logMessageReceivedThreaded` queues each log with its time from any thread; the main
  thread files them a few milliseconds per editor update. Identical texts are stored once, so a message repeated a
  million times costs one string and 16 bytes per occurrence.
- **Unity's console.** At the start of an editor session the Logger reads what Unity's console already holds
  (`UnityEditor.LogEntries`, through compiled delegates): the first two lines and timestamp of each row, the full
  entry once per distinct message. Up to 20,000 rows are read at once; more are read a slice per editor update with
  progress in the toolbar, and appear when done. A step behind the live capture, each new console row is matched to
  its log: that adds the object a log is about and the file of engine errors, and any row the Logger never received
  live is added, so nothing Unity shows is missing.
- **Compiler messages** come from the compilation pipeline. A compilation that reports a message again replaces the
  earlier one; one that no longer reports it (or a script reload, which only happens once everything compiles) hides
  it, as in Unity's console.
- **Script reloads.** Before a reload the session is written to `Library/Orbiters/Logger/session.bin` (columns as
  raw arrays: a million logs in tens of milliseconds) and read back after it, with what Unity logged in between. The
  file belongs to the editor process; it is deleted when Unity quits.
- **Filtering** is one pass over the logs that fills the rows, group counts, per-level and per-source counts, the
  timeline and the sparklines. Search runs once per distinct text, not per log. Passes over large sessions run on a
  worker thread while the last results stay on screen; new logs are added to the current results as they arrive.
- **The list** is virtual and scrolls by row index, so a hundred million rows scroll like a hundred (Unity's
  ListView loses pixels past a few million rows).
- **No dependency.** The Logger only uses Unity's editor API, so it keeps working while other packages fail to
  compile. Its building blocks follow Orbiters Toolkit's look and its press-first buttons.

## Unit Git

When Unit Git 0.2 or newer is installed, an extra assembly (`Orbiters.Logger.Editor.UnitGit`, compiled only then)
puts the project's history on the timeline: Unit Git releases from its release catalog, and commits from `git log`
(the last 30 days, read on a worker thread). Only what happened inside the logged time shows. It refreshes when Unit
Git records something and every minute while a Logger window is open.

Other tools can add their own markers:

```csharp
TimelineMarkers.Set("mytool", new[] { new TimelineMarker(DateTime.UtcNow, TimelineMarkerKind.Note, "Avatar uploaded") });
```

## Explanations for your tool

```csharp
using Orbiters.Logger.Editor;

[UnityEditor.InitializeOnLoad]
static class MyToolExplanations
{
    static MyToolExplanations()
    {
        LogExplanations.Register(new LogExplanation("mytool.no-avatar", "[MyTool] No avatar", "No avatar selected")
        {
            Source = "My Tool",
            Severity = LogExplanationSeverity.Warning,
            Summary = "My Tool needs an avatar with a VRC Avatar Descriptor.",
            Fixes = new[] { "Select the avatar root in the Hierarchy.", "Click Refresh in My Tool." },
            StackNeedle = null,     // text the stack trace must contain, for messages shared with other tools
            Pattern = null          // a regular expression; named groups fill {name} placeholders
        });
    }
}
```

Registered explanations are tried before the built-in ones. Reference the `Orbiters.Logger.Editor` assembly through
a version define so your tool still compiles without the Logger.
