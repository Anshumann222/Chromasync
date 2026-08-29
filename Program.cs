namespace ChromaSync;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("ChromaSync — Spotify ambient color -> MSI Mystic Light");
        Console.WriteLine();

        var config = AppConfig.LoadOrCreate();

        using var light = new MysticLightController();
        if (!light.Initialize())
        {
            Console.WriteLine("Could not start the Mystic Light SDK. Exiting.");
            return;
        }

        if (config.SelectedDeviceTypes.Count == 0 || args.Contains("--reconfigure"))
        {
            if (!SelectDevices(light, config))
            {
                Console.WriteLine("No devices selected. Exiting.");
                return;
            }
            config.Save();
        }

        // Save original color state before applying any styles or color changes
        light.SaveInitialColors(config.SelectedDeviceTypes);

        foreach (var type in config.SelectedDeviceTypes)
        {
            int infoStatus = MysticLightNative.GetLedInfo(type, 0, out var devName, out var styles);
            Console.WriteLine($"[MysticLight-Diag] MLAPI_GetLedInfo(Type: '{type}', Index: 0) -> Status Code: {infoStatus}");
            Console.WriteLine($"[MysticLight-Diag] Reported Name: '{devName}'");
            if (styles is null)
            {
                Console.WriteLine("[MysticLight-Diag] Supported Styles: null");
            }
            else
            {
                Console.WriteLine($"[MysticLight-Diag] Supported Styles count: {styles.Length}");
                for (int s = 0; s < styles.Length; s++)
                {
                    Console.WriteLine($"[MysticLight-Diag]   Style[{s}]: '{styles[s]}'");
                }
            }

            light.SetLedStyle(type, 0, "Steady");
        }

        Console.WriteLine($"Controlling: {string.Join(", ", config.SelectedDeviceTypes)}");
        Console.WriteLine("Waiting for Spotify — open the Now Playing / expanded view for real ambient colors.");
        Console.WriteLine("Press Ctrl+C to exit.");
        Console.WriteLine();

        var detector = new SpotifyAmbientColorDetector();
        var engine = new ColorTransitionEngine(
            TimeSpan.FromMilliseconds(config.TransitionDurationMs),
            config.ColorChangeThreshold);

        using var cts = new CancellationTokenSource();

        void OnShutdown(object? sender, EventArgs e)
        {
            try
            {
                cts.Cancel();
                light.RestoreOriginalState();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Shutdown] Error during shutdown cleanup: {ex.Message}");
            }
        }

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            OnShutdown(s, e);
        };
        AppDomain.CurrentDomain.ProcessExit += OnShutdown;

        var captureTask = CaptureLoop(detector, engine, config, cts.Token);
        var renderTask = RenderLoop(light, engine, config, cts.Token);

        await Task.WhenAll(captureTask, renderTask);
    }

    private static async Task CaptureLoop(SpotifyAmbientColorDetector detector, ColorTransitionEngine engine,
        AppConfig config, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (detector.TryFindSpotifyWindow(out var hWnd))
            {
                var color = detector.CaptureAmbientColor(hWnd);
                if (color is { } c)
                {
                    Console.WriteLine($"[Capture] Sampled ambient RGB: R={c.R}, G={c.G}, B={c.B}");
                    engine.SetTarget(c);
                }
                else
                {
                    Console.WriteLine("[Capture] Capture failed (bitmap capture returned null).");
                }
            }

            try { await Task.Delay(config.CaptureIntervalMs, token); }
            catch (TaskCanceledException) { }
        }
    }

    private static async Task RenderLoop(MysticLightController light, ColorTransitionEngine engine,
        AppConfig config, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var color = engine.Tick();
            light.SetColor(config.SelectedDeviceTypes, color);

            try { await Task.Delay(config.HardwareUpdateIntervalMs, token); }
            catch (TaskCanceledException) { }
        }
    }

    private static bool SelectDevices(MysticLightController light, AppConfig config)
    {
        if (light.Devices.Count == 0)
        {
            Console.WriteLine("Mystic Light didn't report any controllable devices.");
            return false;
        }

        Console.WriteLine("Found these Mystic Light devices:");
        for (int i = 0; i < light.Devices.Count; i++)
        {
            var d = light.Devices[i];
            Console.WriteLine($"  [{i}] {d.Type}  ({d.LedCount} LED area(s))");
        }

        Console.WriteLine();
        Console.Write("Enter the number(s) for your CPU cooler / fans, comma-separated (e.g. 0,2): ");
        var input = Console.ReadLine() ?? string.Empty;

        var selected = new List<string>();
        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out int idx) && idx >= 0 && idx < light.Devices.Count)
                selected.Add(light.Devices[idx].Type);
        }

        config.SelectedDeviceTypes = selected;
        return selected.Count > 0;
    }
}
