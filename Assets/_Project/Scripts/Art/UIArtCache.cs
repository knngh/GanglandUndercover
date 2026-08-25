using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// UI 美术资产缓存（noir/neon 风格）。
    /// 从 Resources/Sprites/UI/ 加载 PNG sprite，加载失败返回 null，
    /// 调用方回退到程序化 DrawPanel / 纯色。
    ///
    /// 路径约定：
    ///   Resources/Sprites/UI/Buttons/button_noir_clean.png
    ///   Resources/Sprites/UI/Panels/panel_noir_clean.png
    ///   Resources/Sprites/UI/ProgressBar/progress_clean.png
    ///   Resources/Sprites/UI/Meeting/meeting_panel_clean.png
    ///   Resources/Sprites/UI/Meeting/vote_card_clean.png
    /// </summary>
    public static class UIArtCache
    {
        private static readonly Dictionary<string, Sprite> _cache = new();
        private static bool _initialized;

        // ── 基础组件 ──
        public static Sprite ButtonNormal { get; private set; }
        public static Sprite ButtonRound { get; private set; }
        public static Sprite PanelFrame { get; private set; }
        public static Sprite ProgressBar { get; private set; }

        // ── 破坏图标 ──
        public static Sprite IconSabotageBlackout { get; private set; }
        public static Sprite IconSabotageLockdown { get; private set; }
        public static Sprite IconSabotageCommJam { get; private set; }
        public static Sprite IconSabotageEvidence { get; private set; }
        public static Sprite IconSabotagePatrol { get; private set; }

        // ── 任务图标 ──
        public static Sprite IconTaskWire { get; private set; }
        public static Sprite IconTaskKeypad { get; private set; }
        public static Sprite IconTaskScan { get; private set; }
        public static Sprite IconTaskDownload { get; private set; }
        public static Sprite IconTaskMemory { get; private set; }
        public static Sprite IconTaskSwipe { get; private set; }

        // ── 会议/投票 ──
        public static Sprite MeetingTableBg { get; private set; }
        public static Sprite VoteCard { get; private set; }
        public static Sprite EjectFrame { get; private set; }

        public static void Ensure()
        {
            if (_initialized) return;
            _initialized = true;

            // 基础组件
            // Runtime UI uses only reviewed, no-watermark assets. The larger generated
            // previews remain in the art workspace but are intentionally not loaded.
            ButtonNormal = Load("Buttons/button_noir_clean");
            ButtonRound = Load("Buttons/button_round_gloss");
            PanelFrame = Load("Panels/panel_noir_clean");
            ProgressBar = Load("ProgressBar/progress_clean");

            // Reviewed derivatives of licensed LimeZu and CC0 device sprites.
            IconSabotageBlackout = Load("Icons/sabotage_blackout_clean");
            IconSabotageLockdown = Load("Icons/sabotage_lockdown_clean");
            IconSabotageCommJam = Load("Icons/sabotage_commjam_clean");
            IconSabotageEvidence = Load("Icons/sabotage_evidence_clean");
            IconSabotagePatrol = Load("Icons/sabotage_patrol_clean");

            IconTaskWire = Load("Icons/task_wire_clean");
            IconTaskKeypad = Load("Icons/task_keypad_clean");
            IconTaskScan = Load("Icons/task_scan_clean");
            IconTaskDownload = Load("Icons/task_download_clean");
            IconTaskMemory = Load("Icons/task_memory_clean");
            IconTaskSwipe = Load("Icons/task_swipe_clean");

            // 会议/投票
            MeetingTableBg = Load("Meeting/meeting_panel_clean");
            VoteCard = Load("Meeting/vote_card_clean");
            EjectFrame = Load("Meeting/meeting_panel_clean");

            int loaded = 0;
            foreach (var kv in _cache)
                if (kv.Value != null) loaded++;
            Debug.Log($"[UIArtCache] Initialized: {loaded} sprites loaded.");
        }

        /// <summary>按破坏类型获取图标</summary>
        public static Sprite SabotageIcon(string type)
        {
            return type switch
            {
                "blackout" or "Blackout" => IconSabotageBlackout,
                "lockdown" or "Lockdown" => IconSabotageLockdown,
                "commjam" or "CommJam" or "Communications" => IconSabotageCommJam,
                "evidence" or "EvidenceLeak" => IconSabotageEvidence,
                "patrol" or "PatrolAlert" => IconSabotagePatrol,
                _ => null
            };
        }

        /// <summary>按任务模板名获取图标</summary>
        public static Sprite TaskIcon(string taskType)
        {
            return taskType switch
            {
                "wire" or "WireTask" => IconTaskWire,
                "keypad" or "KeypadTask" => IconTaskKeypad,
                "scan" or "ScanTask" => IconTaskScan,
                "download" or "DownloadTask" => IconTaskDownload,
                "memory" or "MemoryTask" => IconTaskMemory,
                "swipe" or "SwipeCardTask" => IconTaskSwipe,
                _ => null
            };
        }

        private static Sprite Load(string relativePath)
        {
            string fullPath = $"Sprites/UI/{relativePath}";
            if (_cache.TryGetValue(fullPath, out var cached))
                return cached;

            var tex = Resources.Load<Texture2D>(fullPath);
            if (tex == null)
            {
                _cache[fullPath] = null;
                return null;
            }

            tex.filterMode = FilterMode.Point;
            float ppu = tex.width >= 256 ? 128f : 64f;
            Vector4 border = StretchableBorder(relativePath);
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect,
                border);
            _cache[fullPath] = sprite;
            return sprite;
        }

        private static Vector4 StretchableBorder(string relativePath)
        {
            if (relativePath == "Buttons/button_noir_clean"
                || relativePath.StartsWith("Panels/", System.StringComparison.Ordinal)
                || relativePath.StartsWith("Meeting/", System.StringComparison.Ordinal))
            {
                return new Vector4(2f, 2f, 2f, 2f);
            }

            return Vector4.zero;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _initialized = false;
        }
    }
}
