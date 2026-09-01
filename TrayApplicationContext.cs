using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ChromaSync;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly AppConfig _config;
    private readonly Action _onExitRequested;
    private readonly Icon _trayIcon;
    private MysticLightController? _light;
    private ColorTransitionEngine? _engine;

    public TrayApplicationContext(AppConfig config, Action onExitRequested)
    {
        _config = config;
        _onExitRequested = onExitRequested;
        _trayIcon = LoadAppIcon();

        var contextMenu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("ChromaSync")
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold),
            Enabled = false
        };

        _statusItem = new ToolStripMenuItem(GetDevicesStatusText())
        {
            Enabled = false
        };

        var settingsItem = new ToolStripMenuItem("Settings...", null, OnSettingsClicked);
        var reconfigureItem = new ToolStripMenuItem("Reconfigure Device...", null, OnReconfigureClicked);
        var openLogItem = new ToolStripMenuItem("Open Log File", null, OnOpenLogClicked);
        var exitItem = new ToolStripMenuItem("Exit", null, OnExitClicked);

        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(_statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(reconfigureItem);
        contextMenu.Items.Add(openLogItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            ContextMenuStrip = contextMenu,
            Text = "ChromaSync — Spotify RGB Sync",
            Visible = false
        };

        _notifyIcon.DoubleClick += (s, e) => OpenReconfigureDialog();
    }

    public void ShowTray(MysticLightController light, ColorTransitionEngine? engine = null)
    {
        _light = light;
        _engine = engine;
        UpdateDevicesText();
        _notifyIcon.Visible = true;
        _notifyIcon.ShowBalloonTip(3000, "ChromaSync", "Spotify active. Running in system tray.", ToolTipIcon.Info);
    }

    public void HideTray()
    {
        _notifyIcon.Visible = false;
        _light = null;
        _engine = null;
    }

    public void UpdateDevicesText()
    {
        _statusItem.Text = GetDevicesStatusText();
    }

    private string GetDevicesStatusText()
    {
        return _config.SelectedDeviceTypes.Count > 0
            ? $"Devices: {string.Join(", ", _config.SelectedDeviceTypes)}"
            : "Devices: (none)";
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        OpenSettingsDialog();
    }

    private void OpenSettingsDialog()
    {
        using var dialog = new SettingsForm(_config.TransitionDurationMs, _config.ColorChangeThreshold);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _config.TransitionDurationMs = dialog.TransitionDurationMs;
            _config.ColorChangeThreshold = dialog.ColorChangeThreshold;
            _config.Save();

            if (_engine != null)
            {
                _engine.Duration = TimeSpan.FromMilliseconds(_config.TransitionDurationMs);
                _engine.ChangeThreshold = _config.ColorChangeThreshold;
            }

            Logger.Info($"[Config] Applied settings: TransitionDuration={_config.TransitionDurationMs}ms ({(dialog.TransitionDurationMs / 1000.0):0.0}s), ColorChangeThreshold={_config.ColorChangeThreshold:0.0}");
        }
    }

    private void OnReconfigureClicked(object? sender, EventArgs e)
    {
        OpenReconfigureDialog();
    }

    private void OpenReconfigureDialog()
    {
        if (_light == null) return;

        using var dialog = new DevicePickerForm(_light.Devices, _config.SelectedDeviceTypes);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.SelectedTypes.Count > 0)
        {
            _config.SelectedDeviceTypes = dialog.SelectedTypes;
            _config.Save();

            _light.SaveInitialColors(_config.SelectedDeviceTypes);
            foreach (var type in _config.SelectedDeviceTypes)
            {
                _light.SetLedStyle(type, 0, "Steady");
            }

            UpdateDevicesText();
            Logger.Info($"[Config] Reconfigured devices: {string.Join(", ", _config.SelectedDeviceTypes)}");
            _notifyIcon.ShowBalloonTip(2000, "ChromaSync", $"Updated devices: {string.Join(", ", _config.SelectedDeviceTypes)}", ToolTipIcon.Info);
        }
    }

    private void OnOpenLogClicked(object? sender, EventArgs e)
    {
        try
        {
            if (File.Exists(Logger.LogFilePath))
            {
                Process.Start(new ProcessStartInfo(Logger.LogFilePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("No log file found yet.", "ChromaSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log file: {ex.Message}", "ChromaSync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        _onExitRequested();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}
