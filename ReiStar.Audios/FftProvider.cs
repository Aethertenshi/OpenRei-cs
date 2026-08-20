namespace reistar.Audios;

using System;

/// <summary>
/// High-performance zero-allocation Fast Fourier Transform (FFT) and spectrum analysis for real-time visualizers.
/// </summary>
public static class FftProvider
{
    private static readonly float[] _hannWindow512 = new float[512];
    private static readonly float[] _hannWindow1024 = new float[1024];

    static FftProvider()
    {
        for (int i = 0; i < 512; i++)
            _hannWindow512[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / 511f));

        for (int i = 0; i < 1024; i++)
            _hannWindow1024[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / 1023f));
    }

    /// <summary>
    /// Computes magnitude frequency spectrum from 16-bit PCM samples into an output magnitude buffer.
    /// Matched to BASS audio spectrum visualizer scaling.
    /// </summary>
    /// <param name="pcm16Samples">Input signed 16-bit PCM samples.</param>
    /// <param name="outputSpectrum">Output array of frequency magnitudes (size = fftSize / 2).</param>
    public static void ComputeSpectrum(ReadOnlySpan<short> pcm16Samples, Span<float> outputSpectrum)
    {
        int outputBins = outputSpectrum.Length;
        int fftSize = outputBins * 2;
        if (fftSize < 64 || (fftSize & (fftSize - 1)) != 0)
        {
            fftSize = 512;
            outputBins = 256;
        }

        Span<float> real = stackalloc float[fftSize];
        Span<float> imag = stackalloc float[fftSize];
        imag.Clear();

        int available = Math.Min(pcm16Samples.Length, fftSize);
        ReadOnlySpan<float> window = fftSize == 1024 ? _hannWindow1024 : _hannWindow512;

        for (int i = 0; i < available; i++)
        {
            float norm = pcm16Samples[i] / 32768f;
            float win = (i < window.Length && fftSize <= 1024) 
                ? window[i] 
                : (0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (fftSize - 1))));
            real[i] = norm * win;
        }

        for (int i = available; i < fftSize; i++)
        {
            real[i] = 0f;
        }

        // In-place Cooley-Tukey Radix-2 FFT
        TransformRadix2(real, imag);

        // BASS visualizer scaling: 4.0 / sqrt(N) gives natural 0.0 .. 1.0 range for music spectrums
        float scale = 4.0f / MathF.Sqrt(fftSize);
        for (int i = 0; i < outputBins; i++)
        {
            float mag = MathF.Sqrt(real[i] * real[i] + imag[i] * imag[i]) * scale;
            outputSpectrum[i] = mag;
        }
    }

    /// <summary>
    /// Computes RMS and Peak amplitude from 16-bit PCM samples.
    /// </summary>
    public static (float Rms, float Peak) ComputeLevel(ReadOnlySpan<short> pcm16Samples)
    {
        if (pcm16Samples.Length == 0) return (0f, 0f);

        float sumSquares = 0f;
        float peak = 0f;

        for (int i = 0; i < pcm16Samples.Length; i++)
        {
            float val = MathF.Abs(pcm16Samples[i] / 32768f);
            sumSquares += val * val;
            if (val > peak) peak = val;
        }

        float rms = MathF.Sqrt(sumSquares / pcm16Samples.Length);
        return (rms, peak);
    }

    private static void TransformRadix2(Span<float> real, Span<float> imag)
    {
        int n = real.Length;
        int j = 0;

        // Bit-reversal permutation
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
            int k = n >> 1;
            while (k <= j)
            {
                j -= k;
                k >>= 1;
            }
            j += k;
        }

        // Cooley-Tukey Radix-2 butterflies
        for (int len = 2; len <= n; len <<= 1)
        {
            float angle = -2f * MathF.PI / len;
            float wlenReal = MathF.Cos(angle);
            float wlenImag = MathF.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float wReal = 1f;
                float wImag = 0f;

                int half = len >> 1;
                for (int k = 0; k < half; k++)
                {
                    int uIdx = i + k;
                    int vIdx = i + k + half;

                    float uReal = real[uIdx];
                    float uImag = imag[uIdx];

                    float vReal = real[vIdx] * wReal - imag[vIdx] * wImag;
                    float vImag = real[vIdx] * wImag + imag[vIdx] * wReal;

                    real[uIdx] = uReal + vReal;
                    imag[uIdx] = uImag + vImag;
                    real[vIdx] = uReal - vReal;
                    imag[vIdx] = uImag - vImag;

                    float nextWReal = wReal * wlenReal - wImag * wlenImag;
                    float nextWImag = wReal * wlenImag + wImag * wlenReal;
                    wReal = nextWReal;
                    wImag = nextWImag;
                }
            }
        }
    }
}
