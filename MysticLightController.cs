using System.Drawing;

namespace ChromaSync;

public record MysticLightDevice(string Type, string[] LedNames)
{
    public int LedCount => LedNames.Length;
}

public class MysticLightController : IDisposable
{
    private bool _initialized;
    private readonly Dictionary<string, (int R, int G, int B)> _savedColors = new();
    private readonly object _shutdownLock = new();
    private bool _restored;

    public List<MysticLightDevice> Devices { get; } = new();

    /// <summary>
    /// Initializes the SDK and enumerates devices. We deliberately don't
    /// hardcode a device type string (e.g. a guess at a fan-header name) —
    /// the exact identifiers MSI's SDK reports depend on your specific
    /// motherboard/AIO, so callers pick from the real discovered list.
    /// </summary>
    public bool Initialize()
    {
        int status;
        try
        {
            status = MysticLightNative.Initialize();
        }
        catch (DllNotFoundException)
        {
            Logger.Error("MysticLight_SDK.dll wasn't found. Place it next to ChromaSync.exe " +
                         "(see README for where to get it), and make sure MSI Center's " +
                         "Mystic Light module is installed.");
            return false;
        }

        if (status != 0)
        {
            Logger.Error($"Mystic Light SDK failed to initialize (code {status}). Try running as Administrator.");
            return false;
        }

        _initialized = true;
        LoadDevices();
        return true;
    }

    private void LoadDevices()
    {
        Devices.Clear();

        int status = MysticLightNative.GetDeviceInfo(out var types, out var ledCounts);
        if (status != 0 || types is null)
        {
            Logger.Warn($"Could not enumerate Mystic Light devices (code {status}).");
            return;
        }

        for (int i = 0; i < types.Length; i++)
        {
            var type = types[i];
            string reportedCount = (ledCounts != null && i < ledCounts.Length) ? ledCounts[i] : "N/A";
            Logger.Info($"[MysticLight] Discovered Device[{i}]: Type = '{type}', Reported LedCount = '{reportedCount}'");

            string[] ledNames = Array.Empty<string>();

            int ledStatus = MysticLightNative.GetLedName(type, out var retrievedNames);
            Logger.Info($"[MysticLight] MLAPI_GetLedName('{type}') -> Status Code: {ledStatus}, Count: {retrievedNames?.Length ?? 0}");

            if (ledStatus == 0 && retrievedNames is not null)
            {
                ledNames = retrievedNames;
            }

            Devices.Add(new MysticLightDevice(type, ledNames));
        }
    }

    public void SaveInitialColor(string type)
    {
        if (!_initialized || _savedColors.ContainsKey(type)) return;

        int status = MysticLightNative.GetLedColor(type, 0, out int r, out int g, out int b);
        if (status == 0)
        {
            _savedColors[type] = (r, g, b);
            Logger.Info($"[MysticLight] Saved initial color for '{type}': RGB({r},{g},{b})");
        }
        else
        {
            Logger.Warn($"[MysticLight] Could not get initial color for '{type}' (code {status}). Defaulting restore to RGB(0,0,0).");
            _savedColors[type] = (0, 0, 0);
        }
    }

    public void SaveInitialColors(IEnumerable<string> deviceTypes)
    {
        foreach (var type in deviceTypes)
        {
            SaveInitialColor(type);
        }
    }

    public void SetLedStyle(string type, int index, string style)
    {
        if (!_initialized) return;

        int status = MysticLightNative.SetLedStyle(type, index, style);
        Logger.Info($"[MysticLight] SetLedStyle(Type: '{type}', Index: {index}, Style: '{style}') -> Status Code: {status}");
    }

    public void SetColor(IEnumerable<string> deviceTypes, Color color)
    {
        if (!_initialized) return;

        foreach (var type in deviceTypes)
        {
            var device = Devices.FirstOrDefault(d => d.Type == type);
            if (device is null) continue;

            if (device.LedNames.Length > 0)
            {
                var ledNames = (string[])device.LedNames.Clone();
                int[] rArray = new int[ledNames.Length];
                int[] gArray = new int[ledNames.Length];
                int[] bArray = new int[ledNames.Length];
                for (int i = 0; i < ledNames.Length; i++)
                {
                    rArray[i] = color.R;
                    gArray[i] = color.G;
                    bArray[i] = color.B;
                }

                int status = MysticLightNative.SetLedColorsSync(type, ref ledNames, rArray, gArray, bArray);
                if (status != 0)
                {
                    Logger.Error($"[MysticLight] SetLedColorsSync failed (Type: '{type}', RGB: {color.R},{color.G},{color.B}) -> Status Code: {status}");
                }
            }
            else
            {
                int status = MysticLightNative.SetLedColor(type, 0, color.R, color.G, color.B);
                if (status != 0)
                {
                    Logger.Error($"[MysticLight] SetLedColor failed (Type: '{type}', Index: 0, RGB: {color.R},{color.G},{color.B}) -> Status Code: {status}");
                }
            }
        }
    }

    public void RestoreOriginalState()
    {
        lock (_shutdownLock)
        {
            if (_restored || !_initialized) return;
            _restored = true;

            try
            {
                foreach (var (type, (r, g, b)) in _savedColors)
                {
                    Logger.Info($"[MysticLight] Restoring original color for '{type}': RGB({r},{g},{b})...");
                    int status = MysticLightNative.SetLedColor(type, 0, r, g, b);
                    Logger.Info($"[MysticLight] Restored color for '{type}' -> Status Code: {status}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[MysticLight] Error restoring original color: {ex.Message}");
            }
            finally
            {
                Release();
            }
        }
    }

    public void Release()
    {
        if (!_initialized) return;

        try
        {
            int status = MysticLightNative.Release();
            Logger.Info($"[MysticLight] Mystic Light SDK released (code {status}).");
        }
        catch (Exception ex)
        {
            Logger.Error($"[MysticLight] Error releasing Mystic Light SDK: {ex.Message}");
        }
        finally
        {
            _initialized = false;
        }
    }

    public void Dispose()
    {
        RestoreOriginalState();
        GC.SuppressFinalize(this);
    }
}
