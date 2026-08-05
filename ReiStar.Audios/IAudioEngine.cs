namespace reistar.Audios;

public interface IAudioEngine : IDisposable
{
    void PlaySound(string soundId, float volume = 1.0f);
    void PlayMusic(string musicId, bool loop = true, float volume = 1.0f);
    void StopMusic();
    void SetMasterVolume(float volume);
}
