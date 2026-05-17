namespace VoxScope.Analysis;

public sealed class PitchDetector
{
    private const double MinimumCorrelation = 0.55d;
    private const double MinimumRms = 0.01d;

    public double? Detect(
        ReadOnlySpan<float> samples,
        int sampleRate,
        double minimumFrequencyHz = 70d,
        double maximumFrequencyHz = 350d)
    {
        if (samples.Length < 2)
        {
            return null;
        }

        var mean = 0d;

        for (var index = 0; index < samples.Length; index++)
        {
            mean += samples[index];
        }

        mean /= samples.Length;

        var centeredSamples = new double[samples.Length];
        var sumSquares = 0d;

        for (var index = 0; index < samples.Length; index++)
        {
            var centeredSample = samples[index] - mean;
            centeredSamples[index] = centeredSample;
            sumSquares += centeredSample * centeredSample;
        }

        var rms = Math.Sqrt(sumSquares / samples.Length);

        if (rms < MinimumRms)
        {
            return null;
        }

        var minimumLag = Math.Max(1, (int)Math.Floor(sampleRate / maximumFrequencyHz));
        var maximumLag = Math.Min(samples.Length - 2, (int)Math.Ceiling(sampleRate / minimumFrequencyHz));
        var correlations = new double[maximumLag + 1];

        var bestLag = 0;
        var bestCorrelation = double.MinValue;

        for (var lag = minimumLag; lag <= maximumLag; lag++)
        {
            var numerator = 0d;
            var firstEnergy = 0d;
            var secondEnergy = 0d;
            var limit = samples.Length - lag;

            for (var index = 0; index < limit; index++)
            {
                var first = centeredSamples[index];
                var second = centeredSamples[index + lag];
                numerator += first * second;
                firstEnergy += first * first;
                secondEnergy += second * second;
            }

            var denominator = Math.Sqrt(firstEnergy * secondEnergy);
            var correlation = denominator <= double.Epsilon ? 0d : numerator / denominator;
            correlations[lag] = correlation;

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestLag = lag;
            }
        }

        if (bestCorrelation < MinimumCorrelation)
        {
            return null;
        }

        var refinedLag = RefineLag(correlations, bestLag, minimumLag, maximumLag);
        return sampleRate / refinedLag;
    }

    private static double RefineLag(double[] correlations, int bestLag, int minimumLag, int maximumLag)
    {
        if (bestLag <= minimumLag || bestLag >= maximumLag)
        {
            return bestLag;
        }

        var left = correlations[bestLag - 1];
        var center = correlations[bestLag];
        var right = correlations[bestLag + 1];
        var denominator = left - (2d * center) + right;

        if (Math.Abs(denominator) < double.Epsilon)
        {
            return bestLag;
        }

        var correction = 0.5d * (left - right) / denominator;
        return bestLag + Math.Clamp(correction, -1d, 1d);
    }
}
