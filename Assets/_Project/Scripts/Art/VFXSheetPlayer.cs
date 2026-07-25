using System;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// 帧动画播放器：从 Resources/Sprites/VFX/{effect}/ 加载序列帧并播放。
    /// 支持 Loop（破坏遮罩循环）和 OneShot（击杀/命中一次性）两种模式。
    /// 若 Resources 加载失败则返回 null，调用方回退到程序化特效。
    /// </summary>
    public sealed class VFXSheetPlayer : MonoBehaviour
    {
        public enum PlayMode { Loop, OneShot }

        // ── 静态帧缓存（避免重复 Resources.Load）──
        private static readonly Dictionary<string, Sprite[]> _cache = new();

        [SerializeField] private float _fps = 12f;
        [SerializeField] private PlayMode _mode = PlayMode.OneShot;

        private Sprite[] _frames;
        private SpriteRenderer _sr;
        private int _currentFrame;
        private float _timer;
        private bool _playing;
        private bool _hasFrames;

        /// <summary>播放完毕回调（OneShot 模式）</summary>
        public event Action OnComplete;

        public bool IsPlaying => _playing;
        public bool HasFrames => _hasFrames;
        public int FrameCount => _frames != null ? _frames.Length : 0;
        public int CurrentFrameIndex => _currentFrame;
        public float FramesPerSecond => _fps;

        /// <summary>
        /// 初始化并加载指定特效的帧序列。
        /// effectName 对应 Resources/Sprites/VFX/{effectName}/ 目录。
        /// 返回 true 表示加载成功，false 表示无资源（调用方应回退程序化）。
        /// </summary>
        public bool Init(string effectName, PlayMode mode = PlayMode.OneShot, float fps = 12f)
        {
            _mode = mode;
            _fps = Mathf.Max(0.01f, fps);
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _frames = LoadFrames(effectName);
            _hasFrames = _frames != null && _frames.Length > 0;
            return _hasFrames;
        }

        /// <summary>开始/重新播放</summary>
        public void Play()
        {
            if (!_hasFrames) return;
            _currentFrame = 0;
            _timer = 0f;
            _playing = true;
            _sr.enabled = true;
            _sr.sprite = _frames[0];
        }

        /// <summary>停止播放并隐藏</summary>
        public void Stop()
        {
            _playing = false;
            if (_sr != null) _sr.enabled = false;
        }

        /// <summary>设置颜色（用于叠加/渐隐）</summary>
        public void SetColor(Color color)
        {
            if (_sr != null) _sr.color = color;
        }

        /// <summary>设置 sortingOrder</summary>
        public void SetSortingOrder(int order)
        {
            if (_sr != null) _sr.sortingOrder = order;
        }

        private void Update()
        {
            if (!_playing || !_hasFrames) return;

            _timer += Time.deltaTime;
            float frameDuration = 1f / _fps;

            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _currentFrame++;

                if (_currentFrame >= _frames.Length)
                {
                    if (_mode == PlayMode.Loop)
                    {
                        _currentFrame = 0;
                    }
                    else
                    {
                        _currentFrame = _frames.Length - 1;
                        _playing = false;
                        OnComplete?.Invoke();
                        return;
                    }
                }

                _sr.sprite = _frames[_currentFrame];
            }
        }

        // ── 静态帧加载 ──

        /// <summary>
        /// 从 Resources/Sprites/VFX/{effectName}/ 加载所有帧 Texture2D，
        /// 按文件名排序后转换为 Sprite[]。
        /// </summary>
        private static Sprite[] LoadFrames(string effectName)
        {
            if (_cache.TryGetValue(effectName, out var cached))
                return cached;

            string path = $"Sprites/VFX/{effectName}";
            var textures = Resources.LoadAll<Texture2D>(path);

            if (textures == null || textures.Length == 0)
            {
                _cache[effectName] = null;
                return null;
            }

            // 按名称排序确保帧顺序正确（kill_00, kill_01, ...）
            Array.Sort(textures, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            var sprites = new Sprite[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                var tex = textures[i];
                float ppu = tex.width >= 128 ? 32f : 16f;
                sprites[i] = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    ppu);
            }

            _cache[effectName] = sprites;
            return sprites;
        }

        /// <summary>预加载指定特效帧（可在匹配初始化时调用减少首次卡顿）</summary>
        public static void Preload(string effectName)
        {
            LoadFrames(effectName);
        }

        /// <summary>清除所有缓存（场景切换时调用）</summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
