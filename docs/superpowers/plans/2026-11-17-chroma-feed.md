# Chroma Feed Mode (v0.7.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drive the ASUS lighting and the DSX light bar from Synapse's own colours (Razer Chroma Broadcast feed), using whichever published app ID Synapse accepts, with the clock effect as the fallback.

**Architecture:** Pure logic (the ID list and its ordering, the feed state machine, settings parsing, relaunch argument quoting) lives in small files covered by a self-test exe that also runs under Mono. A thin `Feed` class glues `ChromaBroadcast` to that logic. The existing `Sync` loop asks the feed for a colour each frame and falls back to `Effects.Base` when the feed has nothing.

**Tech Stack:** C# 5, .NET Framework 4.x, built by the `csc.exe` that ships inside Windows (`build.cmd`). Checks in the dev container use Mono `mcs` with the same flags.

**Spec:** `docs/superpowers/specs/2026-11-17-chroma-feed-design.md`

## Global Constraints

- C# 5 only: no `$""`, no `?.`, no `nameof`, no expression-bodied members, no auto-property initialisers.
- References only `System.dll` and `System.Core.dll`; no new dependencies.
- x64 process; Razer DLL is `RzChromaBroadcastAPI64.dll`.
- Never let an exception cross back into Razer's native callback.
- `razer_app_id` default is `off`; the committed `settings.ini` keeps `razer_app_id = off`.
- `Program.Version` becomes `"0.7.0"`.
- Log and comment style matches the existing code: plain sentences, no jargon.
- The ID list is the 64 GUIDs from OpenChromaConnect `inc/RzChromaBroadcastAPIDefines.h` (MIT, commit 1800e64), credited in the source and README.

## Review Focus

- A static Synapse effect sends no colour events for minutes: the bridge must keep showing the feed colour, not drop to the clock. (Task 3 test `static_colour_never_stalls`.)
- Synapse closes or crashes mid-animation without sending NOT_LIVE: the clock takes over within 5 s and the feed reconnects at most every 30 s. (Task 3 test `animation_stall_falls_back`; Task 6 rule.)
- An ID is accepted but the app is switched off in Chroma Connect, so no colours ever arrive: the clock keeps running and `scan` tells the user to switch it on. (Task 3 test `live_without_colours_uses_clock`; Task 5 message.)
- A typo in `razer_app_id`: the previous value is kept, a line is logged, nothing crashes. (Task 2 test `razer_app_id_garbage_keeps_previous`.)
- Every ID refused, which is normal on many PCs: clock only, and no periodic rescans that flood Razer's own log (about 10 lines per `Init`). (Task 1 test `first_accepted_all_refused`; Task 6 rule.)

## Local commands

Self-test in the dev container (from the repo root):

```bash
mcs -noconfig -langversion:5 -target:exe -platform:x64 -warn:4 -out:/tmp/selftest.exe \
  -main:AuraChromaBridge.Tests.SelfTest -r:System.dll -r:System.Core.dll src/*.cs tests/*.cs \
  && mono /tmp/selftest.exe
```

Main build check (must stay warning-for-warning with build.cmd):

```bash
mcs -noconfig -langversion:5 -target:exe -platform:x64 -optimize+ -warn:4 -out:/tmp/acb.exe \
  -r:System.dll -r:System.Core.dll src/*.cs
```

---

### Task 1: Known app IDs, scan order, and the self-test harness

**Files:**
- Create: `src/AppIds.cs`
- Create: `tests/SelfTest.cs`, `tests/AppIdsTests.cs`
- Modify: `build.cmd` (add a `selftest` kind)

**Interfaces:**
- Produces:
  - `internal enum AppIdMode { Off, Auto, Pinned }` (in `src/AppIds.cs`)
  - `internal sealed class KnownAppId { public readonly Guid Id; public readonly string Name; public KnownAppId(Guid id, string name) }`
  - `internal static class KnownAppIds`
    - `public static readonly KnownAppId[] All` — 64 entries in scan order
    - `public static KnownAppId[] Candidates(AppIdMode mode, Guid pinned)` — `Off` → empty; `Auto` → `All`; `Pinned` → one entry, named from `All` if the GUID is known, else named `"razer_app_id"`
    - `public static KnownAppId[] ForScan(AppIdMode mode, Guid pinned)` — `All`, with the pinned ID first when `mode == Pinned` (no duplicate if it is already in `All`)
    - `public static KnownAppId FirstAccepted(IList<KnownAppId> candidates, Func<Guid, int> tryInit, Action<KnownAppId, int> report)` — calls `tryInit` in order, `report` after each, stops at the first `0`; returns it or `null`
    - `public static List<KeyValuePair<KnownAppId, int>> ProbeAll(IList<KnownAppId> candidates, Func<Guid, int> tryInit, Action uninit)` — tries every candidate, calls `uninit` after each accepted one, returns every (ID, result code) in order
  - Test harness: `namespace AuraChromaBridge.Tests`, `internal static class SelfTest` with `private static int Main()` returning the failure count; `internal static class Check` with `Equal<T>(T expected, T actual, string what)`, `True(bool value, string what)`; each test file has a static class with `public static void AddTo(List<KeyValuePair<string, Action>> tests)`.

Scan order for `All`: `Chroma Control - ASUS` {DEF05DCE-1662-4D9A-A312-A31028651915}, then `ASUS` {4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}, then every other partner ID in header order, then `Chroma Broadcast Test Application 01`–`10`, then `Chroma Broadcast Sample Application` {8FB61199-44F5-4C53-A706-747373A21796} last. Names and GUIDs copied exactly from the header; the file header comment credits OpenChromaConnect (MIT) and says these are a compatibility shim, not Razer-issued credentials.

- [ ] **Step 1: Write the failing tests** in `tests/AppIdsTests.cs`
  - `all_has_64_distinct_ids`: `All.Length == 64`; 64 distinct GUIDs.
  - `auto_order_asus_first_sample_last`: `Candidates(Auto, Guid.Empty)[0].Name == "Chroma Control - ASUS"`, `[1].Id == {4C24AD2B-…}`, `[63].Name == "Chroma Broadcast Sample Application"`, `[53..62]` all start with `"Chroma Broadcast Test Application "`.
  - `off_has_no_candidates`: `Candidates(Off, x).Length == 0`.
  - `pinned_known_uses_list_name`: `Candidates(Pinned, {4C24AD2B-…})` is one entry named `"ASUS"`.
  - `pinned_unknown_named_setting`: `Candidates(Pinned, Guid.NewGuid())` is one entry named `"razer_app_id"`.
  - `for_scan_pinned_unknown_goes_first`: `ForScan(Pinned, unknown).Length == 65`, `[0].Name == "razer_app_id"`.
  - `for_scan_pinned_known_no_duplicate`: `ForScan(Pinned, {4C24AD2B-…}).Length == 64`, `[0].Name == "ASUS"`.
  - `first_accepted_stops_at_first_zero`: fake returns `4309` except for the 3rd candidate (`0`) → returns the 3rd; `report` called exactly 3 times.
  - `first_accepted_all_refused`: fake always `4309` → `null`; `report` called 64 times.
  - `probe_all_tries_everything`: fake accepts the 2nd and 5th → result has 64 entries, codes `0` at indexes 1 and 4; `uninit` called exactly 2 times.

- [ ] **Step 2: Run the self-test; expect a compile failure** (`KnownAppIds` not defined).

- [ ] **Step 3: Implement `src/AppIds.cs` and `tests/SelfTest.cs`** per the Interfaces block. `SelfTest.Main` prints `PASS <name>` / `FAIL <name>: <message>` and a final `N passed, M failed`.

- [ ] **Step 4: Add `build.cmd selftest`**: `OUT=AuraChromaBridge-selftest.exe`, sources `src\*.cs tests\*.cs`, extra flag `/main:AuraChromaBridge.Tests.SelfTest`. Other kinds keep compiling `src\*.cs` only.

- [ ] **Step 5: Run the self-test; expect all PASS. Run the main build check; expect success.**

- [ ] **Step 6: Commit** — `feat: known Chroma Broadcast app IDs and scan order, with self-test harness`

### Task 2: Settings for the feed and the clock anchor

**Files:**
- Modify: `src/Program.cs` (`Settings`, `Program.Version`)
- Create: `tests/SettingsTests.cs`

**Interfaces:**
- Consumes: `AppIdMode` (Task 1).
- Produces on `Settings`: `public AppIdMode FeedMode = AppIdMode.Off; public Guid FeedAppId = Guid.Empty; public string ClockAnchor = "fixed";` and `public string FeedKey { get; }` returning `"off"`, `"auto"`, or the pinned GUID in `"B"` format.

Keys: `razer_app_id` = `off` | `auto` | a GUID with or without braces, case-insensitive. `clock_anchor` = `fixed` | `unlock`. Invalid values log `settings: … - keeping …` (same style as the other keys) and keep the previous value. Leave the old `appid` key, `AppId` field and `RazerSampleAppId` alone; Task 5 removes them together with their only user. `ToString()` adds `razer_app_id={FeedKey} clock_anchor={ClockAnchor}`. `Version = "0.7.0"`.

- [ ] **Step 1: Write the failing tests** in `tests/SettingsTests.cs`
  - `defaults_off_and_fixed`: new `Settings` → `FeedMode == Off`, `ClockAnchor == "fixed"`, `FeedKey == "off"`.
  - `razer_app_id_auto`: `Set("razer_app_id", "AUTO")` → `Auto`, `FeedKey == "auto"`.
  - `razer_app_id_guid_with_braces`: `"{4C24AD2B-D0A6-409F-B895-EC7632F3BEE6}"` → `Pinned`, `FeedAppId` equals it, `FeedKey == "{4c24ad2b-d0a6-409f-b895-ec7632f3bee6}"`.
  - `razer_app_id_guid_without_braces`: `"4c24ad2b-d0a6-409f-b895-ec7632f3bee6"` → `Pinned`.
  - `razer_app_id_garbage_keeps_previous`: after `auto`, `Set("razer_app_id", "banana")` → still `Auto`.
  - `clock_anchor_unlock`: `"unlock"` → `"unlock"`; then `"sometimes"` → still `"unlock"`.

- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement the keys and properties in `Settings`.**
- [ ] **Step 4: Run the self-test and the main build check; expect all PASS and success.**
- [ ] **Step 5: Commit** — `feat: razer_app_id and clock_anchor settings`

### Task 3: Feed state

**Files:**
- Create: `src/FeedState.cs`, `tests/FeedStateTests.cs`

**Interfaces:**
- Produces: `internal sealed class FeedState` (thread-safe; colours arrive on Razer's thread)
  - `public const long StallMs = 5000;`
  - `public void OnColors(int[] colors, long nowMs)` — ignores `null`/empty; keeps `colors[0] & 0xFFFFFF` (CL1); any colour also means live
  - `public void OnLive(bool live, long nowMs)`
  - `public bool TryGetColor(long nowMs, out int color)` — true only when live, at least one colour has arrived, and not stalled
  - `public bool IsStalled(long nowMs)` — true when the last two colours differed and `nowMs - lastColourMs > StallMs`
  - `public void Reset()`

- [ ] **Step 1: Write the failing tests** in `tests/FeedStateTests.cs` (`red = 0x0000FF`, `green = 0x00FF00`, `blue = 0xFF0000`)
  - `new_state_has_no_colour`: `TryGetColor(0, out c)` false.
  - `colour_is_cl1`: `OnColors({red, green, …}, 1000)` → `TryGetColor(1500)` true, `c == red`.
  - `colour_masked`: `OnColors({0x7F0000FF}, 0)` → `c == 0x0000FF`.
  - `static_colour_never_stalls`: red at 1000 and 1100 → `TryGetColor(61100)` true, red; `IsStalled(61100)` false.
  - `animation_stall_falls_back`: red at 1000, green at 1100 → `TryGetColor(6100)` true; `TryGetColor(6101)` false; `IsStalled(6101)` true.
  - `not_live_uses_clock_until_next_colour`: red at 1000, `OnLive(false, 1200)` → false at 1300; blue at 1400 → true at 1500, blue.
  - `live_without_colours_uses_clock`: `OnLive(true, 0)` → `TryGetColor(100)` false.
  - `null_and_empty_ignored`: red at 1000, then `OnColors(null, 1100)` and `OnColors(new int[0], 1200)` → still red at 1300.
  - `reset_clears`: red at 1000, `Reset()` → false at 1100.

- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement `FeedState`** with one `lock` around all fields.
- [ ] **Step 4: Run the self-test; expect all PASS.**
- [ ] **Step 5: Commit** — `feat: feed state with stall and not-live fallback`

### Task 4: Feed connection

**Files:**
- Create: `src/Feed.cs`

**Interfaces:**
- Consumes: `ChromaBroadcast` (`Load`, `Init`, `Register`, `ColorsReceived`, `LiveChanged`, `Dispose`, `Describe`), `KnownAppIds.Candidates`, `KnownAppIds.FirstAccepted`, `FeedState`, `Settings.FeedMode/FeedAppId`.
- Produces: `internal sealed class Feed : IDisposable`
  - `public static Feed Connect(Settings settings, bool verbose)` — `null` when the mode is `Off`, the DLL won't load, no candidate is accepted, or `Register` fails
  - `public KnownAppId AppId { get; }`
  - `public bool TryGetColor(out int color)` / `public bool IsStalled { get; }` — delegate to `FeedState` with `NowMs()`
  - `public bool Reconnect()` — disposes the current `ChromaBroadcast`, resets state, loads a fresh one, `Init` + `Register` with the same `AppId` only
  - `public static long NowMs()` — `Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency`

Logging: one summary line per `Connect`, never one line per ID. Accepted: `Feed: Synapse accepted the '{Name}' app ID ({tried} tried) - following Synapse's colours.` None: `Feed: none of the {n} app IDs were accepted (most common: {code} = {Describe}). Using the clock effect.` With `verbose == false`, only log on success.

- [ ] **Step 1: Implement `Feed`.** This is glue around the native DLL, so there is no unit test; behaviour is checked on the PC in Task 9.
- [ ] **Step 2: Run the main build check and the self-test; expect success and all PASS.**
- [ ] **Step 3: Commit** — `feat: connect to the Chroma Broadcast feed with the first accepted app ID`

### Task 5: The scan command (replaces chroma-test)

**Files:**
- Create: `src/Scan.cs`, `4-scan-razer-ids.cmd`
- Modify: `src/Program.cs` (mode list, usage line, dispatch; delete the `appid` key, `Settings.AppId` and `RazerSampleAppId`), `src/Modes.cs` (delete `ChromaTest`)

**Interfaces:**
- Consumes: `ChromaBroadcast`, `KnownAppIds.ForScan`, `FirstAccepted`, `ProbeAll`, `Settings.FeedMode/FeedAppId`.
- Produces: `internal static class Scan { public static int Run(Settings settings, bool all) }`; `Program.Main` accepts mode `scan`, with `all` detected as any later argument equal to `all` (case-insensitive). Usage: `AuraChromaBridge.exe [run | scan [all] | aura-test | identify | razer-test] [key=value ...]`.

Behaviour:
- Default: walk `ForScan(...)` with `FirstAccepted`, logging one line per ID: `  refused   {code,5}  {Name}  {GUID:B}` or `  ACCEPTED         {Name}  {GUID:B}`. Code 1152 reads `in use (probably by the running sync)`.
- `all`: first log `Each accepted ID may be added to the app list in the Razer Chroma app's Chroma Connect page.`, then `ProbeAll` with the same per-ID lines, then `Accepted on this PC: {names}`; then `Init` the first accepted ID again for listening.
- Listening: `Register`, then 15 s, printing each change of the five colours (format of the old chroma-test).
- Endings (exit codes): none accepted → `No app ID was accepted on this PC. The bridge will keep using the clock effect.` (1). Accepted, zero colours → `Synapse accepted '{Name}' but sent no colours. Open the Razer Chroma app > Chroma Connect, switch '{Name}' on, then run the scan again.` (1). Colours received → `The feed works with '{Name}'. In settings.ini set razer_app_id = auto (or razer_app_id = {GUID:B}).` (0).
- `4-scan-razer-ids.cmd`: same shape as `2-test-razer.cmd` (no admin; builds the test exe; runs `AuraChromaBridge-test.exe scan %*`; pauses).

- [ ] **Step 1: Implement `Scan`, wire `Program.Main`, delete `ChromaTest`, add the `.cmd`.**
- [ ] **Step 2: Run the main build check and the self-test; expect success and all PASS.**
- [ ] **Step 3: Commit** — `feat: scan command lists the app IDs Synapse accepts on this PC`

### Task 6: Sync follows the feed

**Files:**
- Modify: `src/Modes.cs` (`Sync.Run`, `OnSessionSwitch`, `OnPowerModeChanged`)

**Interfaces:**
- Consumes: `Feed` (Task 4), `Settings.FeedMode/FeedKey/ClockAnchor` (Task 2), `Effects.Base`, `Effects.Fill`.

Rules (each one decided; the implementer writes the code):
- Start: `Feed.Connect(settings, true)` unless `FeedMode == Off`. If that returns `null`, log once: `Feed: not available - using the clock effect. Trying again after the next unlock, wake or settings change.`
- Retry: no periodic full rescans. Full `Connect` only at start, in the `_reconnect` block (unlock, wake, sleep gap), and when `FeedKey` changes in a settings reload.
- Each frame: if the feed gives a colour, use it as `baseColor` and fill every ASUS LED with it (ignore `wave`); otherwise `baseColor = Effects.Base(settings, t)` and `Effects.Fill` as today. DSX uses `baseColor` as today. `brightness` and `asus_brightness` still apply.
- Log source changes once each: `Feed: following Synapse ('{Name}').` / `Feed: no colours from Synapse - using the clock effect.`
- Stall: when `feed.IsStalled` and at least 30 s since the last attempt, log `Feed: Synapse stopped sending colours - reconnecting.` and `feed.Reconnect()`.
- Status line: append ` | source feed` or ` | source clock`.
- Shutdown (`finally`): dispose the feed and log `Feed: disconnected.`
- Clock anchor: a static `_anchorUtcTicks` set at `Run` start and on unlock, logon, console connect, resume and the sleep-gap detector. When `ClockAnchor == "unlock"`, `t = (UtcNow - anchor) + Phase`; otherwise `t = (UtcNow - EffectEpoch) + Phase` as today. `phase` then means minus the delay between unlocking and Synapse restarting its cycle.

- [ ] **Step 1: Implement the rules in `Sync`.**
- [ ] **Step 2: Run the main build check and the self-test; expect success and all PASS.**
- [ ] **Step 3: Commit** — `feat: sync follows the Chroma feed, with clock fallback and unlock anchor`

### Task 7: Background copy relaunches itself after the watchdog

Deviation from the spec, recorded here: the spec said to add restart-on-failure to the scheduled task. Task Scheduler does not restart a task whose program started and then exited with an error code, so that setting would never fire for the watchdog's `Exit(3)`. The background copy relaunches itself instead. The console copy already restarts through `3-run-bridge.cmd`'s exit-code-3 loop, so it must not relaunch itself.

**Files:**
- Create: `src/Relaunch.cs`, `tests/RelaunchTests.cs`
- Modify: `src/Program.cs` (parse `relaunch=N`, skip it from settings), `src/Modes.cs` (watchdog, instance mutex)

**Interfaces:**
- Produces: `internal static class Relauncher`
  - `public static bool HasConsole()` — `GetConsoleWindow() != IntPtr.Zero` (kernel32)
  - `public static string BuildArguments(string[] args, int nextCount)` — drops any existing `relaunch=` argument, appends `relaunch={nextCount}`, quotes each argument by the Windows `CommandLineToArgvW` rules (quote when it has a space, tab or quote; double the backslashes before a quote and before the closing quote)
  - `public static bool TryRelaunch(string reason)` — refuses (and logs `Restarted 5 times already - not restarting again.`) when `Program.RelaunchCount >= 5`; otherwise starts the same exe with `BuildArguments(original args, RelaunchCount + 1)`, `UseShellExecute = false`
- `Program.RelaunchCount` (`int`, from `relaunch=N`, default 0).
- Watchdog: before `Environment.Exit(3)`, call `TryRelaunch("an ASUS write got stuck")` only when `!HasConsole()`.
- Instance mutex: when `RelaunchCount > 0`, wait up to 15 s for it (an `AbandonedMutexException` counts as acquired); otherwise the current immediate check.

- [ ] **Step 1: Write the failing tests** in `tests/RelaunchTests.cs`
  - `plain_args`: `{"run"}`, 1 → `run relaunch=1`.
  - `quotes_spaces`: `{"run", @"data=C:\My Folder"}`, 1 → `run "data=C:\My Folder" relaunch=1`.
  - `trailing_backslash_doubled`: `{"run", @"data=C:\My Folder\"}`, 1 → `run "data=C:\My Folder\\" relaunch=1`.
  - `replaces_old_count`: `{"run", "relaunch=2"}`, 3 → `run relaunch=3`.
- [ ] **Step 2: Run the self-test; expect compile failure.**
- [ ] **Step 3: Implement `Relauncher` and wire it into `Program` and `Sync`.**
- [ ] **Step 4: Run the self-test and the main build check; expect all PASS and success.**
- [ ] **Step 5: Commit** — `feat: background copy relaunches itself after a stuck ASUS write`

### Task 8: Settings file, .gitignore, README

**Files:**
- Modify: `settings.ini`, `README.md`
- Create: `.gitignore`

- [ ] **Step 1: `settings.ini`** — keep every current value; add, with comments in the file's style, `razer_app_id = off` (explaining `off`/`auto`/GUID, that the IDs are published partner IDs, and to run `4-scan-razer-ids.cmd` first) and `clock_anchor = fixed` (explaining `unlock` and what `phase` means with it). State that while the feed is live, `effect`, `color`, `speed` and `phase` are not used.
- [ ] **Step 2: `.gitignore`** — `logs/`, `*.exe`, `stop.request`.
- [ ] **Step 3: `README.md`** — rewrite to match 0.7.0: what it does now (Synapse keeps the Razer gear; the bridge drives ASUS and DSX), the two colour sources (feed, clock), the scripts table including `4-scan-razer-ids.cmd` and `build.cmd selftest`, every setting including `phase`, `dsx`, `dsx_port`, `razer_app_id` and `clock_anchor`, the files list, and a credit plus caveat for the OpenChromaConnect ID list.
- [ ] **Step 4: Check** that the README's settings list matches every `case` in `Settings.Set`, and its scripts table matches the `.cmd` files.
- [ ] **Step 5: Commit** — `docs: settings, .gitignore and README for 0.7.0`

### Task 9: Verify on the PC

Run by Mohamed; the dev container cannot reach Synapse or the ASUS SDK.

- [ ] **Step 1:** `build.cmd selftest`, then `AuraChromaBridge-selftest.exe` → expect `0 failed`.
- [ ] **Step 2:** `4-scan-razer-ids.cmd` → expect an `ACCEPTED` line and `The feed works with '…'`. If it says no colours were sent, switch the app on in the Razer Chroma app's Chroma Connect page and scan again.
- [ ] **Step 3:** Set `razer_app_id = auto`, run `install-startup.cmd`, and check `logs\run.log` shows `Feed: following Synapse` and `source feed` in the status lines.
- [ ] **Step 4:** Lock and unlock Windows; within a few seconds the log shows the reconnect and `Feed: following Synapse` again, and the fans match the keyboard by eye.
