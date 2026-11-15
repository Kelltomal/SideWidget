using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SideWidget.Core;
using Windows.Foundation;
using Windows.UI;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace SideWidget.App;

public sealed partial class MainWindow
{
    const int VisualizerBandCount = 32;
    const double VisualizerHeight = 52;
    static readonly VisualizerOptions DefaultHeaderVisualizer = new();
    readonly double[] visualizerLevels = new double[VisualizerBandCount];
    readonly double[] visualizerTargets = new double[VisualizerBandCount];
    readonly BezierSegment[] visualizerCurves = new BezierSegment[VisualizerBandCount * 2 - 1];
    readonly ScaleTransform[] visualizerBarScales = new ScaleTransform[VisualizerBandCount];
    readonly Rectangle[] visualizerBars = new Rectangle[VisualizerBandCount];
    Canvas? visualizerCanvas;
    XamlPath? visualizerWave;
    PathFigure? visualizerFigure;
    bool visualizerRendering;
    bool visualizerTargetsDirty;
    bool visualizerGeometryDirty;
    bool visualizerHasLevels;
    double visualizerRenderedAt;
    long visualizerReducedPaintAt;
    double visualizerWidth;
    long visualizerSequence = long.MinValue;
    long visualizerLastFrameAt;
    long visualizerMotionCheckedAt;
    bool visualizerReduced;
    bool visualizerStopped;

    void InitializeAudioVisualizer()
    {
        if (visualizerCanvas is not null) return;
        visualizerCanvas = new Canvas
        {
            Height = VisualizerHeight,
            Margin = new Thickness(-16, 0, -12, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumnSpan(visualizerCanvas, 3);
        visualizerFigure = new PathFigure { IsClosed = true, IsFilled = true };
        for (var i = 0; i < visualizerCurves.Length; i++)
        {
            var curve = new BezierSegment();
            visualizerCurves[i] = curve;
            visualizerFigure.Segments.Add(curve);
        }
        var geometry = new PathGeometry();
        geometry.Figures.Add(visualizerFigure);
        var gradient = new LinearGradientBrush { StartPoint = new Point(.5, 0), EndPoint = new Point(.5, 1) };
        gradient.GradientStops.Add(new GradientStop { Offset = 0, Color = Color.FromArgb(255, 246, 99, 75) });
        gradient.GradientStops.Add(new GradientStop { Offset = .4, Color = Color.FromArgb(255, 224, 62, 111) });
        gradient.GradientStops.Add(new GradientStop { Offset = 1, Color = Color.FromArgb(255, 137, 91, 225) });
        visualizerWave = new XamlPath { Data = geometry, Fill = gradient, IsHitTestVisible = false };
        visualizerCanvas.Children.Add(visualizerWave);
        for (var i = 0; i < VisualizerBandCount; i++)
        {
            var fraction = i / (double)(VisualizerBandCount - 1);
            var scale = new ScaleTransform { CenterY = 18, ScaleY = 0 };
            visualizerBarScales[i] = scale;
            var bar = new Rectangle
            {
                Height = 36,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(255,
                    (byte)(246 + (137 - 246) * fraction),
                    (byte)(99 + (91 - 99) * fraction),
                    (byte)(75 + (225 - 75) * fraction))),
                RenderTransform = scale,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Canvas.SetTop(bar, 8);
            visualizerBars[i] = bar;
            visualizerCanvas.Children.Add(bar);
        }
        // Keep timer tint and every text element above the spectrum. No input surface is added.
        header.Children.Insert(0, visualizerCanvas);
        UpdateAudioVisualizerGeometry();
    }

    void UpdateAudioVisualizerGeometry()
    {
        if (visualizerCanvas is null || visualizerStopped) return;
        var nextWidth = double.IsFinite(header.Width) ? Math.Max(0, header.Width) : Math.Max(0, header.ActualWidth);
        if (nextWidth != visualizerWidth)
        {
            visualizerWidth = nextWidth;
            visualizerCanvas.Width = nextWidth;
            visualizerCanvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, nextWidth, VisualizerHeight) };
            var step = nextWidth / VisualizerBandCount;
            for (var i = 0; i < VisualizerBandCount; i++)
            {
                visualizerBars[i].Width = Math.Max(0, step - 2);
                Canvas.SetLeft(visualizerBars[i], step * i + 1);
            }
        }
        RefreshAudioVisualizerSettings();
    }

    void RefreshAudioVisualizerSettings()
    {
        compactStrip?.ConfigureSpectrum(()=>controller.Spectrum.Latest,controller.Settings.Visualizer??DefaultHeaderVisualizer);
        if (visualizerCanvas is null || visualizerStopped) return;
        var settings = controller.Settings.Visualizer ?? DefaultHeaderVisualizer;
        var brightness = double.IsFinite(settings.Brightness) ? Math.Clamp(settings.Brightness, 0, 1) : .35;
        var visible = !closing && !compactHidden && motion.State == PanelState.Collapsed && settings.Enabled && settings.ShowInHeader && brightness > 0 && visualizerWidth > 0;
        if (!visible)
        {
            ReleaseAudioVisualizerRendering();
            ClearAudioVisualizer();
            return;
        }
        visualizerCanvas.Opacity = AudioVisualizerOpacity(brightness);
        var bars = settings.Style == "bars";
        visualizerWave!.Visibility = bars ? Visibility.Collapsed : Visibility.Visible;
        foreach (var bar in visualizerBars) bar.Visibility = bars ? Visibility.Visible : Visibility.Collapsed;
        visualizerTargetsDirty = true;
        visualizerGeometryDirty = true;
        if (visualizerRendering) return;
        visualizerReduced = ReducedMotion();
        visualizerMotionCheckedAt = watch.ElapsedMilliseconds;
        visualizerSequence = long.MinValue;
        visualizerRenderedAt = watch.Elapsed.TotalSeconds;
        visualizerReducedPaintAt = watch.ElapsedMilliseconds - 100;
        CompositionTarget.Rendering += DrawAudioVisualizer;
        visualizerRendering = true;
        DrawAudioVisualizer(null, null!);
    }

    void DrawAudioVisualizer(object? sender, object args)
    {
        if (visualizerCanvas is null || visualizerStopped) return;
        var settings = controller.Settings.Visualizer ?? DefaultHeaderVisualizer;
        if (closing || compactHidden || motion.State != PanelState.Collapsed || !settings.Enabled || !settings.ShowInHeader || settings.Brightness <= 0)
        {
            ReleaseAudioVisualizerRendering();
            ClearAudioVisualizer();
            return;
        }
        var now = watch.ElapsedMilliseconds;
        if (now - visualizerMotionCheckedAt >= 1000)
        {
            visualizerMotionCheckedAt = now;
            var reduced = ReducedMotion();
            visualizerTargetsDirty |= reduced != visualizerReduced;
            visualizerReduced = reduced;
        }
        var frame = controller.Spectrum.Latest;
        var newFrame = frame.Sequence != visualizerSequence;
        if (newFrame)
        {
            visualizerSequence = frame.Sequence;
            visualizerLastFrameAt = now;
        }
        // A stalled capture must not leave its last loud frame painted indefinitely.
        if (!frame.Available || !double.IsFinite(frame.Rms) || frame.Bands.Length == 0 || now - visualizerLastFrameAt > 500)
        {
            ClearAudioVisualizer();
            visualizerTargetsDirty = true;
            visualizerRenderedAt = watch.Elapsed.TotalSeconds;
            return;
        }
        var silent = frame.Rms <= .00001;
        if (newFrame || visualizerTargetsDirty)
        {
            var sensitivity = double.IsFinite(settings.Sensitivity) ? Math.Clamp(settings.Sensitivity, .25, 4) : 1;
            for (var i = 0; i < VisualizerBandCount; i++)
            {
                var source = frame.Bands[Math.Min(i, frame.Bands.Length - 1)];
                var target = !silent && float.IsFinite(source) ? Math.Clamp(source * sensitivity, 0, 1) : 0;
                visualizerTargets[i] = target * (visualizerReduced ? .45 : 1);
            }
            visualizerTargetsDirty = false;
        }
        // Reduced motion uses discrete, gentler measurements at 10 Hz, without per-display interpolation.
        if (visualizerReduced && now - visualizerReducedPaintAt < 100) return;
        visualizerReducedPaintAt = now;
        var renderTime = watch.Elapsed.TotalSeconds;
        var delta = Math.Clamp(renderTime - visualizerRenderedAt, 0, .1);
        visualizerRenderedAt = renderTime;
        var changed = visualizerGeometryDirty;
        var maximum = 0d;
        for (var i = 0; i < VisualizerBandCount; i++)
        {
            var previous = visualizerLevels[i];
            var target = visualizerTargets[i];
            // Measured targets update at capture rate; displayed envelope follows at display rate.
            // Elapsed-time response remains identical on 60/120/144 Hz displays and after UI stalls.
            var response = target > previous ? .030 : silent ? .040 : .080;
            var next = visualizerReduced ? target : previous + (target - previous) * (1 - Math.Exp(-delta / response));
            if (Math.Abs(target - next) < .0005) next = target;
            visualizerLevels[i] = next;
            changed |= next != previous;
            maximum = Math.Max(maximum, visualizerLevels[i]);
        }
        if (maximum < .001 && (silent || maximum == 0))
        {
            ClearAudioVisualizer();
            return;
        }
        // Silence and settled data do not mutate XAML geometry on every display callback.
        var opacity = AudioVisualizerOpacity(settings.Brightness);
        if (visualizerCanvas.Opacity != opacity) visualizerCanvas.Opacity = opacity;
        if (!changed) return;
        visualizerHasLevels = true;
        visualizerGeometryDirty = false;
        visualizerCanvas.Visibility = Visibility.Visible;
        if (settings.Style == "bars")
        {
            for (var i = 0; i < VisualizerBandCount; i++) visualizerBarScales[i].ScaleY = visualizerLevels[i];
            return;
        }
        var step = visualizerWidth / (VisualizerBandCount - 1);
        visualizerFigure!.StartPoint = AudioVisualizerPoint(0, false, step);
        for (var i = 0; i < VisualizerBandCount - 1; i++)
            SetAudioVisualizerCurve(i, AudioVisualizerPoint(i, false, step), AudioVisualizerPoint(i + 1, false, step));
        var rightTop = AudioVisualizerPoint(VisualizerBandCount - 1, false, step);
        var rightBottom = AudioVisualizerPoint(VisualizerBandCount - 1, true, step);
        SetAudioVisualizerCurve(VisualizerBandCount - 1, rightTop, rightBottom);
        for (var i = VisualizerBandCount - 1; i > 0; i--)
            SetAudioVisualizerCurve(VisualizerBandCount * 2 - 1 - i, AudioVisualizerPoint(i, true, step), AudioVisualizerPoint(i - 1, true, step));
    }

    Point AudioVisualizerPoint(int band, bool bottom, double step)
    {
        // Spatial interpolation only; both contours come directly from measured spectrum bands.
        var level = (visualizerLevels[Math.Max(0, band - 1)] + visualizerLevels[band] * 2 + visualizerLevels[Math.Min(VisualizerBandCount - 1, band + 1)]) / 4;
        var edge = band == 0 || band == VisualizerBandCount - 1 ? .2 : 1;
        return new Point(band * step, VisualizerHeight / 2 + (bottom ? 1 : -1) * 18 * level * edge);
    }

    void SetAudioVisualizerCurve(int index, Point from, Point to)
    {
        var middle = (from.X + to.X) / 2;
        var curve = visualizerCurves[index];
        curve.Point1 = new Point(middle, from.Y);
        curve.Point2 = new Point(middle, to.Y);
        curve.Point3 = to;
    }

    double AudioVisualizerOpacity(double brightness)
    {
        // Preserve secondary header text contrast at maximum user brightness in both themes.
        var value = double.IsFinite(brightness) ? Math.Clamp(brightness, 0, 1) : .35;
        return value * (currentLightTheme == true ? .15 : .35) * (timerRinging ? .45 : 1);
    }

    void ClearAudioVisualizer()
    {
        if (visualizerCanvas is not null && visualizerCanvas.Visibility != Visibility.Collapsed)
            visualizerCanvas.Visibility = Visibility.Collapsed;
        if (!visualizerHasLevels) return;
        Array.Clear(visualizerLevels);
        Array.Clear(visualizerTargets);
        visualizerHasLevels = false;
    }

    void ReleaseAudioVisualizerRendering()
    {
        if (!visualizerRendering) return;
        CompositionTarget.Rendering -= DrawAudioVisualizer;
        visualizerRendering = false;
    }

    void StopAudioVisualizer()
    {
        visualizerStopped = true;
        ReleaseAudioVisualizerRendering();
        ClearAudioVisualizer();
    }
}
