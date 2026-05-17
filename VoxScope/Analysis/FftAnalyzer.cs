using NAudio.Dsp;

namespace VoxScope.Analysis;

public sealed class FftAnalyzer
{
    public const int FftSize = 2048;

    public float[] Analyze(ReadOnlySpan<float> samples)
    {
        var fftBuffer = new Complex[FftSize];
        var sourceOffset = Math.Max(0, samples.Length - FftSize);
        var samplesToCopy = Math.Min(samples.Length, FftSize);
        var destinationOffset = FftSize - samplesToCopy;

        for (var index = 0; index < samplesToCopy; index++)
        {
            var windowIndex = destinationOffset + index;
            var window = 0.5f * (1f - MathF.Cos((2f * MathF.PI * windowIndex) / (FftSize - 1)));
            fftBuffer[windowIndex].X = samples[sourceOffset + index] * window;
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(FftSize), fftBuffer);

        var spectrum = new float[FftSize / 2];

        for (var index = 0; index < spectrum.Length; index++)
        {
            var real = fftBuffer[index].X;
            var imaginary = fftBuffer[index].Y;
            var magnitude = MathF.Sqrt((real * real) + (imaginary * imaginary));
            spectrum[index] = 20f * MathF.Log10(MathF.Max(magnitude, 1e-5f));
        }

        return spectrum;
    }
}
