using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// 小游戏交互美术资产缓存。
    /// 优先从 Resources/Sprites/MiniGames/{taskType}/ 加载 PNG sprite，
    /// 加载失败时返回 null，调用方回退到程序化绘制。
    ///
    /// 路径约定：
    ///   Resources/Sprites/MiniGames/wire/panel_bg.png
    ///   Resources/Sprites/MiniGames/wire/node_red.png
    ///   Resources/Sprites/MiniGames/keypad/key_0.png ... key_9.png
    ///   Resources/Sprites/MiniGames/scan/scanner_frame.png
    /// </summary>
    public static class MinigameArtCache
    {
        private static readonly Dictionary<string, Sprite> _cache = new();
        private static bool _initialized;

        // ── 通用面板 ──
        public static Sprite PanelBackground { get; private set; }
        public static Sprite PanelHeader { get; private set; }
        public static Sprite SuccessBadge { get; private set; }
        public static Sprite FailBadge { get; private set; }

        // ── Wire Task ──
        public static Sprite WirePanelBg { get; private set; }
        public static Sprite WireNodeRed { get; private set; }
        public static Sprite WireNodeBlue { get; private set; }
        public static Sprite WireNodeGreen { get; private set; }
        public static Sprite WireNodeYellow { get; private set; }
        public static Sprite WireConnector { get; private set; }

        // ── Keypad Task ──
        public static Sprite KeypadPanelBg { get; private set; }
        public static Sprite[] KeypadDigits { get; private set; } // 0-9
        public static Sprite KeypadClear { get; private set; }
        public static Sprite KeypadEnter { get; private set; }
        public static Sprite KeypadLCD { get; private set; }

        // ── Scan Task ──
        public static Sprite ScanPanelBg { get; private set; }
        public static Sprite ScanFrame { get; private set; }
        public static Sprite ScanFingerprint { get; private set; }
        public static Sprite ScanIdCard { get; private set; }
        public static Sprite ScanLine { get; private set; }

        // ── Download Task ──
        public static Sprite DownloadPanelBg { get; private set; }
        public static Sprite DownloadServerIcon { get; private set; }
        public static Sprite DownloadDataPacket { get; private set; }
        public static Sprite DownloadProgressBar { get; private set; }

        // ── Memory Task ──
        public static Sprite MemoryCardBack { get; private set; }
        public static Sprite[] MemoryCardFaces { get; private set; } // 8 icons

        // ── SwipeCard Task ──
        public static Sprite SwipeSlot { get; private set; }
        public static Sprite SwipeCard { get; private set; }
        public static Sprite SwipeTrack { get; private set; }

        /// <summary>
        /// 初始化所有小游戏 sprite（CC0 优先，null 表示无资源）。
        /// 在 Sprite2DAssetCache.Ensure() 之后调用。
        /// </summary>
        public static void Ensure()
        {
            if (_initialized) return;
            _initialized = true;

            // 通用
            PanelBackground = Load("shared/panel_bg");
            PanelHeader = Load("shared/panel_header");
            SuccessBadge = Load("shared/success_badge");
            FailBadge = Load("shared/fail_badge");

            // Wire
            WirePanelBg = Load("wire/panel_bg");
            WireNodeRed = Load("wire/node_red");
            WireNodeBlue = Load("wire/node_blue");
            WireNodeGreen = Load("wire/node_green");
            WireNodeYellow = Load("wire/node_yellow");
            WireConnector = Load("wire/connector");

            // Keypad
            KeypadPanelBg = Load("keypad/panel_bg");
            KeypadDigits = new Sprite[10];
            for (int i = 0; i < 10; i++)
                KeypadDigits[i] = Load($"keypad/key_{i}");
            KeypadClear = Load("keypad/key_clear");
            KeypadEnter = Load("keypad/key_enter");
            KeypadLCD = Load("keypad/lcd_frame");

            // Scan
            ScanPanelBg = Load("scan/panel_bg");
            ScanFrame = Load("scan/scanner_frame");
            ScanFingerprint = Load("scan/fingerprint");
            ScanIdCard = Load("scan/id_card");
            ScanLine = Load("scan/scan_line");

            // Download
            DownloadPanelBg = Load("download/panel_bg");
            DownloadServerIcon = Load("download/server_icon");
            DownloadDataPacket = Load("download/data_packet");
            DownloadProgressBar = Load("download/progress_bar");

            // Memory
            MemoryCardBack = Load("memory/card_back");
            MemoryCardFaces = new Sprite[8];
            for (int i = 0; i < 8; i++)
                MemoryCardFaces[i] = Load($"memory/icon_{i}");

            // SwipeCard
            SwipeSlot = Load("swipe/card_slot");
            SwipeCard = Load("swipe/card");
            SwipeTrack = Load("swipe/track");

            int loaded = CountLoaded();
            Debug.Log($"[MinigameArtCache] Initialized: {loaded} sprites loaded from Resources.");
        }

        /// <summary>按任务类型获取面板背景（有则用资源，无则 null）</summary>
        public static Sprite PanelBgFor(string taskType)
        {
            return taskType switch
            {
                "wire" => WirePanelBg,
                "keypad" => KeypadPanelBg,
                "scan" => ScanPanelBg,
                "download" => DownloadPanelBg,
                _ => PanelBackground
            };
        }

        /// <summary>获取指定颜色的线缆节点 sprite</summary>
        public static Sprite WireNodeForColor(Color color)
        {
            // 按最近颜色匹配
            if (ColorDistance(color, new Color(0.91f, 0.30f, 0.24f)) < 0.3f) return WireNodeRed;
            if (ColorDistance(color, new Color(0.20f, 0.60f, 0.86f)) < 0.3f) return WireNodeBlue;
            if (ColorDistance(color, new Color(0.35f, 0.78f, 0.36f)) < 0.3f) return WireNodeGreen;
            if (ColorDistance(color, new Color(0.95f, 0.85f, 0.20f)) < 0.3f) return WireNodeYellow;
            return WireNodeRed;
        }

        // ── 内部 ──

        private static Sprite Load(string relativePath)
        {
            string fullPath = $"Sprites/MiniGames/{relativePath}";
            if (_cache.TryGetValue(fullPath, out var cached))
                return cached;

            var tex = Resources.Load<Texture2D>(fullPath);
            if (tex == null)
            {
                _cache[fullPath] = null;
                return null;
            }

            tex.filterMode = FilterMode.Point;
            float ppu = tex.width >= 128 ? 64f : 32f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), ppu);
            _cache[fullPath] = sprite;
            return sprite;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static int CountLoaded()
        {
            int count = 0;
            foreach (var kv in _cache)
                if (kv.Value != null) count++;
            return count;
        }

        /// <summary>清除缓存（场景切换时）</summary>
        public static void ClearCache()
        {
            _cache.Clear();
            _initialized = false;
        }
    }
}
