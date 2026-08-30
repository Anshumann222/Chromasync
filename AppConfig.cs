using System.Text.Json;

namespace ChromaSync;

/// <summary>
/// Small persisted config: which Mystic Light device type(s) to drive, and
/// the timing knobs for capture / transition / hardware update.
/// </summary>
public class AppConfig
{
    public List<string> SelectedDeviceTypes { get; set; } = new();

    /// <summary>How long a color transition takes, in milliseconds.</summary>
    public int TransitionDurationMs { get; set; } = 3500;

    /// <summary>How often we re-sample Spotify's window, in milliseconds.</summary>
    public int CaptureIntervalMs { get; set; } = 400;

    /// <summary>How often we push a color to the RGB hardware, in milliseconds.
    /// Kept lower-frequency than the capture/transition tick since most RGB
    /// controllers don't need (or handle well) high-frequency updates.</summary>
    public int HardwareUpdateIntervalMs { get; set; } = 50;

    /// <summary>Minimum perceptual (CIE Lab) distance before a newly sampled
    /// color is treated as a real change rather than sampling noise.</summary>
    public double ColorChangeThreshold { get; set; } = 4.0;

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "chromasync.config.json");

    public static AppConfig LoadOrCreate()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg is not null)
                    return cfg;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Couldn't read existing config ({ex.Message}); starting fresh.");
            }
        }

        return new AppConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
