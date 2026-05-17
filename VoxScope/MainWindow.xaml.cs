using System.ComponentModel;
using System.Windows;
using VoxScope.Analysis;
using VoxScope.Audio;

namespace VoxScope;

public partial class MainWindow : Window
{
    private readonly DeviceManager _deviceManager = new();
    private readonly AudioEngine _audioEngine;
    private bool _effectControlsReady;

    public MainWindow()
    {
        _audioEngine = new AudioEngine(_deviceManager);
        _audioEngine.Analyzer.AnalysisUpdated += OnAnalysisUpdated;
        InitializeComponent();
        _effectControlsReady = true;
        SyncEffectsFromControls();
        ResetAnalysisUi();
        LoadDevices();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _audioEngine.Analyzer.AnalysisUpdated -= OnAnalysisUpdated;
        _audioEngine.Dispose();
        base.OnClosing(e);
    }

    private void LoadDevices()
    {
        var selectedInputDeviceNumber = (InputDeviceComboBox.SelectedItem as AudioDevice)?.DeviceNumber;
        var selectedOutputDeviceNumber = (OutputDeviceComboBox.SelectedItem as AudioDevice)?.DeviceNumber;

        var inputDevices = _deviceManager.GetInputDevices();
        var outputDevices = _deviceManager.GetOutputDevices();

        InputDeviceComboBox.ItemsSource = inputDevices;
        OutputDeviceComboBox.ItemsSource = outputDevices;

        InputDeviceComboBox.SelectedItem = inputDevices.FirstOrDefault(device => device.DeviceNumber == selectedInputDeviceNumber)
            ?? inputDevices.FirstOrDefault();

        OutputDeviceComboBox.SelectedItem = outputDevices.FirstOrDefault(device => device.DeviceNumber == selectedOutputDeviceNumber)
            ?? outputDevices.FirstOrDefault();

        UpdateUiState();
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_audioEngine.IsRunning)
        {
            _audioEngine.Stop();
            ResetAnalysisUi();
            UpdateUiState();
            return;
        }

        if (InputDeviceComboBox.SelectedItem is not AudioDevice inputDevice
            || OutputDeviceComboBox.SelectedItem is not AudioDevice outputDevice)
        {
            UpdateUiState();
            return;
        }

        try
        {
            _audioEngine.Start(inputDevice, outputDevice);
            UpdateUiState();
        }
        catch (Exception exception)
        {
            _audioEngine.Stop();
            ResetAnalysisUi();
            StatusTextBlock.Text = "開始できませんでした";
            FormatTextBlock.Text = exception.Message;

            MessageBox.Show(
                this,
                exception.Message,
                "VoxScope",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDevices();
    }

    private void PitchShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PitchShift.Semitones = (float)PitchShiftSlider.Value;
        PitchShiftValueTextBlock.Text = $"{PitchShiftSlider.Value:+0;-0;0} st";
    }

    private void OutputGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Gain.GainDb = (float)OutputGainSlider.Value;
        OutputGainValueTextBlock.Text = $"{OutputGainSlider.Value:+0;-0;0} dB";
    }

    private void NoiseGateCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.NoiseGate.Enabled = NoiseGateCheckBox.IsChecked == true;
        NoiseGateStateTextBlock.Text = _audioEngine.Effects.NoiseGate.Enabled ? "有効" : "無効";
    }

    private void NoiseGateThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.NoiseGate.ThresholdDb = (float)NoiseGateThresholdSlider.Value;
        NoiseGateThresholdValueTextBlock.Text = $"{NoiseGateThresholdSlider.Value:0} dB";
    }

    private void EqLowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Eq.LowGainDb = (float)EqLowSlider.Value;
        EqLowValueTextBlock.Text = $"{EqLowSlider.Value:+0;-0;0} dB";
    }

    private void EqMidSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Eq.MidGainDb = (float)EqMidSlider.Value;
        EqMidValueTextBlock.Text = $"{EqMidSlider.Value:+0;-0;0} dB";
    }

    private void EqHighSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Eq.HighGainDb = (float)EqHighSlider.Value;
        EqHighValueTextBlock.Text = $"{EqHighSlider.Value:+0;-0;0} dB";
    }

    private void SyncEffectsFromControls()
    {
        PitchShiftSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, PitchShiftSlider.Value));
        OutputGainSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, OutputGainSlider.Value));
        NoiseGateCheckBox_Checked(this, new RoutedEventArgs());
        NoiseGateThresholdSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, NoiseGateThresholdSlider.Value));
        EqLowSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqLowSlider.Value));
        EqMidSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqMidSlider.Value));
        EqHighSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqHighSlider.Value));
    }

    private void OnAnalysisUpdated(AudioAnalysisFrame frame)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_audioEngine.IsRunning)
            {
                return;
            }

            SpectrumView.UpdateFrame(frame);
            InputPitchTextBlock.Text = FormatPitch(frame.InputPitchHz);
            InputAveragePitchTextBlock.Text = FormatPitch(frame.InputAveragePitchHz);
            OutputPitchTextBlock.Text = FormatPitch(frame.OutputPitchHz);
            OutputAveragePitchTextBlock.Text = FormatPitch(frame.OutputAveragePitchHz);
            GateOpenTextBlock.Text = frame.GateOpen ? "開いています" : "閉じています";
            InputLevelMeter.UpdateLevels(frame.InputRmsDb, frame.InputPeakDb);
            OutputLevelMeter.UpdateLevels(frame.OutputRmsDb, frame.OutputPeakDb);
        });
    }

    private void ResetAnalysisUi()
    {
        SpectrumView.Clear();
        InputPitchTextBlock.Text = "--";
        InputAveragePitchTextBlock.Text = "--";
        OutputPitchTextBlock.Text = "--";
        OutputAveragePitchTextBlock.Text = "--";
        GateOpenTextBlock.Text = "--";
        InputLevelMeter.Reset();
        OutputLevelMeter.Reset();
    }

    private void UpdateUiState()
    {
        var hasInputDevice = InputDeviceComboBox.SelectedItem is AudioDevice;
        var hasOutputDevice = OutputDeviceComboBox.SelectedItem is AudioDevice;
        var isRunning = _audioEngine.IsRunning;

        InputDeviceComboBox.IsEnabled = !isRunning;
        OutputDeviceComboBox.IsEnabled = !isRunning;
        RefreshDevicesButton.IsEnabled = !isRunning;
        StartStopButton.IsEnabled = isRunning || (hasInputDevice && hasOutputDevice);
        StartStopButton.Content = isRunning ? "Stop" : "Start";

        if (isRunning && _audioEngine.CurrentWaveFormat is { } waveFormat)
        {
            StatusTextBlock.Text = "再生中";
            FormatTextBlock.Text = $"{waveFormat.SampleRate / 1000.0:0.#} kHz / {waveFormat.BitsPerSample}-bit / {GetChannelLabel(waveFormat.Channels)}";
            return;
        }

        if (!hasInputDevice || !hasOutputDevice)
        {
            StatusTextBlock.Text = "利用できる入出力デバイスが見つかりません";
            FormatTextBlock.Text = "-";
            return;
        }

        StatusTextBlock.Text = "待機中";
        FormatTextBlock.Text = "選択した入出力をそのまま接続します";
    }

    private static string FormatPitch(double? pitchHz)
    {
        return pitchHz is { } value ? $"{value:0.0} Hz" : "--";
    }

    private static string GetChannelLabel(int channels)
    {
        return channels switch
        {
            1 => "mono",
            2 => "stereo",
            _ => $"{channels} ch",
        };
    }
}
