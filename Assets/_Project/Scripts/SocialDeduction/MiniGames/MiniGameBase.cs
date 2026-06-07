using UnityEngine;

namespace GanglandUndercover.SocialDeduction.MiniGames
{
    /// <summary>
    /// 小游戏抽象基类，定义 Show / Hide / OnComplete 回调。
    /// 所有 Among Us 风格小游戏（连线、刷卡、记忆）继承此类。
    /// </summary>
    public abstract class MiniGameBase : MonoBehaviour
    {
        /// <summary>
        /// 小游戏成功完成时触发（由子类调用）。
        /// </summary>
        public System.Action<MiniGameBase> OnComplete;

        /// <summary>
        /// 小游戏失败或取消时触发（由子类调用）。
        /// </summary>
        public System.Action<MiniGameBase> OnCancel;

        /// <summary>
        /// 显示小游戏 UI（由 TaskStation 或 Controller 调用）。
        /// </summary>
        public abstract void Show();

        /// <summary>
        /// 隐藏并清理小游戏 UI。
        /// </summary>
        public abstract void Hide();

        protected void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 通知外部：小游戏成功完成。
        /// </summary>
        protected void Complete()
        {
            OnComplete?.Invoke(this);
        }

        /// <summary>
        /// 通知外部：小游戏取消或失败。
        /// </summary>
        protected void Cancel()
        {
            OnCancel?.Invoke(this);
        }
    }
}
