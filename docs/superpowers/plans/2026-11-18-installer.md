# x64 Installer (0.8.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `AuraChromaBridge-Setup-x64.exe`, built by GitHub Actions, that installs, upgrades and uninstalls the bridge and offers the Chroma feed (`razer_app_id = auto`) ticked for people with both Razer and ASUS.

**Architecture:** Anything with logic the installer needs (reading and changing one `settings.ini` key, finding the data folder of an existing scheduled task) becomes a small, tested command of the console exe. The Inno Setup script stays thin: it detects Razer and ASUS, calls those commands, copies files, registers the task with the existing `install-task.ps1`. A GitHub Actions job on Windows builds the exes, runs the self-test and compiles the setup file.

**Tech Stack:** C# 5 on .NET Framework 4.x (Windows' own `csc.exe`, Mono `mcs` in the dev container), Inno Setup 6 (installed on the build server with Chocolatey), GitHub Actions `windows-latest`.

**Spec:** `docs/superpowers/specs/2026-11-18-installer-design.md`

## Global Constraints

- C# 5 only (no `$""`, `?.`, `nameof`, expression-bodied members); references only `System.dll` and `System.Core.dll`.
- Files keep CRLF line endings, except README.md and docs, which are LF in this repo.
- `Program.Version` = `"0.8.0"`; setup version must equal it.
- The repo's `settings.ini` keeps `razer_app_id = off`.
- Program folder `{autopf}\AuraChromaBridge` (`C:\Program Files\AuraChromaBridge`); new installs keep data in `%LOCALAPPDATA%\AuraChromaBridge`; an existing `AuraChromaBridge` task's data folder wins.
- Inno `AppId={{B0BD0AC8-A696-4F66-B755-5BC4047EAC45}`, never changed.
- Feed option label: "Follow Synapse's colours (unofficial: uses published app IDs from other brands until Razer issues one for this project)".
- Releases are created as drafts only.

## Review Focus

- Upgrading over an `install-startup.cmd` install: the old task's data folder (here `Documents\AuraChromaBridge`) is reused and its `settings.ini` is not replaced. (Task 2 test `task_xml_quoted_data`; Task 7 PC check.)
- Running the Start-menu scan while the sync is running: the scan must stop at the ID the sync already holds (1152), not register a second brand. (Task 4 test `first_accepted_stops_at_in_use`.)
- Synapse feeding only black: the scan must say how to fix the Connect effect. (Task 4 test `verdict_only_black`.)
- An older `settings.ini` with no `razer_app_id` line, or with a trailing `; comment`: setup still reads and writes the key. (Task 1 tests `set_value_appends_when_missing`, `get_value_strips_inline_comment`.)
- Razer or ASUS missing: the feed option is unticked and disabled with the reason, or setup warns, and setup still completes. (Task 6 detection; Task 7 PC check.)

## Commands

Dev-container checks (repo root):

```bash
mcs -noconfig -langversion:5 -target:exe -platform:x64 -optimize+ -warn:4 -out:/tmp/acb.exe -r:System.dll -r:System.Core.dll src/*.cs
mcs -noconfig -langversion:5 -target:exe -platform:x64 -warn:4 -out:/tmp/selftest.exe -main:AuraChromaBridge.Tests.SelfTest -r:System.dll -r:System.Core.dll src/*.cs tests/*.cs && mono /tmp/selftest.exe
```

CI checks: push the branch, then `gh api repos/MoCodesHeavy/AuraChromaBridge/actions/runs?branch=<branch>` until the latest run has `conclusion: success`.

---

### Task 1: Read and change one settings.ini key

**Files:** Create `src/SettingsFile.cs`, `tests/SettingsFileTests.cs`; modify `tests/SelfTest.cs` (register).

**Interfaces — Produces:** `internal static class SettingsFile`
- `public static string GetValue(string[] lines, string key)` — the value of the first non-comment line whose key matches (case-insensitive), trimmed, with anything from `;` removed (same rule as `Settings.Load`); `null` if absent
- `public static string[] SetValue(string[] lines, string key, string value)` — replaces that first matching line with `key = value`; appends `key = value` if absent; every other line unchanged
- `public static string Get(string path, string key)` / `public static void Set(string path, string key, string value)` — file wrappers; `Set` keeps CRLF and creates the file if missing

- [ ] **Step 1: Failing tests**
  - `get_value_reads_key`: `{"; comment", "razer_app_id = auto", "dsx = on"}`, `razer_app_id` → `"auto"`.
  - `get_value_ignores_comments_and_case`: `{"; razer_app_id = off", "RAZER_APP_ID = {4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}"}` → the GUID.
  - `get_value_strips_inline_comment`: `{"razer_app_id = auto ; tried"}` → `"auto"`.
  - `get_value_missing_is_null`.
  - `set_value_replaces_line_keeps_others`: 4 lines incl. comments → same length, only the key line now `razer_app_id = off`.
  - `set_value_appends_when_missing`: `{"dsx = on"}` → `{"dsx = on", "razer_app_id = auto"}`.
  - `set_value_replaces_only_first`: two matching lines → only the first changes.
  - `file_set_then_get_round_trips`: temp file without the key → `Set` → `Get` returns the value; file bytes contain `\r\n`.
- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement `SettingsFile`.**
- [ ] **Step 4: Run both checks; expect success and all PASS.**
- [ ] **Step 5: Commit** — `feat: read and change one settings.ini key`

### Task 2: Data folder of an existing scheduled task

**Files:** Create `src/TaskInfo.cs`, `tests/TaskInfoTests.cs`; modify `tests/SelfTest.cs`.

**Interfaces — Produces:** `internal static class TaskInfo`
- `public static string DataFolderFromTaskXml(string xml)` — reads the text of `<Arguments>`, decodes `&quot; &amp; &lt; &gt; &apos;`, returns the value of `data=` (quoted or not), or `null`
- `public static string ExistingDataFolder()` — runs `schtasks /query /tn AuraChromaBridge /xml`, returns `DataFolderFromTaskXml` of its output, `null` on any failure (no task, no schtasks)

- [ ] **Step 1: Failing tests**
  - `task_xml_quoted_data`: `<Exec><Command>C:\Program Files\AuraChromaBridge\AuraChromaBridge-bg.exe</Command><Arguments>run data=&quot;C:\Users\Mo\Documents\AuraChromaBridge&quot;</Arguments></Exec>` → `C:\Users\Mo\Documents\AuraChromaBridge`.
  - `task_xml_unquoted_data`: `<Arguments>run data=C:\Data</Arguments>` → `C:\Data`.
  - `task_xml_path_with_spaces`: `data=&quot;C:\My Data\ACB&quot; relaunch=1` → `C:\My Data\ACB`.
  - `task_xml_without_data_is_null`: `<Arguments>run</Arguments>` → `null`; `""` → `null`.
- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement `TaskInfo`.**
- [ ] **Step 4: Run both checks; expect success and all PASS.**
- [ ] **Step 5: Commit** — `feat: find the data folder of an existing scheduled task`

### Task 3: Setup commands and version 0.8.0

**Files:** Create `src/SetupCommands.cs`; modify `src/Program.cs` (mode list, usage, dispatch before `Log.Open`, `Version`).

**Interfaces**
- Consumes: `SettingsFile.Get/Set`, `TaskInfo.ExistingDataFolder`, `Program.SettingsPath`.
- Produces: `AuraChromaBridge.exe get-setting KEY [data=DIR] [out=FILE]`, `set-setting KEY VALUE [data=DIR]`, `task-data [out=FILE]`. Output goes to `out=` when given (UTF-8, no BOM, no newline), else the console; exit 0 on success (an absent value or task is success, written as empty), 1 on error. These modes open no log file and print no banner. `Program.Version = "0.8.0"`.

- [ ] **Step 1: Implement `SetupCommands.Run(string mode, string[] args)` and dispatch it in `Main` right after the `data=`/`relaunch=` loop.** `out=` is parsed there too.
- [ ] **Step 2: Check end to end under Mono:**
  ```bash
  d=$(mktemp -d) && printf 'dsx = on\r\n' > $d/settings.ini
  mono /tmp/acb.exe set-setting razer_app_id auto data=$d && mono /tmp/acb.exe get-setting razer_app_id data=$d out=$d/v.txt && cat $d/v.txt   # auto
  mono /tmp/acb.exe task-data out=$d/t.txt; echo "exit $?"; wc -c < $d/t.txt                                                                 # exit 0, 0 bytes
  ls $d   # no logs folder
  ```
- [ ] **Step 3: Run both checks; expect success and all PASS.**
- [ ] **Step 4: Commit** — `feat: setup commands for the installer; version 0.8.0`

### Task 4: Scan fit for a Start-menu shortcut

**Files:** Modify `src/AppIds.cs`, `src/Feed.cs`, `src/Scan.cs`, `src/Program.cs` (`pause` argument), `tests/AppIdsTests.cs`; create `tests/ScanTests.cs`.

**Interfaces**
- Changes: `KnownAppIds.FirstAccepted(IList<KnownAppId> candidates, Func<Guid, int> tryInit, Action<KnownAppId, int> report, out int code)` — stops at the first result `0` (accepted) or `1152` (in use) and returns that candidate with its code; `null` with the last code when all were refused. `Feed.Connect` treats 1152 as not ready (it already is, via `IsNotReady`) and logs `Feed: the '{Name}' app ID is already in use (another copy of the bridge?) - trying again later.`
- Produces: `internal enum ScanVerdict { NoColours, OnlyBlack, Works }`; `public static ScanVerdict Scan.Verdict(int updates, bool sawNonBlack)`; `Scan.Run(Settings settings, bool all, bool pauseAtEnd)`; `Main` passes `pauseAtEnd` = any argument equal to `pause`.
- Scan messages: in use → `The sync is already connected with '{Name}'. Stop it first (Start menu > AuraChromaBridge > Stop sync), then scan again.` (exit 1). Only black → `Synapse accepted '{Name}' but sent only black. In the Razer Chroma app > Connect, set the effect to one that lights Connect devices (for example Quick Effects > Spectrum Cycling), then scan again.` (exit 1). With `pause`, end with `Press any key to close.` and wait.

- [ ] **Step 1: Failing tests**
  - Update `first_accepted_stops_at_first_zero` and `first_accepted_all_refused` to the new signature (`code` is 0, and 4309 respectively).
  - `first_accepted_stops_at_in_use`: 2nd candidate returns 1152 → returns it, `code == 1152`, `report` called 2 times.
  - `verdict_no_colours`: `(0, false)` → `NoColours`; `verdict_only_black`: `(260, false)` → `OnlyBlack`; `verdict_works`: `(280, true)` → `Works`.
- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement; `Scan` tracks whether any received colour was non-black.**
- [ ] **Step 4: Run both checks; expect success and all PASS.**
- [ ] **Step 5: Commit** — `feat: scan stops at an ID already in use, explains black-only feeds, can pause`

### Task 5: Build on GitHub Actions

**Files:** Create `.github/workflows/build.yml`.

- Triggers: `push` (all branches), `pull_request`, tags `v*`. Runner `windows-latest`. Permissions `contents: write` (draft release only).
- Steps: checkout; `build.cmd main` and `build.cmd bg`; `build.cmd selftest` then run `AuraChromaBridge-selftest.exe` from the repo root (fails the job on a non-zero exit); read `Version` from `src/Program.cs`; on a tag, fail unless the tag is `v` + that version; upload both exes as an artifact.

- [ ] **Step 1: Write the workflow.**
- [ ] **Step 2: Push the branch; expect the run to succeed and show `0 failed` in the self-test step log.**
- [ ] **Step 3: Commit** (before pushing) — `ci: build and self-test on Windows`

### Task 6: The Inno Setup script

**Files:** Create `installer/AuraChromaBridge.iss`; modify `.github/workflows/build.yml`.

Decisions:
- `[Setup]`: the AppId above; `AppVersion` from `/DAppVersion=`; `ArchitecturesAllowed=x64compatible`, `ArchitecturesInstallIn64BitMode=x64compatible`; `PrivilegesRequired=admin`; `DefaultDirName={autopf}\AuraChromaBridge`; `OutputBaseFilename=AuraChromaBridge-Setup-x64`; `LicenseFile=..\LICENSE`; `UninstallDisplayName=AuraChromaBridge`.
- `[Files]`: both exes, `install-task.ps1`, `README.md`, `LICENSE` to `{app}`; `settings.ini` to `{code:DataDir}` with `onlyifdoesntexist uninsneveruninstall`.
- `[Registry]`: `HKLM\Software\AuraChromaBridge` value `DataDir` (`uninsdeletekey`), read by the uninstaller.
- `[Code]`:
  - On start: extract `AuraChromaBridge.exe` to `{tmp}`; `task-data` decides `DataDir` (existing folder) else `{localappdata}\AuraChromaBridge`.
  - Detection: Razer = `{sys}\RzChromaBroadcastAPI64.dll` exists; ASUS = `aura.sdk.1` key in `HKCR64` or `HKCR32`.
  - One option page with the feed checkbox (label in Global Constraints). Default: existing `settings.ini` → ticked unless its `razer_app_id` is `off` or absent; fresh → ticked if Razer and ASUS. Razer missing → unticked, disabled, "Razer Synapse's Chroma Connect wasn't found." ASUS missing → warning text, install still allowed.
  - `PrepareToInstall`: write `stop.request` in `DataDir`, wait 3 s, `taskkill /im AuraChromaBridge-bg.exe /f`.
  - After files: `set-setting razer_app_id` with `off`, or the existing GUID if the file has one and the box is ticked, else `auto`; then `install-task.ps1 -Exe "{app}\AuraChromaBridge-bg.exe" -Data "<DataDir>"` (hidden, `-ExecutionPolicy Bypass`).
  - Uninstall: stop as in `PrepareToInstall`; `schtasks /delete /tn AuraChromaBridge /f`; ask "Keep your settings and logs?" (default Yes); No → delete `DataDir`.
- `[Icons]` (Start menu group "AuraChromaBridge"): Scan Razer app IDs (`AuraChromaBridge.exe scan pause data="<DataDir>"`), Edit settings (notepad), Open logs folder, Start sync (`schtasks /run /tn AuraChromaBridge`), Stop sync (`cmd /c echo stop> "<DataDir>\stop.request"`), Uninstall.
- `[Run]` postinstall, in order: "Run the app ID scan now" (scan with `pause`, ticked when the feed option is ticked, wait until it ends), then "Start the sync now" (ticked).
- Finish page text: the Chroma Connect note from the spec.
- Workflow: `choco install innosetup -y --no-progress`; `ISCC.exe /DAppVersion=<version> installer\AuraChromaBridge.iss`; output to `dist\`; upload as an artifact; on a `v*` tag, `gh release create <tag> --draft --title "AuraChromaBridge <version>" dist\AuraChromaBridge-Setup-x64.exe`.

- [ ] **Step 1: Write the script and the workflow steps.**
- [ ] **Step 2: Push; expect the run to succeed and the artifact to contain `AuraChromaBridge-Setup-x64.exe`.**
- [ ] **Step 3: Commit** (before pushing) — `feat: x64 installer built with Inno Setup`

### Task 7: Docs and the check on Mohamed's PC

**Files:** Modify `README.md`; create `docs/roadmap.md`.

- [ ] **Step 1: README** — new "Install" section before "Quick start": download `AuraChromaBridge-Setup-x64.exe` from Releases; SmartScreen → "More info" → "Run anyway" (not code-signed); what the feed option does (unofficial, other brands' published IDs, off in the repo); Start menu entries; settings in `%LOCALAPPDATA%\AuraChromaBridge`. Rename "Quick start" to "Build from source". "Building" mentions the workflow.
- [ ] **Step 2: `docs/roadmap.md`** — zone mapping per the spec's Docs section.
- [ ] **Step 3: Commit** — `docs: install section and roadmap`
- [ ] **Step 4: PC check (Mohamed, with help)** — download the artifact from the latest run; run setup over the existing install; expect: settings stay in `Documents\AuraChromaBridge`, feed option ticked, scan runs and reports the feed working, `logs\run.log` shows `0.8.0` and `source feed`; Start-menu Scan while running reports "already connected"; Stop/Start sync work.
- [ ] **Step 5:** with Mohamed's go-ahead: merge to `main`, tag `v0.8.0`, check the draft release has the setup file; Mohamed publishes it.
