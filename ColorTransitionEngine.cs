using System.Drawing;

namespace ChromaSync;

/// <summary>
/// Eases between colors in CIE Lab space (rather than raw RGB) so a
/// grey-to-blue transition, for example, doesn't pass through a muddy
/// brownish middle the way linear RGB interpolation does.
/// </summary>
public class ColorTransitionEngine
{
    private readonly TimeSpan _duration;
    private readonly double _changeThreshold;

    private (double L, double a, double b) _current;
    private (double L, double a, double b) _target;
    private (double L, double a, double b) _transitionFrom;
    private DateTime _transitionStart;
    private bool _hasColor;

    public ColorTransitionEngine(TimeSpan duration, double changeThreshold = 4.0)
    {
        _duration = duration;
        _changeThreshold = changeThreshold;
    }

    public void SetTarget(Color color)
    {
        var lab = RgbToLab(color);

        if (!_hasColor)
        {
            _current = lab;
            _target = lab;
            _transitionFrom = lab;
            _hasColor = true;
            Console.WriteLine($"[Transition] Initial target set: RGB({color.R},{color.G},{color.B}) [Lab L={lab.L:F1}, a={lab.a:F1}, b={lab.b:F1}]");
            return;
        }

        double distance = LabDistance(lab, _target);
        if (distance < _changeThreshold)
        {
            Console.WriteLine($"[Transition] Ignored color change as noise (Lab dist: {distance:F2} < threshold: {_changeThreshold:F1})");
            return; // treat as sampling noise, not a real color change
        }

        Console.WriteLine($"[Transition] Accepted color change (Lab dist: {distance:F2} >= threshold: {_changeThreshold:F1}) -> RGB({color.R},{color.G},{color.B})");
        _transitionFrom = _current;
        _target = lab;
        _transitionStart = DateTime.UtcNow;
    }

    /// <summary>Advances the transition and returns the color to display right now.</summary>
    public Color Tick()
    {
        if (!_hasColor)
            return Color.Black;

        double t = _duration.TotalMilliseconds <= 0
            ? 1.0
            : Math.Clamp((DateTime.UtcNow - _transitionStart).TotalMilliseconds / _duration.TotalMilliseconds, 0.0, 1.0);

        double eased = EaseInOutCubic(t);

        _current = (
            Lerp(_transitionFrom.L, _target.L, eased),
            Lerp(_transitionFrom.a, _target.a, eased),
            Lerp(_transitionFrom.b, _target.b, eased));

        return LabToRgb(_current);
    }

    private static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double LabDistance((double L, double a, double b) c1, (double L, double a, double b) c2)
    {
        double dl = c1.L - c2.L, da = c1.a - c2.a, db = c1.b - c2.b;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    // --- sRGB (D65) <-> CIE Lab ---

    public static (double L, double a, double b) RgbToLab(Color c)
    {
        double r = InverseGamma(c.R / 255.0);
        double g = InverseGamma(c.G / 255.0);
        double b = InverseGamma(c.B / 255.0);

        double x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047;
        double y = (r * 0.2126 + g * 0.7152 + b * 0.0722) / 1.00000;
        double z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;

        double fx = LabF(x), fy = LabF(y), fz = LabF(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    public static Color LabToRgb((double L, double a, double b) lab)
    {
        double fy = (lab.L + 16) / 116;
        double fx = fy + lab.a / 500;
        double fz = fy - lab.b / 200;

        double x = 0.95047 * LabFInv(fx);
        double y = 1.00000 * LabFInv(fy);
        double z = 1.08883 * LabFInv(fz);

        double r = x * 3.2406 + y * -1.5372 + z * -0.4986;
        double g = x * -0.9689 + y * 1.8758 + z * 0.0415;
        double b = x * 0.0557 + y * -0.2040 + z * 1.0570;

        return Color.FromArgb(ToByte(Gamma(r)), ToByte(Gamma(g)), ToByte(Gamma(b)));
    }

    private static double InverseGamma(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    private static double Gamma(double c) => c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(Math.Clamp(c, 0, 1), 1 / 2.4) - 0.055;
    private static double LabF(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
    private static double LabFInv(double t) => Math.Pow(t, 3) > 0.008856 ? Math.Pow(t, 3) : (t - 16.0 / 116) / 7.787;
    private static byte ToByte(double v) => (byte)Math.Clamp(v * 255.0, 0, 255);
}
