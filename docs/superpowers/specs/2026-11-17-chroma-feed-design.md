# AuraChromaBridge v0.7.0 - Chroma feed mode (design)

Approved by Mohamed, 2026-11-17.

## Intent

Lock the ASUS side (fans, strip, AIO, motherboard) and the PS5 light bar exactly to what
Razer Synapse is showing, by receiving Synapse's own colours through Razer's Chroma
Broadcast feed instead of estimating them from the clock. Today the clock estimate drifts
over hours, and falls out of step every time Windows is locked, because Synapse resets to
its default lighting and restarts its cycle.

The feed only accepts Razer-approved app IDs (it checks them against `broadcast.dat` and
refuses test IDs with error 4309). Razer's own ID for this project has been requested but
may never arrive, so the bridge must work without waiting for it.

## Goals

- Receive the live colour from the broadcast feed and drive the ASUS devices and DSX from it.
- Get an app ID Synapse accepts without waiting on Razer, in a way that works on anyone's PC.
- Fall back cleanly to the clock effect whenever the feed isn't available, so the lights never freeze.
- Recover automatically when the watchdog restarts the bridge.

## Non-goals

- Driving Razer gear from the bridge. Synapse keeps it (`razer_skip = all` unchanged).
- Shipping private credentials. The IDs used are the ones already published in the
  open-source OpenChromaConnect headers.

## Anti-goals

- An update that changes behaviour on someone's PC without them opting in.
- Lights freezing on a colour because the feed went quiet.

## Approaches considered

| Approach | Trade-off | Decision |
|---|---|---|
| Wait for Razer's own app ID | Cleanest, but slow and uncertain | Parallel track, not a dependency |
| Hard-code one partner ID | Simplest, but fails outright on any PC where that ID isn't accepted | Rejected |
| Scan a list of known IDs, use the first Synapse accepts | Portable, self-healing, no per-PC setup | **Chosen** |

## Decided

- New setting `razer_app_id = off | auto | {GUID}`.
  - `off` (default): today's clock behaviour, unchanged.
  - `auto`: on connect, try each known ID until Synapse accepts one, register for the feed,
    and log which ID won.
  - `{GUID}`: use only that ID (for Razer's own ID when it arrives).
- New command `AuraChromaBridge.exe scan`: tries every known ID, prints which ones Synapse
  accepts on this PC, then exits. Runs alongside a running sync.
- Feed live: the ASUS devices and the DSX light bar follow the received colour; the clock is ignored.
- Feed not live, or no ID accepted: fall back to the clock effect. After a lock or wake,
  re-anchor the clock fallback so it lines up again. `speed` and `phase` stay as the
  fallback tuning.
- The known-ID list ships in source with a README note that it is a compatibility shim,
  not a Razer-issued credential.
- `install-task.ps1`: restart on failure, so the watchdog exit relaunches the bridge.
- `.gitignore` for `logs/`, `*.exe` and `stop.request`.
- README rewrite covering ASUS-only and feed modes, `phase`, DSX, the new settings and `scan`.
- The committed `settings.ini` keeps `razer_app_id = off`.

## Left to the builder

- How long the feed must be silent before falling back to the clock.
- Whether the feed colour drives the ASUS `wave` effect per LED or as one colour.
