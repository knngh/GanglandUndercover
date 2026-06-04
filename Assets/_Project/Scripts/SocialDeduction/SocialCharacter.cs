using System;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class SocialCharacter : MonoBehaviour
    {
        private static readonly Color GangColor = new Color(0.55f, 0.12f, 0.1f, 1f);
        private static readonly Color PoliceColor = new Color(0.1f, 0.28f, 0.62f, 1f);
        private static readonly Color UndercoverColor = new Color(0.88f, 0.66f, 0.22f, 1f);
        private static readonly Color MoleColor = new Color(0.18f, 0.48f, 0.42f, 1f);
        private static readonly Color DeadColor = new Color(0.12f, 0.12f, 0.12f, 1f);

        private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
        private static readonly int AnimDeadHash = Animator.StringToHash("Dead");
        private static readonly int AnimActionHash = Animator.StringToHash("Action");

        private Renderer[] renderers;
        private Animator animator;
        private AnimationController animController;
        private TextMesh label;
        private Color visualColor;
        private Material[] materials;

        /// <summary>
        /// 死亡动画完成后的回调，通常用于生成 BodyVisual / BodyMarker。
        /// </summary>
        public event Action<SocialCharacter> DeathAnimationComplete;

        public string CharacterName { get; private set; }
        public SocialRole Role { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public bool isInsideVent;
        public Vector2 BotDirection { get; set; }
        public Vector3 BotTarget { get; set; }
        public float BotDecisionTimer { get; set; }
        public float BotActionCooldown { get; set; }
        public bool HasBotTarget { get; set; }
        public float MoveSpeed { get; set; } = 2.5f;

        public void Bind(string characterName, SocialRole role, bool isPlayer)
        {
            CharacterName = characterName;
            Role = role;
            IsPlayer = isPlayer;
            IsAlive = true;
            visualColor = isPlayer ? GetPlayerColor(role) : GetCivilianColor(characterName);

            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>(true);

            // 初始化 AnimationController
            animController = GetComponent<AnimationController>();
            if (animController == null)
                animController = gameObject.AddComponent<AnimationController>();
            animController.Bind(animator);

            if (renderers != null && renderers.Length > 0)
            {
                Shader shader = FindColorShader();
                materials = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    materials[i] = new Material(shader);
                    renderers[i].sharedMaterial = materials[i];
                }
            }

            label = GetComponentInChildren<TextMesh>();
            RefreshVisual();
        }

        /// <summary>
        /// 针对预制体角色的初始化：保留原始材质引用（不替换），材质颜色由外部 Tint 着色。
        /// RefreshVisual 仍可通过 materials 数组控制 alive/dead 颜色变化。
        /// </summary>
        public void BindForPrefab(string characterName, SocialRole role, bool isPlayer)
        {
            CharacterName = characterName;
            Role = role;
            IsPlayer = isPlayer;
            IsAlive = true;
            visualColor = isPlayer ? GetPlayerColor(role) : GetCivilianColor(characterName);

            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>(true);

            animController = GetComponent<AnimationController>();
            if (animController == null)
                animController = gameObject.AddComponent<AnimationController>();
            animController.Bind(animator);

            if (renderers != null && renderers.Length > 0)
            {
                materials = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    materials[i] = renderers[i].material;
                }
            }

            label = GetComponentInChildren<TextMesh>();
        }

        /// <summary>
        /// 轻量初始化（Online 模式）：仅绑定 Animator，不改变材质/颜色/label。
        /// 用于 OnlineMatchController 中通过 SocialCharacter 封装驱动动画参数。
        /// </summary>
        public void BindAnimator(Animator externalAnimator)
        {
            animator = externalAnimator;

            animController = GetComponent<AnimationController>();
            if (animController == null)
                animController = gameObject.AddComponent<AnimationController>();
            animController.Bind(animator);
        }

        public void Kill()
        {
            IsAlive = false;
            HasBotTarget = false;

            if (animator != null)
            {
                animator.SetBool(AnimDeadHash, true);
            }

            // 通过 AnimationController 播放死亡倒地动画序列
            if (animController != null)
            {
                animController.PlayDeathSequence(1.5f, () =>
                {
                    DeathAnimationComplete?.Invoke(this);
                });
            }
            else
            {
                DeathAnimationComplete?.Invoke(this);
            }

            RefreshVisual();
        }

        public void SetMoveSpeed(float speed)
        {
            if (animator != null)
            {
                animator.SetFloat(AnimSpeedHash, speed);
            }
        }

        public void TriggerAction()
        {
            if (animator != null)
            {
                animator.SetTrigger(AnimActionHash);
            }
        }

        public AnimationController AnimController => animController;

        /// <summary>
        /// 设置黑屏透明度回调，用于通风管过渡。
        /// </summary>
        public void SetBlackoutCallback(Action<float> callback)
        {
            if (animController != null)
                animController.SetBlackoutCallback(callback);
        }

        /// <summary>
        /// 播放通风管瞬移过渡：黑屏→传送→淡入。
        /// </summary>
        public void PlayVentTransition(Vector3 destination, Action onMidTeleport)
        {
            if (animController != null)
                animController.PlayVentTransition(destination, onMidTeleport);
        }

        public void RefreshVisual()
        {
            if (materials != null)
            {
                Color targetColor = IsAlive ? visualColor : DeadColor;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        materials[i].color = targetColor;
                    }
                }
            }

            if (label != null)
            {
                label.text = CharacterName;
                label.color = IsPlayer ? new Color(1f, 0.9f, 0.35f, 1f) : Color.white;
            }
        }

        private static Color GetPlayerColor(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    return GangColor;
                case SocialRole.Police:
                    return PoliceColor;
                case SocialRole.Mole:
                    return MoleColor;
                default:
                    return UndercoverColor;
            }
        }

        private static Color GetCivilianColor(string characterName)
        {
            switch (characterName)
            {
                case "巡警陈":
                    return new Color(0.18f, 0.42f, 0.68f, 1f);
                case "技侦周":
                    return new Color(0.15f, 0.42f, 0.38f, 1f);
                case "线人林":
                    return new Color(0.74f, 0.54f, 0.22f, 1f);
                case "疤脸":
                    return new Color(0.52f, 0.24f, 0.42f, 1f);
                default:
                    return new Color(0.52f, 0.52f, 0.48f, 1f);
            }
        }

        private static Shader FindColorShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDestroy()
        {
            if (materials == null)
            {
                return;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(materials[i]);
                }
                else
                {
                    DestroyImmediate(materials[i]);
                }
            }
        }
    }
}
