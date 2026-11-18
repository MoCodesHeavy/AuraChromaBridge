# AuraChromaBridge 0.8.0 - x64 installer (design)

Approach chosen by Mohamed, 2026-11-18. Awaiting approval of this document.

## Intent

Let anyone with Razer and ASUS lighting download one setup file from the GitHub releases page and
get a working bridge, without the source folder or Windows' C# compiler. Today the only install
path is `install-startup.cmd`, which compiles the bridge from the source folder and keeps settings
and logs in that folder.

Setup should also make the Chroma feed easy to try: for people who have both Razer Synapse and
ASUS Armoury Crate, following Synapse's colours (`razer_app_id = auto`, which scans the published
app IDs) is ticked by default.

## Goals

- One `AuraChromaBridge-Setup-x64.exe` per release, built from source on GitHub's build servers.
- Install, upgrade and uninstall that behave like any normal Windows program (Add/Remove Programs).
- During setup: detect Razer Synapse and ASUS Armoury Crate, offer "Follow Synapse's colours",
  ticked when both are present, and offer to run the app ID scan and show its results.
- The sync starts at sign-in afterwards, exactly as `install-startup.cmd` sets it up today.

## Non-goals

- Code signing (costs money; README explains the SmartScreen warning instead).
- Changing how the sync works, apart from the scan hint below.
- Zone mapping and a mapping GUI (recorded in `docs/roadmap.md` for a later release).
- 32-bit or ARM Windows.

## Anti-goals

- Breaking an existing install made with `install-startup.cmd` (Mohamed's PC is one).
- A ticked feed option that doesn't say what it does: it must say plainly that it uses other
  brands' published app IDs, unofficially, until Razer issues one for this project.
- Touching Razer or ASUS settings, or leaving a scheduled task or files behind after uninstall
  (settings and logs are kept only if the person chooses to keep them).
- Publishing a release nobody has tested on a real PC.

## Constraints

- The bridge only drives ASUS lighting from an elevated process, and the scheduled task runs as
  the signed-in user with highest privileges. Setup therefore runs as administrator and supports
  the usual case where the signed-in user is an administrator.
- The source still builds with Windows' built-in C# 5 compiler; the installer adds no dependency
  for people building from source, and the source-folder scripts keep working.
- The repo's own `settings.ini` stays `razer_app_id = off`; only setup writes `auto`.

## Approaches

| Approach | Trade-off | Decision |
|---|---|---|
| Inno Setup script, compiled on a GitHub Actions Windows job | Battle-tested install, upgrade, file-in-use and uninstall handling; one new tool, on the build server only | **Chosen** |
| Our own setup program in C# (built-in compiler) | Keeps "nothing to install", but we write and maintain all install, upgrade and uninstall logic, untestable from the dev container | Rejected |
| MSI with WiX | Built for corporate deployment; more than this needs | Rejected |

## Decided

**What gets installed**
- `C:\Program Files\AuraChromaBridge\`: `AuraChromaBridge-bg.exe` (no window, run by the task),
  `AuraChromaBridge.exe` (console, for the scan and tests), `install-task.ps1`, `README.md`,
  `LICENSE`.
- Settings and logs: `%LOCALAPPDATA%\AuraChromaBridge\` (`settings.ini`, `logs\`). On an upgrade
  or over an `install-startup.cmd` install, setup keeps whatever data folder the existing
  `AuraChromaBridge` task already points at (its `data="..."` argument), so existing settings and
  logs stay where they are.
- Start menu folder "AuraChromaBridge": Scan Razer app IDs, Edit settings, Open logs folder,
  Start sync, Stop sync, Uninstall.
- The scheduled task is registered with the existing `install-task.ps1` (same name, same
  settings), then started.

**Setup pages**
- Detection: Razer = `RzChromaBroadcastAPI64.dll` in System32 (installed with Synapse's Chroma
  Connect); ASUS = the `aura.sdk.1` COM class is registered (installed with Armoury Crate).
- Option "Follow Synapse's colours (unofficial: uses published app IDs from other brands until
  Razer issues one for this project)". Ticked and enabled when both are found; unticked and
  disabled, with the reason, when Razer is missing. If ASUS is missing, setup warns that there is
  nothing for the bridge to drive, but still lets the install continue.
- Finish page: "Run the app ID scan now" (ticked when the feed option is ticked) runs the console
  scan in a window before the sync starts, so the ID isn't already in use by the sync; then the
  sync starts.
- Finish page text: in the Razer Chroma app > Connect, Chroma Connect and the bridge's entry must
  be on, and the Connect effect must light Connect devices (for example Quick Effects >
  Spectrum Cycling).

**Settings**
- Fresh install: the repo's `settings.ini` as the template, with `razer_app_id` set from the
  option (`auto` or `off`).
- Upgrade: the existing `settings.ini` is never replaced; only its `razer_app_id` line is set to
  match the option, whose default on an upgrade is the file's current value (`off` → unticked).

**Uninstall**
- Stops the sync (`stop.request`, then ends the process), removes the task, the program folder
  and the Start menu entries. Asks whether to keep settings and logs; default keep.

**Scan hint (small code change)**
- When the scan receives colours but every one is black, it says the Connect effect doesn't light
  Connect devices and points to the Razer Chroma app > Connect page.

**Build and release**
- `.github/workflows/build.yml` on a Windows runner: build both exes with `build.cmd`, build and
  run the self-test, install Inno Setup with Chocolatey, compile `installer/AuraChromaBridge.iss`.
- Every push and pull request uploads the setup file as a build artifact.
- A `v*` tag creates a **draft** GitHub release with the setup file attached; Mohamed tests it on
  his PC and publishes it himself.
- Setup version comes from `Program.Version` (0.8.0); the Inno app ID (GUID) is fixed forever so
  later versions upgrade in place.

**Docs**
- README: an "Install" section (download the setup, SmartScreen "More info > Run anyway", what the
  feed option does), with "Build from source" keeping the scripts.
- `docs/roadmap.md`: the zone-mapping plan from 2026-11-17 (learn which Studio cells map to the
  five feed zones, map headers to zones, later a mapping GUI with autoscan and drag-to-assign; the
  bridge can't add or label cells in Chroma Studio, because Razer draws each app ID's layout).

## Left to the builder

- How setup reads the existing task's data folder (for example a short PowerShell call).
- Exact wording of setup pages and Start menu shortcut targets.
- Whether the Chocolatey Inno Setup version is pinned.
