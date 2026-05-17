using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using VoxScope.Analysis;
using VoxScope.Audio;
using VoxScope.Presets;
using VoxScope.Settings;

namespace VoxScope;

public partial class MainWindow : Window
{
    private readonly DeviceManager _deviceManager = new();
    private readonly AudioEngine _audioEngine;
    private readonly PresetStore _presetStore = new();
    private readonly AppSettingsStore _settingsStore = new();
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private readonly DispatcherTimer _telemetryTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1),
    };
    private List<Preset> _presets = [];
    private AppSettings _appSettings = AppSettings.Default;
    private bool _effectControlsReady;
    private DateTime _lastTelemetrySampleUtc;
    private TimeSpan _lastProcessorTime;

    public MainWindow()
    {
        _audioEngine = new AudioEngine(_deviceManager);
        _audioEngine.Analyzer.AnalysisUpdated += OnAnalysisUpdated;
        InitializeComponent();
        _lastTelemetrySampleUtc = DateTime.UtcNow;
        _lastProcessorTime = _currentProcess.TotalProcessorTime;
        _telemetryTimer.Tick += TelemetryTimer_Tick;
        _telemetryTimer.Start();
        LoadPresets();
        LoadAppSettings();
        _effectControlsReady = true;
        SyncEffectsFromControls();
        ResetAnalysisUi();
        ResetTelemetryUi();
        LoadDevices();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveAppSettings();
        _telemetryTimer.Stop();
        _telemetryTimer.Tick -= TelemetryTimer_Tick;
        _audioEngine.Analyzer.AnalysisUpdated -= OnAnalysisUpdated;
        _audioEngine.Dispose();
        base.OnClosing(e);
    }

    private void LoadDevices()
    {
        var selectedInputDevice = InputDeviceComboBox.SelectedItem as AudioDevice;
        var selectedOutputDevice = OutputDeviceComboBox.SelectedItem as AudioDevice;
        var selectedInputDeviceNumber = selectedInputDevice?.DeviceNumber ?? _appSettings.InputDeviceNumber;
        var selectedInputDeviceName = selectedInputDevice?.Name ?? _appSettings.InputDeviceName;
        var selectedOutputDeviceNumber = selectedOutputDevice?.DeviceNumber ?? _appSettings.OutputDeviceNumber;
        var selectedOutputDeviceName = selectedOutputDevice?.Name ?? _appSettings.OutputDeviceName;

        var inputDevices = _deviceManager.GetInputDevices();
        var outputDevices = _deviceManager.GetOutputDevices();

        InputDeviceComboBox.ItemsSource = inputDevices;
        OutputDeviceComboBox.ItemsSource = outputDevices;

        InputDeviceComboBox.SelectedItem = inputDevices.FirstOrDefault(device => device.DeviceNumber == selectedInputDeviceNumber)
            ?? inputDevices.FirstOrDefault(device => string.Equals(device.Name, selectedInputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? inputDevices.FirstOrDefault();

        OutputDeviceComboBox.SelectedItem = outputDevices.FirstOrDefault(device => device.DeviceNumber == selectedOutputDeviceNumber)
            ?? outputDevices.FirstOrDefault(device => string.Equals(device.Name, selectedOutputDeviceName, StringComparison.OrdinalIgnoreCase))
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

    private void TelemetryTimer_Tick(object? sender, EventArgs e)
    {
        UpdateRuntimeMetrics();
    }

    private void PitchShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PitchShift.TargetSemitones = (float)PitchShiftSlider.Value;
        PitchShiftValueTextBlock.Text = $"{PitchShiftSlider.Value:+0;-0;0} st";
    }

    private void PitchStageModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PitchShift.RequestedStageCount = Math.Max(0, PitchStageModeComboBox.SelectedIndex);
        UpdatePitchStageCountText();
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

    private void PostPitchCorrectionEqCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchCorrectionEq.Enabled = PostPitchCorrectionEqCheckBox.IsChecked == true;
        PostPitchCorrectionEqStateTextBlock.Text = _audioEngine.Effects.PostPitchCorrectionEq.Enabled ? "有効" : "無効";
    }

    private void PostPitchCorrectionEqStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchCorrectionEq.Strength = (float)(PostPitchCorrectionEqStrengthSlider.Value / 100d);
        PostPitchCorrectionEqStrengthValueTextBlock.Text = $"{PostPitchCorrectionEqStrengthSlider.Value:0}%";
    }

    private void HighBandArtifactReducerCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.HighBandArtifactReducer.Enabled = HighBandArtifactReducerCheckBox.IsChecked == true;
        HighBandArtifactReducerStateTextBlock.Text = _audioEngine.Effects.HighBandArtifactReducer.Enabled ? "有効" : "無効";
    }

    private void HighBandArtifactReducerMaxReductionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.HighBandArtifactReducer.MaxReductionDb = (float)HighBandArtifactReducerMaxReductionSlider.Value;
        HighBandArtifactReducerMaxReductionValueTextBlock.Text = $"-{HighBandArtifactReducerMaxReductionSlider.Value:0.#} dB";
    }

    private void PostPitchDeEsserCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchDeEsser.Enabled = PostPitchDeEsserCheckBox.IsChecked == true;
        PostPitchDeEsserStateTextBlock.Text = _audioEngine.Effects.PostPitchDeEsser.Enabled ? "有効" : "無効";
    }

    private void PostPitchDeEsserMaxReductionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchDeEsser.MaxReductionDb = (float)PostPitchDeEsserMaxReductionSlider.Value;
        PostPitchDeEsserMaxReductionValueTextBlock.Text = $"-{PostPitchDeEsserMaxReductionSlider.Value:0.#} dB";
    }

    private void PostPitchSmoothingCompressorCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchSmoothingCompressor.Enabled = PostPitchSmoothingCompressorCheckBox.IsChecked == true;
        PostPitchSmoothingCompressorStateTextBlock.Text = _audioEngine.Effects.PostPitchSmoothingCompressor.Enabled ? "有効" : "無効";
    }

    private void PostPitchSmoothingCompressorStrengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.PostPitchSmoothingCompressor.Strength = (float)(PostPitchSmoothingCompressorStrengthSlider.Value / 100d);
        PostPitchSmoothingCompressorStrengthValueTextBlock.Text = $"{PostPitchSmoothingCompressorStrengthSlider.Value:0}%";
    }

    private void FormantShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.FormantShift.Semitones = (float)FormantShiftSlider.Value;
        FormantShiftValueTextBlock.Text = $"{FormantShiftSlider.Value:+0;-0;0} st";
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

    private void CompressorCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Compressor.Enabled = CompressorCheckBox.IsChecked == true;
        CompressorStateTextBlock.Text = _audioEngine.Effects.Compressor.Enabled ? "有効" : "無効";
    }

    private void CompressorThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Compressor.ThresholdDb = (float)CompressorThresholdSlider.Value;
        CompressorThresholdValueTextBlock.Text = $"{CompressorThresholdSlider.Value:0} dB";
    }

    private void CompressorRatioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Compressor.Ratio = (float)CompressorRatioSlider.Value;
        CompressorRatioValueTextBlock.Text = $"{CompressorRatioSlider.Value:0.#}:1";
    }

    private void CompressorMakeupGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_effectControlsReady)
        {
            return;
        }

        _audioEngine.Effects.Compressor.MakeupGainDb = (float)CompressorMakeupGainSlider.Value;
        CompressorMakeupGainValueTextBlock.Text = $"{CompressorMakeupGainSlider.Value:+0;-0;0} dB";
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
        _audioEngine.Effects.PitchShift.RequestedStageCount = Math.Max(0, PitchStageModeComboBox.SelectedIndex);
        UpdatePitchStageCountText();
        FormantShiftSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, FormantShiftSlider.Value));
        HighBandArtifactReducerCheckBox_Checked(this, new RoutedEventArgs());
        HighBandArtifactReducerMaxReductionSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, HighBandArtifactReducerMaxReductionSlider.Value));
        PostPitchCorrectionEqCheckBox_Checked(this, new RoutedEventArgs());
        PostPitchCorrectionEqStrengthSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, PostPitchCorrectionEqStrengthSlider.Value));
        PostPitchDeEsserCheckBox_Checked(this, new RoutedEventArgs());
        PostPitchDeEsserMaxReductionSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, PostPitchDeEsserMaxReductionSlider.Value));
        PostPitchSmoothingCompressorCheckBox_Checked(this, new RoutedEventArgs());
        PostPitchSmoothingCompressorStrengthSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, PostPitchSmoothingCompressorStrengthSlider.Value));
        OutputGainSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, OutputGainSlider.Value));
        NoiseGateCheckBox_Checked(this, new RoutedEventArgs());
        NoiseGateThresholdSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, NoiseGateThresholdSlider.Value));
        CompressorCheckBox_Checked(this, new RoutedEventArgs());
        CompressorThresholdSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, CompressorThresholdSlider.Value));
        CompressorRatioSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, CompressorRatioSlider.Value));
        CompressorMakeupGainSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, CompressorMakeupGainSlider.Value));
        EqLowSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqLowSlider.Value));
        EqMidSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqMidSlider.Value));
        EqHighSlider_ValueChanged(this, new RoutedPropertyChangedEventArgs<double>(0, EqHighSlider.Value));
    }

    private void PresetComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is Preset preset)
        {
            PresetNameTextBox.Text = preset.Name;
        }
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var presetName = PresetNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(presetName))
        {
            StatusTextBlock.Text = "プリセット名を入力してください";
            return;
        }

        var preset = CapturePreset(presetName);
        var existingIndex = _presets.FindIndex(existingPreset =>
            string.Equals(existingPreset.Name, presetName, StringComparison.CurrentCultureIgnoreCase));

        if (existingIndex >= 0)
        {
            _presets[existingIndex] = preset;
        }
        else
        {
            _presets.Add(preset);
        }

        SavePresets();
        RefreshPresetItems(preset);
        StatusTextBlock.Text = $"プリセットを保存しました: {preset.Name}";
    }

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not Preset preset)
        {
            StatusTextBlock.Text = "読み込むプリセットを選んでください";
            return;
        }

        ApplyPreset(preset);
        StatusTextBlock.Text = $"プリセットを読み込みました: {preset.Name}";
    }

    private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not Preset preset)
        {
            StatusTextBlock.Text = "削除するプリセットを選んでください";
            return;
        }

        _presets.Remove(preset);
        SavePresets();
        RefreshPresetItems(null);
        PresetNameTextBox.Text = string.Empty;
        StatusTextBlock.Text = $"プリセットを削除しました: {preset.Name}";
    }

    private void ResetPresetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyPreset(Preset.Default);
        PresetComboBox.SelectedItem = null;
        PresetNameTextBox.Text = string.Empty;
        StatusTextBlock.Text = "設定を初期化しました";
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
            UpdatePitchStageCountText();
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
            UpdateRuntimeMetrics();
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
        UpdateRuntimeMetrics();
    }

    private void ResetTelemetryUi()
    {
        LatencyTextBlock.Text = "--";
        CpuUsageTextBlock.Text = "--";
    }

    private void UpdateRuntimeMetrics()
    {
        var now = DateTime.UtcNow;
        var processorTime = _currentProcess.TotalProcessorTime;
        var elapsedMilliseconds = (now - _lastTelemetrySampleUtc).TotalMilliseconds;
        var processorMilliseconds = (processorTime - _lastProcessorTime).TotalMilliseconds;
        var cpuUsage = elapsedMilliseconds <= 0d
            ? 0d
            : processorMilliseconds / (elapsedMilliseconds * Environment.ProcessorCount) * 100d;

        _lastTelemetrySampleUtc = now;
        _lastProcessorTime = processorTime;
        LatencyTextBlock.Text = _audioEngine.IsRunning
            ? $"{_audioEngine.EstimatedLatencyMilliseconds} ms"
            : "--";
        CpuUsageTextBlock.Text = $"{Math.Clamp(cpuUsage, 0d, 999d):0.#}%";
    }

    private void LoadPresets()
    {
        _presets = _presetStore.Load().ToList();
        RefreshPresetItems(null);
    }

    private void LoadAppSettings()
    {
        _appSettings = _settingsStore.Load() ?? AppSettings.Default;
        ApplyPreset(_appSettings.CurrentSettings);
        EffectTabControl.SelectedIndex = Math.Clamp(_appSettings.SelectedEffectTabIndex, 0, EffectTabControl.Items.Count - 1);
    }

    private void SavePresets()
    {
        _presetStore.Save(_presets);
    }

    private void SaveAppSettings()
    {
        var inputDevice = InputDeviceComboBox.SelectedItem as AudioDevice;
        var outputDevice = OutputDeviceComboBox.SelectedItem as AudioDevice;

        _appSettings = new AppSettings(
            inputDevice?.DeviceNumber,
            inputDevice?.Name,
            outputDevice?.DeviceNumber,
            outputDevice?.Name,
            EffectTabControl.SelectedIndex,
            CapturePreset("Last Session"));

        _settingsStore.Save(_appSettings);
    }

    private void RefreshPresetItems(Preset? selectedPreset)
    {
        PresetComboBox.ItemsSource = null;
        PresetComboBox.ItemsSource = _presets
            .OrderBy(preset => preset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        PresetComboBox.SelectedItem = selectedPreset is null
            ? null
            : _presets.FirstOrDefault(preset =>
                string.Equals(preset.Name, selectedPreset.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    private Preset CapturePreset(string name)
    {
        return new Preset(
            name,
            (float)PitchShiftSlider.Value,
            PitchStageModeComboBox.SelectedIndex,
            (float)FormantShiftSlider.Value,
            HighBandArtifactReducerCheckBox.IsChecked == true,
            (float)HighBandArtifactReducerMaxReductionSlider.Value,
            PostPitchCorrectionEqCheckBox.IsChecked == true,
            (float)(PostPitchCorrectionEqStrengthSlider.Value / 100d),
            PostPitchDeEsserCheckBox.IsChecked == true,
            (float)PostPitchDeEsserMaxReductionSlider.Value,
            PostPitchSmoothingCompressorCheckBox.IsChecked == true,
            (float)(PostPitchSmoothingCompressorStrengthSlider.Value / 100d),
            (float)OutputGainSlider.Value,
            NoiseGateCheckBox.IsChecked == true,
            (float)NoiseGateThresholdSlider.Value,
            CompressorCheckBox.IsChecked == true,
            (float)CompressorThresholdSlider.Value,
            (float)CompressorRatioSlider.Value,
            (float)CompressorMakeupGainSlider.Value,
            (float)EqLowSlider.Value,
            (float)EqMidSlider.Value,
            (float)EqHighSlider.Value);
    }

    private void ApplyPreset(Preset preset)
    {
        var controlsWereReady = _effectControlsReady;
        _effectControlsReady = false;

        PitchShiftSlider.Value = preset.PitchShiftSemitones;
        PitchStageModeComboBox.SelectedIndex = Math.Clamp(preset.PitchShiftStageMode, 0, 3);
        FormantShiftSlider.Value = preset.FormantShiftSemitones;
        HighBandArtifactReducerCheckBox.IsChecked = preset.HighBandArtifactReducerEnabled;
        HighBandArtifactReducerMaxReductionSlider.Value = preset.HighBandArtifactReducerMaxReductionDb;
        PostPitchCorrectionEqCheckBox.IsChecked = preset.PostPitchCorrectionEqEnabled;
        PostPitchCorrectionEqStrengthSlider.Value = preset.PostPitchCorrectionEqStrength * 100d;
        PostPitchDeEsserCheckBox.IsChecked = preset.PostPitchDeEsserEnabled;
        PostPitchDeEsserMaxReductionSlider.Value = preset.PostPitchDeEsserMaxReductionDb;
        PostPitchSmoothingCompressorCheckBox.IsChecked = preset.PostPitchSmoothingCompressorEnabled;
        PostPitchSmoothingCompressorStrengthSlider.Value = preset.PostPitchSmoothingCompressorStrength * 100d;
        OutputGainSlider.Value = preset.OutputGainDb;
        NoiseGateCheckBox.IsChecked = preset.NoiseGateEnabled;
        NoiseGateThresholdSlider.Value = preset.NoiseGateThresholdDb;
        CompressorCheckBox.IsChecked = preset.CompressorEnabled;
        CompressorThresholdSlider.Value = preset.CompressorThresholdDb;
        CompressorRatioSlider.Value = preset.CompressorRatio;
        CompressorMakeupGainSlider.Value = preset.CompressorMakeupGainDb;
        EqLowSlider.Value = preset.EqLowGainDb;
        EqMidSlider.Value = preset.EqMidGainDb;
        EqHighSlider.Value = preset.EqHighGainDb;

        _effectControlsReady = controlsWereReady;

        if (controlsWereReady)
        {
            SyncEffectsFromControls();
        }
    }

    private static string FormatPitch(double? pitchHz)
    {
        return pitchHz is { } value ? $"{value:0.0} Hz" : "--";
    }

    private void UpdatePitchStageCountText()
    {
        PitchStageCountTextBlock.Text = $"{_audioEngine.Effects.PitchShift.ActiveStageCount}段";
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
