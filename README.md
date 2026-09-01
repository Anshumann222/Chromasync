# ChromaSync

Live sync of Spotify's ambient background color to MSI Mystic Light RGB (CPU cooler/fans) with smooth perceptual color transitions.

## What it does

Detects the ambient background color Spotify's desktop app renders in its expanded "Now Playing" view (not the dominant color of the album art itself — the actual background color/gradient shown around it) and syncs it live to CPU cooler/fan RGB via MSI's Mystic Light SDK, with smooth color transitions as tracks change.

## How it works

- **Window capture**: Captures Spotify's window pixels via Win32 `PrintWindow` (works even if the window is occluded behind another application or game).
- **Corner sampling**: Samples the four corners of the captured bitmap (always background, never the centered album art or text) and calculates the per-channel median.
- **Perceptual smoothing**: Eases between colors in CIE Lab space rather than raw RGB, preventing muddy desaturated middle tones during color transitions.
- **Hardware control**: Communicates directly with `MysticLight_SDK.dll` via P/Invoke.

## Verified hardware behavior

Found through real hardware debugging on MSI motherboards:

- **Indexed LED writes**: On this board, `MLAPI_GetLedName` returns zero entries. Color updates bypass name-based calls (`SetLedColorsSync`) and directly invoke the indexed method:
  ```csharp
  MLAPI_SetLedColor(deviceType, 0, r, g, b);
  ```
- **Supported LED style**: `"Direct All Sync"` is **not** supported on this board/SDK version (fails with `-102 DEVICE_NOT_FOUND`). `"Steady"` is the style that works and renders color writes correctly.
- **Manual Steady profile prerequisite**: Before running ChromaSync, manually set the active Mystic Light profile to **Steady** style (any color) in MSI Center. The application's programmatic attempt to force this style currently fails silently (logged as non-fatal) and relies on the board already being in Steady mode from this prior manual step.
- **State restoration on shutdown**: The application caches whatever RGB color was active at startup and restores it upon clean exit (`Ctrl+C` or closing the console window). Note that killing the process via Task Manager bypasses exit handlers and cannot restore the color — this is a Windows-level limitation, not an application bug.

## Setup

1. **Prerequisites**:
   - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the full SDK is required for building, not just the Desktop Runtime).
   - **MSI Center** with the **Mystic Light** module installed.
   - In Mystic Light Settings, disable both **"Third Party RGB"** and **"LED Power Saving Mode"** so external writes are not overwritten or put to sleep.
   - Set Mystic Light to **Steady** style once in MSI Center (see hardware behavior above).

2. **Clone the repository**:
   ```bash
   git clone <repo-url>
   cd Mysticlight
   ```

3. **Install the Mystic Light SDK**:
   - Download the official SDK from MSI: [Mystic Light RGB Gaming PC Download](https://www.msi.com/Landing/mystic-light-rgb-gaming-pc/download)
   - Extract the archive and copy the 64-bit `MysticLight_SDK.dll` (along with any accompanying docs/sample files) into `vendor\MysticLightSDK\` at the project root.
   - *Note*: The `vendor/` folder is gitignored as MSI's proprietary SDK cannot be redistributed.

4. **Build**:
   ```bash
   dotnet build
   ```
   The build automatically copies `MysticLight_SDK.dll` from `vendor\MysticLightSDK\` into the output folder via a post-build MSBuild target.

5. **Run**:
   Run the compiled executable **as Administrator** (the Mystic Light SDK requires elevated permissions to interface with the motherboard embedded controller):
   ```bash
   # From bin\Debug\net8.0-windows\
   ChromaSync.exe
   ```

6. **Configuration & Settings**:
   - **Device Configuration**: On first run, the app prompts you to select the device(s) corresponding to your CPU cooler / fans. You can reconfigure this at any time via the tray menu (**Reconfigure Device...**).
   - **Live Settings**: Right-click the system tray icon and select **Settings...** to adjust **Transition Speed** (500ms–6000ms) and **Noise Threshold** (0.0–20.0 Lab ΔE) with live-updating sliders. Changes take effect immediately at runtime without requiring an application restart.
   - **Persistence**: All settings are persisted to `chromasync.config.json` and loaded automatically across restarts.

## Known limitations

- **Single-zone testing**: Verified only against a single-zone `MSI_MB` device. Motherboards that expose separate addressable zones for individual fan or pump headers may exhibit different indexing behavior.

## Roadmap

- **Peripherals expansion**: Support for Cosmic Byte Phantom TKL keyboard and HyperX Pulsefire Haste 2 Wireless mouse.
- **Modular architecture**: The codebase is decoupled into three distinct layers:
  1. *Detection* (`SpotifyAmbientColorDetector`)
  2. *Transition Engine* (`ColorTransitionEngine`)
  3. *Hardware Controller* (`MysticLightController`)
  
  Adding support for new keyboards, mice, or lighting ecosystems requires implementing a new controller class without modifying color detection or transition interpolation logic.
