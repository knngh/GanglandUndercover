using UnityEngine;

namespace GanglandUndercover.Audio
{
    /// <summary>
    /// Sound effect identifiers used throughout the game.
    /// Maps 1:1 to Among Us audio cues where applicable.
    /// </summary>
    public enum SoundEffect
    {
        UIClick,
        Footstep,
        Kill,
        BodyReport,
        Report,
        MeetingStart,
        VoteCast,
        PlayerEliminated,
        TaskComplete,
        Sabotage,
        Victory,
        Defeat,
        Emergency,
        Ambient,
        VentOpen,
        VentClose,
        ButtonHover
    }

    /// <summary>
    /// Background music tracks.
    /// </summary>
    public enum MusicTrack
    {
        MainMenu,   // 主菜单 BGM
        InGame,     // 游戏内 BGM
        Meeting,    // 会议 BGM
    }

    /// <summary>
    /// Singleton audio manager (DontDestroyOnLoad).
    /// Provides centralized SFX playback, volume control, and spatialized audio support.
    ///
    /// All AudioClip fields are Inspector-ready placeholders — drag audio assets in the Editor
    /// or leave them null; null clips silently skip playback.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────────────
        private static AudioManager _instance;
        public static AudioManager Instance => _instance;

        // ── Inspector AudioClip slots ──────────────────────────
        [Header("Sound Effects")]
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip footstepClip;
        [SerializeField] private AudioClip killClip;
        [SerializeField] private AudioClip bodyReportClip;
        [SerializeField] private AudioClip reportClip;
        [SerializeField] private AudioClip meetingStartClip;
        [SerializeField] private AudioClip voteCastClip;
        [SerializeField] private AudioClip playerEliminatedClip;
        [SerializeField] private AudioClip taskCompleteClip;
        [SerializeField] private AudioClip sabotageClip;
        [SerializeField] private AudioClip victoryClip;
        [SerializeField] private AudioClip defeatClip;
        [SerializeField] private AudioClip emergencyClip;
        [SerializeField] private AudioClip ambientClip;
        [SerializeField] private AudioClip ventOpenClip;
        [SerializeField] private AudioClip ventCloseClip;
        [SerializeField] private AudioClip buttonHoverClip;

        [Header("Background Music")]
        [SerializeField] private AudioClip mainMenuBGM;
        [SerializeField] private AudioClip inGameBGM;
        [SerializeField] private AudioClip meetingBGM;
        [SerializeField] [Range(0.5f, 3f)] private float musicFadeDuration = 1.2f;

        // ── Volume —────────────────────────────────────────────
        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float musicVolume = 1f;

        // ── Internal audio sources ─────────────────────────────
        private AudioSource sfxSource;      // 2D one-shot pool
        private AudioSource musicSource;    // looping BGM
        private AudioSource ambientSource;  // looping ambient

        // ── Public volume properties ───────────────────────────
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        // ── MonoBehaviour ──────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply volume changes made in the Inspector at edit time.
            if (Application.isPlaying)
            {
                ApplyVolumes();
            }
        }
#endif

        // ── Initialization ─────────────────────────────────────
        private void InitializeAudioSources()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.loop = false;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f;
            ambientSource.loop = true;

            ApplyVolumes();

            // Auto-start ambient loop if clip is assigned.
            if (ambientClip != null)
            {
                ambientSource.clip = ambientClip;
                ambientSource.Play();
            }
        }

        private void ApplyVolumes()
        {
            if (sfxSource != null)
                sfxSource.volume = masterVolume * sfxVolume;

            if (musicSource != null)
                musicSource.volume = masterVolume * musicVolume;

            if (ambientSource != null)
                ambientSource.volume = masterVolume * sfxVolume * 0.5f;
        }

        // ── Public API ─────────────────────────────────────────

        /// <summary>Play a sound effect globally (2D, non-spatialized).</summary>
        public void PlaySFX(SoundEffect effect)
        {
            AudioClip clip = ResolveClip(effect);
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Play a sound effect at a world position (3D spatialized).
        /// Intended for footsteps, kills, and other positional cues.
        /// </summary>
        public void PlaySFXAtPoint(SoundEffect effect, Vector3 position)
        {
            AudioClip clip = ResolveClip(effect);
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, masterVolume * sfxVolume);
            }
        }

        // ── Clip lookup ────────────────────────────────────────
        private AudioClip ResolveClip(SoundEffect effect)
        {
            return effect switch
            {
                SoundEffect.UIClick          => uiClickClip,
                SoundEffect.Footstep         => footstepClip,
                SoundEffect.Kill             => killClip,
                SoundEffect.BodyReport       => bodyReportClip,
                SoundEffect.Report           => reportClip,
                SoundEffect.MeetingStart     => meetingStartClip,
                SoundEffect.VoteCast         => voteCastClip,
                SoundEffect.PlayerEliminated => playerEliminatedClip,
                SoundEffect.TaskComplete     => taskCompleteClip,
                SoundEffect.Sabotage         => sabotageClip,
                SoundEffect.Victory          => victoryClip,
                SoundEffect.Defeat           => defeatClip,
                SoundEffect.Emergency        => emergencyClip,
                SoundEffect.Ambient          => ambientClip,
                SoundEffect.VentOpen         => ventOpenClip,
                SoundEffect.VentClose        => ventCloseClip,
                SoundEffect.ButtonHover      => buttonHoverClip,
                _ => null
            };
        }

        private AudioClip ResolveMusicClip(MusicTrack track)
        {
            return track switch
            {
                MusicTrack.MainMenu => mainMenuBGM,
                MusicTrack.InGame   => inGameBGM,
                MusicTrack.Meeting  => meetingBGM,
                _ => null
            };
        }

        // ── Background Music API ────────────────────────────────

        /// <summary>Play a background music track with optional crossfade.</summary>
        public void PlayBGM(MusicTrack track)
        {
            AudioClip clip = ResolveMusicClip(track);
            if (clip == null || musicSource == null) return;

            // Same track already playing — skip
            if (musicSource.isPlaying && musicSource.clip == clip) return;

            if (musicSource.isPlaying)
            {
                StartCoroutine(CrossfadeMusic(clip));
            }
            else
            {
                musicSource.clip = clip;
                musicSource.Play();
            }
        }

        /// <summary>Stop background music with fade-out.</summary>
        public void StopBGM()
        {
            if (musicSource == null || !musicSource.isPlaying) return;
            StartCoroutine(FadeOutMusic());
        }

        /// <summary>Pause background music (e.g. during pause menu).</summary>
        public void PauseBGM()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        /// <summary>Resume paused background music.</summary>
        public void ResumeBGM()
        {
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            {
                musicSource.UnPause();
            }
        }

        private System.Collections.IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            float startVolume = musicSource.volume;

            // Fade out current
            float elapsed = 0f;
            while (elapsed < musicFadeDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (musicFadeDuration * 0.5f));
                yield return null;
            }

            musicSource.Stop();
            musicSource.clip = newClip;

            if (newClip != null)
            {
                musicSource.Play();
            }
            else
            {
                yield break;
            }

            // Fade in new
            elapsed = 0f;
            while (elapsed < musicFadeDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, startVolume, elapsed / (musicFadeDuration * 0.5f));
                yield return null;
            }

            musicSource.volume = startVolume;
        }

        private System.Collections.IEnumerator FadeOutMusic()
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeDuration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / musicFadeDuration);
                yield return null;
            }

            musicSource.Stop();
            musicSource.clip = null;
        }
    }
}