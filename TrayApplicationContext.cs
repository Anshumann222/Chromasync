using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChromaSync;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly MysticLightController _light;
    private readonly AppConfig _config;
    private readonly Action _onExitRequested;
    private readonly Icon _trayIcon;

    public TrayApplicationContext(MysticLightController light, AppConfig config, Action onExitRequested)
    {
        _light = light;
        _config = config;
        _onExitRequested = onExitRequested;

        _trayIcon = LoadAppIcon();

        var contextMenu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("ChromaSync")
        {
            Font = new Font(contextMenu.Font, FontStyle.Bold),
            Enabled = false
        };

        var statusItem = new ToolStripMenuItem($"Devices: {string.Join(", ", _config.SelectedDeviceTypes)}")
        {
            Enabled = false
        };

        var reconfigureItem = new ToolStripMenuItem("Reconfigure Device...", null, OnReconfigureClicked);
        var openLogItem = new ToolStripMenuItem("Open Log File", null, OnOpenLogClicked);
        var exitItem = new ToolStripMenuItem("Exit", null, OnExitClicked);

        contextMenu.Items.Add(titleItem);
        contextMenu.Items.Add(statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(reconfigureItem);
        contextMenu.Items.Add(openLogItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            ContextMenuStrip = contextMenu,
            Text = "ChromaSync — Spotify RGB Sync",
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => OpenReconfigureDialog();

        _notifyIcon.ShowBalloonTip(3000, "ChromaSync", "Running in system tray. Right-click icon for options.", ToolTipIcon.Info);
    }

    private void OnReconfigureClicked(object? sender, EventArgs e)
    {
        OpenReconfigureDialog();
    }

    private void OpenReconfigureDialog()
    {
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
