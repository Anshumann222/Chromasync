using System.Drawing;
using System.Windows.Forms;

namespace ChromaSync;

internal static class Program
{
    private static int _shutdownStarted = 0;
    private static CancellationTokenSource? _cts;
    private static MysticLightController? _light;

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        Logger.Info("========================================");
        Logger.Info("ChromaSync starting up");
        Logger.Info("========================================");

        var config = AppConfig.LoadOrCreate();

        _light = new MysticLightController();
        if (!_light.Initialize())
        {
            MessageBox.Show("Could not start the Mystic Light SDK.\n\nMake sure MysticLight_SDK.dll is present, MSI Center is installed, and ChromaSync is running as Administrator.",
                "ChromaSync — Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Logger.Error("Mystic Light SDK initialization failed. Exiting.");
            return;
        }

        if (config.SelectedDeviceTypes.Count == 0 || args.Contains("--reconfigure"))
        {
            using var picker = new DevicePickerForm(_light.Devices, config.SelectedDeviceTypes);
            if (picker.ShowDialog() != DialogResult.OK || picker.SelectedTypes.Count == 0)
            {
                Logger.Info("No devices selected in picker. Exiting.");
                _light.Release();
                return;
            }

            config.SelectedDeviceTypes = picker.SelectedTypes;
            config.Save();
            Logger.Info($"Selected device(s): {string.Join(", ", config.SelectedDeviceTypes)}");
        }

        // Save original color state before applying any styles or color changes
        _light.SaveInitialColors(config.SelectedDeviceTypes);

        foreach (var type in config.SelectedDeviceTypes)
        {
            _light.SetLedStyle(type, 0, "Steady");
        }

        Logger.Info($"Controlling devices: {string.Join(", ", config.SelectedDeviceTypes)}");
        Logger.Info("Starting capture and render loops in background...");

        var detector = new SpotifyAmbientColorDetector();
        var engine = new ColorTransitionEngine(
            TimeSpan.FromMilliseconds(config.TransitionDurationMs),
            config.ColorChangeThreshold);

        _cts = new CancellationTokenSource();

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
        try
        {
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Shutdown();
            };
        }
        catch { }

        var token = _cts.Token;
        _ = Task.Run(() => CaptureLoop(detector, engine, _light, config, token), token);
        _ = Task.Run(() => RenderLoop(_light, engine, config, token), token);

        using var trayContext = new TrayApplicationContext(_light, config, Shutdown);
        Application.Run(trayContext);
    }

    private static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return;

        Logger.Info("[Shutdown] Initiating graceful shutdown...");

        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Logger.Warn($"[Shutdown] Warning cancelling tasks: {ex.Message}");
        }

        try
        {
            _light?.RestoreOriginalState();
        }
        catch (Exception ex)
        {
            Logger.Error($"[Shutdown] Error restoring light state: {ex.Message}");
        }

        try
        {
            Application.Exit();
        }
        catch { }

        Logger.Info("[Shutdown] ChromaSync shutdown complete.");
    }

    private static async Task CaptureLoop(SpotifyAmbientColorDetector detector, ColorTransitionEngine engine,
        MysticLightController light, AppConfig config, CancellationToken token)
    {
        bool? lastIsLive = null;

        while (!token.IsCancellationRequested)
        {
            bool found = detector.TryFindSpotifyWindow(out var hWnd);
            bool isMinimized = found && NativeMethods.IsIconic(hWnd);
            bool isLive = found && !isMinimized;

            if (isLive != lastIsLive)
            {
                lastIsLive = isLive;
                if (isLive)
                {
                    Logger.Info("[SpotifyDetector] Spotify window visible. Resuming live ambient color capture.");
                }
                else
                {
                    Logger.Info("[SpotifyDetector] Spotify not visible (minimized or closed). Using original color.");
                }
            }

            if (isLive)
            {
                var color = detector.CaptureAmbientColor(hWnd);
                if (color is { } c)
                {
                    engine.SetTarget(c);
                }
            }
            else
            {
                var primaryDevice = config.SelectedDeviceTypes.FirstOrDefault();
                var fallbackColor = (primaryDevice != null ? light.GetOriginalColor(primaryDevice) : null) ?? Color.Black;
                engine.SetTarget(fallbackColor);
            }

            try
            {
                await Task.Delay(config.CaptureIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private static async Task RenderLoop(MysticLightController light, ColorTransitionEngine engine,
        AppConfig config, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var color = engine.Tick();
            light.SetColor(config.SelectedDeviceTypes, color);

            try
            {
                await Task.Delay(config.HardwareUpdateIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
