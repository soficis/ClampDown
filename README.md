# ClampDown

ClampDown is a Windows utility for figuring out "what is locking this file?" and for safely ejecting removable drives. It provides:

- A desktop UI: `ClampDown.UI.exe` (C#/.NET + Windows Forms)
- A CLI: `ClampDown.Cli.exe` (for scripting and automation)
- A tray app: `ClampDown.Tray.exe` (quick drive ejection menu)

ClampDown follows a "safe-first, forceful-last" approach and uses documented Windows APIs (no kernel drivers).

## Table of Contents

- [Quick Start](#quick-start)
- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
  - [Graphical User Interface](#graphical-user-interface)
  - [Command Line Interface (CLI)](#command-line-interface-cli)
  - [System Tray](#system-tray)
  - [Explorer Context Menu](#explorer-context-menu)
- [Troubleshooting](#troubleshooting)
- [Architecture](#architecture)
- [Building from Source](#building-from-source)
- [Contributing](#contributing)
- [License](#license)

## Quick Start

1. Download the latest release ZIP and extract it somewhere permanent (example: `C:\Tools\ClampDown`).
2. Launch the UI: `ClampDown.UI.exe`.
3. Optional: run the tray app: `ClampDown.Tray.exe`.
4. Optional: try the CLI: `ClampDown.Cli.exe --help`.

## Features

- File lock detection via Windows Restart Manager (see which processes are locking a path)
- "Unlock and act" workflows for delete / move / rename / copy (with "schedule on reboot" fallback)
- Removable drive listing and safe ejection
- Process close / terminate actions with a built-in safety blocklist
- Activity logging + export (JSON/Markdown) and a support bundle exporter
- Multiple entry points: UI, CLI, tray app, and Explorer context menu integration

## Installation

### Option 1: Prebuilt ZIP (Recommended)

1. Download the latest release ZIP.
2. Extract it to a permanent folder (example: `C:\Tools\ClampDown`).
3. Run what you need:
   - `ClampDown.UI.exe` (desktop UI)
   - `ClampDown.Tray.exe` (tray app)
   - `ClampDown.Cli.exe` (CLI)

### Option 2: Add The CLI To Your PATH (Optional)

The Explorer context menu script defaults to calling `clampdown` (a command name), so it's convenient to create a stable CLI command.

Two easy approaches:

1) Add the extracted folder to your user PATH so you can run `ClampDown.Cli.exe` from anywhere.

2) Create a small wrapper named `clampdown.cmd` somewhere already on PATH:

```bat
@echo off
"C:\Tools\ClampDown\ClampDown.Cli.exe" %*
```

Then you can run `clampdown analyze "C:\path\file.txt"` from any terminal.

### Option 3: Build from Source

See [Building from Source](#building-from-source).

## Usage

### Graphical User Interface

Launch `ClampDown.UI.exe`. The UI has these tabs:

- Overview: navigation cards to the main areas
- Files: analyze a file/folder and take actions
- Drives: manage removable drives and safe eject
- Activity Log: view/export actions taken
- Settings: theme, elevation helpers, startup-at-login

#### File Analysis (Files tab)

1. Enter a path (file or folder), or use **Browse File... / Browse Folder...**, or drag-and-drop into the path box.
2. Optional: enable **Scan recursively** (useful for folders).
3. Click **Analyze** to list processes reported as locking the path.
4. Use the action buttons at the bottom:
   - **Close Selected Apps**: asks apps to close their main window (safe-first).
   - **Force Kill Selected**: requires typing `KILL`, then terminates the selected processes.
   - **Unlock & Delete**: deletes to Recycle Bin and will schedule delete-on-reboot if blocked.
   - **Unlock & Rename / Move**: will schedule move-on-reboot if blocked.
   - **Unlock & Copy**: copies the file (does not schedule-on-reboot).
   - **Schedule Delete at Reboot**: always schedules delete for the next Windows restart.
   - **Copy Blockers**: copies the "who is locking it" list to your clipboard.

#### Drive Ejection (Drives tab)

1. Click **Refresh** if needed.
2. Select your removable drive.
3. Optional helpers before ejection:
   - **Close Explorer**: closes Explorer windows pointing at that drive.
   - **Show Lockers**: runs a quick Restart Manager scan and copies results to clipboard.
   - **Stop Apps**: finds processes whose executable was launched from that drive; you can close or force-kill them.
4. Click **Safe Eject**.

#### Activity Log & Support Bundle

The **Activity Log** tab shows actions taken in the current session. You can:

- **Export JSON...** or **Export Markdown...**
- **Export Support Bundle...** (creates a ZIP with the activity log + basic environment info)

#### Startup At Login (Settings tab)

The Settings tab includes a checkbox: **Start ClampDown at Windows login (current user)**.

- This writes a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- It starts whichever ClampDown executable you used to enable the checkbox (normally `ClampDown.UI.exe`).

### Command Line Interface (CLI)

The CLI executable is `ClampDown.Cli.exe`. If you created a `clampdown` wrapper/alias (see [Installation](#installation)), you can use `clampdown` in the examples below.

#### CLI Help

```powershell
ClampDown.Cli.exe --help
ClampDown.Cli.exe help
ClampDown.Cli.exe analyze --help
```

If you run the CLI with no args, it prints the top-level help.

#### Command Reference

All commands support `-h`, `--help`, `/?`, or `help` to show usage.

- `analyze <path> [--recursive] [--json]`  
  Prints processes locking a file or directory (directory scan can be recursive).

- `unlock-delete <filePath> [--recycle-bin] [--permanent] [--schedule] [--json]`  
  Deletes a file. Defaults to sending to the Recycle Bin unless `--permanent` is provided. With `--schedule`, a blocked delete can be scheduled for the next reboot.

- `unlock-move <sourcePath> <destinationPath> [--schedule] [--json]`  
  Moves (or renames) a file. With `--schedule`, a blocked move can be scheduled for the next reboot.

- `unlock-copy <sourcePath> <destinationPath> [--json]`  
  Copies a file.

- `drive-list [--json]`  
  Lists removable drives (using WMI).

- `eject <driveLetter> [--json]`  
  Requests safe ejection for a removable drive (example input: `E:\`, `E:`, or `E`).

#### CLI Examples

```powershell
# Analyze a file
ClampDown.Cli.exe analyze "C:\path\to\file.txt"

# Analyze a folder (recursive)
ClampDown.Cli.exe analyze "C:\path\to\folder" --recursive

# JSON output (useful for scripts)
ClampDown.Cli.exe analyze "C:\path\to\file.txt" --json

# Delete (defaults to Recycle Bin); schedule-on-reboot if blocked
ClampDown.Cli.exe unlock-delete "C:\path\to\file.txt" --schedule

# Permanent delete (still respects --schedule)
ClampDown.Cli.exe unlock-delete "C:\path\to\file.txt" --permanent --schedule

# Move/rename, schedule if blocked
ClampDown.Cli.exe unlock-move "C:\old\file.txt" "C:\new\file.txt" --schedule

# Copy
ClampDown.Cli.exe unlock-copy "C:\source\file.txt" "C:\dest\file.txt"

# List removable drives
ClampDown.Cli.exe drive-list

# Eject a drive (accepts E, E:, or E:\)
ClampDown.Cli.exe eject "E:\"
```

#### CLI Output, JSON, and Exit Codes

- Default output is text meant for humans (tab-separated columns).
- With `--json`, output is printed to stdout as pretty-printed JSON.
- Exit codes:
  - `0` success (or help displayed)
  - `1` command failed (drive not found, operation failed, etc.)
  - `2` usage error (unknown command, missing required args)

### System Tray

The tray app (`ClampDown.Tray.exe`) runs in the background and provides a quick menu for drive ejection:

1. Look for the ClampDown icon in your system tray.
2. Left-click or right-click to show the menu.
3. Use **Refresh** to update the drive list.
4. Pick a drive and click **Eject**.

#### Start The Tray App Automatically At Login

ClampDown provides a "start at login" toggle in the UI for the UI app (see [Startup At Login](#startup-at-login-settings-tab)). If you want the tray app to start at login, use one of these Windows-native approaches:

Option A: Startup folder (recommended for most users)

1. Press `Win + R`, type `shell:startup`, press Enter.
2. Create a shortcut to `ClampDown.Tray.exe` inside that folder.

To disable: delete the shortcut from that folder.

Option B: Registry Run key (current user)

```powershell
$tray = "C:\Tools\ClampDown\ClampDown.Tray.exe"
New-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ClampDown.Tray" -Value "`"$tray`"" -PropertyType String -Force
```

To disable:

```powershell
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "ClampDown.Tray" -ErrorAction SilentlyContinue
```

### Explorer Context Menu

ClampDown can register per-user Explorer context menu commands (stored under HKCU).

1. Decide how Explorer should find the CLI:
   - If you have a `clampdown` command on PATH, you can keep the default.
   - Otherwise, pass `-CliPath` to point at `ClampDown.Cli.exe`.

2. Run the registration script (admin is not required):

```powershell
.\scripts\Register-ExplorerContextMenu.ps1
# or:
.\scripts\Register-ExplorerContextMenu.ps1 -CliPath "C:\Tools\ClampDown\ClampDown.Cli.exe"
```

3. Right-click any file or folder to see ClampDown options:
   - Analyze locks (ClampDown): runs `analyze`
   - Unlock & Delete (ClampDown): runs `unlock-delete` (with Recycle Bin + schedule-on-reboot)

4. Right-click a removable drive to see:
   - Safe eject (ClampDown): runs `eject`

To unregister:

```powershell
.\scripts\Unregister-ExplorerContextMenu.ps1
```

## Troubleshooting

### "Analyze shows no lockers"

- Restart Manager can't always identify every lock type (some locks are not reported, especially for certain system components or drivers).
- Try analyzing the specific file instead of a broad folder path.
- If the lock is on a removable drive, check the Drives tab and try Close Explorer / Stop Apps.

### "Eject was vetoed"

- Something still has an open handle to the drive.
- Use the Drives tab:
  - Show Lockers (copies results to clipboard)
  - Close Explorer
  - Stop Apps

### "Delete/move says it was scheduled for reboot"

- ClampDown used the Windows "delay until reboot" mechanism.
- Restart Windows to complete the operation.

### "Access denied"

- You may not have permission to modify that file.
- For UI: use Settings -> Restart as Administrator (UAC prompt).

## Architecture

ClampDown is split into projects:

```
+------------------+          +--------------------+
|  ClampDown.UI    |          |  ClampDown.Tray    |
|  (WinForms)      |          |  (WinForms)        |
+--------+---------+          +---------+----------+
         |                              |
         v                              v
+------------------+          +--------------------+
|  ClampDown.Core  |<-------->|  ClampDown.Win32   |
|  (services/policy)|         |  (P/Invoke + WMI)  |
+------------------+          +--------------------+
         ^
         |
+------------------+
|  ClampDown.Cli   |
|  (console)       |
+------------------+

(Optional/experimental) ClampDown.Helper: an elevated helper process for IPC operations.
```

## Building from Source

### Prerequisites

- Windows 10/11
- .NET SDK that supports `net9.0-windows` (recommended: .NET 9 SDK)
- Visual Studio 2022 (recommended) with the ".NET desktop development" workload, or VS Code with a C# extension

### Build, Test, Run

```powershell
dotnet restore
dotnet build ClampDown.sln -c Release
dotnet test ClampDown.sln
```

Run from source:

```powershell
dotnet run --project src\ClampDown.UI
dotnet run --project src\ClampDown.Tray
dotnet run --project src\ClampDown.Cli -- --help
```

### Publish (Novice-Friendly)

This repo includes a script that publishes all apps into `dist\` as self-contained binaries:

```powershell
.\scripts\Publish-Exe.ps1 -Runtime win-x64 -Configuration Release
```

Outputs:

- `dist\ClampDown.UI\win-x64\ClampDown.UI.exe`
- `dist\ClampDown.Tray\win-x64\ClampDown.Tray.exe`
- `dist\ClampDown.Cli\win-x64\ClampDown.Cli.exe`
- `dist\ClampDown.Helper\win-x64\ClampDown.Helper.exe`

Note: `scripts\Publish-Exe.ps1` deletes and recreates the `dist\` folder each run.


## License

ClampDown is licensed under the GNU General Public License v3.0 (GPLv3).

---

Disclaimer: ClampDown uses documented Windows APIs. While designed with safety in mind, forceful operations (especially process termination) can cause data loss or system instability. Use with care and keep backups of important data.
