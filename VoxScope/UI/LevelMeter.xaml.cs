using System.Windows.Controls;

namespace VoxScope.UI;

public partial class LevelMeter : UserControl
{
    public LevelMeter()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => TitleTextBlock.Text;
        set => TitleTextBlock.Text = value;
    }

    public void UpdateLevels(double rmsDb, double peakDb)
    {
        RmsProgressBar.Value = ToMeterValue(rmsDb);
        PeakProgressBar.Value = ToMeterValue(peakDb);
        RmsValueTextBlock.Text = $"{rmsDb:0} dB";
        PeakValueTextBlock.Text = $"{peakDb:0} dB";
    }

    public void Reset()
    {
        RmsProgressBar.Value = 0d;
        PeakProgressBar.Value = 0d;
        RmsValueTextBlock.Text = "--";
        PeakValueTextBlock.Text = "--";
    }

    private static double ToMeterValue(double db)
    {
        return Math.Clamp((db + 60d) / 60d, 0d, 1d);
    }
}
