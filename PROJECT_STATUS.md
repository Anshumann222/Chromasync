# ChromaSync — Agent Context & Handoff Log

**Read this file completely before doing anything else in this project.** It exists because development has moved across multiple agent sessions (account/credit limits), and it is the only thing that carries real context between them — the code works, but the *reasoning* behind several non-obvious decisions lives here, not in comments.

## Standing instruction for whoever is reading this (you, the agent)

From now on, **update this file yourself** after completing any meaningful change, hitting a significant blocker, or before your context/session is likely to end. Add a dated entry to the "Session Log" section at the bottom — what you did, what you found, what's still pending. Don't wait to be asked. Treat this file as the project's memory, since chat history does not carry over between sessions. Keep the sections above "Session Log" (Overview, Verified Findings, Do Not Touch, Architecture) updated in place if something in them becomes outdated — don't just append contradictions to the log and leave the summary wrong.

## Project overview

A Windows console/tray app (C#/.NET 8) that detects Spotify's live ambient background color (the color Spotify's own UI renders around the album art in its expanded "Now Playing" view — NOT the dominant color of the album artwork itself) and syncs it in real time to CPU cooler/fan RGB via MSI's Mystic Light SDK, with smooth CIE Lab-space color transitions. Project folder: wherever this file lives (historically `D:\Mysticlight` — the folder name doesn't match the app name "ChromaSync" and that's fine, don't rename the folder without being asked). GitHub: https://github.com/Anshumann222/Chromasync (public, `vendor/MysticLightSDK/` is gitignored — MSI's proprietary SDK isn't redistributed).

## Verified findings from real hardware debugging (do not re-derive these — they are settled facts, not hypotheses)

1. **`MLAPI_GetLedName` returns zero entries** on this board. The name-based `MLAPI_SetLedColorsSync` path is unusable here — color writes use the direct indexed `MLAPI_SetLedColor(type, 0, r, g, b)` call instead.
2. **`"Direct All Sync"` is not a supported style** on this board/SDK version — fails with status `-102 DEVICE_NOT_FOUND`. `"Steady"` is the real, confirmed-working style (verified via `MLAPI_GetLedInfo`'s returned style list: Off, Wave, Steady, Flame, Breath, ColorRing, Lightning, Meteor, Default).
3. **Manual prerequisite, not yet automated**: before running the app, Mystic Light's active profile must be manually set to Steady style (any color) in MSI Center. The app's own attempt to set this style was failing (see finding #2) — as of the last session, a fix to change the attempted style string to `"Steady"` was requested but not yet confirmed built/tested.
4. **Restore-on-exit works correctly**, verified against real hardware: the app saves whatever color was active at startup and restores it on clean shutdown (Ctrl+C or closing the window). This cannot work if the process is killed via Task Manager — that's a Windows limitation, not a bug, don't try to "fix" it.
5. **Build environment note**: .NET 8 SDK (8.0.424) is installed in this environment. `dotnet build` builds cleanly with 0 warnings and 0 errors.
6. **Device topology**: this specific board only exposes one Mystic Light device/zone (`MSI_MB`, reported as 1 LED area) — no separate zones for individual fan/pump headers were found via the public SDK, even though MSI Center's own internal UI can reach more. Don't assume other boards behave the same way if this project is ever run on different hardware.

## Do not touch without a strong, explicit reason

These are proven working end-to-end against real hardware — treat any planned change to them as high-risk:
- `SpotifyAmbientColorDetector.cs` — PrintWindow capture + four-corner median sampling
- `ColorTransitionEngine.cs` — CIE Lab interpolation math
- The indexed `MLAPI_SetLedColor` write path in `MysticLightController.cs`
- The save-on-startup / restore-on-exit logic

## Architecture (why it's structured this way)

Three deliberately decoupled layers so future peripherals (Cosmic Byte Phantom TKL keyboard, HyperX Pulsefire Haste 2 Wireless mouse — not yet built) can be added as new controller classes without touching detection or transition logic:
1. **Detection** — `SpotifyAmbientColorDetector.cs`
2. **Transition engine** — `ColorTransitionEngine.cs`
3. **Hardware controller** — `MysticLightController.cs` / `MysticLightNative.cs`

`Program.cs` wires these together and manages the WinForms system tray message loop (`TrayApplicationContext.cs`). `AppConfig.cs` persists the user's selected device(s) to a local JSON config (`chromasync.config.json`, gitignored). `Logger.cs` handles thread-safe rolling logging to `chromasync.log`. `DevicePickerForm.cs` provides a native WinForms dialog for device selection on first run and reconfiguration. `vendor/MysticLightSDK/` holds MSI's proprietary SDK DLL + docs/sample (gitignored, must be manually placed by anyone who clones the repo — see README).

## Completed Tasks

### 1. Quieted Logging & Persistent Rolling Log
- Logging only triggers on state changes (Spotify found/lost, accepted color changes, non-zero hardware write errors).
- Suppressed per-tick noise and status code 0 hardware write spam.
- Integrated thread-safe rolling file logger (`Logger.cs`) writing to `chromasync.log`.

### 2. Fixed Cosmetic & Shutdown Issues
- Set startup LED style to `"Steady"` and logged once at startup.
- Fixed `CancellationTokenSource has been disposed` shutdown error using atomic `Interlocked.Exchange` flag guard, ensuring the restore-on-shutdown sequence runs exactly once without race conditions.

### 3. Minimized to System Tray
- Changed `OutputType` to `WinExe` and enabled `<UseWindowsForms>true</UseWindowsForms>`.
- Added system tray icon (`TrayApplicationContext.cs`) with context menu: "ChromaSync", status, "Reconfigure Device...", "Open Log File", and "Exit".
- Added native WinForms device selection dialog (`DevicePickerForm.cs`) for first run and reconfiguration.

---

## Session Log

*(Add a new dated entry below each time you make progress, hit a blocker, or a session is ending. Keep entries brief — a few lines each.)*

- **Handoff created**: MVP fully working and verified (real-time color sync confirmed against physical hardware, restore-on-exit confirmed working, pushed to GitHub with a complete README).
- **2026-08-29**: Implemented all three prioritized polish items:
  1. Converted application to `WinExe` with WinForms tray icon (`TrayApplicationContext`), context menu (Exit, Reconfigure Device, Open Log), and GUI device picker (`DevicePickerForm`).
  2. Implemented centralized rolling file logger (`Logger.cs` -> `chromasync.log`), quieted per-frame capture ticks, noise rejects, and status 0 hardware write logs.
  3. Fixed shutdown race condition on `CancellationTokenSource` with atomic flag guard so cleanup runs cleanly exactly once.
  4. Verified `dotnet build` succeeded with 0 warnings and 0 errors.
