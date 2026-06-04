using System;
using GanglandUndercover.Audio;
using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Gameplay
{
    public sealed class GameController
    {
        private const int MeetingInterval = 3;

        private readonly ActionResolver actionResolver = new ActionResolver();
        private readonly OpponentAi opponentAi = new OpponentAi();
        private readonly EventResolver eventResolver = new EventResolver();
        private readonly VictoryEvaluator victoryEvaluator = new VictoryEvaluator();

        public GameController()
        {
            State = new GameState();
            SelectedDistrict = DistrictType.Dockyard;
        }

        public GameState State { get; }
        public ActionResolver Actions => actionResolver;
        public DistrictType SelectedDistrict { get; private set; }
        public bool ShouldHoldMeeting { get; private set; }
        public event Action Changed;

        public void Reset()
        {
            State.Reset();
            SelectedDistrict = DistrictType.Dockyard;
            ShouldHoldMeeting = false;
            eventResolver.Reset();
            Changed?.Invoke();
        }

        public void SelectFaction(Faction faction)
        {
            State.SelectFaction(faction);
            Changed?.Invoke();
        }

        public void SelectRole(SocialRole role)
        {
            State.SelectRole(role);
            Changed?.Invoke();
        }

        public void SelectDistrict(DistrictType districtType)
        {
            SelectedDistrict = districtType;
            Changed?.Invoke();
        }

        public void ToggleLanguage()
        {
            State.ToggleLanguage();
            Changed?.Invoke();
        }

        /// <summary>暂停 AI 操作（紧急任务期间）。</summary>
        public void PauseAI()
        {
            if (State.Phase == GamePhase.AiTurn)
            {
                State.SetPhase(GamePhase.Paused);
            }
        }

        /// <summary>恢复 AI 操作。</summary>
        public void ResumeAI()
        {
            if (State.Phase == GamePhase.Paused)
            {
                State.SetPhase(GamePhase.AiTurn);
            }
        }

        public void RunPlayerAction(DistrictType districtType, PlayerAction action)
        {
            if (State.Phase != GamePhase.PlayerTurn)
            {
                return;
            }

            State.SetPhase(GamePhase.AiTurn);
            DistrictState district = State.GetDistrict(districtType);
            actionResolver.Resolve(State, district, action);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            // AI 对手回合（四角色模型）
            opponentAi.Run(State);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            // 环境事件结算
            eventResolver.Resolve(State);

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            // 每 MeetingInterval 天召开一次阵营会议
            if (State.Day % MeetingInterval == 0)
            {
                ShouldHoldMeeting = true;
                State.SetPhase(GamePhase.Meeting);
                State.MeetingInProgress = true;
                State.AddLog("港区召开阵营会议，各方开始投票。");
                AudioManager.Instance?.PlaySFX(SoundEffect.MeetingStart);
                Changed?.Invoke();
                return;
            }

            AdvanceToNextDay();
        }

        /// <summary>
        /// 执行会议投票阶段。AI 阵营根据策略投票，淘汰角色。
        /// </summary>
        public void RunMeeting()
        {
            if (State.Phase != GamePhase.Meeting || !ShouldHoldMeeting)
            {
                return;
            }

            SocialRole? eliminated = opponentAi.CastMeetingVote(State);

            if (eliminated.HasValue)
            {
                State.EliminateRole(eliminated.Value);
                AudioManager.Instance?.PlaySFX(SoundEffect.VoteCast);
                AudioManager.Instance?.PlaySFX(SoundEffect.PlayerEliminated);
            }
            else
            {
                State.AddLog("AI 全部弃权，本轮无人被淘汰。");
            }

            ShouldHoldMeeting = false;
            State.MeetingInProgress = false;

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            AdvanceToNextDay();
        }

        /// <summary>
        /// 玩家投票：玩家直接投票淘汰目标角色。
        /// </summary>
        public void PlayerCastVote(SocialRole targetRole)
        {
            if (State.Phase != GamePhase.Meeting) return;

            State.EliminateRole(targetRole);
            State.AddLog($"玩家投票淘汰了 {SocialKnowledge.DescribeRole(targetRole)}。");
            AudioManager.Instance?.PlaySFX(SoundEffect.VoteCast);
            AudioManager.Instance?.PlaySFX(SoundEffect.PlayerEliminated);

            ShouldHoldMeeting = false;
            State.MeetingInProgress = false;

            if (TryEndGame())
            {
                Changed?.Invoke();
                return;
            }

            AdvanceToNextDay();
        }

        /// <summary>
        /// 强制触发紧急会议。
        /// </summary>
        public void ForceMeeting()
        {
            if (State.Phase != GamePhase.PlayerTurn) return;
            ShouldHoldMeeting = true;
            State.MeetingInProgress = true;
            State.SetPhase(GamePhase.Meeting);
            State.AddLog("紧急会议被召集。");
            AudioManager.Instance?.PlaySFX(SoundEffect.Emergency);
            Changed?.Invoke();
        }

        private void AdvanceToNextDay()
        {
            State.AdvanceDay();
            State.SetPhase(GamePhase.PlayerTurn);
            Changed?.Invoke();
        }

        private bool TryEndGame()
        {
            if (!victoryEvaluator.TryEvaluate(State, out string result))
            {
                return false;
            }

            State.Finish(result);
            return true;
        }
    }
}