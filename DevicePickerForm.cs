using System.Drawing;
using System.Windows.Forms;

namespace ChromaSync;

public class DevicePickerForm : Form
{
    private readonly CheckedListBox _deviceList;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;
    private readonly List<MysticLightDevice> _devices;

    public List<string> SelectedTypes { get; private set; } = new();

    public DevicePickerForm(List<MysticLightDevice> devices, List<string> currentSelection)
    {
        _devices = devices;

        Text = "ChromaSync — Select Devices";
        Width = 420;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9.5f);

        var lblInstruction = new Label
        {
            Text = "Select the Mystic Light device(s) for your CPU cooler / fans:",
            Location = new Point(16, 16),
            AutoSize = true
        };

        _deviceList = new CheckedListBox
        {
            Location = new Point(16, 45),
            Width = 370,
            Height = 160,
            CheckOnClick = true
        };

        for (int i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            string displayText = $"[{i}] {d.Type} ({d.LedCount} LED area(s))";
            bool isChecked = currentSelection.Contains(d.Type);
            _deviceList.Items.Add(displayText, isChecked);
        }

        _btnSave = new Button
        {
            Text = "Save Selection",
            Location = new Point(180, 225),
            Width = 115,
            Height = 32,
            DialogResult = DialogResult.None
        };
        _btnSave.Click += OnSaveClicked;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(305, 225),
            Width = 80,
            Height = 32,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(lblInstruction);
        Controls.Add(_deviceList);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var selected = new List<string>();
        for (int i = 0; i < _deviceList.Items.Count; i++)
        {
            if (_deviceList.GetItemChecked(i) && i < _devices.Count)
            {
                selected.Add(_devices[i].Type);
            }
        }

        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Please select at least one device before saving.", "No Device Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedTypes = selected;
        DialogResult = DialogResult.OK;
        Close();
    }
}
