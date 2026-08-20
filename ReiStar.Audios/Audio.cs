namespace reistar.Audios;

using System;

/// <summary>
/// Static high-level audio manager for simple sound playback, track loading, and volume control.
/// </summary>
public static class Audio
{
    private static IAudioTrack? _currentMusic;

    public static float MasterVolume
    {
        get => AudioEngine.MasterVolume;
        set => AudioEngine.MasterVolume = value;
    }

    /// <summary>
    /// Loads a music track for streaming playback.
    /// </summary>
    public static MusicTrack Load(string filePath, double startPositionMs = 0)
    {
        AudioEngine.Initialize();
        return new MusicTrack(filePath, startPositionMs);
    }

    /// <summary>
    /// Loads a full-PCM audio stream for instant sub-millisecond precision seeking and playback.
    /// </summary>
    public static AudioStream LoadStream(string filePath)
    {
        AudioEngine.Initialize();
        return new AudioStream(filePath);
    }

    /// <summary>
    /// Plays a short sound effect with zero-latency polyphony.
    /// </summary>
    public static void PlaySound(string filePath, float volume = 1f, float pitch = 1f)
    {
        AudioEngine.Initialize();
        var sfx = AudioCache.GetSound(filePath);
        sfx.Play(volume, pitch);
    }

    /// <summary>
    /// Plays background music, replacing any currently playing music track.
    /// </summary>
    public static void PlayMusic(string filePath, bool loop = true, float volume = 1f)
    {
        StopMusic();
        var music = Load(filePath);
        music.Loop = loop;
        music.Volume = volume;
        music.Play();
        _currentMusic = music;
    }

    /// <summary>
    /// Stops the currently playing background music.
    /// </summary>
    public static void StopMusic()
    {
        if (_currentMusic != null)
        {
            _currentMusic.Stop();
            _currentMusic.Dispose();
            _currentMusic = null;
        }
    }
}
