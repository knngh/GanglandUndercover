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

        private Sprite _bodyF, _bodyB, _bodyL, _bodyR;
        private Sprite _corpseSprite;

        public void Initialize(OnlinePlayerState state, Sprite bodyF, Sprite bodyB, Sprite bodyL, Sprite bodyR,
            Sprite corpseSprite, Sprite dirArrow)
        {
            State = state;
            _bodyF = bodyF; _bodyB = bodyB; _bodyL = bodyL; _bodyR = bodyR;
            _corpseSprite = corpseSprite;

            BodyRenderer = GetComponent<SpriteRenderer>();
            if (BodyRenderer == null && transform.childCount > 0)
                BodyRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();

            // 方向指示器在 Body 的子节点上
            if (transform.childCount > 1)
                DirRenderer = transform.GetChild(1).GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (State == null) return;

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
                    BodyRenderer.sprite = _bodyF;
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
            Sprite bodySprite = _bodyF;
            if (speed > 0.05f)
            {
                if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                    bodySprite = move.x > 0 ? _bodyR : _bodyL;
                else
                    bodySprite = move.y > 0 ? _bodyB : _bodyF;

                // 行走动画帧切换
                _animTimer += Time.deltaTime;
                if (_animTimer > 0.25f) { _walkFrame = (_walkFrame + 1) % 3; _animTimer = 0f; }
            }
            else
            {
                _walkFrame = 0;
            }

            if (BodyRenderer != null && bodySprite != null)
            {
                BodyRenderer.sprite = bodySprite;
                // 微调缩放模拟行走动画
                float scale = 1f + (_walkFrame == 1 ? 0.04f : _walkFrame == 2 ? -0.02f : 0f);
                BodyRenderer.transform.localScale = new Vector3(0.8f * scale, 0.8f / scale, 1f);
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
    }
}
