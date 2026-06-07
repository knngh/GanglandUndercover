using GanglandUndercover.Online;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// E2 角色动画控制器。
    /// 管理玩家 2D sprite 切换：
    /// - 根据移动方向切换 4 向 sprite
    /// - 根据 alive/dead 切换身体/尸体 sprite
    /// - 鬼魂模式半透明
    ///
    /// 挂载到每个玩家角色的 2D Adapter GameObject 上。
    /// </summary>
    public sealed class CharacterAnimController : MonoBehaviour
    {
        public SpriteRenderer BodyRenderer;
        public SpriteRenderer DirRenderer;
        public OnlinePlayerState State;

        private Vector3 _lastPos;
        private float _animTimer;
        private int _walkFrame;

        private Sprite[] _frontFrames;
        private Sprite[] _backFrames;
        private Sprite[] _leftFrames;
        private Sprite[] _rightFrames;
        private Sprite _corpseSprite;

        public void Initialize(OnlinePlayerState state, Sprite2DAssetCache.ProfSpriteSet spriteSet,
            Sprite corpseSprite, Sprite dirArrow)
        {
            State = state;
            _frontFrames = BuildFrames(spriteSet?.Front_Frame0, spriteSet?.Front_Frame1, spriteSet?.Front_Frame2, Sprite2DAssetCache.CharBody_Front);
            _backFrames = BuildFrames(spriteSet?.Back_Frame0, spriteSet?.Back_Frame1, spriteSet?.Back_Frame2, Sprite2DAssetCache.CharBody_Back);
            _leftFrames = BuildFrames(spriteSet?.Left_Frame0, spriteSet?.Left_Frame1, spriteSet?.Left_Frame2, Sprite2DAssetCache.CharBody_Left);
            _rightFrames = BuildFrames(spriteSet?.Right_Frame0, spriteSet?.Right_Frame1, spriteSet?.Right_Frame2, Sprite2DAssetCache.CharBody_Right);
            _corpseSprite = corpseSprite ?? spriteSet?.Dead ?? Sprite2DAssetCache.CorpseMarker;

            BodyRenderer = GetComponent<SpriteRenderer>();
            if (BodyRenderer == null && transform.childCount > 0)
                BodyRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();

            // 方向指示器在 Body 的子节点上
            if (transform.childCount > 1)
                DirRenderer = transform.GetChild(1).GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (State.ClientId == 0) return;

            // 死亡 → 尸体 sprite
            if (!State.Alive)
            {
                if (BodyRenderer != null && _corpseSprite != null)
                {
                    BodyRenderer.sprite = _corpseSprite;
                    BodyRenderer.color = ProfessionPalette.CorpseColor;
                }
                if (DirRenderer != null) DirRenderer.enabled = false;
                return;
            }

            // 鬼魂模式
            if (State.IsGhost)
            {
                if (BodyRenderer != null)
                {
                    BodyRenderer.sprite = FrameAt(_frontFrames, 0);
                    BodyRenderer.color = ProfessionPalette.GhostColor;
                }
                if (DirRenderer != null) DirRenderer.enabled = false;
                return;
            }

            // 正常存活状态
            Vector3 move = State.Position - _lastPos;
            _lastPos = State.Position;
            float speed = move.magnitude / Mathf.Max(0.016f, Time.deltaTime);

            // 面向方向
            Sprite[] bodyFrames = _frontFrames;
            if (speed > 0.05f)
            {
                if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                    bodyFrames = move.x > 0 ? _rightFrames : _leftFrames;
                else
                    bodyFrames = move.y > 0 ? _backFrames : _frontFrames;

                // 行走动画帧切换
                _animTimer += Time.deltaTime;
                if (_animTimer > 0.25f) { _walkFrame = (_walkFrame + 1) % 3; _animTimer = 0f; }
            }
            else
            {
                _walkFrame = 0;
            }

            Sprite bodySprite = FrameAt(bodyFrames, _walkFrame);
            if (BodyRenderer != null && bodySprite != null)
            {
                BodyRenderer.sprite = bodySprite;
                BodyRenderer.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            }

            // 方向箭头跟随移动方向
            if (DirRenderer != null)
            {
                DirRenderer.enabled = speed > 0.01f;
                if (speed > 0.01f)
                {
                    float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg - 90f;
                    DirRenderer.transform.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
        }

        private static Sprite[] BuildFrames(Sprite idle, Sprite walk0, Sprite walk1, Sprite fallback)
        {
            Sprite baseFrame = idle != null ? idle : fallback;
            return new[]
            {
                baseFrame,
                walk0 != null ? walk0 : baseFrame,
                walk1 != null ? walk1 : baseFrame
            };
        }

        private static Sprite FrameAt(Sprite[] frames, int index)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }
}
