using System.Diagnostics;
using System.Runtime.InteropServices;
using SideWidget.Core;

namespace SideWidget.App;

internal sealed partial class CompactStripWindow
{
    const nuint SpectrumTimerId = 0x53504543;
    const int SpectrumBandCount = 32;
    const int SpectrumSamples = (SpectrumBandCount - 1) * 4 + 1;
    readonly double[] spectrumTargets = new double[SpectrumBandCount];
    readonly double[] spectrumDisplayed = new double[SpectrumBandCount];
    readonly SpectrumPoint[] spectrumPolygon = new SpectrumPoint[SpectrumSamples * 2];
    readonly SpectrumVertex[] spectrumGradient = new SpectrumVertex[2];
    readonly SpectrumGradientRect[] spectrumGradientRect = [new() { UpperLeft = 0, LowerRight = 1 }];
    Func<AudioSpectrumFrame>? spectrumReader;
    VisualizerOptions spectrumOptions = new();
    long spectrumSequence = long.MinValue, spectrumSeenAt, spectrumTickAt;
    bool spectrumTimerRunning, spectrumDrawing, spectrumDirty, spectrumDisposed, spectrumReduced;
    uint spectrumInterval;

    public void ConfigureSpectrum(Func<AudioSpectrumFrame> reader, VisualizerOptions options)
    {
        if (spectrumDisposed) return;
        spectrumReader = reader;
        if (spectrumOptions != options) { spectrumOptions = options; spectrumDirty = true; }
        RefreshSpectrumAnimation();
    }

    void RefreshSpectrumAnimation()
    {
        if (spectrumDisposed || !visible || spectrumReader is null || !spectrumOptions.Enabled ||
            !spectrumOptions.ShowInHeader || SpectrumBrightness() <= 0)
        {
            StopSpectrumAnimation();
            return;
        }
        var enabled = 1;
        Native.SystemParametersInfo(0x1042, 0, ref enabled, 0);
        var reduced = enabled == 0;
        if (spectrumReduced != reduced) { spectrumReduced = reduced; spectrumDirty = true; }
        var interval = reduced ? 100u : 16u;
        if (spectrumTimerRunning && spectrumInterval == interval) return;
        if (spectrumTimerRunning) KillTimerSpectrum(Handle, SpectrumTimerId);
        spectrumTimerRunning = SetTimerSpectrum(Handle, SpectrumTimerId, interval, 0) != 0;
        spectrumInterval = interval;
        spectrumTickAt = Stopwatch.GetTimestamp();
        if (spectrumTimerRunning) TickSpectrum();
    }

    void StopSpectrumAnimation()
    {
        if (spectrumTimerRunning) KillTimerSpectrum(Handle, SpectrumTimerId);
        spectrumTimerRunning = false;
        ClearSpectrumDrawing();
        spectrumSequence = long.MinValue;
    }

    void ClearSpectrumDrawing()
    {
        Array.Clear(spectrumDisplayed);
        Array.Clear(spectrumTargets);
        if (spectrumDrawing && visible) InvalidateRect(Handle, 0, false);
        spectrumDrawing = false;
        spectrumDirty = true;
    }

    void TickSpectrum()
    {
        if (spectrumDisposed || !visible || spectrumReader is null || !spectrumOptions.Enabled ||
            !spectrumOptions.ShowInHeader || SpectrumBrightness() <= 0)
        {
            StopSpectrumAnimation();
            return;
        }
        AudioSpectrumFrame frame;
        try { frame = spectrumReader(); }
        catch (Exception) { StopSpectrumAnimation(); return; }
        if (frame is null || !frame.Available || frame.Bands is null || frame.Bands.Length == 0 || !double.IsFinite(frame.Rms))
        {
            // Show/Configure refreshes recovery; do not animate an absent source.
            StopSpectrumAnimation();
            return;
        }
        var now = Stopwatch.GetTimestamp();
        var fresh = frame.Sequence != spectrumSequence;
        if (fresh) { spectrumSequence = frame.Sequence; spectrumSeenAt = now; }
        if (Stopwatch.GetElapsedTime(spectrumSeenAt, now).TotalMilliseconds > 500)
        {
            ClearSpectrumDrawing();
            spectrumTickAt = now;
            return;
        }
        var silent = frame.Rms <= .00001;
        if (fresh || spectrumDirty)
        {
            var sensitivity = double.IsFinite(spectrumOptions.Sensitivity) ? Math.Clamp(spectrumOptions.Sensitivity, .25, 4) : 1;
            for (var i = 0; i < SpectrumBandCount; i++)
            {
                var measured = frame.Bands[Math.Min(i, frame.Bands.Length - 1)];
                spectrumTargets[i] = silent || !float.IsFinite(measured) ? 0 :
                    Math.Clamp(measured * sensitivity, 0, 1) * (spectrumReduced ? .45 : 1);
            }
        }
        var delta = Math.Clamp(Stopwatch.GetElapsedTime(spectrumTickAt, now).TotalSeconds, 0, .1);
        spectrumTickAt = now;
        var changed = spectrumDirty;
        var maximum = 0d;
        for (var i = 0; i < SpectrumBandCount; i++)
        {
            var previous = spectrumDisplayed[i];
            var target = spectrumTargets[i];
            var response = target > previous ? .030 : silent ? .040 : .080;
            var next = spectrumReduced ? target : previous + (target - previous) * (1 - Math.Exp(-delta / response));
            if (Math.Abs(target - next) < .0005) next = target;
            spectrumDisplayed[i] = next;
            maximum = Math.Max(maximum, next);
            changed |= next != previous;
        }
        spectrumDirty = false;
        if (maximum < .001 && (silent || maximum == 0))
        {
            if (spectrumDrawing) ClearSpectrumDrawing();
            return;
        }
        if (!changed) return;
        spectrumDrawing = true;
        InvalidateRect(Handle, 0, false);
    }

    void PaintSpectrum(nint dc, Native.Rect bounds)
    {
        if (!spectrumDrawing || spectrumDisposed || !visible) return;
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0) return;
        var saved = SaveDCSpectrum(dc);
        if (saved == 0) return;
        try
        {
            if (!BeginPathSpectrum(dc)) return;
            if (spectrumOptions.Style == "bars")
            {
                var step = width / (double)SpectrumBandCount;
                for (var i = 0; i < SpectrumBandCount; i++)
                {
                    var amplitude = height * .38 * spectrumDisplayed[i];
                    var x = bounds.Left + step * i;
                    RectangleSpectrum(dc, (int)Math.Round(x + scale),
                        (int)Math.Round(bounds.Top + height / 2d - amplitude),
                        (int)Math.Round(x + Math.Max(scale, step - scale)),
                        (int)Math.Round(bounds.Top + height / 2d + amplitude));
                }
            }
            else
            {
                for (var i = 0; i < SpectrumSamples; i++)
                {
                    var position = i / 4d;
                    var band = Math.Min(SpectrumBandCount - 2, (int)position);
                    var t = position - band;
                    var smooth = t * t * (3 - 2 * t);
                    var level = SpectrumLevel(band) * (1 - smooth) + SpectrumLevel(band + 1) * smooth;
                    var x = bounds.Left + width * i / (double)(SpectrumSamples - 1);
                    var amplitude = height * .38 * level;
                    spectrumPolygon[i] = new() { X = (int)Math.Round(x), Y = (int)Math.Round(bounds.Top + height / 2d - amplitude) };
                    spectrumPolygon[spectrumPolygon.Length - i - 1] = new() { X = (int)Math.Round(x), Y = (int)Math.Round(bounds.Top + height / 2d + amplitude) };
                }
                PolygonSpectrum(dc, spectrumPolygon, spectrumPolygon.Length);
            }
            if (!EndPathSpectrum(dc) || !SelectClipPathSpectrum(dc, 1)) return; // RGN_AND
            var opacity = SpectrumBrightness() * (light ? .15 : .35);
            spectrumGradient[0] = SpectrumColor(bounds.Left, bounds.Top, 246, 99, 75, opacity);
            spectrumGradient[1] = SpectrumColor(bounds.Right, bounds.Bottom, 137, 91, 225, opacity);
            GradientFillSpectrum(dc, spectrumGradient, 2, spectrumGradientRect, 1, 1); // GRADIENT_FILL_RECT_V
        }
        finally { RestoreDCSpectrum(dc, saved); }
    }

    double SpectrumLevel(int band)
    {
        var level = (spectrumDisplayed[Math.Max(0, band - 1)] + spectrumDisplayed[band] * 2 + spectrumDisplayed[Math.Min(SpectrumBandCount - 1, band + 1)]) / 4;
        return level * (band == 0 || band == SpectrumBandCount - 1 ? .2 : 1);
    }

    double SpectrumBrightness() => double.IsFinite(spectrumOptions.Brightness) ? Math.Clamp(spectrumOptions.Brightness, 0, 1) : .35;

    SpectrumVertex SpectrumColor(int x, int y, byte r, byte g, byte b, double opacity)
    {
        var backgroundR = light ? 244 : 24;
        var backgroundG = light ? 245 : 26;
        var backgroundB = light ? 246 : 28;
        return new()
        {
            X = x, Y = y,
            Red = (ushort)(Math.Round(backgroundR + (r - backgroundR) * opacity) * 256),
            Green = (ushort)(Math.Round(backgroundG + (g - backgroundG) * opacity) * 256),
            Blue = (ushort)(Math.Round(backgroundB + (b - backgroundB) * opacity) * 256)
        };
    }

    [StructLayout(LayoutKind.Sequential)] struct SpectrumPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SpectrumVertex { public int X, Y; public ushort Red, Green, Blue, Alpha; }
    [StructLayout(LayoutKind.Sequential)] struct SpectrumGradientRect { public uint UpperLeft, LowerRight; }
    [DllImport("user32.dll", EntryPoint = "SetTimer")] static extern nuint SetTimerSpectrum(nint window, nuint id, uint interval, nint callback);
    [DllImport("user32.dll", EntryPoint = "KillTimer")] static extern bool KillTimerSpectrum(nint window, nuint id);
    [DllImport("gdi32.dll", EntryPoint = "SaveDC")] static extern int SaveDCSpectrum(nint dc);
    [DllImport("gdi32.dll", EntryPoint = "RestoreDC")] static extern bool RestoreDCSpectrum(nint dc, int saved);
    [DllImport("gdi32.dll", EntryPoint = "BeginPath")] static extern bool BeginPathSpectrum(nint dc);
    [DllImport("gdi32.dll", EntryPoint = "EndPath")] static extern bool EndPathSpectrum(nint dc);
    [DllImport("gdi32.dll", EntryPoint = "SelectClipPath")] static extern bool SelectClipPathSpectrum(nint dc, int mode);
    [DllImport("gdi32.dll", EntryPoint = "Polygon")] static extern bool PolygonSpectrum(nint dc, [In] SpectrumPoint[] points, int count);
    [DllImport("gdi32.dll", EntryPoint = "Rectangle")] static extern bool RectangleSpectrum(nint dc, int left, int top, int right, int bottom);
    [DllImport("msimg32.dll", EntryPoint = "GradientFill")] static extern bool GradientFillSpectrum(nint dc, [In] SpectrumVertex[] vertices, uint count, [In] SpectrumGradientRect[] mesh, uint meshCount, uint mode);
}
