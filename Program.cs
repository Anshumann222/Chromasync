using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ChromaSync;

internal static class Program
{
    private enum AppState
    {
        Dormant,
        Active
    }

    private static AppState _currentState = AppState.Dormant;
    private static DateTime? _spotifyAbsentSince;
    private static int _shutdownStarted = 0;
    private static bool _isStateTransitioning = false;

    private static CancellationTokenSource? _activeCts;
    private static MysticLightController? _light;
    private static TrayApplicationContext? _trayContext;
    private static AppConfig _config = null!;
    private static System.Windows.Forms.Timer? _stateTimer;

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        Logger.Info("========================================");
        Logger.Info("ChromaSync starting up (Dormant mode)");
        Logger.Info("========================================");

        _config = AppConfig.LoadOrCreate();

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

        _trayContext = new TrayApplicationContext(_config, Shutdown);

        // State polling timer: checks every 5 seconds
        _stateTimer = new System.Windows.Forms.Timer
        {
            Interval = 5000
        };
        _stateTimer.Tick += OnStateTimerTick;
        _stateTimer.Start();

        // Perform an initial state check immediately upon startup
        CheckState();

        Application.Run(_trayContext);
    }

    private static void OnStateTimerTick(object? sender, EventArgs e)
    {
        CheckState();
    }

    private static void CheckState()
    {
        if (_shutdownStarted != 0 || _isStateTransitioning)
            return;

        _isStateTransitioning = true;
        try
        {
            bool isRunning = IsSpotifyRunning();

            if (_currentState == AppState.Dormant)
            {
                if (isRunning)
                {
                    TransitionToActive();
                }
            }
            else if (_currentState == AppState.Active)
            {
                if (isRunning)
                {
                    _spotifyAbsentSince = null;
                }
                else
                {
                    if (_spotifyAbsentSince == null)
                    {
                        _spotifyAbsentSince = DateTime.UtcNow;
                    }
                    else if (DateTime.UtcNow - _spotifyAbsentSince.Value >= TimeSpan.FromSeconds(15))
                    {
                        TransitionToDormant();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[StateMachine] Error in state check: {ex.Message}");
        }
        finally
        {
            _isStateTransitioning = false;
        }
    }

    private static bool IsSpotifyRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("Spotify");
            bool isRunning = processes.Length > 0;
            foreach (var p in processes)
            {
                p.Dispose();
            }
            return isRunning;
        }
        catch
        {
            return false;
        }
    }

    private static void TransitionToActive()
    {
        Logger.Info("[StateMachine] Spotify process detected. Transitioning from Dormant to Active.");

        _light = new MysticLightController();
        if (!_light.Initialize())
        {
            Logger.Error("[StateMachine] Mystic Light SDK initialization failed. Remaining in Dormant state.");
            _light.Dispose();
            _light = null;
            return;
        }

        if (_config.SelectedDeviceTypes.Count == 0)
        {
            using var picker = new DevicePickerForm(_light.Devices, _config.SelectedDeviceTypes);
            if (picker.ShowDialog() != DialogResult.OK || picker.SelectedTypes.Count == 0)
            {
                Logger.Info("[StateMachine] No devices selected in picker. Returning to Dormant.");
                _light.RestoreOriginalState();
                _light.Dispose();
                _light = null;
                return;
            }

            _config.SelectedDeviceTypes = picker.SelectedTypes;
            _config.Save();
            Logger.Info($"[StateMachine] Selected device(s): {string.Join(", ", _config.SelectedDeviceTypes)}");
        }

        // Save original color state before applying any styles or color changes
        _light.SaveInitialColors(_config.SelectedDeviceTypes);

        foreach (var type in _config.SelectedDeviceTypes)
        {
            _light.SetLedStyle(type, 0, "Steady");
        }

        Logger.Info($"[StateMachine] Controlling devices: {string.Join(", ", _config.SelectedDeviceTypes)}");
        Logger.Info("[StateMachine] Starting capture and render loops in background...");

        var detector = new SpotifyAmbientColorDetector();
        var engine = new ColorTransitionEngine(
            TimeSpan.FromMilliseconds(_config.TransitionDurationMs),
            _config.ColorChangeThreshold);

        _activeCts = new CancellationTokenSource();
        var token = _activeCts.Token;

        _ = Task.Run(() => CaptureLoop(detector, engine, _light, _config, token), token);
        _ = Task.Run(() => RenderLoop(_light, engine, _config, token), token);

        _trayContext?.ShowTray(_light, engine);

        _currentState = AppState.Active;
        _spotifyAbsentSince = null;
    }

    private static void TransitionToDormant()
    {
        Logger.Info("[StateMachine] Spotify process absent for 15s grace period. Transitioning from Active to Dormant.");

        try
        {
            _activeCts?.Cancel();
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            Logger.Warn($"[StateMachine] Warning cancelling active tasks: {ex.Message}");
        }

        _trayContext?.HideTray();

        try
        {
            _light?.RestoreOriginalState();
        }
        catch (Exception ex)
        {
            Logger.Error($"[StateMachine] Error restoring light state: {ex.Message}");
        }
        finally
        {
            _light?.Dispose();
            _light = null;
        }

        try
        {
            _activeCts?.Dispose();
        }
        catch { }
        finally
        {
            _activeCts = null;
        }

        _spotifyAbsentSince = null;
        _currentState = AppState.Dormant;
    }

    private static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return;

        Logger.Info("[Shutdown] Initiating graceful shutdown...");

        try
        {
            _stateTimer?.Stop();
            _stateTimer?.Dispose();
            _stateTimer = null;
        }
        catch { }

        try
        {
            _activeCts?.Cancel();
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
        finally
        {
            _light?.Dispose();
            _light = null;
        }

        try
        {
            _activeCts?.Dispose();
        }
        catch { }
        finally
        {
            _activeCts = null;
        }

        try
        {
            if (_trayContext != null)
            {
                _trayContext.HideTray();
                _trayContext.Dispose();
            }
        }
        catch { }

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
