namespace reistar.Audios;

using System;
using System.IO;
using NLayer;
using NVorbis;

internal abstract class StreamingDecoder : IDisposable
{
    public abstract int SampleRate { get; }
    public abstract int Channels { get; }
    public abstract double LengthSeconds { get; }
    public abstract double PositionSeconds { get; }
    public abstract bool CanSeek { get; }

    /// <summary>
    /// Reads up to maxBytes of 16-bit PCM data. Returns bytes written. Returns 0 at EOF.
    /// </summary>
    public abstract int ReadPcm16(byte[] buffer, int offset, int maxBytes);
    public abstract void SeekSeconds(double seconds);

    public static StreamingDecoder Open(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => new Mp3StreamingDecoder(path),
            ".ogg" => new OggStreamingDecoder(path),
            _ => new WavStreamingDecoder(path),
        };
    }

    public abstract void Dispose();
}

internal sealed class Mp3StreamingDecoder : StreamingDecoder
{
    private readonly MpegFile _mpeg;
    private readonly float[] _floatBuf;

    public override int SampleRate => _mpeg.SampleRate;
    public override int Channels => _mpeg.Channels;
    public override double LengthSeconds => _mpeg.Duration.TotalSeconds;
    public override double PositionSeconds => _mpeg.Time.TotalSeconds;
    public override bool CanSeek => _mpeg.CanSeek;

    // Standard MP3 encoder delay (LAME/ISO MDCT filterbank delay is 528 samples per channel)
    private const int Mp3EncoderDelaySamples = 528;

    public Mp3StreamingDecoder(string path)
    {
        _mpeg = new MpegFile(path);
        _floatBuf = new float[16384];
        SkipEncoderPadding();
    }

    private void SkipEncoderPadding()
    {
        int paddingSamples = Mp3EncoderDelaySamples * _mpeg.Channels;
        while (paddingSamples > 0)
        {
            int toRead = Math.Min(paddingSamples, _floatBuf.Length);
            int read = _mpeg.ReadSamples(_floatBuf, 0, toRead);
            if (read <= 0) break;
            paddingSamples -= read;
        }
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int bytesWritten = 0;
        while (bytesWritten < maxBytes)
        {
            int samplesNeeded = (maxBytes - bytesWritten) / sizeof(short);
            int samplesToRead = Math.Min(samplesNeeded, _floatBuf.Length);
            if (samplesToRead <= 0) break;

            int read = _mpeg.ReadSamples(_floatBuf, 0, samplesToRead);
            if (read <= 0) break;

            for (int i = 0; i < read; i++)
            {
                short sample = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
                buffer[offset + bytesWritten++] = (byte)(sample & 0xFF);
                buffer[offset + bytesWritten++] = (byte)((sample >> 8) & 0xFF);
            }
        }

        return bytesWritten;
    }

    public override void SeekSeconds(double seconds)
    {
        if (CanSeek)
        {
            if (seconds <= 0.001)
            {
                _mpeg.Time = TimeSpan.Zero;
                SkipEncoderPadding();
            }
            else
            {
                double delaySec = (double)Mp3EncoderDelaySamples / _mpeg.SampleRate;
                _mpeg.Time = TimeSpan.FromSeconds(Math.Clamp(seconds + delaySec, 0, LengthSeconds));
            }
        }
    }

    public override void Dispose() => _mpeg.Dispose();
}

internal sealed class OggStreamingDecoder : StreamingDecoder
{
    private readonly VorbisReader _vorbis;
    private readonly float[] _floatBuf;

    public override int SampleRate => _vorbis.SampleRate;
    public override int Channels => _vorbis.Channels;
    public override double LengthSeconds => _vorbis.TotalTime.TotalSeconds;
    public override double PositionSeconds => _vorbis.TimePosition.TotalSeconds;
    public override bool CanSeek => true;

    public OggStreamingDecoder(string path)
    {
        _vorbis = new VorbisReader(path);
        _floatBuf = new float[16384];
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int bytesWritten = 0;
        while (bytesWritten < maxBytes)
        {
            int samplesNeeded = (maxBytes - bytesWritten) / sizeof(short);
            int samplesToRead = Math.Min(samplesNeeded, _floatBuf.Length);
            if (samplesToRead <= 0) break;

            int read = _vorbis.ReadSamples(_floatBuf, 0, samplesToRead);
            if (read <= 0) break;

            for (int i = 0; i < read; i++)
            {
                short sample = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
                buffer[offset + bytesWritten++] = (byte)(sample & 0xFF);
                buffer[offset + bytesWritten++] = (byte)((sample >> 8) & 0xFF);
            }
        }

        return bytesWritten;
    }

    public override void SeekSeconds(double seconds)
    {
        if (CanSeek)
        {
            _vorbis.TimePosition = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, LengthSeconds));
        }
    }

    public override void Dispose() => _vorbis.Dispose();
}

internal sealed class WavStreamingDecoder : StreamingDecoder
{
    private readonly byte[] _pcm16;
    private readonly int _sampleRate;
    private readonly int _channels;
    private int _readOffset;

    public override int SampleRate => _sampleRate;
    public override int Channels => _channels;
    public override double LengthSeconds => (double)_pcm16.Length / (_sampleRate * _channels * 2);
    public override double PositionSeconds => (double)_readOffset / (_sampleRate * _channels * 2);
    public override bool CanSeek => true;

    public WavStreamingDecoder(string path)
    {
        var decoded = AudioDecoder.DecodeFile(path);
        _pcm16 = decoded.PcmData;
        _sampleRate = decoded.SampleRate;
        _channels = decoded.Channels;
        _readOffset = 0;
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int remaining = _pcm16.Length - _readOffset;
        if (remaining <= 0) return 0;

        int toCopy = Math.Min(maxBytes, remaining);
        Buffer.BlockCopy(_pcm16, _readOffset, buffer, offset, toCopy);
        _readOffset += toCopy;
        return toCopy;
    }

    public override void SeekSeconds(double seconds)
    {
        int targetByte = (int)(seconds * _sampleRate * _channels * 2);
        int frameSize = _channels * 2;
        int aligned = (targetByte / frameSize) * frameSize;
        _readOffset = Math.Clamp(aligned, 0, _pcm16.Length);
    }

    public override void Dispose() { }
}
