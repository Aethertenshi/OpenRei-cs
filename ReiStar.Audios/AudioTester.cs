using System;
using System.IO;
using reistar.Audios;

public static class AudioTester
{
    public static void RunDiagnostic()
    {
        Console.WriteLine("--- AUDIO TEST START ---");
        
        string wavPath = @"D:\ProjectTemp\PulseMatrix\include\audios\welcome.wav";
        if (File.Exists(wavPath))
        {
            var wavData = AudioDecoder.DecodeFile(wavPath);
            Console.WriteLine($"WAV: Rate={wavData.SampleRate}, Channels={wavData.Channels}, Bits={wavData.BitsPerSample}, Duration={wavData.DurationSeconds}s, Bytes={wavData.PcmData.Length}");
        }

        string clickPath = @"D:\ProjectTemp\PulseMatrix\include\audios\click.wav";
        if (File.Exists(clickPath))
        {
            var clickData = AudioDecoder.DecodeFile(clickPath);
            Console.WriteLine($"Click: Rate={clickData.SampleRate}, Channels={clickData.Channels}, Bits={clickData.BitsPerSample}, Duration={clickData.DurationSeconds}s, Bytes={clickData.PcmData.Length}");
        }

        if (File.Exists(wavPath))
        {
            using var dec = StreamingDecoder.Open(wavPath);
            Console.WriteLine($"WavDecoder: Rate={dec.SampleRate}, Channels={dec.Channels}, Length={dec.LengthSeconds}s");
        }

        Console.WriteLine("--- AUDIO TEST END ---");
    }
}
