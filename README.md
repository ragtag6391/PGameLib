# PGameLib

A compact, offline game launcher for Windows. It keeps every installed and standalone game in
one place and launches any of them with a single click. It stores **information about** your
games — name, launch file, folder, size, icon — and never copies, moves, renames, or deletes the
actual game files.

Built with **C# / WPF on .NET 9** using the MVVM pattern. No online account, no cloud, no
telemetry. Everything stays on your PC.

---

## Contents

- [Requirements](#requirements)
- [Build it](#build-it)
- [Run it](#run-it)
- [Make an installer or portable release](#make-an-installer-or-portable-release)
- [How to add a game](#how-to-add-a-game)
- [Using the app](#using-the-app)
- [Automatic importing](#automatic-importing)
- [Where your data lives](#where-your-data-lives)
- [Testing checklist](#testing-checklist)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)

---

## Requirements

**To build:**
- Windows 10 (1809+) or Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
  (WPF is Windows-only, so this must be built on Windows — it will not build on Linux/macOS.)
- Optional: Visual Studio 2022 (17.11+) with the *.NET desktop development* workload, or just the
  command line.

**To run the published portable build:** nothing — it's self-contained.
**To run the framework-dependent build:** the
[.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0).

---

## Build it

### Option A — one command (recommended)

From the repository root in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

This restores, builds in Release, and publishes **two** builds under `dist\`:

| Output | What it is |
|---|---|
| `dist\portable\PGameLib.exe` | Self-contained single EXE. No .NET install needed. Just run it. |
| `dist\framework-dependent\` | Smaller build that needs the .NET 9 Desktop Runtime. |
| `dist\PGameLib-portable.zip` | The portable build zipped up, ready to copy to another PC. |

For an ARM PC: `.\build.ps1 -Runtime win-arm64`.

### Option B — plain dotnet CLI

```powershell
# from the repository root
dotnet build PGameLib.sln -c Release

# self-contained single-file portable EXE
dotnet publish src\PGameLib\PGameLib.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\portable
```

### Option C — Visual Studio

Open `PGameLib.sln`, set the configuration to **Release**, and press **F5** (run) or
**Ctrl+Shift+B** (build).

### Option D — VS Code

VS Code works too, and this project ships with a `.vscode/` folder so it's one keystroke:

1. **Install the .NET 10 SDK** from <https://dotnet.microsoft.com/download/dotnet/10.0>.
   (This is what gives you the `dotnet` command. The SDK is separate from VS Code.)
2. **Unzip** `PGameLib.zip` somewhere, then in VS Code: **File → Open Folder…** and pick
   the unzipped `PGameLib` folder (the one that contains `PGameLib.sln`, **not** its
   parent and not the zip).
3. **Install the recommended extensions** when VS Code prompts (bottom-right notification):
   *C# Dev Kit* and *C#*. If you miss the prompt, open the **Extensions** sidebar
   (Ctrl+Shift+X) and search for `ms-dotnettools.csdevkit`.
4. Press **F5**. The included `tasks.json` builds the project, then `launch.json` runs the EXE.

#### "VS Code is showing nothing"

Three usual causes, in order:

- **Explorer panel is empty.** You opened an empty folder, the zip itself, or the wrong level.
  Close the folder (**File → Close Folder**) and open the one that directly contains
  `PGameLib.sln`.
- **Files are there but no colours, no squiggles, no Run button.** The C# Dev Kit isn't
  installed yet. Install it from the Extensions sidebar; the project reloads automatically
  and Solution Explorer appears under the Explorer panel.
- **"The .NET SDK could not be located" when you press F5.** .NET 10 SDK isn't installed.
  Install it from the link in step 1, then restart VS Code.

Visual XAML designer only exists in Visual Studio — not in VS Code. You can still edit XAML
by hand in VS Code; the app runs the same.

---

## Run it

- **During development:** `dotnet run --project src\PGameLib\PGameLib.csproj`, or F5 in
  Visual Studio.
- **Published portable:** double-click `dist\portable\PGameLib.exe`.

The app does **not** require administrator rights and never asks for elevation.

On first launch it quietly looks for a short list of well-known games already installed on your PC
(see [Automatic importing](#automatic-importing)) and adds the ones it can actually find.

---

## Make an installer or portable release

**Portable release:** already produced by `build.ps1` as `dist\PGameLib-portable.zip`. Unzip
anywhere and run `PGameLib.exe`. It carries its own runtime.

**Installer:** install [Inno Setup 6](https://jrsoftware.org/isdl.php), then after `build.ps1`:

```powershell
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\PGameLib.iss
```

This produces `dist\installer\PGameLib-Setup-1.0.0.exe` — a per-user installer (no admin
prompt) that bundles the self-contained build, adds Start Menu / optional desktop shortcuts, and
registers a proper uninstaller. Uninstalling removes the program files but leaves your library in
`%AppData%\PGameLib` intact.

---

## How to add a game

1. Click **Add Game** (top-right).
2. Click **Browse…** and select the game's main executable or shortcut (`.exe`, `.lnk`, `.bat`,
   or `.cmd`).
3. Confirm the **name** and **installation folder** — both are filled in automatically, and you
   can edit either.
4. Optionally add **launch arguments** (e.g. `-windowed`) and a custom **working directory**.
5. Click **Save**.

The entry is saved immediately and persists across restarts. Its icon and folder size are filled
in a moment later in the background.

---

## Using the app

- **Play** — the big button on each card (or double-click a card, or press **Enter** on the
  selected game). Launches via the Windows shell, so shortcuts, `.bat`/`.cmd`, quoted paths,
  spaces, and Unicode names all work.
- **Open folder** — opens the install folder in Explorer with the launch file selected.
- **Edit** — change name, launch file, folder, source, arguments, working directory, or cover
  image.
- **Remove** — removes the entry *from the library only*. Your game and its files are never
  touched. (Also: **Delete** key.)
- **Locate** — shown on a game whose launch file is missing; point it at the new executable.
- **Search** — filter by name, folder, or path.
- **Sort** — by name, size, last played, date added, or source.
- **Grid / list views** — toggle at the top-right of the toolbar.
- **Sidebar** — Library, Steam, Epic, Standalone, Missing, and Settings.
- **Total on disk** — combined size of every game, shown bottom-left.
- **Refresh** (bottom-right, or **F5**) — recalculates folder sizes and re-checks whether each
  launch file still exists, marking missing games.

Folder sizes are cached and recomputed in the background, so the interface stays responsive even
while large folders are being measured. The app also honours Windows display scaling
(per-monitor DPI aware).

---

## Automatic importing

From **Import** (top-right) or the **Settings** page:

- **Steam** — reads Steam's `libraryfolders.vdf` and each `appmanifest_*.acf` across every Steam
  library. Steam games launch through `steam://rungameid/<id>`.
- **Epic** — reads the Epic launcher's manifest files
  (`ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item`) and uses the real launch
  executable each manifest names.
- **Start Menu** — scans your and the all-users Start Menu for game shortcuts.
- **Scan a folder or drive** — you pick a location and the app looks for likely game executables.

Every scan shows a **review list** before anything is imported, with duplicates flagged and
unchecked. Automatic results exclude uninstallers, crash reporters, redistributables
(`vcredist`, DirectX, etc.), setup programs, launch helpers, and common development tools.

### Preloaded games

On first run the app checks for these specific titles and adds **only the ones whose launch
target actually exists** on your PC right now — nothing is invented:

Marvel Rivals · Doki Doki Literature Club · Fortnite · Hollow Knight · Stardew Valley · Undertale
· Minecraft (via TLauncher) · Roblox · Microsoft Solitaire Collection · SCRAPCRAFT

Detection uses concrete signals (Steam AppID, Epic manifest, a known install path, or a Microsoft
Store package folder). Anything that can't be located is simply skipped.

---

## Where your data lives

Everything is under **`%AppData%\PGameLib`**:

- `library.json` — your library (written atomically; a damaged file is backed up and the app
  starts clean rather than crashing).
- `icons\` — cached PNG icons extracted from your games' executables.

No data about your installed games is ever sent anywhere. There is no network call in the app at
all. You can open this folder from **Settings → Open data folder**.

---

## Testing checklist

These are the scenarios the app was built to handle — worth walking through on your machine:

- **Add** a game by EXE; confirm the name is suggested and the folder is detected.
- **Add** a game by `.lnk` shortcut; confirm the target folder is resolved from the shortcut.
- **Edit** a game's name and arguments; reopen the app and confirm they persisted.
- **Remove** a game; confirm the files on disk are still there, only the entry is gone.
- **Launch** a game whose path contains spaces and/or Unicode characters.
- **Launch** a game with custom launch arguments and a custom working directory.
- **Refresh**; confirm sizes update and the combined total changes.
- **Make a game "missing"** (rename its EXE) → Refresh → it's flagged Missing → use **Locate** to
  point at the renamed file → it becomes available again.
- **Duplicate guard** — try adding the same EXE twice; the second is rejected.
- **Search / filter / sort / grid+list** all behave.
- **Import** Steam / Epic / Start Menu / folder scan; confirm the review list appears and
  duplicates are pre-unchecked.

---

## Project structure

```
PGameLib.sln
build.ps1                      one-command build + publish
installer/PGameLib.iss    Inno Setup installer script
src/PGameLib/
  App.xaml(.cs)                startup + composition
  app.manifest                 asInvoker (no admin) + per-monitor DPI v2
  Models/                      GameEntry, enums, LibraryData
  Services/                    persistence, icon extraction, folder size,
                               launching, shortcut resolution, dialogs
    Import/                    Steam / Epic / Start Menu / folder scan,
                               exclusion filter, preload seeder, VDF parser
  ViewModels/                  MVVM: Main, AddEdit, ScanResults, Settings,
                               GameCard, commands, ObservableObject
  Views/                       MainWindow, AddEditGameWindow, ScanResultsWindow
  Converters/                  value converters for the bindings
  Themes/                      DarkTheme.xaml (palette) + Controls.xaml (styles)
  Assets/app.ico               application icon
```

Design notes: clean MVVM (view models never reference windows directly — dialogs go through
`IDialogService`); no third-party NuGet packages, so `dotnet restore` needs only the Windows
Desktop SDK; icon extraction and shortcut resolution use Windows Shell APIs via interop; launching
uses `ProcessStartInfo` with `UseShellExecute = true`.

---

## Troubleshooting

- **Build fails with `NETSDK1045`.** Install the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0),
  or use a compatible newer SDK.
- **Build fails on Linux/macOS.** WPF is Windows-only; build on Windows.
- **A game won't launch.** The app shows a friendly message. If it says the file is missing, use
  **Locate**. For Steam/Epic entries, make sure the store client is installed.
- **An icon is blank.** Some launchers/shortcuts don't expose an icon; the app falls back to a
  placeholder. You can set a **cover image** in Edit.
- **Folder size shows "—".** It hasn't finished calculating yet, or the folder isn't readable;
  press **Refresh**.
