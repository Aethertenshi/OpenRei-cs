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
    /// Reads up to maxBytes of PCM16 data. Returns bytes written (always aligned to full audio frame boundaries). Returns 0 at EOF.
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

    public Mp3StreamingDecoder(string path)
    {
        _mpeg = new MpegFile(path);
        _floatBuf = new float[8192];
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int samplesNeeded = maxBytes / sizeof(short);
        int samplesToRead = Math.Min(samplesNeeded, _floatBuf.Length);

        int read = _mpeg.ReadSamples(_floatBuf, 0, samplesToRead);
        if (read <= 0) return 0;

        int bytesWritten = 0;
        for (int i = 0; i < read; i++)
        {
            short sample = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
            buffer[offset + bytesWritten++] = (byte)(sample & 0xFF);
            buffer[offset + bytesWritten++] = (byte)((sample >> 8) & 0xFF);
        }

        return bytesWritten;
    }

    public override void SeekSeconds(double seconds)
    {
        if (CanSeek)
        {
            _mpeg.Time = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, LengthSeconds));
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
        _floatBuf = new float[8192];
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int samplesNeeded = maxBytes / sizeof(short);
        int samplesToRead = Math.Min(samplesNeeded, _floatBuf.Length);

        int read = _vorbis.ReadSamples(_floatBuf, 0, samplesToRead);
        if (read <= 0) return 0;

        int bytesWritten = 0;
        for (int i = 0; i < read; i++)
        {
            short sample = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
            buffer[offset + bytesWritten++] = (byte)(sample & 0xFF);
            buffer[offset + bytesWritten++] = (byte)((sample >> 8) & 0xFF);
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
    private readonly FileStream _fs;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly long _dataStartPos;
    private readonly long _dataLength;

    public override int SampleRate => _sampleRate;
    public override int Channels => _channels;
    public override double LengthSeconds => (double)_dataLength / (_sampleRate * _channels * 2);
    public override double PositionSeconds => (double)(_fs.Position - _dataStartPos) / (_sampleRate * _channels * 2);
    public override bool CanSeek => true;

    public WavStreamingDecoder(string path)
    {
        _fs = File.OpenRead(path);
        using var reader = new BinaryReader(_fs, System.Text.Encoding.ASCII, leaveOpen: true);

        reader.ReadChars(4); // RIFF
        reader.ReadInt32();
        reader.ReadChars(4); // WAVE

        _channels = 2;
        _sampleRate = 44100;

        while (_fs.Position < _fs.Length)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                reader.ReadInt16(); // format
                _channels = reader.ReadInt16();
                _sampleRate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt16();
                reader.ReadInt16(); // bits
                int extra = chunkSize - 16;
                if (extra > 0) reader.ReadBytes(extra);
            }
            else if (chunkId == "data")
            {
                _dataStartPos = _fs.Position;
                _dataLength = chunkSize;
                break;
            }
            else
            {
                _fs.Seek(chunkSize, SeekOrigin.Current);
            }
        }
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        long remaining = _dataStartPos + _dataLength - _fs.Position;
        if (remaining <= 0) return 0;

        int toRead = (int)Math.Min(maxBytes, remaining);
        return _fs.Read(buffer, offset, toRead);
    }

    public override void SeekSeconds(double seconds)
    {
        long targetByte = _dataStartPos + (long)(seconds * _sampleRate * _channels * 2);
        long alignedTarget = _dataStartPos + ((targetByte - _dataStartPos) / (_channels * 2)) * (_channels * 2);
        _fs.Position = Math.Clamp(alignedTarget, _dataStartPos, _dataStartPos + _dataLength);
    }

    public override void Dispose() => _fs.Dispose();
}
