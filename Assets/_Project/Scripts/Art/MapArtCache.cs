using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// 地图场景美术资产缓存。
    /// 从 Resources/Sprites/Map/ 加载门/通风口/犯罪现场/标识牌 sprite，
    /// 加载失败返回 null，调用方回退到程序化绘制。
    ///
    /// 路径约定：
    ///   Resources/Sprites/Map/Doors/door_closed.png
    ///   Resources/Sprites/Map/Doors/door_open.png
    ///   Resources/Sprites/Map/Doors/door_locked.png
    ///   Resources/Sprites/Map/Doors/vent_closed.png
    ///   Resources/Sprites/Map/Doors/vent_open.png
    ///   Resources/Sprites/Map/Doors/security_camera.png
    ///   Resources/Sprites/Map/CrimeScene/body_outline.png
    ///   Resources/Sprites/Map/CrimeScene/caution_tape.png
    ///   Resources/Sprites/Map/CrimeScene/evidence_marker.png
    ///   Resources/Sprites/Map/Signs/room_harbour.png
    /// </summary>
    public static class MapArtCache
    {
        private static readonly Dictionary<string, Sprite> _cache = new();
        private static bool _initialized;

        // ── 门 ──
        public static Sprite DoorClosed { get; private set; }
        public static Sprite DoorOpen { get; private set; }
        public static Sprite DoorLocked { get; private set; }

        // ── 通风口 ──
        public static Sprite VentClosed { get; private set; }
        public static Sprite VentOpen { get; private set; }

        // ── 监控 ──
        public static Sprite SecurityCamera { get; private set; }

        // ── 犯罪现场 ──
        public static Sprite BodyOutline { get; private set; }
        public static Sprite CautionTape { get; private set; }
        public static Sprite EvidenceMarker { get; private set; }

        // ── 房间标识 ──
        public static Sprite RoomSignHarbour { get; private set; }

        public static void Ensure()
        {
            if (_initialized) return;
            _initialized = true;

            // 门
            DoorClosed = Load("Doors/door_closed");
            DoorOpen = Load("Doors/door_open");
            DoorLocked = Load("Doors/door_locked");

            // 通风口
            VentClosed = Load("Doors/vent_closed");
            VentOpen = Load("Doors/vent_open");

            // 监控
            SecurityCamera = Load("Doors/security_camera");

            // 犯罪现场
            BodyOutline = Load("CrimeScene/body_outline");
            CautionTape = Load("CrimeScene/caution_tape");
            EvidenceMarker = Load("CrimeScene/evidence_marker");

            // 房间标识
            RoomSignHarbour = Load("Signs/room_harbour");

            int loaded = 0;
            foreach (var kv in _cache)
                if (kv.Value != null) loaded++;
            Debug.Log($"[MapArtCache] Initialized: {loaded} sprites loaded.");
        }

        /// <summary>按门状态获取 sprite</summary>
        public static Sprite DoorForState(bool open, bool locked)
        {
            if (locked) return DoorLocked;
            return open ? DoorOpen : DoorClosed;
        }

        /// <summary>按通风口状态获取 sprite</summary>
        public static Sprite VentForState(bool open)
        {
            return open ? VentOpen : VentClosed;
        }

        private static Sprite Load(string relativePath)
        {
            string fullPath = $"Sprites/Map/{relativePath}";
            if (_cache.TryGetValue(fullPath, out var cached))
                return cached;

            var tex = Resources.Load<Texture2D>(fullPath);
            if (tex == null)
            {
                _cache[fullPath] = null;
                return null;
            }

            tex.filterMode = FilterMode.Point;
            float ppu = tex.width >= 128 ? 32f : 16f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), ppu);
            _cache[fullPath] = sprite;
            return sprite;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _initialized = false;
        }
    }
}
