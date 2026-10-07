# Logger

## 0.2.0 — 2026-10-07

- **Timings**: every script reload, entry into Play Mode and avatar build or upload is logged with what each package
  took, as a stacked bar in the list, a breakdown by package in the details (click a package for its parts) with the
  same timing over the session, and a mark per package on the timeline's new lane. Reloads are read from Unity's own
  reload profile (its `EnableDomainReloadTimings` diagnostic switch is turned on for that, and its console notice about
  it hidden); builds and Play Mode time each tool's VRChat SDK build step. Each can be turned off in the settings.
- **Since which commit**: a message that keeps coming back shows when it was first seen (across editor sessions and
  clears) and the commit the project was at then, from Git's own history of HEAD, with how many commits ago.
- **Explanations** for every message of a real creator session: 311 entries (VRCFury, MCB, ReFit, Unit Git, MCP for
  Unity, Unity…), kept in `Editor/Knowledge/explanations.json` and kept up to date from the Orbiters server between
  releases (members signed in through Orbiters Toolkit also get the entries for members). Entries have a priority, so a
  tool's catch-all never hides a specific explanation.
- **Undo history timeline (beta, off by default)**: your undo steps under the activity chart with a snapshot of the
  Scene view after each; hover to preview, click or drag to go back and forth, scroll for one step.
- Hover read-outs of the charts draw above everything (they were hidden behind the rows under them).
- Importer messages that start with an asset path ("Packages/x/y.uss (line 27): …") belong to that asset's package, and
  logs whose stack trace Unity cut take their source from the file Unity's console names.
- "Unused code warning" no longer explains CS0108, CS0114 and CS0162.

## 0.1.2 — 2026-10-07

- Commits and releases on the timeline carry a small label.
- With Unit Git 0.2.5 or newer, every Git command Unit Git runs is a log under the Unit Git source: the command and its
  result as the message, its output (timing, stdout, stderr) in the details. They don't go to Unity's console.
- A commit shows on the timeline as soon as it is made (from Unit Git or any Git client), and project pins sit in a lane
  at the top so Play Mode and reload markers no longer hide them.

## 0.1.1 — 2026-10-07

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
- **Timings**: measure script reloads, entering Play Mode, and avatar builds and uploads (all on by default).
- **Beta**: the undo history timeline.

**Clear** (Ctrl+L) empties the Logger and Unity's console. Compile errors that still stand stay, as in Unity.
Clearing Unity's console from its own window doesn't clear the Logger.

## Timings

Each script reload, entry into Play Mode and avatar build or upload becomes a log under the **Timings** source:
"Script reload took 6.6 s", "Entered Play Mode in 9.1 s", "Built and uploaded Rexouium in 2 min 13 s". Its row shows the
time as a stacked bar, one colour per package (the engine's part in grey at the end), scaled to the slowest of its
kind so rows compare at a glance. The details show the total, how it compares with the usual time, each package's
share (click one for its parts: which startup method, which build step) and the same kind of timing over the session
as stacked columns: when a package suddenly costs more, it shows. The timeline has a lane with one mark per timing,
stacked by package; click a mark to show its log.

- **Script reloads**: Unity writes a "Domain Reload Profiling" tree to Editor.log after each reload. With its
  `EnableDomainReloadTimings` diagnostic switch on (the Logger turns it on while reloads are measured, and back off when
  you turn measuring off) the tree lists every type and method that runs on load, every before/after-reload callback
  and every window restored; the Logger reads it back on a worker thread and gives each entry to the package its code
  belongs to. Unity's console notice about the switch is hidden while it is the only one on.
- **Avatar builds and uploads** (VRChat SDK, through the optional `Orbiters.Logger.Editor.VRChat` assembly): from
  Build & Publish or Build & Test to the end of the upload. A probe sits in front of each group of build steps in the
  SDK's list, so each tool's step (VRCFury, MCB, My Avatar, ReFit, Orbiters Toolkit, the SDK's own…) is timed without
  touching it, then the asset bundle and the upload.
- **Entering Play Mode**: leaving Edit Mode, the script reload by package, the avatar build steps VRCFury runs on each
  avatar of the scene (merged across avatars), then the scene load and first frames.

## Since which commit

For a message that keeps coming back, the details show **Appearing since**: the commit the project was at when it was
first seen, its subject, when that was and how many commits ago. First and last appearances are kept per message in
`Library/Orbiters/Logger/ledger.bin` (across editor sessions and clears, up to 100,000 messages); the commit comes
from Git's own history of HEAD (`.git/logs/HEAD`), so no Git process runs. Click the hash to copy it.

## Undo history timeline (beta)

Off by default: turn on **Undo history timeline** in the settings. A strip under the activity chart shows Unity's
undo steps (Edit › Undo History) like a video's progress bar: one tick per step (selections smaller), the part already
done filled, the playhead where the project is. Hover a step for a snapshot of the Scene view right after it, its
name and when it happened (its moment is also marked on the activity chart). Click or drag to go there: the Logger
undoes or redoes one step at a time until the project is as it was right after that step. Scroll over the strip for
one step back or forward. Snapshots are read from the Scene view window's own framebuffer when it is the visible tab
(no extra render), scaled down on the GPU and kept as small JPEGs (the last 600).

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

### The explanation database

The Logger's own explanations are in `Editor/Knowledge/explanations.json` (`{ "explanations": [ … ] }`), one object per
entry: `id`, `needle` (text the message must contain, checked first), optional `pattern` (.NET regex with named
groups for `{name}` placeholders), `stackNeedle`, `levels` (`error`, `warning`, `log`), `priority` (catch-alls use
negative values), `severity` (`harmless`, `info`, `warning`, `problem`, `blocking`), `source`, `title`, `summary`,
`fixes`, `link`, `linkLabel`. Entries tied to a stack trace are tried first, then by priority, then in file order.

The same list lives on the Orbiters server (`GET /logger/explanations`, with `If-None-Match`), where it is updated
between releases: the Logger asks for it every six hours, keeps the last answer per user in
`%LOCALAPPDATA%/Orbiters/Logger/`, and server entries replace local ones with the same id. With Orbiters Toolkit
installed (optional `Orbiters.Logger.Editor.Toolkit` assembly), it asks the server Orbiters tools use, as the
signed-in member.
