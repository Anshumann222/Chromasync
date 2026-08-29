using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace ChromaSync;

/// <summary>
/// Captures Spotify's own rendered pixels (not the album art file) and reads
/// the ambient background color directly off them. This sidesteps having to
/// reverse-engineer Spotify's color-extraction algorithm: whatever grey/blue/
/// whatever Spotify is actually showing around the art is exactly what gets
/// sampled.
///
/// Note: Spotify only renders that ambient gradient in the expanded
/// "Now Playing" view (the one with the big blurred background behind the
/// centered album art / lyrics). The compact mini-player and the library
/// view don't show it — keep that view open for this to pick up real colors.
/// </summary>
public class SpotifyAmbientColorDetector
{
    public bool TryFindSpotifyWindow(out IntPtr hWnd)
    {
        IntPtr found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out int pid);

            try
            {
                using var proc = Process.GetProcessById(pid);
                if (!string.Equals(proc.ProcessName, "Spotify", StringComparison.OrdinalIgnoreCase))
                    return true; // not Spotify, keep looking

                if (!NativeMethods.IsWindowVisible(hwnd))
                    return true;

                if (NativeMethods.GetWindowTextLength(hwnd) == 0)
                    return true; // skip Spotify's titleless helper/child windows

                NativeMethods.GetClientRect(hwnd, out var rect);
                if (rect.Width < 300 || rect.Height < 300)
                    return true; // skip tooltips / tiny popups

                found = hwnd;
                return false; // stop enumerating, this is the main window
            }
            catch
            {
                return true;
            }
        }, IntPtr.Zero);

        hWnd = found;
        return found != IntPtr.Zero;
    }

    public Color? CaptureAmbientColor(IntPtr hWnd)
    {
        if (!NativeMethods.GetClientRect(hWnd, out var rect))
            return null;

        int width = rect.Width;
        int height = rect.Height;
        if (width <= 0 || height <= 0)
            return null;

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var gfx = Graphics.FromImage(bmp))
        {
            IntPtr hdc = gfx.GetHdc();
            bool ok;
            try
            {
                ok = NativeMethods.PrintWindow(hWnd, hdc,
                    NativeMethods.PW_CLIENTONLY | NativeMethods.PW_RENDERFULLCONTENT);
            }
            finally
            {
                gfx.ReleaseHdc(hdc);
            }

            if (!ok)
                return null;
        }

        return SampleBackgroundColor(bmp);
    }

    /// <summary>
    /// Samples small patches near the four corners of the window — the album
    /// art and track text are always centered in the "Now Playing" view, so
    /// the corners are reliably pure background regardless of window size.
    /// Returns the per-channel median across all sampled pixels, which is
    /// robust against a stray icon or scrollbar sliver skewing a plain average.
    /// </summary>
    private static Color SampleBackgroundColor(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        int patch = Math.Clamp(Math.Min(w, h) / 12, 16, 60);
        int inset = Math.Max(8, patch / 2);

        var corners = new (int x, int y)[]
        {
            (inset, inset),
            (w - inset - patch, inset),
            (inset, h - inset - patch),
            (w - inset - patch, h - inset - patch),
        };

        var reds = new List<byte>();
        var greens = new List<byte>();
        var blues = new List<byte>();

        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                foreach (var (sx, sy) in corners)
                {
                    for (int y = sy; y < sy + patch; y++)
                    {
                        byte* row = (byte*)data.Scan0 + y * data.Stride;
                        for (int x = sx; x < sx + patch; x++)
                        {
                            int i = x * 4;
                            blues.Add(row[i + 0]);
                            greens.Add(row[i + 1]);
                            reds.Add(row[i + 2]);
                        }
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        return Color.FromArgb(Median(reds), Median(greens), Median(blues));
    }

    private static byte Median(List<byte> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }
}
