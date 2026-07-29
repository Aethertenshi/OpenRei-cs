using System.Text;
using NLayer;
using NVorbis;

namespace OpenRei.Audio;

internal abstract class StreamingDecoder : IDisposable
{
    public abstract int SampleRate { get; }
    public abstract int Channels { get; }
    public abstract double LengthSeconds { get; }
    public abstract double PositionSeconds { get; }
    public abstract bool CanSeek { get; }

    /// <summary>Reads up to maxBytes of PCM16 data. Returns bytes written (always aligned to full audio frame boundaries). Returns 0 at EOF.</summary>
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
    private float[]? _floatBuf;
    private double _delaySeconds;

    public override int SampleRate => _mpeg.SampleRate;
    public override int Channels => _mpeg.Channels;
    public override double LengthSeconds => Math.Max(0, _mpeg.Duration.TotalSeconds - _delaySeconds);
    public override double PositionSeconds => Math.Max(0, _mpeg.Time.TotalSeconds - _delaySeconds);
    public override bool CanSeek => _mpeg.CanSeek;

    public Mp3StreamingDecoder(string path)
    {
        _mpeg = new MpegFile(path);
        _floatBuf = new float[8192];
        int delaySamples = ParseXingDelay(path);
        if (delaySamples <= 0 || delaySamples > 1152 * 10)
            delaySamples = 1152;
        _delaySeconds = delaySamples / (double)_mpeg.SampleRate;
    }

    /// <summary>Parses the Xing/LAME header in the first MP3 frame for encoder delay.</summary>
    private static int ParseXingDelay(string path)
    {
        try
        {
            byte[] data = new byte[4096];
            int read;
            using (var fs = File.OpenRead(path))
                read = fs.Read(data, 0, data.Length);
            if (read < 100) return 0;

            int syncPos = -1;
            for (int i = 0; i < read - 8; i++)
            {
                if (data[i] == 0xFF && (data[i + 1] & 0xE0) == 0xE0)
                {
                    syncPos = i;
                    break;
                }
            }
            if (syncPos < 0) return 0;

            int h2 = data[syncPos + 2];
            int h3 = data[syncPos + 3];
            int version = (h2 >> 3) & 3;
            int channelMode = (h3 >> 6) & 3;

            int sideInfoSize;
            if (version == 3)
                sideInfoSize = channelMode == 3 ? 17 : 32;
            else
                sideInfoSize = channelMode == 3 ? 9 : 17;

            bool hasCrc = (h2 & 1) == 0;
            int xingOffset = syncPos + 4 + sideInfoSize + (hasCrc ? 2 : 0);

            if (xingOffset + 12 > read) return 0;

            bool hasXing = false;
            for (int v = 0; v < 2; v++)
            {
                string marker = v == 0 ? "Xing" : "Info";
                bool found = true;
                for (int m = 0; m < 4; m++)
                    if (data[xingOffset + m] != marker[m]) { found = false; break; }
                if (found) { hasXing = true; break; }
            }
            if (!hasXing) return 0;

            int flags = (data[xingOffset + 4] << 24) | (data[xingOffset + 5] << 16)
                      | (data[xingOffset + 6] << 8) | data[xingOffset + 7];

            if ((flags & 0x0004) == 0) return 0;
            if (xingOffset + 16 > read) return 0;

            return (data[xingOffset + 8] << 24) | (data[xingOffset + 9] << 16)
                 | (data[xingOffset + 10] << 8) | data[xingOffset + 11];
        }
        catch
        {
            return 0;
        }
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int frameSize = _mpeg.Channels * 2;
        int maxFrames = maxBytes / frameSize;
        if (maxFrames <= 0) return 0;

        int totalSamplesRequested = maxFrames * _mpeg.Channels;
        if (_floatBuf == null || _floatBuf.Length < totalSamplesRequested)
            _floatBuf = new float[totalSamplesRequested];

        // NLayer may return fewer samples than requested (MP3 frame boundaries).
        // Loop until buffer is full or EOF.
        int totalSamplesRead = 0;
        while (totalSamplesRead < totalSamplesRequested)
        {
            int read = _mpeg.ReadSamples(_floatBuf, totalSamplesRead,
                totalSamplesRequested - totalSamplesRead);
            if (read <= 0) break;
            totalSamplesRead += read;
        }

        int framesRead = totalSamplesRead / _mpeg.Channels;
        int totalSamples = framesRead * _mpeg.Channels;

        for (int i = 0; i < totalSamples; i++)
        {
            short val = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
            buffer[offset + i * 2] = (byte)(val & 0xFF);
            buffer[offset + i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }

        return framesRead * frameSize;
    }

    public override void SeekSeconds(double seconds)
    {
        _mpeg.Time = TimeSpan.FromSeconds(Math.Clamp(seconds + _delaySeconds, _delaySeconds, _mpeg.Duration.TotalSeconds));
    }

    public override void Dispose() => _mpeg?.Dispose();
}

internal sealed class OggStreamingDecoder : StreamingDecoder
{
    private readonly VorbisReader _vorbis;
    private float[]? _floatBuf;

    public override int SampleRate => _vorbis.SampleRate;
    public override int Channels => _vorbis.Channels;
    public override double LengthSeconds => _vorbis.TotalTime.TotalSeconds;
    public override double PositionSeconds => _vorbis.TimePosition.TotalSeconds;
    public override bool CanSeek => true;

    public OggStreamingDecoder(string path) => _vorbis = new VorbisReader(path);

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        int frameSize = _vorbis.Channels * 2;
        int maxFrames = maxBytes / frameSize;
        if (maxFrames <= 0) return 0;

        int maxSamples = maxFrames * _vorbis.Channels;
        if (_floatBuf == null || _floatBuf.Length < maxSamples)
            _floatBuf = new float[maxSamples];

        int samplesRead = _vorbis.ReadSamples(_floatBuf, 0, maxSamples);
        int framesRead = samplesRead / _vorbis.Channels;
        int totalSamples = framesRead * _vorbis.Channels;

        for (int i = 0; i < totalSamples; i++)
        {
            short val = (short)Math.Clamp(_floatBuf[i] * 32767f, -32768f, 32767f);
            buffer[offset + i * 2] = (byte)(val & 0xFF);
            buffer[offset + i * 2 + 1] = (byte)((val >> 8) & 0xFF);
        }
        return framesRead * frameSize;
    }

    public override void SeekSeconds(double seconds)
    {
        _vorbis.TimePosition = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, _vorbis.TotalTime.TotalSeconds));
    }

    public override void Dispose() => _vorbis?.Dispose();
}

internal sealed class WavStreamingDecoder : StreamingDecoder
{
    private readonly FileStream _stream;
    private readonly long _dataStart;
    private readonly long _dataLength;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _inputFrameSize;

    public override int SampleRate => _sampleRate;
    public override int Channels => _channels;
    public override double LengthSeconds => _dataLength / (double)(_inputFrameSize * _sampleRate);
    public override double PositionSeconds => (_stream.Position - _dataStart) / (double)(_inputFrameSize * _sampleRate);
    public override bool CanSeek => true;

    public WavStreamingDecoder(string path)
    {
        _stream = File.OpenRead(path);
        using var reader = new BinaryReader(_stream, Encoding.ASCII, true);

        string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (chunkId != "RIFF") throw new InvalidDataException("Not a WAV file");
        reader.ReadInt32();
        string format = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (format != "WAVE") throw new InvalidDataException("Not a WAVE file");

        int channels = 2, sampleRate = 44100, bitsPerSample = 16;
        long dataStart = 0, dataLen = 0;

        while (_stream.Position < _stream.Length - 8)
        {
            string sub = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int size = reader.ReadInt32();

            if (sub == "fmt ")
            {
                reader.ReadInt16(); // audio format
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadInt16(); // block align
                bitsPerSample = reader.ReadInt16();
                if (size > 16) reader.BaseStream.Seek(size - 16, SeekOrigin.Current);
            }
            else if (sub == "data")
            {
                dataStart = _stream.Position;
                dataLen = size;
                break;
            }
            else
            {
                if (size > 0) reader.BaseStream.Seek(size, SeekOrigin.Current);
            }
        }

        _channels = channels;
        _sampleRate = sampleRate;
        _bitsPerSample = bitsPerSample;
        _inputFrameSize = (_bitsPerSample / 8) * _channels;
        _dataStart = dataStart;
        _dataLength = dataLen;
        _stream.Seek(dataStart, SeekOrigin.Begin);
    }

    public override int ReadPcm16(byte[] buffer, int offset, int maxBytes)
    {
        long inputPos = _stream.Position - _dataStart;
        long remaining = _dataLength - inputPos;
        if (remaining <= 0) return 0;

        if (_bitsPerSample == 16)
        {
            int toRead = (int)Math.Min(maxBytes, remaining);
            toRead = (toRead / _inputFrameSize) * _inputFrameSize;
            if (toRead <= 0) return 0;
            return _stream.Read(buffer, offset, toRead);
        }

        if (_bitsPerSample == 8)
        {
            int toRead = (int)Math.Min(maxBytes / 2, remaining);
            toRead = (toRead / _channels) * _channels;
            if (toRead <= 0) return 0;

            byte[] tmp = new byte[toRead];
            int read = _stream.Read(tmp, 0, toRead);
            int framesRead = read / _channels;
            int totalSamples = framesRead * _channels;

            for (int i = 0; i < totalSamples; i++)
            {
                short val = (short)((tmp[i] - 128) * 256);
                buffer[offset + i * 2] = (byte)(val & 0xFF);
                buffer[offset + i * 2 + 1] = (byte)((val >> 8) & 0xFF);
            }
            return totalSamples * 2;
        }

        return 0;
    }

    public override void SeekSeconds(double seconds)
    {
        long frameOffset = (long)(seconds * _sampleRate);
        long bytePos = frameOffset * _inputFrameSize;
        bytePos = Math.Clamp(bytePos, 0, _dataLength);
        _stream.Seek(_dataStart + bytePos, SeekOrigin.Begin);
    }

    public override void Dispose() => _stream?.Dispose();
}
