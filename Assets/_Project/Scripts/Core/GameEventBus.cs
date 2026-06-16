using System;
using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Core;

namespace GanglandUndercover.Online
{
    // ══════════════════════════════════════════════════════════════
    //  IGameEventBus — 服务间解耦通信接口
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 游戏事件总线接口。
    /// 各服务（VotingService / SabotageService / EvidenceService / MeetingService）
    /// 通过此接口发布和订阅事件，实现松耦合通信。
    /// 所有事件类型必须为 value type（struct），以避免 GC 分配。
    /// </summary>
    public interface IGameEventBus
    {
        /// <summary>订阅指定类型的事件。</summary>
        void Subscribe<T>(Action<T> handler) where T : struct;

        /// <summary>取消订阅指定类型的事件。</summary>
        void Unsubscribe<T>(Action<T> handler) where T : struct;

        /// <summary>发布事件，同步通知所有已订阅的处理器。</summary>
        void Publish<T>(T evt) where T : struct;
    }

    // ══════════════════════════════════════════════════════════════
    //  事件定义（全部使用 struct 避免堆分配）
    // ══════════════════════════════════════════════════════════════

    /// <summary>对局阶段切换事件。</summary>
    public struct PhaseChangedEvent
    {
        public OnlineMatchPhase OldPhase;
        public OnlineMatchPhase NewPhase;
    }

    /// <summary>玩家提交投票事件。</summary>
    public struct VoteSubmittedEvent
    {
        public ulong ClientId;
        public ulong TargetId;
        public bool IsSkip;
    }

    /// <summary>投票结算结果事件。</summary>
    public struct VoteResultEvent
    {
        public ulong EjectedId;
        public bool IsTie;
        public int[] VoteCounts;
    }

    /// <summary>破坏触发事件。</summary>
    public struct SabotageTriggeredEvent
    {
        public SabotageType Type;
        public ulong InitiatorId;
    }

    /// <summary>破坏修复事件。</summary>
    public struct SabotageResolvedEvent
    {
        public SabotageType Type;
    }

    /// <summary>证据收集事件。</summary>
    public struct EvidenceCollectedEvent
    {
        public ulong CollectorId;
        public int EvidenceIndex;
    }

    /// <summary>证据链目标达成事件。</summary>
    public struct EvidenceTargetReachedEvent
    {
        /// <summary>达成时的证据分数。</summary>
        public int Score;
        /// <summary>证据目标值。</summary>
        public int Target;
    }

    /// <summary>会议召开事件。</summary>
    public struct MeetingCalledEvent
    {
        public ulong CallerId;
        public bool IsEmergency;
    }

    /// <summary>会议结束事件。</summary>
    public struct MeetingEndedEvent
    {
        public ulong EjectedId;
        public bool WasEmergency;
    }

    /// <summary>玩家被击杀事件。</summary>
    public struct PlayerKilledEvent
    {
        public ulong VictimId;
        public ulong KillerId;
    }

    /// <summary>尸体被报告事件。</summary>
    public struct BodyReportedEvent
    {
        public ulong ReporterId;
        public ulong VictimId;
    }

    /// <summary>对局结束事件。</summary>
    public struct MatchEndedEvent
    {
        public Faction WinnerFaction;
    }

    /// <summary>任务完成事件。</summary>
    public struct TaskCompletedEvent
    {
        public ulong PlayerId;
        public int TaskIndex;
    }

    // ══════════════════════════════════════════════════════════════
    //  SimpleGameEventBus — 默认实现（MonoBehaviour 单例）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 事件总线的默认实现。
    /// 挂载为 MonoBehaviour 单例，供所有服务在 Awake / Start 中获取引用。
    /// 内部使用 Dictionary&lt;Type, Delegate&gt; 存储订阅，
    /// Publish 时同步调用所有处理器（无异步队列）。
    /// </summary>
    public sealed class SimpleGameEventBus : MonoBehaviour, IGameEventBus
    {
        private static SimpleGameEventBus _instance;

        /// <summary>全局单例访问。若场景中不存在则返回 null。</summary>
        public static SimpleGameEventBus Instance => _instance;

        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[SimpleGameEventBus] 重复实例已销毁，保留先创建的实例。");
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <inheritdoc />
        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;

            Type type = typeof(T);
            if (_handlers.TryGetValue(type, out Delegate existing))
            {
                _handlers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                _handlers[type] = handler;
            }
        }

        /// <inheritdoc />
        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;

            Type type = typeof(T);
            if (_handlers.TryGetValue(type, out Delegate existing))
            {
                Delegate result = Delegate.Remove(existing, handler);
                if (result == null)
                {
                    _handlers.Remove(type);
                }
                else
                {
                    _handlers[type] = result;
                }
            }
        }

        /// <inheritdoc />
        public void Publish<T>(T evt) where T : struct
        {
            Type type = typeof(T);
            if (_handlers.TryGetValue(type, out Delegate existing))
            {
                // 强转为 Action<T> 调用；Delegate.Combine 保证类型兼容。
                if (existing is Action<T> action)
                {
                    action.Invoke(evt);
                }
            }
        }

        /// <summary>
        /// 清除所有已注册的事件处理器。
        /// 对局重置或场景切换时调用，防止悬挂引用。
        /// </summary>
        public void ClearAll()
        {
            _handlers.Clear();
        }
    }
}
