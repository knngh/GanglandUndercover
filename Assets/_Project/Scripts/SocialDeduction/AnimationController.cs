using System;
using System.Collections;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    /// <summary>
    /// 角色动画控制器：基于位置差自动计算移动速度并驱动 Animator 的 Speed 参数，
    /// 管理击杀 Action 触发、死亡倒地序列、通风管瞬移黑屏过渡。
    ///
    /// Speed 阈值映射：
    ///   0.0 = Idle（静止 / 速度 &lt; 0.1m/s）
    ///   0.5 = Walk（0.1 ~ 2.0m/s）
    ///   1.0 = Jog（&gt; 2.0m/s）
    /// </summary>
    public sealed class AnimationController : MonoBehaviour
    {
        private static readonly int AnimSpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AnimDeadHash   = Animator.StringToHash("Dead");
        private static readonly int AnimActionHash = Animator.StringToHash("Action");

        private Animator animator;
        private Vector3 lastPosition;

        private bool isDead;
        private Coroutine deathRoutine;
        private Coroutine ventRoutine;

        private Action<float> onBlackoutAlpha;

        /// <summary>是否正在播放死亡动画序列。</summary>
        public bool IsPlayingDeath { get; private set; }

        /// <summary>是否正在通风管过渡中。</summary>
        public bool IsInVentTransition => ventRoutine != null;

        public void Bind(Animator target)
        {
            animator = target;
            lastPosition = transform.position;
        }

        /// <summary>
        /// 绑定黑屏回调（对应 SocialPrototypeController / OnlineMatchController 中的黑屏遮罩）。
        /// 在通风管过渡期间逐帧回调 alpha（0~1）。
        /// </summary>
        public void SetBlackoutCallback(Action<float> callback)
        {
            onBlackoutAlpha = callback;
        }

        private void Update()
        {
            if (animator == null || isDead) return;

            float delta = Vector3.Distance(transform.position, lastPosition);
            float speedRaw = delta / Mathf.Max(Time.deltaTime, 0.0001f);

            float normalizedSpeed;
            if (speedRaw < 0.1f)
                normalizedSpeed = 0f;   // Idle
            else if (speedRaw < 2.0f)
                normalizedSpeed = 0.5f; // Walk
            else
                normalizedSpeed = 1f;   // Jog

            animator.SetFloat(AnimSpeedHash, normalizedSpeed);
            lastPosition = transform.position;
        }

        /// <summary>
        /// 触发短暂 Action 动画，自动在 ExitTime 后回到 Idle（由 Controller 的 Action→Idle 过渡保证）。
        /// </summary>
        public void TriggerAction()
        {
            if (animator != null && !isDead)
                animator.SetTrigger(AnimActionHash);
        }

        /// <summary>
        /// 播放死亡倒地动画序列。duration 秒后触发 onComplete（生成尸体 / BodyVisual）。
        /// </summary>
        public Coroutine PlayDeathSequence(float duration = 1.5f, Action onComplete = null)
        {
            if (isDead) return null;
            isDead = true;
            IsPlayingDeath = true;

            if (deathRoutine != null) StopCoroutine(deathRoutine);
            deathRoutine = StartCoroutine(DeathRoutine(duration, onComplete));
            return deathRoutine;
        }

        private IEnumerator DeathRoutine(float duration, Action onComplete)
        {
            // 触发死亡动画
            if (animator != null)
                animator.SetBool(AnimDeadHash, true);

            // 等待倒地动画播完
            yield return new WaitForSeconds(duration);

            IsPlayingDeath = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// 播放通风管瞬移过渡：黑屏 → 传送 → 淡入。
        /// 在中点调用 onMidTeleport（执行实际传送），totalDuration 为总时长。
        /// </summary>
        public Coroutine PlayVentTransition(Vector3 destination, Action onMidTeleport, float totalDuration = 0.5f)
        {
            if (ventRoutine != null) StopCoroutine(ventRoutine);
            ventRoutine = StartCoroutine(VentRoutine(destination, onMidTeleport, totalDuration));
            return ventRoutine;
        }

        private IEnumerator VentRoutine(Vector3 destination, Action onMidTeleport, float totalDuration)
        {
            float half = totalDuration * 0.5f;
            float elapsed = 0f;

            // 渐入黑屏
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / half);
                onBlackoutAlpha?.Invoke(alpha);
                yield return null;
            }

            onBlackoutAlpha?.Invoke(1f);

            // 中点传送
            onMidTeleport?.Invoke();
            transform.position = destination;
            lastPosition = destination;

            // 渐出黑屏
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / half);
                onBlackoutAlpha?.Invoke(alpha);
                yield return null;
            }

            onBlackoutAlpha?.Invoke(0f);
            ventRoutine = null;
        }

        private void OnDestroy()
        {
            if (deathRoutine != null) StopCoroutine(deathRoutine);
            if (ventRoutine != null) StopCoroutine(ventRoutine);
        }
    }
}