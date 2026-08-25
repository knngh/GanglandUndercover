using System.Collections.Generic;

namespace GanglandUndercover.Art
{
    public sealed class VfxEffectProfile
    {
        public static readonly VfxEffectProfile Blackout = new VfxEffectProfile(
            "blackout",
            12,
            96,
            96,
            6f,
            "Global sabotage blackout overlay",
            VFXSheetPlayer.PlayMode.Loop,
            500,
            "P2",
            "map readability and emergency-light contrast",
            "Use a full-field dim pass with readable player silhouettes and cyan power arcs.");

        public static readonly VfxEffectProfile CommsJam = new VfxEffectProfile(
            "comms_jam",
            8,
            64,
            64,
            14f,
            "Communication sabotage glitch overlay",
            VFXSheetPlayer.PlayMode.Loop,
            502,
            "P2",
            "glitch cadence and screen noise density",
            "Use deterministic sparse bands and noise so interference does not hide task prompts.");

        public static readonly VfxEffectProfile DoorLock = new VfxEffectProfile(
            "door_lock",
            6,
            48,
            48,
            10f,
            "Lockdown door-state overlay",
            VFXSheetPlayer.PlayMode.Loop,
            501,
            "P2",
            "door icon silhouette and red warning edge",
            "Use a compact lock plate plus warning edge instead of only a full-screen X.");

        public static readonly VfxEffectProfile EmergencyLight = new VfxEffectProfile(
            "emergency_light",
            8,
            48,
            48,
            12f,
            "Blackout emergency-light pulse",
            VFXSheetPlayer.PlayMode.Loop,
            505,
            "P3",
            "secondary pulse contrast",
            "Tune red pulse so it supports blackout without becoming a combat cue.");

        public static readonly VfxEffectProfile EvidenceLeak = new VfxEffectProfile(
            "evidence_leak",
            12,
            48,
            48,
            9f,
            "Evidence leak clue pulse",
            VFXSheetPlayer.PlayMode.Loop,
            499,
            "P1",
            "evidence pulse visibility over floor props",
            "Check the first and brightest frames against busy evidence rooms.");

        public static readonly VfxEffectProfile Hit = new VfxEffectProfile(
            "hit",
            4,
            32,
            32,
            18f,
            "Instant hit impact feedback",
            VFXSheetPlayer.PlayMode.OneShot,
            506,
            "P1",
            "short one-shot readability at character scale",
            "Confirm the 32px flash is visible over every character profession skin.");

        public static readonly VfxEffectProfile Kill = new VfxEffectProfile(
            "kill",
            10,
            128,
            128,
            15f,
            "Kill blood impact and body drop accent",
            VFXSheetPlayer.PlayMode.OneShot,
            504,
            "P1",
            "top-layer combat scale and opacity",
            "Check scale against corpse marker and local player silhouette.");

        public static readonly VfxEffectProfile PatrolAlert = new VfxEffectProfile(
            "patrol_alert",
            4,
            64,
            64,
            6f,
            "Patrol alert warning overlay",
            VFXSheetPlayer.PlayMode.Loop,
            503,
            "P2",
            "warning cadence and color separation",
            "Use amber patrol-search iconography separated from red lockdown and emergency cues.");

        private static readonly VfxEffectProfile[] Profiles =
        {
            Blackout,
            CommsJam,
            DoorLock,
            EmergencyLight,
            EvidenceLeak,
            Hit,
            Kill,
            PatrolAlert,
        };

        public static IReadOnlyList<VfxEffectProfile> All => Profiles;

        public readonly string Name;
        public readonly int FrameCount;
        public readonly int Width;
        public readonly int Height;
        public readonly float FramesPerSecond;
        public readonly string RuntimeUse;
        public readonly VFXSheetPlayer.PlayMode PlaybackMode;
        public readonly int SortingOrder;
        public readonly string PolishPriority;
        public readonly string PolishFocus;
        public readonly string FirstAdjustment;

        public string PlaybackModeName => PlaybackMode == VFXSheetPlayer.PlayMode.Loop ? "Loop" : "OneShot";
        public float DurationSeconds => FramesPerSecond > 0f ? FrameCount / FramesPerSecond : 0f;

        private VfxEffectProfile(
            string name,
            int frameCount,
            int width,
            int height,
            float framesPerSecond,
            string runtimeUse,
            VFXSheetPlayer.PlayMode playbackMode,
            int sortingOrder,
            string polishPriority,
            string polishFocus,
            string firstAdjustment)
        {
            Name = name;
            FrameCount = frameCount;
            Width = width;
            Height = height;
            FramesPerSecond = framesPerSecond;
            RuntimeUse = runtimeUse;
            PlaybackMode = playbackMode;
            SortingOrder = sortingOrder;
            PolishPriority = polishPriority;
            PolishFocus = polishFocus;
            FirstAdjustment = firstAdjustment;
        }
    }
}
