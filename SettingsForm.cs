using System.Drawing;
using System.Windows.Forms;

namespace ChromaSync;

public class SettingsForm : Form
{
    private readonly TrackBar _tbTransition;
    private readonly Label _lblTransitionVal;
    private readonly TrackBar _tbThreshold;
    private readonly Label _lblThresholdVal;
    private readonly Button _btnSave;
    private readonly Button _btnCancel;

    public int TransitionDurationMs { get; private set; }
    public double ColorChangeThreshold { get; private set; }

    public SettingsForm(int currentTransitionDurationMs, double currentColorChangeThreshold)
    {
        TransitionDurationMs = currentTransitionDurationMs;
        ColorChangeThreshold = currentColorChangeThreshold;

        Text = "ChromaSync — Settings";
        Width = 420;
        Height = 270;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9.5f);

        // --- Transition Speed Control ---
        var lblTransition = new Label
        {
            Text = "Transition Speed:",
            Location = new Point(20, 18),
            AutoSize = true
        };

        _tbTransition = new TrackBar
        {
            Location = new Point(20, 42),
            Width = 300,
            Height = 45,
            Minimum = 500,
            Maximum = 6000,
            SmallChange = 100,
            LargeChange = 500,
            TickFrequency = 500,
            Value = Math.Clamp(currentTransitionDurationMs, 500, 6000)
        };

        _lblTransitionVal = new Label
        {
            Location = new Point(330, 44),
            Width = 60,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _tbTransition.Scroll += (s, e) => UpdateTransitionLabel();
        _tbTransition.ValueChanged += (s, e) => UpdateTransitionLabel();
        UpdateTransitionLabel();

        // --- Noise Threshold Control ---
        var lblThreshold = new Label
        {
            Text = "Noise Threshold (Lab \u0394E):",
            Location = new Point(20, 95),
            AutoSize = true
        };

        int thresholdSliderVal = (int)Math.Round(Math.Clamp(currentColorChangeThreshold, 0.0, 20.0) * 10.0);

        _tbThreshold = new TrackBar
        {
            Location = new Point(20, 119),
            Width = 300,
            Height = 45,
            Minimum = 0,
            Maximum = 200,
            SmallChange = 5,
            LargeChange = 20,
            TickFrequency = 20,
            Value = Math.Clamp(thresholdSliderVal, 0, 200)
        };

        _lblThresholdVal = new Label
        {
            Location = new Point(330, 121),
            Width = 60,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _tbThreshold.Scroll += (s, e) => UpdateThresholdLabel();
        _tbThreshold.ValueChanged += (s, e) => UpdateThresholdLabel();
        UpdateThresholdLabel();

        // --- Buttons ---
        _btnSave = new Button
        {
            Text = "Save",
            Location = new Point(215, 180),
            Width = 85,
            Height = 32,
            DialogResult = DialogResult.None
        };
        _btnSave.Click += OnSaveClicked;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(310, 180),
            Width = 80,
            Height = 32,
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(lblTransition);
        Controls.Add(_tbTransition);
        Controls.Add(_lblTransitionVal);
        Controls.Add(lblThreshold);
        Controls.Add(_tbThreshold);
        Controls.Add(_lblThresholdVal);
        Controls.Add(_btnSave);
        Controls.Add(_btnCancel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void UpdateTransitionLabel()
    {
        double seconds = _tbTransition.Value / 1000.0;
        _lblTransitionVal.Text = $"{seconds:0.0}s";
    }

    private void UpdateThresholdLabel()
    {
        double threshold = _tbThreshold.Value / 10.0;
        _lblThresholdVal.Text = $"{threshold:0.0}";
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        TransitionDurationMs = _tbTransition.Value;
        ColorChangeThreshold = _tbThreshold.Value / 10.0;
        DialogResult = DialogResult.OK;
        Close();
    }
}
