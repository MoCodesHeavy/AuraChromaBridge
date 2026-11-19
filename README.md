# AuraChromaBridge

A small Windows utility that keeps ASUS Aura motherboard lighting (ARGB headers and onboard LEDs),
Razer Chroma peripherals and a PS5 DualSense light bar on the same lighting effect.

It is a single C# program with no dependencies. It builds with the C# compiler that ships with
Windows, so there is nothing to install.

Unofficial project. Not affiliated with Razer, ASUS or Sony.

## What it drives

| Device | How |
|---|---|
| ASUS motherboard LEDs and ARGB headers (fans, strips, AIO) | ASUS Aura SDK (`aura.sdk.1` COM object, installed with Armoury Crate) |
| Razer keyboard, mouse, mousepad/stand, keypad, Chroma Link | Razer Chroma SDK (`RzChromaSDK64.dll`, installed with Synapse) |
| PS5 DualSense light bar | [DSX](https://store.steampowered.com/app/1812620/DSX/) local UDP port |

## Two ways to run it

**1. Bridge is the lighting master.** The bridge sends one effect to the Razer devices and the ASUS
devices from the same clock, so they are frame-accurate with each other. Use this when all your Razer
devices accept colours from Chroma apps.

**2. Synapse is the master for Razer gear (`razer_skip = all`).** Some devices, such as the Kraken V4
headset, ignore Chroma apps and only follow Synapse's own effects. In this mode Synapse runs
Spectrum Cycling on every Razer device and the bridge drives only the ASUS side and DSX. By default
the bridge's cycle is tied to the PC clock, and `speed` and `phase` in `settings.ini` line it up with
Synapse's cycle. This is timed, not locked, so it can drift over long sessions, and Synapse restarts
its cycle after Windows is locked (`clock_anchor = unlock` helps with that).

### Following Synapse's colour feed (`razer_app_id`)

Razer publishes a colour feed for third-party lighting (Chroma Connect / Chroma Broadcast). With it,
mode 2 becomes an exact lock: the bridge receives the colour Synapse is showing and passes it to the
ASUS side and DSX, so there is nothing to tune and nothing to drift. When the feed has nothing (Synapse
closed, restarting, or not started yet after sign-in), the bridge falls back to the clock effect and
reconnects by itself.

Synapse only sends the feed to applications with a Razer-issued application ID. This project has asked
Razer for one; until it arrives, **`razer_app_id` is `off` by default** and the bridge behaves exactly
as before.

For local testing, `razer_app_id = auto` tries the 64 IDs published in the open-source
[OpenChromaConnect](https://github.com/OpenChromaConnect/ChromaBroadcastAPI) headers (MIT) until
Synapse accepts one. These are other brands' IDs, not ones Razer issued to this project: Synapse shows
the bridge under that brand's name in Chroma Connect, and an update may stop accepting them.
`4-scan-razer-ids.cmd` shows which ID works on your PC and checks that colours arrive. Once Razer
issues an ID, set `razer_app_id = {that ID}`.

While the feed is on, the bridge leaves every Razer device to Synapse whatever `razer_skip` says, since
it would otherwise receive its own colours back.

## Requirements

- Windows 10/11, 64-bit
- ASUS Armoury Crate, with **Lighting & Aura Sync > Aura Effects > In-Game lighting effects** turned ON
- Razer Synapse (3 or 4) for the Razer side
- Administrator rights. On the board this was developed on (ROG STRIX X870-F GAMING WIFI), the Aura
  SDK only writes to the LEDs from an elevated process.
- Optional: DSX with its UDP listener enabled (port 6969) for the DualSense light bar

## Install

1. Download `AuraChromaBridge-Setup-x64.exe` from the
   [Releases](https://github.com/MoCodesHeavy/AuraChromaBridge/releases) page.
2. Run it. The setup file isn't code-signed, so Windows may show "Windows protected your PC": choose
   **More info**, then **Run anyway**. Setup asks for administrator rights, because the bridge needs
   them to drive ASUS lighting.
3. Choose whether to **follow Synapse's colours**. Setup ticks this when it finds both Razer Synapse
   (with Chroma Connect) and ASUS Armoury Crate. It is unofficial: until Razer issues an app ID for this
   project, the bridge uses another brand's published ID (see `razer_app_id` above). Untick it to use
   the bridge's own clock effect.
4. On the last page you can run the app ID scan, which checks that Synapse's colours arrive, and start
   the sync. From then on it starts by itself at sign-in.

The Start menu folder **AuraChromaBridge** has: Scan Razer app IDs, Edit settings, Open logs folder,
Start sync, Stop sync and Uninstall. Settings and logs live in `%LOCALAPPDATA%\AuraChromaBridge`, or in
the folder an earlier install already used. Upgrading keeps your `settings.ini`. Uninstalling removes the
program and its start-at-sign-in task, and asks before deleting settings it created.

## Build from source

1. `1-test-fans.cmd` - checks that the ASUS lighting can be driven.
2. `1b-find-fan-header.cmd` - lights each ASUS device in a different colour so you can tell which is which.
3. `2-test-razer.cmd` - checks that the Razer devices accept colours.
4. Edit `settings.ini` (effect, speed, which devices to drive).
5. Optional: `4-scan-razer-ids.cmd` - checks whether Synapse's colour feed works on this PC
   (see `razer_app_id` above).
6. `3-run-bridge.cmd` - runs the sync in a window, or `install-startup.cmd` to install it as a
   scheduled task that starts at sign-in with administrator rights.

`start-sync.cmd` and `stop-sync.cmd` start and stop the installed task without an admin prompt.
`uninstall-startup.cmd` removes it. `dsx-test.cmd` checks the DSX connection.

All scripts write a log to `logs\`. If an ASUS write ever hangs, the installed copy restarts itself
(at most 5 times per sign-in); the windowed copy is restarted by `3-run-bridge.cmd`.

## Settings

`settings.ini` is re-read every second while the sync runs.

| Key | Meaning |
|---|---|
| `effect` | `static`, `breathing`, `spectrum`, `wave` |
| `color` | `#RRGGBB` or a colour name, for static and breathing |
| `speed` | seconds per cycle |
| `phase` | seconds to shift the cycle (to line up with Synapse in mode 2) |
| `clock_anchor` | `fixed` (count from a fixed date) or `unlock` (count from the last unlock or wake, when Synapse restarts its cycle) |
| `razer_app_id` | `off` (default), `auto`, or one app ID: follow Synapse's colour feed (see above) |
| `brightness`, `asus_brightness` | 1-100 |
| `maxfps` | updates per second |
| `razer_style` | `auto`, `static`, `custom` (per-LED command) |
| `razer_skip` | Razer device kinds to leave to Synapse, or `all` |
| `only`, `skip` | which ASUS devices to drive, by name |
| `dsx`, `dsx_port` | DualSense light bar through DSX |

## Notes from testing

- If ASUS writes return in about 0.1 ms instead of 20-40 ms, Armoury Crate (or another lighting app
  registered with it) has control. The bridge detects this and reconnects. Check the priority order under
  In-Game lighting effects > Game list.
- In Synapse 4, any active Chroma app takes over all Razer devices, regardless of the device mask the
  app registers with.
- With the colour feed, the Chroma Connect page's effect decides what the bridge receives. An Advanced
  Effect that doesn't light Chroma Connect devices sends black, so the ASUS side goes dark; pick an
  effect that covers them (for example Quick Effects > Spectrum Cycling).
- Tested with: BlackWidow V4 X, DeathAdder V2, Base Station V2 Chroma, Kraken V4, ROG STRIX X870-F
  GAMING WIFI, DSX 3.1.7.

## Building

Every push is built on GitHub Actions (`.github/workflows/build.yml`): the exes, the self-test and the
installer (`installer/AuraChromaBridge.iss`, Inno Setup). A `v*` tag matching `Program.Version` creates
a draft release with the setup file attached.

`build.cmd [main|test|bg]` compiles `src\*.cs` with
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The code is C# 5 so that compiler accepts it.
`build.cmd selftest` builds `AuraChromaBridge-selftest.exe` from `src` and `tests`; run it from the
project folder to check the parts that need no Razer or ASUS hardware.

## Licence

MIT
