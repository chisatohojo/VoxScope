using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VoxScope.Analysis;

namespace VoxScope.UI;

public partial class SpectrumView : UserControl
{
    private const double MinimumFrequencyHz = 20d;
    private const double MaximumFrequencyHz = 4000d;
    private const double MinimumDb = -96d;
    private const double MaximumDb = 0d;
    private const double LeftMargin = 56d;
    private const double TopMargin = 18d;
    private const double RightMargin = 16d;
    private const double BottomMargin = 34d;

    private static readonly double[] MajorDbTicks = [-96d, -72d, -48d, -24d, 0d];
    private static readonly double[] MajorFrequencyTicks = [20d, 50d, 100d, 200d, 500d, 1000d, 2000d, 4000d];
    private static readonly Color InputSpectrumColor = Color.FromRgb(59, 130, 246);
    private static readonly Color InputPitchColor = Color.FromRgb(29, 78, 216);
    private static readonly Color OutputSpectrumColor = Color.FromRgb(249, 115, 22);
    private static readonly Color OutputPitchColor = Color.FromRgb(194, 65, 12);
    private static readonly DoubleCollection OutputDashArray = [5d, 3d];

    private float[] _inputSpectrumDb = [];
    private float[] _outputSpectrumDb = [];
    private int _sampleRate;
    private double? _inputPitchHz;
    private double? _outputPitchHz;

    public SpectrumView()
    {
        InitializeComponent();
    }

    public void UpdateFrame(AudioAnalysisFrame frame)
    {
        _inputSpectrumDb = frame.InputSpectrumDb.ToArray();
        _outputSpectrumDb = frame.OutputSpectrumDb.ToArray();
        _sampleRate = frame.SampleRate;
        _inputPitchHz = frame.InputPitchHz;
        _outputPitchHz = frame.OutputPitchHz;
        Redraw();
    }

    public void Clear()
    {
        _inputSpectrumDb = [];
        _outputSpectrumDb = [];
        _sampleRate = 0;
        _inputPitchHz = null;
        _outputPitchHz = null;
        Redraw();
    }

    private void SpectrumView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void Redraw()
    {
        PlotCanvas.Children.Clear();

        var plotRect = GetPlotRect();

        if (plotRect.Width <= 0d || plotRect.Height <= 0d)
        {
            return;
        }

        DrawBackground(plotRect);
        DrawGuideBands(plotRect);
        DrawGrid(plotRect);

        if (_inputSpectrumDb.Length > 0 && _sampleRate > 0)
        {
            DrawSpectrum(plotRect, _inputSpectrumDb, InputSpectrumColor, 1.75d, null);
        }

        if (_outputSpectrumDb.Length > 0 && _sampleRate > 0)
        {
            DrawSpectrum(plotRect, _outputSpectrumDb, OutputSpectrumColor, 1.75d, OutputDashArray);
        }

        if (_inputPitchHz is { } inputPitch
            && inputPitch >= MinimumFrequencyHz
            && inputPitch <= MaximumFrequencyHz)
        {
            DrawPitchMarker(plotRect, inputPitch, InputPitchColor, null);
        }

        if (_outputPitchHz is { } outputPitch
            && outputPitch >= MinimumFrequencyHz
            && outputPitch <= MaximumFrequencyHz)
        {
            DrawPitchMarker(plotRect, outputPitch, OutputPitchColor, OutputDashArray);
        }

        DrawLegend(plotRect);
    }

    private void DrawBackground(Rect plotRect)
    {
        PlotCanvas.Children.Add(
            new Rectangle
            {
                Width = plotRect.Width,
                Height = plotRect.Height,
                Stroke = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                StrokeThickness = 1d,
                Fill = Brushes.White,
            }.WithPosition(plotRect.Left, plotRect.Top));
    }

    private void DrawGuideBands(Rect plotRect)
    {
        DrawGuideBand(
            plotRect,
            VoiceBands.LowPitchGuideStartHz,
            VoiceBands.LowPitchGuideEndHz,
            Color.FromArgb(32, 59, 130, 246));

        DrawGuideBand(
            plotRect,
            VoiceBands.HighPitchGuideStartHz,
            VoiceBands.HighPitchGuideEndHz,
            Color.FromArgb(32, 236, 72, 153));

        DrawGuideBand(
            plotRect,
            VoiceBands.ClarityBandStartHz,
            VoiceBands.ClarityBandEndHz,
            Color.FromArgb(24, 16, 185, 129));
    }

    private void DrawGuideBand(Rect plotRect, double startFrequencyHz, double endFrequencyHz, Color fillColor)
    {
        var left = FrequencyToX(plotRect, startFrequencyHz);
        var right = FrequencyToX(plotRect, endFrequencyHz);

        PlotCanvas.Children.Add(
            new Rectangle
            {
                Width = Math.Max(0d, right - left),
                Height = plotRect.Height,
                Fill = new SolidColorBrush(fillColor),
            }.WithPosition(left, plotRect.Top));
    }

    private void DrawGrid(Rect plotRect)
    {
        foreach (var db in MajorDbTicks)
        {
            var y = DbToY(plotRect, db);

            PlotCanvas.Children.Add(
                new Line
                {
                    X1 = plotRect.Left,
                    Y1 = y,
                    X2 = plotRect.Right,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                    StrokeThickness = 1d,
                });

            var label = new TextBlock
            {
                Text = $"{db:0} dB",
                FontSize = 11,
                Foreground = Brushes.DimGray,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, plotRect.Left - label.DesiredSize.Width - 8d);
            Canvas.SetTop(label, y - (label.DesiredSize.Height / 2d));
            PlotCanvas.Children.Add(label);
        }

        foreach (var frequency in MajorFrequencyTicks)
        {
            var x = FrequencyToX(plotRect, frequency);

            PlotCanvas.Children.Add(
                new Line
                {
                    X1 = x,
                    Y1 = plotRect.Top,
                    X2 = x,
                    Y2 = plotRect.Bottom,
                    Stroke = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                    StrokeThickness = 1d,
                });

            var label = new TextBlock
            {
                Text = FormatFrequencyLabel(frequency),
                FontSize = 11,
                Foreground = Brushes.DimGray,
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - (label.DesiredSize.Width / 2d));
            Canvas.SetTop(label, plotRect.Bottom + 8d);
            PlotCanvas.Children.Add(label);
        }
    }

    private void DrawSpectrum(
        Rect plotRect,
        IReadOnlyList<float> spectrumDb,
        Color color,
        double strokeThickness,
        DoubleCollection? dashArray)
    {
        var points = new PointCollection();
        var nyquist = _sampleRate / 2d;

        for (var index = 0; index < spectrumDb.Count; index++)
        {
            var frequency = index * nyquist / spectrumDb.Count;

            if (frequency < MinimumFrequencyHz || frequency > MaximumFrequencyHz)
            {
                continue;
            }

            points.Add(
                new Point(
                    FrequencyToX(plotRect, frequency),
                    DbToY(plotRect, Math.Clamp(spectrumDb[index], MinimumDb, MaximumDb))));
        }

        PlotCanvas.Children.Add(
            new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = strokeThickness,
                StrokeDashArray = dashArray,
                Points = points,
            });
    }

    private void DrawPitchMarker(Rect plotRect, double pitchHz, Color color, DoubleCollection? dashArray)
    {
        var x = FrequencyToX(plotRect, pitchHz);

        PlotCanvas.Children.Add(
            new Line
            {
                X1 = x,
                Y1 = plotRect.Top,
                X2 = x,
                Y2 = plotRect.Bottom,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2d,
                StrokeDashArray = dashArray,
            });
    }

    private void DrawLegend(Rect plotRect)
    {
        DrawLegendItem("入力FFT", InputSpectrumColor, plotRect.Right - 204d, plotRect.Top + 10d, null);
        DrawLegendItem("出力FFT", OutputSpectrumColor, plotRect.Right - 102d, plotRect.Top + 10d, OutputDashArray);
        DrawLegendItem("入力F0", InputPitchColor, plotRect.Right - 204d, plotRect.Top + 28d, null);
        DrawLegendItem("出力F0", OutputPitchColor, plotRect.Right - 102d, plotRect.Top + 28d, OutputDashArray);
    }

    private void DrawLegendItem(string label, Color color, double left, double top, DoubleCollection? dashArray)
    {
        PlotCanvas.Children.Add(
            new Line
            {
                X1 = left,
                Y1 = top + 8d,
                X2 = left + 18d,
                Y2 = top + 8d,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2d,
                StrokeDashArray = dashArray,
            });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = Brushes.DimGray,
        };

        Canvas.SetLeft(labelBlock, left + 22d);
        Canvas.SetTop(labelBlock, top);
        PlotCanvas.Children.Add(labelBlock);
    }

    private Rect GetPlotRect()
    {
        return new Rect(
            LeftMargin,
            TopMargin,
            Math.Max(0d, PlotCanvas.ActualWidth - LeftMargin - RightMargin),
            Math.Max(0d, PlotCanvas.ActualHeight - TopMargin - BottomMargin));
    }

    private static double FrequencyToX(Rect plotRect, double frequencyHz)
    {
        var minimumLog = Math.Log10(MinimumFrequencyHz);
        var maximumLog = Math.Log10(MaximumFrequencyHz);
        var normalized = (Math.Log10(frequencyHz) - minimumLog) / (maximumLog - minimumLog);
        return plotRect.Left + (normalized * plotRect.Width);
    }

    private static string FormatFrequencyLabel(double frequencyHz)
    {
        return frequencyHz >= 1000d
            ? $"{frequencyHz / 1000d:0.#}k"
            : $"{frequencyHz:0}";
    }

    private static double DbToY(Rect plotRect, double db)
    {
        var normalized = (db - MinimumDb) / (MaximumDb - MinimumDb);
        return plotRect.Bottom - (normalized * plotRect.Height);
    }
}

internal static class CanvasShapeExtensions
{
    public static T WithPosition<T>(this T shape, double left, double top)
        where T : Shape
    {
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        return shape;
    }
}
