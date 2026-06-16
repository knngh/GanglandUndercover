using UnityEngine;

namespace GanglandUndercover.Audio
{
    /// <summary>
    /// Audio service abstraction for SFX playback, BGM control, and volume management.
    /// Extracted from <see cref="AudioManager"/> to enable testing and alternative audio backends.
    /// </summary>
    public interface IAudioService
    {
        // ================================================================
        //  Volume Control
        // ================================================================

        /// <summary>Master volume (0..1). Affects all audio output.</summary>
        float MasterVolume { get; set; }

        /// <summary>Sound effects volume (0..1). Multiplied with MasterVolume.</summary>
        float SFXVolume { get; set; }

        /// <summary>Background music volume (0..1). Multiplied with MasterVolume.</summary>
        float MusicVolume { get; set; }

        // ================================================================
        //  SFX Playback
        // ================================================================

        /// <summary>Play a sound effect globally (2D, non-spatialized).</summary>
        void PlaySFX(SoundEffect effect);

        /// <summary>Play a sound effect at a world position (3D spatialized).</summary>
        void PlaySFXAtPoint(SoundEffect effect, Vector3 position);

        // ================================================================
        //  BGM Control
        // ================================================================

        /// <summary>Play a background music track with crossfade.</summary>
        void PlayBGM(MusicTrack track);

        /// <summary>Stop background music with fade-out.</summary>
        void StopBGM();

        /// <summary>Pause background music (e.g. during pause menu).</summary>
        void PauseBGM();

        /// <summary>Resume paused background music.</summary>
        void ResumeBGM();

        /// <summary>Play a new music clip with a custom fade duration.</summary>
        void PlayMusicWithFade(AudioClip nextClip, float fadeDuration = 0.8f);
    }
}
