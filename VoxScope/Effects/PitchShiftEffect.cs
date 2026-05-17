using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxScope.Effects;

public sealed class PitchShiftEffect : IAudioEffect
{
    private const int MaximumStageCount = 3;
    private const float MinimumSemitones = -12f;
    private const float MaximumSemitones = 12f;
    private const float DefaultSmoothingTimeMilliseconds = 120f;
    private PitchShiftSampleProvider? _sampleProvider;
    private float _targetSemitones;
    private float _currentSemitones;
    private int _requestedStageCount;
    private int _activeStageCount = 1;

    // Kept for callers that already use Semitones; this is now the target value.
    public float Semitones
    {
        get => TargetSemitones;
        set => TargetSemitones = value;
    }

    public float TargetSemitones
    {
        get => Volatile.Read(ref _targetSemitones);
        set
        {
            var clampedValue = Math.Clamp(value, MinimumSemitones, MaximumSemitones);
            Volatile.Write(ref _targetSemitones, clampedValue);

            if (_sampleProvider is null)
            {
                Volatile.Write(ref _currentSemitones, clampedValue);
            }
        }
    }

    public float CurrentSemitones => Volatile.Read(ref _currentSemitones);

    public float TargetPitchFactor => SemitonesToPitchFactor(TargetSemitones);

    public float CurrentPitchFactor => SemitonesToPitchFactor(CurrentSemitones);

    // 0 = auto, 1..3 = fixed stage count.
    public int RequestedStageCount
    {
        get => Volatile.Read(ref _requestedStageCount);
        set => Volatile.Write(ref _requestedStageCount, Math.Clamp(value, 0, MaximumStageCount));
    }

    public int ActiveStageCount => Volatile.Read(ref _activeStageCount);

    public float SmoothingTimeMilliseconds { get; set; } = DefaultSmoothingTimeMilliseconds;

    public ISampleProvider CreateSampleProvider(ISampleProvider sourceProvider)
    {
        Volatile.Write(ref _currentSemitones, TargetSemitones);
        _sampleProvider = new PitchShiftSampleProvider(sourceProvider, this);
        return _sampleProvider;
    }

    private void UpdateCurrentSemitones(int requestedSampleCount, int sampleRate, int channels)
    {
        var currentSemitones = CurrentSemitones;
        var targetSemitones = TargetSemitones;
        var frames = Math.Max(1, requestedSampleCount / Math.Max(1, channels));
        var elapsedSeconds = frames / (double)sampleRate;
        var smoothingTimeSeconds = Math.Max(0.001d, SmoothingTimeMilliseconds / 1000d);
        var blend = 1d - Math.Exp(-elapsedSeconds / smoothingTimeSeconds);
        var nextSemitones = currentSemitones + ((targetSemitones - currentSemitones) * blend);

        if (Math.Abs(targetSemitones - nextSemitones) < 0.001d)
        {
            nextSemitones = targetSemitones;
        }

        Volatile.Write(ref _currentSemitones, (float)nextSemitones);
    }

    private int ResolveStageCount()
    {
        var requestedStageCount = RequestedStageCount;

        if (requestedStageCount > 0)
        {
            return requestedStageCount;
        }

        var maximumRelevantSemitoneDistance = Math.Max(Math.Abs(TargetSemitones), Math.Abs(CurrentSemitones));

        return maximumRelevantSemitoneDistance switch
        {
            <= 6f => 1,
            <= 10f => 2,
            _ => 3,
        };
    }

    private void UpdateActiveStageCount(int stageCount)
    {
        Volatile.Write(ref _activeStageCount, stageCount);
    }

    private static float SemitonesToPitchFactor(float semitones)
    {
        return MathF.Pow(2f, semitones / 12f);
    }

    private sealed class PitchShiftSampleProvider : ISampleProvider
    {
        private readonly PitchShiftEffect _effect;
        private readonly SmbPitchShiftingSampleProvider[] _stages;

        public PitchShiftSampleProvider(ISampleProvider sourceProvider, PitchShiftEffect effect)
        {
            _effect = effect;
            _stages = new SmbPitchShiftingSampleProvider[MaximumStageCount];

            ISampleProvider current = sourceProvider;

            for (var index = 0; index < _stages.Length; index++)
            {
                var stage = new SmbPitchShiftingSampleProvider(current);
                _stages[index] = stage;
                current = stage;
            }

            WaveFormat = sourceProvider.WaveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            _effect.UpdateCurrentSemitones(count, WaveFormat.SampleRate, WaveFormat.Channels);

            var stageCount = _effect.ResolveStageCount();
            var currentSemitones = _effect.CurrentSemitones;
            var semitonesPerStage = currentSemitones / stageCount;
            var pitchFactorPerStage = SemitonesToPitchFactor(semitonesPerStage);

            for (var index = 0; index < _stages.Length; index++)
            {
                _stages[index].PitchFactor = index < stageCount ? pitchFactorPerStage : 1f;
            }

            _effect.UpdateActiveStageCount(stageCount);
            return _stages[stageCount - 1].Read(buffer, offset, count);
        }
    }
}
