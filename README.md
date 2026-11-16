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
Spectrum Cycling on every Razer device and the bridge drives only the ASUS side and DSX. The bridge's
cycle is tied to the PC clock, and `speed` and `phase` in `settings.ini` line it up with Synapse's
cycle. This is timed, not locked, so it can drift over long sessions.

### Why not read Synapse's colours directly?

Razer does publish a colour feed for third-party lighting (Chroma Connect / Chroma Broadcast), but
Synapse only sends it to applications with a Razer-issued application ID. `src/ChromaBroadcast.cs`
contains a receive-only client for that feed. It is not used by the sync today: without an ID,
`Init` returns 4309 ("Setup required for Test AppId"). If Razer issues an ID for this project, mode 2
becomes an exact lock with Synapse.

## Requirements

- Windows 10/11, 64-bit
- ASUS Armoury Crate, with **Lighting & Aura Sync > Aura Effects > In-Game lighting effects** turned ON
- Razer Synapse (3 or 4) for the Razer side
- Administrator rights. On the board this was developed on (ROG STRIX X870-F GAMING WIFI), the Aura
  SDK only writes to the LEDs from an elevated process.
- Optional: DSX with its UDP listener enabled (port 6969) for the DualSense light bar

## Quick start

1. `1-test-fans.cmd` - checks that the ASUS lighting can be driven.
2. `1b-find-fan-header.cmd` - lights each ASUS device in a different colour so you can tell which is which.
3. `2-test-razer.cmd` - checks that the Razer devices accept colours.
4. Edit `settings.ini` (effect, speed, which devices to drive).
5. `3-run-bridge.cmd` - runs the sync in a window, or `install-startup.cmd` to install it as a
   scheduled task that starts at sign-in with administrator rights.

`start-sync.cmd` and `stop-sync.cmd` start and stop the installed task without an admin prompt.
`uninstall-startup.cmd` removes it. `dsx-test.cmd` checks the DSX connection.

All scripts write a log to `logs\`.

## Settings

`settings.ini` is re-read every second while the sync runs.

| Key | Meaning |
|---|---|
| `effect` | `static`, `breathing`, `spectrum`, `wave` |
| `color` | `#RRGGBB` or a colour name, for static and breathing |
| `speed` | seconds per cycle |
| `phase` | seconds to shift the cycle (to line up with Synapse in mode 2) |
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
- Tested with: BlackWidow V4 X, DeathAdder V2, Base Station V2 Chroma, Kraken V4, ROG STRIX X870-F
  GAMING WIFI, DSX 3.1.7.

## Building

`build.cmd [main|test|bg]` compiles `src\*.cs` with
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. The code is C# 5 so that compiler accepts it.

## Licence

MIT
