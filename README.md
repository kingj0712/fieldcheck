# FieldCheck

> A lightweight, self-contained Windows checklist app for commissioning, controls verification,
> and field punchlists. Projects → Checklists → Sections → Items → Tags. All data stays local —
> no install, no account, no cloud.

![Windows](https://img.shields.io/badge/platform-Windows-blue)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![License: MIT](https://img.shields.io/badge/license-MIT-green)

<!-- A screenshot sells a desktop app. Save one to docs/screenshot.png and uncomment: -->
<!-- ![FieldCheck](docs/screenshot.png) -->

## Download

Download the latest **`FieldCheck.exe`** from the [Releases](../../releases) page and double-click
it — no installer, and no .NET runtime required. It is a single self-contained file; all your data
is stored locally as JSON in your user profile.

---

## How it's organized

```
Project
  Checklist
    Section
      Item  (text, notes, tags)
```

- **Projects** are first-class containers (e.g. a job or building). The sidebar groups checklists
  under their project, and each group can be expanded/collapsed.
- A **Checklist** belongs to one project and holds items.
- Each **Item** belongs to one **Section** (a structural group — items are shown under section
  headers). A blank section shows as **General**.
- Each item can carry zero or more **Tags** — non-exclusive metadata pills (e.g. `AHU-1`, `BAS`).
  Sections organize; tags label.

## Features

- **Projects**: create, rename (double-click the name), delete (with confirmation), expand/collapse;
  create checklists inside a project.
- **Checklists**: create, rename inline, duplicate, delete (with confirmation), reset; drag-and-drop
  reorder within a project; per-checklist progress badge (e.g. `8/10`).
- **Sections**: items are grouped under section headers with counts; grouping works in the Open,
  Completed, and All views; sections can be collapsed; drag-and-drop reordering of items **within a
  section**.
- **Tags**: add/edit as a semicolon-separated list (`AHU-1; BAS; High Priority`); shown as subtle
  pills; whitespace trimmed, duplicates removed.
- **Open / Completed / All tabs**: completing an item moves it to Completed; unchecking restores it
  to its original position in Open (order is preserved, never lost). All shows everything in order.
- **Search** (top-right of the checklist toolbar) matches item text, notes, section, and tags in the
  current view, with a result count. Reordering is disabled while searching so order can't be
  corrupted.
- **Autosave** after every meaningful change (debounced while typing), with a subtle "Saved 10:56 AM"
  status, safe/atomic writes, and an automatic backup.
- **Restore previous state** on reopen — last checklist, last tab, theme, expand/collapse, and window
  size/position.
- **Light / Dark / System theme** — an explicit three-button control in the sidebar (one click each,
  no hidden cycle); the choice is remembered.
- **CSV import / export**, **CSV template download**, and **print** (grouped by section, with tags).

---

## Running the published app

1. Get `FieldCheck.exe` (see *Publishing* below — output at `publish\win-x64\FieldCheck.exe`).
2. Double-click it. No .NET install is required.

Optionally right-click `FieldCheck.exe` → **Send to → Desktop (create shortcut)** for a desktop icon.

---

## Development

**Prerequisites:** [.NET SDK 8.0](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build                                # build everything
dotnet test                                 # run the unit tests (96)
dotnet run --project src/FieldCheck.App      # launch in Debug
```

### Solution layout

```
FieldCheck/
  FieldCheck.sln
  src/
    FieldCheck.Core/        # Pure logic (no WPF) — fully unit-tested
      Models/               #   AppState, AppSettings, Project, Checklist, ChecklistItem, ThemeMode
      Services/             #   FileStorage (+ v1->v2 migration), Project, Checklist, CsvImport, CsvExport, Template
      Utilities/            #   Csv (RFC-4180), IdGenerator, IClock, AppJson
    FieldCheck.App/         # WPF UI (MVVM), references Core
      ViewModels/  Views/  Services/  Behaviors/  Controls/  Converters/  Themes/  Assets/
    FieldCheck.Tests/       # xUnit tests for Core
  build/
    publish-win-x64.ps1     # builds the self-contained single-file exe
    make-icon.ps1           # regenerates the checkmark app icon
  README.md
```

All hierarchy/ordering/CSV/persistence rules live in **FieldCheck.Core** so they can be tested
without the UI. The WPF layer is a thin MVVM shell over those services.

---

## Publishing the self-contained executable

```powershell
powershell -ExecutionPolicy Bypass -File build\publish-win-x64.ps1
```

Produces `publish\win-x64\FieldCheck.exe` (~63 MB, single file, self-contained). The end user does
not need .NET installed. WPF does not support IL trimming, so the file is large — the trade-off buys
a zero-dependency, double-click experience.

---

## Where data is stored

```
%APPDATA%\FieldCheck\data.json          # your projects, checklists, items + settings
%APPDATA%\FieldCheck\data.backup.json   # automatic backup of the last known-good file
%APPDATA%\FieldCheck\error.log          # only written if an unexpected error occurs
```

(`%APPDATA%` is typically `C:\Users\<you>\AppData\Roaming`.)

**Saving is atomic:** FieldCheck writes to a temp file, validates it, copies the current file to the
backup, then swaps the new file into place. On startup it loads `data.json`; if that file is missing
it starts fresh, and if it is unreadable it automatically restores `data.backup.json` and tells you.
Your data is never silently discarded.

**Migration:** older v1 files (which stored checklists at the root) are upgraded automatically — the
existing checklists are moved into a default project named **General**, item tags initialized empty,
and everything else (names, items, notes, sections, completion, order, timestamps) preserved.

---

## CSV format

### Columns

| Column           | Required | Notes                                                                 |
|------------------|----------|-----------------------------------------------------------------------|
| `item_text`      | **Yes**  | The item. Rows with a blank `item_text` are skipped and reported.     |
| `project_name`   | No       | Blank → `General`. Existing projects (case-insensitive) are reused.   |
| `checklist_name` | No       | Blank → the CSV file name. Duplicate within a project → `Name Copy`.  |
| `section`        | No       | Blank → `General`.                                                    |
| `notes`          | No       | Free text.                                                            |
| `tags`           | No       | Semicolon-separated, e.g. `AHU-1; BAS; High Priority`.                |

Extra columns are ignored. Quoted fields, embedded commas, and `""`-escaped quotes are handled.
One CSV may create multiple projects and checklists. The import summary reports projects
created/reused, checklists created, items imported, rows skipped, and any issues.

### Template / example

```csv
project_name,checklist_name,section,item_text,notes,tags
Commissioning Checklist,AHU_1,Fan,Verify supply fan command,Command fan from BAS and verify output changes.,AHU-1; BAS
Commissioning Checklist,AHU_1,Fan,Verify supply fan status,Confirm proof/status follows command.,AHU-1; BAS
Commissioning Checklist,AHU_1,Sensors,Verify SAT sensor,Compare BAS value to field reading.,AHU-1; Sensor
Commissioning Checklist,Graphics Review,Navigation,Verify AHU graphic links,Confirm graphic opens correct equipment view.,Graphics; AHU-1
Commissioning Checklist,Graphics Review,Alarms,Verify alarm console link,Confirm alarm console opens and filters correctly.,Graphics; Alarms
```

Use **Download CSV template** (welcome screen or sidebar) to save a starter file.

### Export columns

Exporting a checklist writes:
`project_name, checklist_name, section, item_text, notes, tags, completed, completed_at, order`
(tags semicolon-separated, order preserved, completed and open items included).

---

## Editing behavior

- **Project & checklist names** are edited in place: double-click (or the pencil) to edit. **Enter**
  commits, **Esc** reverts, **Tab / clicking away** commits. Names can't be blank.
- **Items** (text, section, tags, notes) are edited in a small dialog. Notes are multi-line.

---

## Troubleshooting

- **"FieldCheck restored your most recent backup."** — `data.json` was unreadable and the backup was
  loaded; your previous work is intact.
- **"Save failed — check the disk"** (top bar) — the app couldn't write to `%APPDATA%`. Check free
  space and permissions; your in-memory work is still there.
- **Reordering is disabled while searching** — clear the search box to drag items again.
- **Something unexpected happened** — details are appended to `%APPDATA%\FieldCheck\error.log`.
- **Clean slate** — close the app and delete the files in `%APPDATA%\FieldCheck`.

---

## Known limitations (v0.2)

- **Reordering projects** and **moving checklists between projects** are not yet supported (checklists
  reorder within their project; projects are ordered by creation).
- **Cross-section item drag-and-drop** is intentionally disabled (drops outside the item's section are
  ignored) to keep ordering reliable; reorder within a section, or edit the item to change its section.
- **Section reordering** is not implemented; sections appear in the order their first item appears.
- Items are edited via a dialog rather than fully inline.
- Print layout is intentionally plain/functional.
- Single user, single machine by design. Running two copies at once is unsupported (they would
  overwrite each other's saves).

---

## Tests

```powershell
dotnet test
```

96 tests cover CSV import/export (projects, tags, quoting, multi-project, copy naming, skipped rows),
project behavior (create/rename/delete/reorder/get-or-create), checklist behavior, section grouping
(open/completed/all), tag parsing/dedup, persistence (save/reload/restore, recover from
missing/corrupt via backup), and **v1→v2 migration**.

---

## License

[MIT](LICENSE) © 2026 Jacob King.
