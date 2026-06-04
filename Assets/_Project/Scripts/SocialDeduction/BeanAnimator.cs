using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 豆子人简易动画控制器 — 不依赖 Animator Controller，纯脚本驱动。
    /// 支持：行走弹跳 + 腿部摆动、闲置呼吸浮动、死亡倾倒变灰、击杀前冲。
    /// </summary>
    public sealed class BeanAnimator : MonoBehaviour
    {
        // ── 参数 ──────────────────────────────────────
        [Header("Walk")]
        public float walkBounceAmplitude  = 0.12f;
        public float walkBounceFrequency  = 8f;
        public float legSwingAmplitude    = 22f;
        public float legSwingFrequency    = 8f;

        [Header("Idle")]
        public float idleFloatAmplitude   = 0.03f;
        public float idleFloatFrequency   = 1.8f;

        [Header("Death")]
        public float deathTiltAngle       = 85f;
        public float deathDuration        = 0.5f;

        [Header("Kill")]
        public float killThrustDistance   = 0.35f;
        public float killThrustDuration   = 0.18f;

        // ── 内部状态 ──────────────────────────────────
        private Transform bodyTransform;
        private Transform headTransform;
        private Transform leftLegTransform;
        private Transform rightLegTransform;
        private Transform visorTransform;
        private Transform backpackTransform;

        private float speed;
        private bool isDead;
        private bool isKilling;

        private Vector3 bodyBasePosition;
        private Vector3 headBasePosition;
        private Vector3 leftLegBasePosition;
        private Vector3 rightLegBasePosition;
        private Vector3 visorBasePosition;
        private Vector3 backpackBasePosition;

        private Quaternion leftLegBaseRotation;
        private Quaternion rightLegBaseRotation;

        private List<Material> allMaterials = new List<Material>();
        private List<Color> originalColors   = new List<Color>();

        // ── 公共 API ──────────────────────────────────

        /// <summary>
        /// 绑定角色部件变换并缓存基准位置。
        /// </summary>
        public void Bind(Transform root)
        {
            CacheParts(root);
            CacheBasePositions();
            CacheMaterials(root);
        }

        /// <summary>
        /// 设置当前移动速度（0 = Idle，>0 = Walk）。
        /// </summary>
        public void SetSpeed(float value) => speed = Mathf.Clamp(value, 0f, 10f);

        /// <summary>
        /// 触发死亡动画（协程）。
        /// </summary>
        public void PlayDeath() { if (!isDead) StartCoroutine(DeathRoutine()); }

        /// <summary>
        /// 触发击杀前冲动画（协程）。
        /// </summary>
        public void PlayKill()  { if (!isKilling && !isDead) StartCoroutine(KillRoutine()); }

        public bool IsDead => isDead;

        // ── Unity 生命周期 ──────────────────────────────

        private void Update()
        {
            if (isDead) return;

            if (speed < 0.1f)
            {
                PlayIdleAnimation();
            }
            else
            {
                PlayWalkAnimation();
            }
        }

        // ── 动画逻辑 ──────────────────────────────────

        private void PlayIdleAnimation()
        {
            float t = Time.time * idleFloatFrequency;
            float offset = Mathf.Sin(t) * idleFloatAmplitude;

            if (bodyTransform != null)
                bodyTransform.localPosition = bodyBasePosition + new Vector3(0f, offset, 0f);
            if (headTransform != null)
                headTransform.localPosition = headBasePosition + new Vector3(0f, offset, 0f);
            if (leftLegTransform != null)
                leftLegTransform.localRotation = leftLegBaseRotation;
            if (rightLegTransform != null)
                rightLegTransform.localRotation = rightLegBaseRotation;

            // 面罩随头部浮动
            if (visorTransform != null)
                visorTransform.localPosition = visorBasePosition + new Vector3(0f, offset, 0f);
            if (backpackTransform != null)
                backpackTransform.localPosition = backpackBasePosition + new Vector3(0f, offset, 0f);
        }

        private void PlayWalkAnimation()
        {
            float t = Time.time * walkBounceFrequency;
            float bounce = Mathf.Abs(Mathf.Sin(t)) * walkBounceAmplitude;
            float legAngle = Mathf.Sin(t) * legSwingAmplitude;

            // 身体上下弹跳
            if (bodyTransform != null)
                bodyTransform.localPosition = bodyBasePosition + new Vector3(0f, bounce, 0f);
            if (headTransform != null)
                headTransform.localPosition = headBasePosition + new Vector3(0f, bounce, 0f);
            if (backpackTransform != null)
                backpackTransform.localPosition = backpackBasePosition + new Vector3(0f, bounce, 0f);
            if (visorTransform != null)
                visorTransform.localPosition = visorBasePosition + new Vector3(0f, bounce, 0f);

            // 腿部交替摆动（绕 X 轴旋转）
            if (leftLegTransform != null)
                leftLegTransform.localRotation = leftLegBaseRotation * Quaternion.Euler(legAngle, 0f, 0f);
            if (rightLegTransform != null)
                rightLegTransform.localRotation = rightLegBaseRotation * Quaternion.Euler(-legAngle, 0f, 0f);
        }

        private IEnumerator DeathRoutine()
        {
            isDead = true;
            float elapsed = 0f;
            Quaternion startRot = transform.localRotation;
            Quaternion targetRot = startRot * Quaternion.Euler(0f, 0f, deathTiltAngle);

            // 缓存材质颜色用于渐灰
            for (int i = 0; i < allMaterials.Count; i++)
            {
                originalColors.Add(allMaterials[i].color);
            }

            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / deathDuration;
                // 缓出曲线
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                transform.localRotation = Quaternion.Slerp(startRot, targetRot, ease);

                // 颜色向灰色过渡
                for (int i = 0; i < allMaterials.Count; i++)
                {
                    allMaterials[i].color = Color.Lerp(originalColors[i], Color.gray, ease);
                }

                yield return null;
            }

            transform.localRotation = targetRot;
        }

        private IEnumerator KillRoutine()
        {
            isKilling = true;
            float elapsed = 0f;

            Vector3 startPos = bodyTransform != null ? bodyTransform.localPosition : Vector3.zero;
            Vector3 forward = transform.forward.normalized;
            Vector3 targetPos = startPos + forward * killThrustDistance;

            while (elapsed < killThrustDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / killThrustDuration;
                // 先冲出再弹回
                float thrust = Mathf.Sin(t * Mathf.PI);

                if (bodyTransform != null)
                    bodyTransform.localPosition = Vector3.Lerp(startPos, targetPos, thrust);

                yield return null;
            }

            // 弹回原位
            elapsed = 0f;
            Vector3 currentPos = bodyTransform != null ? bodyTransform.localPosition : startPos;
            while (elapsed < killThrustDuration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (killThrustDuration * 0.6f);

                if (bodyTransform != null)
                    bodyTransform.localPosition = Vector3.Lerp(currentPos, startPos, t);

                yield return null;
            }

            if (bodyTransform != null)
                bodyTransform.localPosition = startPos;

            isKilling = false;
        }

        // ── 初始化 ──────────────────────────────────────

        private void CacheParts(Transform root)
        {
            // 按名称精确查找子部件（BeanCharacterBuilder 中的命名）
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                switch (child.name)
                {
                    case "Body":      bodyTransform = child;      break;
                    case "Head":      headTransform = child;      break;
                    case "LeftLeg":   leftLegTransform = child;   break;
                    case "RightLeg":  rightLegTransform = child;  break;
                    case "Visor":     visorTransform = child;     break;
                    case "Backpack":  backpackTransform = child;  break;
                }
            }
        }

        private void CacheBasePositions()
        {
            if (bodyTransform != null)      bodyBasePosition      = bodyTransform.localPosition;
            if (headTransform != null)      headBasePosition      = headTransform.localPosition;
            if (leftLegTransform != null)   { leftLegBasePosition = leftLegTransform.localPosition; leftLegBaseRotation = leftLegTransform.localRotation; }
            if (rightLegTransform != null)  { rightLegBasePosition = rightLegTransform.localPosition; rightLegBaseRotation = rightLegTransform.localRotation; }
            if (visorTransform != null)     visorBasePosition     = visorTransform.localPosition;
            if (backpackTransform != null)  backpackBasePosition  = backpackTransform.localPosition;
        }

        private void CacheMaterials(Transform root)
        {
            allMaterials.Clear();
            originalColors.Clear();

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer r in renderers)
            {
                Material mat = r.sharedMaterial;
                if (mat != null)
                {
                    allMaterials.Add(mat);
                    originalColors.Add(mat.color);
                }
            }
        }
    }
}