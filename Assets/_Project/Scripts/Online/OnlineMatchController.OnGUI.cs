using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GanglandUndercover;
using GanglandUndercover.Core;
using GanglandUndercover.Online;
using GanglandUndercover.Gameplay;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {

        // --- BuildMatchPressureSummary (moved from main controller) ---
        public string MatchPressureSummary => BuildMatchPressureSummary();
        public string LobbyReadinessSummary => BuildLobbyReadinessSummary();
        public string LobbyRoadmap => BuildPhaseRoadmap();
        public string LocalObjectiveSummary => BuildLocalObjectiveSummary();
        public string OnboardingBriefingTitle => BuildOnboardingBriefingTitle();
        public string OnboardingBriefingBody => BuildOnboardingBriefingBody();
        public string OnboardingActionPrompt => BuildOnboardingActionPrompt();
        public bool HasOnboardingGuidance => HasReadableOnboardingGuidance();
        public string LocalProfessionDisplayName => LocalProfessionName();
        public string PhaseDisplayName => PhaseName(phase);
        public string MatchTimeText => FormatMatchTime(matchElapsedSeconds);
        public string HazardSummary => BuildHazardSummary();
        public string LocalActionHint => BuildLocalActionHint();
        public string VoiceHudLine => chatSystem != null ? "聊天: " + chatSystem.CurrentChannel + " | " + chatSystem.MessageCount + "条消息" : "聊天未连接";
        public string FocusedIntelText => BuildFocusedIntel();
        public string TaskListText => BuildTaskList();
        public string CaseLogText => BuildCaseLog();
        public string PlayerListText => BuildPlayerList();
        public string ReleaseReadinessText => BuildReleaseReadiness();
        public string VoteTallySummary => BuildVoteTallySummary();
        public string ResultRosterLine => BuildResultRosterLine();
        public string ActiveTaskNameText => activeTaskId >= 0 ? GetTask(activeTaskId).Name : string.Empty;
        public string ActiveTaskInstructionText => activeTaskId >= 0 ? TaskPanelInstruction(activeTaskId) : string.Empty;
        public string ActiveTaskTemplateTitleText => activeTaskId >= 0 ? TaskPanelTemplateTitle(activeTaskId) : string.Empty;
        public string ActiveTaskTemplateSubtitleText => activeTaskId >= 0 ? TaskPanelTemplateSubtitle(activeTaskId) : string.Empty;
        public string ActiveTaskFooterText => activeTaskId >= 0 ? TaskPanelFooter(activeTaskId) : string.Empty;
        public string ActiveTaskProgressText => activeTaskId >= 0 ? "证据价值 +" + TaskEvidenceValue(activeTaskId) + " | 错误 " + activeTaskMistakes + "/3" : string.Empty;
        public int ActiveTaskIdValue => activeTaskId;
        public int ActiveTaskStepValue => activeTaskStep;
        public int ActiveTaskMistakesValue => activeTaskMistakes;
        public float ActiveTaskChargeValue => activeTaskCharge;
        public int ActiveTaskCorrectStepOne => activeTaskId >= 0 ? CorrectTaskStepInput(activeTaskId, 0) : 1;
        public int ActiveTaskCorrectStepTwo => activeTaskId >= 0 ? CorrectTaskStepInput(activeTaskId, 1) : 2;
        public int ActiveTaskCorrectStepThree => activeTaskId >= 0 ? CorrectTaskStepInput(activeTaskId, 2) : 3;
        public bool ActiveTaskStepOneDone => activeTaskStepOneDone;
        public bool ActiveTaskStepTwoDone => activeTaskStepTwoDone;
        public bool ActiveTaskStepThreeDone => activeTaskStepThreeDone;
        public bool ActiveTaskFeedbackPositiveValue => activeTaskFeedbackPositive;
        public float ActiveTaskFeedbackTimerValue => activeTaskFeedbackTimer;
        public string LastMeetingReason => lastMeetingReason;
        public string LastVoteOutcome => lastVoteOutcome;
        public string LastEvidenceEvent => lastEvidenceEvent;
        public string LastSabotageEvent => lastSabotageEvent;
        public int EvidenceMilestoneIndex => evidenceMilestoneIndex;
        public int TacticalMapLabelCount => tasks.Count + mapService.ShipRooms().Length + players.Count + (killSystem != null ? killSystem.bodies.Count : 0) + ruleSet.UnderworldPassageCount;
        public float LocalAbilityCooldown => TryGetLocalPlayer(out OnlinePlayerState localState) ? localState.AbilityCooldown : 0f;
        public float LocalKillCooldown => TryGetLocalPlayer(out OnlinePlayerState localState2) ? localState2.KillCooldown : 0f;
        public bool LocalAlive => IsLocalAlive();
        public string RoleDisplayName(OnlineRole role) => RoleName(role);
        public string ProfessionDisplayName(OnlineProfession profession) => ProfessionName(profession);
        public string TaskDisplayName(int id) => TaskNameFor(id);
        public string TaskDistrictDisplayName(int id) => TaskDistrictName(id);
        public string TaskMapCodeDisplayName(int id) => TaskMapCode(id);
        public string PhaseDisplayNameFor(OnlineMatchPhase matchPhase) => PhaseName(matchPhase);

#if UNITY_EDITOR
        public void EditorSimulateLocalMatch()
        {
            EnsureCanvasHud();

            if (players.Count == 0)
            {
                players[0] = new OnlinePlayerState(0, "玩家0", mapService.SpawnPosition(0), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            EnsureMinimumBots();
            StartOnlineMatchCore(false);
        }
#endif

        // --- OnGUI (moved from main controller) ---
        private void OnGUI()
        {
            // M7.3: Canvas 模式已接管全部 UI，OnGUI 保留仅为编辑器调试回退
#if UNITY_EDITOR
            if (canvasHudEnabled)
            {
                return;
            }
#else
            return;   // M7.3: 发布版永远不走 OnGUI
#endif

            GUI.depth = -100;
            ApplyHudSkin();

            bool actionHud = IsOnline && phase == OnlineMatchPhase.Action;

            if (IsOnline && (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting))
            {
                DrawMeetingScreen();
                return;
            }

            if (IsOnline && phase == OnlineMatchPhase.Result)
            {
                DrawResultScreen();
                return;
            }

            if (actionHud)
            {
                DrawCompactActionHud();

                if (intelBoardOpen)
                {
                    DrawActionIntelPanel();
                }

                if (tacticalMapOpen)
                {
                    DrawLargeMapPreview();
                }

                DrawActiveTaskPanel();

                // 阵营私聊面板（行动阶段）
                DrawActionChatPanel();
                return;
            }

            bool expandedIntel = intelBoardOpen || !actionHud || phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting || phase == OnlineMatchPhase.Result;
            float leftWidth = actionHud ? Mathf.Clamp(Screen.width * 0.25f, 285f, 360f) : Mathf.Clamp(Screen.width * 0.32f, 360f, 470f);
            float rightWidth = expandedIntel ? Mathf.Clamp(Screen.width * 0.26f, 300f, 410f) : Mathf.Clamp(Screen.width * 0.2f, 245f, 310f);
            float leftPanelHeight = actionHud ? Mathf.Clamp(Screen.height * 0.34f, 210f, 310f) : Mathf.Clamp(Screen.height - 36f, 470f, 780f);
            float rightPanelHeight = expandedIntel ? Mathf.Clamp(Screen.height - 36f, 430f, 760f) : 238f;

            GUILayout.BeginArea(new Rect(18f, 18f, leftWidth, leftPanelHeight), GUI.skin.box);
            GUILayout.Label("港区潜线 Release Candidate");
            GUILayout.Label(roomName + " | " + status);
            GUILayout.Label("阶段: " + PhaseName(phase) + " | 局时: " + FormatMatchTime(matchElapsedSeconds) + "/20:00 | 证据链: " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget + " | 危机: " + BuildHazardSummary());
            GUILayout.Label("本机身份: " + RoleName(localRole) + " | 职责: " + LocalProfessionName());

            if (!actionHud)
            {
                GUILayout.Label("Unity Services: " + BuildServiceStatus());
            }

            if (!IsOnline)
            {
                DrawModePillars();
                GUILayout.Space(6f);
                GUILayout.Label("玩家代号");
                localPlayerName = LimitText(GUILayout.TextField(localPlayerName), 16, "港区玩家");
                GUILayout.Label("Host IP / Client 连接地址");
                joinAddress = GUILayout.TextField(joinAddress);
                DrawRoomSettings();
                DrawRelayJoinControls();

                if (GUILayout.Button("创建 Host"))
                {
                    StartHost();
                }

                if (GUILayout.Button("单机试玩局"))
                {
                    StartLocalPreviewRoom();
                    FillBotsAndStart();
                }

                if (GUILayout.Button("加入 Client"))
                {
                    StartClient(joinAddress);
                }
            }
            else
            {
                DrawRoomHeader();
                GUILayout.Label(LobbyReadinessSummary);
                GUILayout.Label(LobbyRoadmap);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(localReady ? "取消 Ready" : "Ready"))
                {
                    localReady = !localReady;
                    SendClientState(true);
                }

                bool previousEnabled = GUI.enabled;
                GUI.enabled = IsHost && CanStartLobbyMatch();

                if (GUILayout.Button("开始在线局"))
                {
                    StartOnlineMatch();
                }

                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();

                if (IsHost && phase == OnlineMatchPhase.Lobby && GUILayout.Button("补 AI 并开始本地可玩局"))
                {
                    FillBotsAndStart();
                }

                if (phase == OnlineMatchPhase.Opening)
                {
                    DrawOpeningBriefing();

                    if (IsHost && GUILayout.Button("跳过简报进入行动"))
                    {
                        phase = OnlineMatchPhase.Action;
                        phaseTimer = 0f;
                        fullMapPreview = false;
                        tacticalMapOpen = false;
                        status = "行动开始：九龙港城进入封控搜证。";
                        AddCaseLog(status);
                        BroadcastSnapshot();
                    }
                }

                if (phase == OnlineMatchPhase.Result)
                {
                    fullMapPreview = true;
                    GUILayout.Label(resultSummary);
                    bool resultPreviousEnabled = GUI.enabled;
                    GUILayout.BeginHorizontal();
                    GUI.enabled = IsHost;

                    if (GUILayout.Button("重开同房间"))
                    {
                        RestartMatch();
                    }

                    GUI.enabled = resultPreviousEnabled;

                    if (GUILayout.Button("返回房间"))
                    {
                        ReturnToLobby();
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Label("操作: WASD 移动 | E 查证/破坏 | Q 击倒 | R 报案/紧急会议 | F 技能 | M/Tab 大地图 | I 案情板");

                if (!actionHud || intelBoardOpen)
                {
                GUILayout.Label("目标: 警方完成证据链或清除黑帮；黑帮破坏、击倒并争取人数压制；卧底加速取证但要隐藏路线。");
                }

                GUILayout.Space(4f);
                GUILayout.Label(BuildLocalActionHint());

                if (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
                {
                    DrawVotePanel();
                }

                if (GUILayout.Button("离开房间"))
                {
                    Shutdown();
                }
            }

            if (!actionHud || intelBoardOpen)
            {
                GUILayout.Space(8f);
                rosterScroll = GUILayout.BeginScrollView(rosterScroll, GUILayout.Height(Mathf.Max(120f, leftPanelHeight * 0.34f)));
                GUILayout.Label(BuildPlayerList());
                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(Screen.width - rightWidth - 18f, 18f, rightWidth, rightPanelHeight), GUI.skin.box);
            GUILayout.Label(expandedIntel ? "案情板" : "小地图");
            DrawTacticalMapMini();

            if (expandedIntel)
            {
                intelScroll = GUILayout.BeginScrollView(intelScroll);
                GUILayout.Space(6f);
                GUILayout.Label(BuildFocusedIntel());
                GUILayout.Space(8f);
                GUILayout.Label(BuildTaskList());
                GUILayout.Space(8f);
                GUILayout.Label(BuildCaseLog());

                if (!IsOnline || phase == OnlineMatchPhase.Lobby)
                {
                    GUILayout.Space(8f);
                    GUILayout.Label(BuildReleaseReadiness());
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Space(6f);
                GUILayout.Label(BuildFocusedIntel());
            }

            GUILayout.EndArea();

            if (tacticalMapOpen)
            {
                DrawLargeMapPreview();
            }

            DrawActiveTaskPanel();
        }

        // --- OnDrawGizmos (moved from main controller) ---
        private void OnDrawGizmos()
        {
            foreach (OnlineTaskState task in tasks)
            {
                Gizmos.color = task.Completed ? Color.green : task.Sabotaged ? Color.red : Color.cyan;
                Gizmos.DrawCube(task.Position, new Vector3(0.35f, 0.35f, 0.05f));
            }

            foreach (OnlineBodyState body in killSystem.bodies)
            {
                if (body.Reported)
                {
                    continue;
                }

                Gizmos.color = Color.red;
                Gizmos.DrawCube(body.Position, new Vector3(0.5f, 0.28f, 0.08f));
            }

            ulong localClientId = LocalClientId();

            foreach (OnlinePlayerState state in players.Values)
            {
                Gizmos.color = state.ClientId == localClientId ? Color.yellow : state.Alive ? Color.white : Color.gray;
                Gizmos.DrawSphere(state.Position, state.Alive ? 0.22f : 0.14f);
            }
        }

        // --- BuildResultSummary (moved from main controller) ---
        private string BuildResultSummary(string resultStatus)
        {
            int alive = CountAlivePlayers();
            int completedTasks = 0;
            int sabotageCount = 0;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Completed) completedTasks++;
                if (task.Sabotaged) sabotageCount++;
            }

            string moleReport = "";
            foreach (var kv in _moleObjectives)
            {
                string name = GetPlayerDisplayName(kv.Key);
                var obj = kv.Value;
                bool bonus = obj.Kills >= 2 && obj.Sabotages >= 1 && obj.SurvivedTilLate;
                moleReport += $"\n内鬼 {name}：击杀{obj.Kills} 破坏{obj.Sabotages}" +
                    (bonus ? " 🏆隐藏目标达成" : "") +
                    (obj.SurvivedTilLate ? " 存活至终局" : "");
            }

            return resultStatus
                + "\n用时 " + FormatMatchTime(matchElapsedSeconds)
                + " | 存活 " + alive + "/" + players.Count
                + " | 完成任务 " + completedTasks + "/" + tasks.Count
                + " | 破坏残留 " + sabotageCount
                + " | 尸体 " + killSystem.bodies.Count
                + moleReport
                + "\n可直接重开同房间，保留玩家与规则配置。";
        }

        // --- BuildPlayerList (moved from main controller) ---
        private string BuildPlayerList()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("玩家列表");
            ulong localClientId = LocalClientId();

            foreach (OnlinePlayerState state in players.Values)
            {
                builder.AppendLine((state.ClientId == localClientId ? "你 " : string.Empty)
                    + state.DisplayName
                    + (state.IsBot ? " [AI]" : string.Empty)
                    + " | "
                    + (state.Alive ? "存活" : "出局")
                    + " | "
                    + (state.Ready ? "Ready" : "Not Ready")
                    + " | "
                    + RoleName(state.PublicRole)
                    + " | "
                    + ProfessionName(state.Profession)
                    + " | 嫌疑 "
                    + state.Suspicion
                    + " | 技能 "
                    + Mathf.CeilToInt(state.AbilityCooldown)
                    + "s"
                    + " | "
                    + state.Position.ToString("F1"));
            }

            if (players.Count == 0)
            {
                builder.AppendLine("创建 Host 或加入房间后显示玩家。");
            }

            return builder.ToString();
        }

        // --- BuildCaseLog (moved from main controller) ---
        private string BuildCaseLog()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("案情记录");

            if (caseLog.Count == 0)
            {
                builder.AppendLine("开局后记录关键事件。");
                return builder.ToString();
            }

            for (int i = caseLog.Count - 1; i >= 0; i--)
            {
                builder.AppendLine(caseLog[i]);
            }

            return builder.ToString();
        }

        // --- BuildTaskList (moved from main controller) ---
        private string BuildTaskList()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("港区任务 | 调查组推进任务，黑帮可伪装靠近并破坏");
            builder.AppendLine("局时: " + FormatMatchTime(matchElapsedSeconds) + "/20:00");
            builder.AppendLine("证据链: " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget);
            builder.AppendLine("紧急会议: " + emergencyMeetingsLeft + " | 危机: " + BuildHazardSummary());
            builder.AppendLine("局势压力: " + BuildMatchPressureSummary());
            builder.AppendLine("最近证据: " + lastEvidenceEvent);
            builder.AppendLine("最近破坏: " + lastSabotageEvent);
            builder.AppendLine("证据阶段: " + EvidenceMilestoneName(evidenceMilestoneIndex) + " | " + BuildNextEvidenceMilestoneHint());
            builder.AppendLine("任务分型: 监控追踪、封条查验、电力修复、证物扫描、账本冻结、路线巡查");

            foreach (OnlineTaskState task in tasks)
            {
                builder.AppendLine(task.Name
                    + " "
                    + task.Progress
                    + "/"
                    + task.RequiredProgress
                    + " | 区域 " + TaskDistrictName(task.Id)
                    + " | +" + TaskEvidenceValue(task.Id) + "证"
                    + " | " + TaskPanelTemplateTitle(task.Id)
                    + (task.Completed ? " 已完成" : task.Sabotaged ? " 被破坏/" + SabotageName(SabotageForTask(task.Id)) : " 待处理"));
            }

            int activeBodies = 0;

            foreach (OnlineBodyState body in killSystem.bodies)
            {
                if (!body.Reported)
                {
                    activeBodies++;
                }
            }

            builder.AppendLine("未报案尸体: " + activeBodies);

            if (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
            {
                builder.AppendLine("投票: " + votes.Count + "/" + CountAlivePlayers() + " | 剩余 " + Mathf.CeilToInt(phaseTimer) + "s");
            }

            return builder.ToString();
        }

        // --- BuildFocusedIntel (moved from main controller) ---
        private string BuildFocusedIntel()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("局时 " + FormatMatchTime(matchElapsedSeconds) + "/20:00 | 证据链 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget + " | 会议 " + emergencyMeetingsLeft);
            builder.AppendLine("目标: 警方闭合证据链或投出黑帮；黑帮通过击倒、破坏和会议误导拖到 20 分钟。");
            builder.AppendLine("局势压力: " + BuildMatchPressureSummary());
            builder.AppendLine("你的任务: " + BuildLocalObjectiveSummary());
            builder.AppendLine("证据阶段: " + EvidenceMilestoneName(evidenceMilestoneIndex) + " | " + BuildNextEvidenceMilestoneHint());

            int activeBodies = CountUnreportedBodies();
            if (activeBodies > 0)
            {
                builder.AppendLine("未报案尸体: " + activeBodies);
            }

            if (phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting)
            {
                builder.AppendLine("投票 " + votes.Count + "/" + CountAlivePlayers() + " | " + Mathf.CeilToInt(phaseTimer) + "s");
                builder.AppendLine("会议原因: " + lastMeetingReason);
                builder.AppendLine("上轮结论: " + lastVoteOutcome);
                return builder.ToString();
            }

            OnlineTaskState nearest = FindNearestTask(LocalCameraTarget());

            if (nearest.Id >= 0)
            {
                builder.AppendLine("当前目标: " + nearest.Name);
                builder.AppendLine("所在区域: " + TaskDistrictName(nearest.Id));
                builder.AppendLine("进度 " + nearest.Progress + "/" + nearest.RequiredProgress + " | " + TaskPanelTemplateTitle(nearest.Id) + " | +" + TaskEvidenceValue(nearest.Id) + "证" + (nearest.Sabotaged ? " | 被破坏/" + SabotageName(SabotageForTask(nearest.Id)) : string.Empty));
                return builder.ToString();
            }

            OnlineTaskState target = FindRecommendedTask(LocalCameraTarget());
            builder.AppendLine("推荐路线: " + target.Name);
            builder.AppendLine("区域: " + TaskDistrictName(target.Id));
            builder.AppendLine("距离 " + Vector3.Distance(LocalCameraTarget(), target.Position).ToString("F1") + " | M 打开大地图");
            return builder.ToString();
        }

        // --- DrawModePillars (moved from main controller) ---
        private void DrawModePillars()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("完整局结构");
            GUILayout.Label("开局: 房间 Ready、身份私发、全图预览。");
            GUILayout.Label("行动: 大地图巡场、小任务、黑帮破坏、暗线换位、尸体报案。");
            GUILayout.Label("会议: 全员文本讨论、投票放逐、结算身份和胜负。");
            GUILayout.EndVertical();
        }

        // --- BuildHazardSummary (moved from main controller) ---
        private string BuildHazardSummary()
        {
            List<string> hazards = new List<string>();

            if (taskService.BlackoutTimer > 0f)
            {
                hazards.Add("黑灯 " + Mathf.CeilToInt(taskService.BlackoutTimer));
            }

            if (taskService.LockdownTimer > 0f)
            {
                hazards.Add("封锁 " + Mathf.CeilToInt(taskService.LockdownTimer));
            }

            if (taskService.CommunicationJamTimer > 0f)
            {
                hazards.Add("断讯 " + Mathf.CeilToInt(taskService.CommunicationJamTimer));
            }

            if (taskService.EvidenceLeakTimer > 0f)
            {
                hazards.Add("泄证 " + Mathf.CeilToInt(taskService.EvidenceLeakTimer));
            }

            if (taskService.PatrolAlertTimer > 0f)
            {
                hazards.Add("巡逻 " + Mathf.CeilToInt(taskService.PatrolAlertTimer));
            }

            return hazards.Count == 0 ? "无" : string.Join(" / ", hazards);
        }

        // --- BuildMatchPressureSummary (moved from main controller) ---
        private string BuildMatchPressureSummary()
        {
            float evidenceRatio = taskService.EvidenceScore / (float)Mathf.Max(1, taskService.EvidenceTarget);
            float taskRatio = CountCompletedTasks() / (float)Mathf.Max(1, tasks.Count);
            float timeRatio = Mathf.Clamp01(matchElapsedSeconds / ruleSet.MatchHardLimitSeconds);
            int aliveGang = CountAliveRole(OnlineRole.Gang);
            int aliveNonGang = CountAlivePlayers() - aliveGang;
            int unresolvedBodies = CountUnreportedBodies();
            int sabotaged = CountSabotagedTasks();
            string leadingSide = evidenceRatio >= timeRatio + 0.12f || taskRatio >= timeRatio + 0.1f ? "警方领先" : aliveGang > 0 && aliveGang >= aliveNonGang - 1 ? "黑帮逼近人数优势" : "局势胶着";
            string urgency = sabotaged > 0 || unresolvedBodies > 0 ? "高压" : timeRatio > 0.65f && evidenceRatio < 0.72f ? "时间压力" : "可控";
            return leadingSide
                + " | " + urgency
                + " | 警方进度 " + Mathf.RoundToInt(evidenceRatio * 100f) + "%"
                + " | 任务 " + Mathf.RoundToInt(taskRatio * 100f) + "%"
                + " | 黑帮 " + aliveGang + " / 非黑帮 " + aliveNonGang
                + (unresolvedBodies > 0 ? " | 未报案 " + unresolvedBodies : string.Empty)
                + (sabotaged > 0 ? " | 待修复 " + sabotaged : string.Empty);
        }

        // --- BuildLocalObjectiveSummary (moved from main controller) ---
        private string BuildLocalObjectiveSummary()
        {
            OnlineRole role = LocalEffectiveRole();

            if (role == OnlineRole.Gang)
            {
                OnlineTaskState sabotageTarget = FindHighestValueOpenTask();
                string targetText = sabotageTarget.Id >= 0 ? sabotageTarget.Name + "/" + SabotageName(SabotageForTask(sabotageTarget.Id)) : "寻找落单目标";
                return "隐藏身份，制造破坏，优先干扰 " + targetText + "，会议中误导投票。";
            }

            if (role == OnlineRole.Undercover)
            {
                OnlineTaskState target = FindRecommendedTask(LocalCameraTarget());
                return "加速取证但控制嫌疑，优先推进 " + target.Name + "，会议里不要暴露路线。";
            }

            if (role == OnlineRole.Mole)
            {
                OnlineTaskState sabotageTargetMol = FindHighestValueOpenTask();
                string targetTextMol = sabotageTargetMol.Id >= 0 ? sabotageTargetMol.Name + "/" + SabotageName(SabotageForTask(sabotageTargetMol.Id)) : "寻找落单目标";
                return "身为线人隐匿在警方之中，破坏证据并掩护黑帮，优先干扰 " + targetTextMol + "，利用警察身份误导搜查方向。";
            }

            OnlineTaskState recommended = FindRecommendedTask(LocalCameraTarget());
            return "完成任务、报案、投出黑帮；当前推荐 " + recommended.Name + "。";
        }

        private string BuildOnboardingBriefingTitle()
        {
            if (!IsOnline)
            {
                return "身份简报 | 行动演练";
            }

            if (phase == OnlineMatchPhase.Lobby)
            {
                return "身份简报 | 大厅准备";
            }

            switch (LocalEffectiveRole())
            {
                case OnlineRole.Gang:
                    return "身份简报 | 黑帮行动";
                case OnlineRole.Undercover:
                    return "身份简报 | 卧底潜线";
                case OnlineRole.Mole:
                    return "身份简报 | 线人掩护";
                default:
                    return "身份简报 | 警方搜证";
            }
        }

        private string BuildOnboardingBriefingBody()
        {
            OnlineRole role = LocalEffectiveRole();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("身份: " + RoleName(role));
            builder.AppendLine("公开身份: " + PublicIdentityName(role));
            builder.AppendLine("职责: " + LocalProfessionName());
            builder.AppendLine("胜利目标: " + RoleWinGoal(role));
            builder.AppendLine("当前目标: " + BuildLocalObjectiveSummary());
            builder.Append("操作入口: " + BuildOnboardingActionPrompt());
            return builder.ToString();
        }

        private string BuildOnboardingActionPrompt()
        {
            if (!IsOnline)
            {
                return "创建 Host 或单机试玩局，先用本地 AI 跑完整流程。";
            }

            if (!IsLocalAlive())
            {
                return "你已出局，继续观察路线、会议票型和结算身份。";
            }

            if (activeTaskId >= 0)
            {
                return "任务面板已打开：按 1/2/3 校准，按住 Space 扫描，Esc 退出。";
            }

            switch (phase)
            {
                case OnlineMatchPhase.Lobby:
                    return "Ready 后等待房主开始；房主可补 AI 并开局。";
                case OnlineMatchPhase.Opening:
                    return "先记住真实身份、公开身份和职责；倒计时结束后按推荐路线行动。";
                case OnlineMatchPhase.Action:
                    return ActionPromptForRole(LocalEffectiveRole());
                case OnlineMatchPhase.Meeting:
                case OnlineMatchPhase.Voting:
                    return "核对证据板和发言，选择可疑目标投票；证据不足时可跳过。";
                case OnlineMatchPhase.Result:
                    return "查看结算和身份复盘，房主可重开或返回房间。";
                default:
                    return BuildLocalActionHint();
            }
        }

        private bool HasReadableOnboardingGuidance()
        {
            string title = BuildOnboardingBriefingTitle();
            string body = BuildOnboardingBriefingBody();
            string prompt = BuildOnboardingActionPrompt();
            return !string.IsNullOrWhiteSpace(title)
                && !string.IsNullOrWhiteSpace(body)
                && !string.IsNullOrWhiteSpace(prompt)
                && body.Contains("身份")
                && body.Contains("目标");
        }

        private static string PublicIdentityName(OnlineRole role)
        {
            if (role == OnlineRole.Undercover)
            {
                return "黑帮成员（隐藏警方目标）";
            }

            if (role == OnlineRole.Mole)
            {
                return "警方成员（隐藏黑帮目标）";
            }

            return RoleName(role);
        }

        private static string RoleWinGoal(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Gang:
                    return "拖慢证据链，利用破坏/击倒创造人数优势，会议里误导投票。";
                case OnlineRole.Undercover:
                    return "伪装成黑帮推进取证，关键时刻帮助警方锁定黑帮。";
                case OnlineRole.Mole:
                    return "披着警方身份掩护黑帮，破坏证据链并把怀疑导向他人。";
                default:
                    return "完成任务收集证据，报案开会，投出黑帮成员。";
            }
        }

        private static string ActionPromptForRole(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Gang:
                    return "E 破坏附近任务，Q 击倒落单目标，F 使用能力或暗线，会议里误导票型。";
                case OnlineRole.Mole:
                    return "E 破坏或伪装修复，F 使用职业能力，会议里用警方身份误导搜查。";
                case OnlineRole.Undercover:
                    return "E 推进任务取证，R 报案，M 看路线，会议里隐藏卧底身份。";
                default:
                    return "E 推进/修复任务，R 报案，M 大地图，I 案情板。";
            }
        }

        // --- BuildNextEvidenceMilestoneHint (moved from main controller) ---
        private string BuildNextEvidenceMilestoneHint()
        {
            int nextMilestone = Mathf.Clamp(evidenceMilestoneIndex + 1, 1, 4);
            float targetRatio = nextMilestone == 1 ? 0.25f : nextMilestone == 2 ? 0.5f : nextMilestone == 3 ? 0.75f : 1f;
            int nextScore = Mathf.CeilToInt(taskService.EvidenceTarget * targetRatio);

            if (taskService.EvidenceScore >= taskService.EvidenceTarget)
            {
                return "证据已闭合";
            }

            return "下阶段还差 " + Mathf.Max(0, nextScore - taskService.EvidenceScore) + " 证";
        }

        // --- BuildLocalActionHint (moved from main controller) ---
        private string BuildLocalActionHint()
        {
            if (!IsOnline)
            {
                return "创建 Host 后可预览完整 2.5D 港区并开局，默认单局目标 10-20 分钟。";
            }

            ulong localClientId = LocalClientId();

            if (!players.TryGetValue(localClientId, out OnlinePlayerState localState) || !localState.Alive)
            {
                return "你已出局，继续观察路线、投票和结算。";
            }

            if (activeTaskId >= 0)
            {
                return "正在处理任务面板：按 1/2/3 校准，按住 Space 推进，Esc 退出。";
            }

            if (localRole == OnlineRole.Gang && IsNearUnderworldPassage(localState.Position))
            {
                return "你在暗线节点旁，按 F 可换位到对侧节点；E 可破坏附近任务。";
            }

            OnlineTaskState nearestTask = FindNearestTask(localState.Position);

            if (nearestTask.Id >= 0)
            {
                return "附近任务: " + nearestTask.Name + " | E " + (localRole == OnlineRole.Gang ? "破坏" : nearestTask.Sabotaged ? "修复" : "推进")
                    + " | 类型: " + SabotageName(SabotageForTask(nearestTask.Id));
            }

            if (TryFindNearestBody(localState.Position, out _))
            {
                return "附近发现尸体，按 R 报案开会。";
            }

            if (Vector3.Distance(localState.Position, mapService.ScaleMapPosition(Vector3.zero)) <= ruleSet.ReportRange)
            {
                return "你在紧急铃旁，剩余会议 " + emergencyMeetingsLeft + "，断讯/冷却会阻止开会。";
            }

            OnlineTaskState target = FindRecommendedTask(localState.Position);
            return "推荐前往: " + target.Name + " | 距离 " + Vector3.Distance(localState.Position, target.Position).ToString("F1");
        }

        // --- BuildReleaseReadiness (moved from main controller) ---
        private string BuildReleaseReadiness()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("发行候选覆盖");
            builder.AppendLine("联网: Host/Client/AI 补位/权威判定");
            builder.AppendLine("玩法: 开局身份、证据链、多类型破坏、击倒、尸体、会议次数、投票、结算，目标单局 10-20 分钟");
            builder.AppendLine("局内完整度: 非黑帮任务小游戏输入门禁、会议证据墙、票型结论、局势压力条、M 键全图任务/玩家标注");
            builder.AppendLine("人物: 督察、鉴证、技侦、卧底、打手、白纸扇、车手");
            builder.AppendLine("场景: 大型九龙港城，道路骨架、12 区域、28 任务点、可替换真实地图底座");
            builder.AppendLine("美术: 2.5D 建筑体、屋顶、外立面、窗格、招牌、门框、道路标线、港口设备、监控墙、夜市摊档、诊所、电房、证物库");
            builder.AppendLine("地图物件: 每区专属装饰、任务设备外观、公共设施、路障、标牌、货架、线缆");
            builder.AppendLine("碰撞: 服务端权威阻挡墙体、货柜、柜台、车辆、重型设备，小装饰不阻挡");
            builder.AppendLine("黑帮路线: 后巷暗线节点、车手换位、断讯/封锁/黑灯等破坏链");
            builder.AppendLine("预览: 局前全图预览、局内小地图、Tab 战术地图、任务推荐提示");
            builder.AppendLine("服务: Unity Services 初始化/匿名登录，等待 Cloud Project 绑定后启用");
            builder.AppendLine("音频: 运行时生成提示音，覆盖开局、任务、破坏、击倒、会议、投票、结算");
            builder.AppendLine("资源: " + CommercialArtAdapterCount + " 个资源适配层 | " + LargePortVistaCount + " 个大场景港区层");
            builder.AppendLine("大厅: " + BuildLobbyReadinessSummary());
            builder.AppendLine(BuildPhaseRoadmap());
            return builder.ToString();
        }

        // --- BuildServiceStatus (moved from main controller) ---
        private string BuildServiceStatus()
        {
            if (serviceBootstrap == null)
            {
                return "未挂载。";
            }

            string player = string.IsNullOrEmpty(serviceBootstrap.PlayerId) ? string.Empty : " | Player " + serviceBootstrap.PlayerId;
            return serviceBootstrap.ServiceReadinessSummary + player;
        }

        // --- BuildLobbyReadinessSummary (moved from main controller) ---
        private string BuildLobbyReadinessSummary()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("大厅准备: ");
            builder.Append(CountHumanPlayers()).Append("/").Append(roomMinPlayers).Append(" 真人");

            if (roomAutoFillAi)
            {
                builder.Append(" | AI补位开启");
            }

            if (localPreviewMode)
            {
                builder.Append(" | 本地可玩局");
            }
            else if (!IsHost)
            {
                builder.Append(" | 等待Host开局");
            }
            else
            {
                builder.Append(" | Host可开局");
            }

            builder.Append(" | ").Append(ReadyPlayerCount()).Append("/").Append(players.Count).Append(" Ready");

            if (players.Count < roomMinPlayers)
            {
                builder.Append(" | 人数不足");
            }

            if (!roomAutoFillAi && CountHumanPlayers() < roomMinPlayers)
            {
                builder.Append(" | 需补真人或开启AI");
            }

            return builder.ToString();
        }

        // --- BuildPhaseRoadmap (moved from main controller) ---
        private string BuildPhaseRoadmap()
        {
            const string roadmap = "流程: Lobby -> Opening -> Action -> Meeting/Voting -> Result";

            if (!IsOnline)
            {
                return "阶段: 直连/Relay/本地试玩 | " + roadmap;
            }

            switch (phase)
            {
                case OnlineMatchPhase.Lobby:
                    return roadmap;
                case OnlineMatchPhase.Opening:
                    return "当前: Opening | 身份简报与初始路线 | " + roadmap;
                case OnlineMatchPhase.Action:
                    return "当前: Action | 巡场、任务、破坏、击倒、报案 | " + roadmap;
                case OnlineMatchPhase.Meeting:
                    return "当前: Meeting | 讨论、对照证据、准备投票 | " + roadmap;
                case OnlineMatchPhase.Voting:
                    return "当前: Voting | 票型结算中 | " + roadmap;
                case OnlineMatchPhase.Result:
                    return "当前: Result | 结算与重开 | " + roadmap;
                default:
                    return "阶段: 未知 | " + roadmap;
            }
        }

        // --- DrawRoomSettings (moved from main controller) ---
        private void DrawRoomSettings()
        {
            GUILayout.Space(8f);
            GUILayout.Label("房间设置");
            roomName = LimitText(GUILayout.TextField(roomName), 20, "九龙港区夜局");
            GUILayout.BeginHorizontal();
            GUILayout.Label("最少人数 " + roomMinPlayers, GUILayout.Width(110f));
            roomMinPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(roomMinPlayers, ruleSet.MinimumRoomPlayers, ruleSet.MaximumRoomPlayers));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("最大人数 " + roomMaxPlayers, GUILayout.Width(110f));
            roomMaxPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(roomMaxPlayers, roomMinPlayers, ruleSet.MaximumRoomPlayers));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("证据目标 " + taskService.EvidenceTarget, GUILayout.Width(110f));
            taskService.EvidenceTarget = Mathf.RoundToInt(GUILayout.HorizontalSlider(taskService.EvidenceTarget, 34, 56));
            GUILayout.EndHorizontal();
            GUILayout.Label("目标局长: " + TargetMatchMinutesMin + "-" + TargetMatchMinutesMax + " 分钟 | 击倒冷却 " + Mathf.RoundToInt(ruleSet.KillCooldownSeconds) + "s | 会议 " + Mathf.RoundToInt(ruleSet.MeetingIntroSeconds + ruleSet.VotingSeconds) + "s");
            roomAutoFillAi = GUILayout.Toggle(roomAutoFillAi, "人数不足时 AI 补位");
            revealRoleOnEject = GUILayout.Toggle(revealRoleOnEject, "投出局时公开身份");
        }

        // --- DrawRelayJoinControls (moved from main controller) ---
        private void DrawRelayJoinControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Relay 联网房间码");
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !relayOperationInProgress;

            if (GUILayout.Button("创建 Relay 房间码"))
            {
                StartRelayHost();
            }

            GUILayout.BeginHorizontal();
            relayJoinInput = CleanRelayJoinInput(GUILayout.TextField(relayJoinInput));

            if (GUILayout.Button("加入房间码", GUILayout.Width(108f)))
            {
                StartRelayClient();
            }

            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;
            GUILayout.Label(RelayLobbySummary);
        }

        // --- DrawRoomHeader (moved from main controller) ---
        private void DrawRoomHeader()
        {
            GUILayout.Label("房间: " + CountHumanPlayers() + " 真人 / " + (_botController?.BotCount ?? 0) + " AI | " + roomMinPlayers + "-" + roomMaxPlayers + " 人");
            GUILayout.Label("规则: " + (roomAutoFillAi ? "AI 补位" : "真人优先") + " | " + (revealRoleOnEject ? "出局公开身份" : "身份隐藏"));

            if (!string.IsNullOrWhiteSpace(relayJoinCode))
            {
                GUILayout.Label("Relay 房间码: " + relayJoinCode);
            }

            if (IsHost && phase == OnlineMatchPhase.Lobby)
            {
                DrawRoomSettings();
            }
        }

        // --- DrawCompactActionHud (moved from main controller) ---
        private void DrawCompactActionHud()
        {
            float topBarWidth = Mathf.Clamp(Screen.width * 0.34f, 360f, 540f);
            float topBarHeight = 68f;
            Rect topBar = new Rect(16f, 14f, topBarWidth, topBarHeight);
            GUILayout.BeginArea(topBar, GUI.skin.box);
            GUILayout.Label("九龙港城行动 | " + RoleName(localRole) + " / " + LocalProfessionName());
            GUILayout.Label("证据 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget + " | 任务 " + CountCompletedTasks() + "/" + tasks.Count + " | 存活 " + CountAlivePlayers() + "/" + players.Count + " | 会议 " + emergencyMeetingsLeft + " | " + BuildHazardSummary() + (aiActionGraceTimer > 0f ? " | 缓冲 " + Mathf.CeilToInt(aiActionGraceTimer) + "s" : string.Empty));
            GUILayout.Label("阶段 " + EvidenceMilestoneName(evidenceMilestoneIndex) + " | " + BuildNextEvidenceMilestoneHint());
            GUILayout.EndArea();

            float promptWidth = Mathf.Clamp(Screen.width * 0.34f, 420f, 560f);
            Rect promptRect = new Rect((Screen.width - promptWidth) * 0.5f, Screen.height - 66f, promptWidth, 48f);
            GUILayout.BeginArea(promptRect, GUI.skin.box);
            GUILayout.Label(BuildLocalActionHint() + " | WASD/E/Q/R/F/V/M/I");
            GUILayout.EndArea();

            float miniWidth = Mathf.Clamp(Screen.width * 0.13f, 165f, 220f);
            Rect miniRect = new Rect(Screen.width - miniWidth - 18f, 14f, miniWidth, 128f);
            GUILayout.BeginArea(miniRect, GUI.skin.box);
            GUILayout.Label("小地图");
            DrawTacticalMapMini();
            GUILayout.EndArea();

            int activeBodies = CountUnreportedBodies();
            if (activeBodies > 0)
            {
                Rect alertRect = new Rect(18f, 86f, Mathf.Clamp(Screen.width * 0.2f, 220f, 300f), 56f);
                GUILayout.BeginArea(alertRect, GUI.skin.box);
                GUILayout.Label("未报案尸体: " + activeBodies);
                GUILayout.Label(status);
                GUILayout.EndArea();
            }

            DrawRoleAbilityMeter();
        }

        // --- DrawRoleAbilityMeter (moved from main controller) ---
        private void DrawRoleAbilityMeter()
        {
            if (!players.TryGetValue(LocalClientId(), out OnlinePlayerState localState) || !localState.Alive)
            {
                return;
            }

            Rect rect = new Rect(18f, Screen.height - 92f, Mathf.Clamp(Screen.width * 0.15f, 180f, 250f), 56f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("技能 | " + ProfessionName(localState.Profession));

            float abilityCooldown = abilityCooldowns.TryGetValue(localState.ClientId, out float value) ? value : localState.AbilityCooldown;
            float ratio = Mathf.Clamp01(1f - abilityCooldown / ruleSet.AbilityCooldownSeconds);
            Rect bar = GUILayoutUtility.GetRect(rect.width - 18f, 12f);
            DrawProgressBar(bar, ratio, ratio >= 1f ? new Color(0.12f, 0.74f, 0.36f, 1f) : new Color(0.08f, 0.42f, 0.72f, 1f));
            GUILayout.Label(ratio >= 1f ? "F 可用" : "冷却 " + Mathf.CeilToInt(abilityCooldown) + "s");
            GUILayout.EndArea();
        }

        // --- DrawActionIntelPanel (moved from main controller) ---
        private void DrawActionIntelPanel()
        {
            float width = Mathf.Clamp(Screen.width * 0.24f, 300f, 420f);
            float height = Mathf.Clamp(Screen.height * 0.52f, 360f, 560f);
            Rect rect = new Rect(18f, 108f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("案情板");
            intelScroll = GUILayout.BeginScrollView(intelScroll);
            GUILayout.Label(BuildFocusedIntel());
            GUILayout.Space(8f);
            GUILayout.Label(BuildTaskList());
            GUILayout.Space(8f);
            GUILayout.Label(BuildCaseLog());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // --- DrawActiveTaskPanel (moved from main controller) ---
        private void DrawActiveTaskPanel()
        {
            // 小游戏接管时由其自建 Canvas 呈现，OnGUI 经典面板让位，避免双层叠加。
            if (activeTaskId < 0 || activeMiniGame != null)
            {
                return;
            }

            OnlineTaskState task = GetTask(activeTaskId);
            float width = Mathf.Clamp(Screen.width * 0.46f, 520f, 760f);
            float height = 428f;
            Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("现场任务 | " + task.Name);
            GUILayout.Label(TaskPanelInstruction(activeTaskId));
            GUILayout.Space(4f);
            Rect tagRect = GUILayoutUtility.GetRect(width - 44f, 42f);
            DrawTaskMiniGameTag(tagRect, TaskPanelTemplateTitle(activeTaskId), TaskPanelTemplateSubtitle(activeTaskId), TaskPanelAccent(activeTaskId));
            GUILayout.Space(6f);
            DrawTaskMiniGameWidget(activeTaskId, width - 44f, 112f);
            GUILayout.Space(6f);
            Rect sequenceRect = GUILayoutUtility.GetRect(width - 44f, 108f);
            DrawTaskSequenceRail(sequenceRect, activeTaskId);
            GUILayout.Space(6f);

            Rect progressRect = GUILayoutUtility.GetRect(width - 44f, 24f);
            DrawProgressBar(progressRect, activeTaskCharge, new Color(0.08f, 0.62f, 0.82f, 1f));
            GUILayout.Label("证据价值 +" + TaskEvidenceValue(activeTaskId) + " | 错误 " + activeTaskMistakes + "/3 | 模板 " + TaskPanelTemplateTitle(activeTaskId));
            DrawTaskFeedbackBanner(width - 44f);

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawTaskStepButton("键 " + CorrectTaskStepInput(activeTaskId, 0), CorrectTaskStepInput(activeTaskId, 0), activeTaskStepOneDone, activeTaskStep == 0);
            DrawTaskStepButton("键 " + CorrectTaskStepInput(activeTaskId, 1), CorrectTaskStepInput(activeTaskId, 1), activeTaskStepTwoDone, activeTaskStep == 1);
            DrawTaskStepButton("键 " + CorrectTaskStepInput(activeTaskId, 2), CorrectTaskStepInput(activeTaskId, 2), activeTaskStepThreeDone, activeTaskStep == 2);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label("按高亮顺序点击或输入数字键，按住 Space 扫描/同步，Esc 退出 | " + TaskPanelFooter(activeTaskId));
            GUILayout.EndArea();
        }

        // --- DrawTaskFeedbackBanner (moved from main controller) ---
        private void DrawTaskFeedbackBanner(float width)
        {
            if (activeTaskFeedbackTimer <= 0f)
            {
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(width, 26f);
            Color oldColor = GUI.color;
            GUI.color = activeTaskFeedbackPositive ? new Color(0.1f, 0.62f, 0.28f, 0.9f) : new Color(0.78f, 0.14f, 0.1f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 20f, rect.height - 6f), activeTaskFeedbackPositive ? "校验通过" : "输入不匹配");
            GUI.color = oldColor;
        }

        // --- DrawTaskMiniGameWidget (moved from main controller) ---
        private void DrawTaskMiniGameWidget(int taskId, float width, float height)
        {
            Rect widget = GUILayoutUtility.GetRect(width, height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.045f, 0.06f, 0.065f, 0.96f);
            GUI.DrawTexture(widget, Texture2D.whiteTexture);

            int mode = TaskTemplateMode(taskId);

            if (mode == 0)
            {
                DrawTaskScreenGrid(widget);
            }
            else if (mode == 1)
            {
                DrawTaskSealScanner(widget);
            }
            else if (mode == 2)
            {
                DrawTaskBreakerWidget(widget);
            }
            else if (mode == 3)
            {
                DrawTaskEvidenceTray(widget);
            }
            else if (mode == 4)
            {
                DrawTaskLedgerWidget(widget);
            }
            else
            {
                DrawTaskRouteWidget(widget);
            }

            GUI.color = oldColor;
        }

        // --- DrawTaskMiniGameTag (moved from main controller) ---
        private void DrawTaskMiniGameTag(Rect rect, string title, string subtitle, Color accent)
        {
            GUI.color = new Color(0.09f, 0.1f, 0.11f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect textRect = new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, rect.height - 12f);
            GUI.Label(textRect, title + "\n" + subtitle);
        }

        // --- DrawTaskSequenceRail (moved from main controller) ---
        private void DrawTaskSequenceRail(Rect rect, int taskId)
        {
            Color oldColor = GUI.color;
            int nextInput = CorrectTaskStepInput(taskId, Mathf.Clamp(activeTaskStep, 0, 2));
            Color accent = TaskPanelAccent(taskId);

            for (int i = 0; i < 3; i++)
            {
                int required = CorrectTaskStepInput(taskId, i);
                bool completed = i == 0 ? activeTaskStepOneDone : i == 1 ? activeTaskStepTwoDone : activeTaskStepThreeDone;
                bool current = !completed && activeTaskStep == i;
                float segmentWidth = rect.width / 3f - 10f;
                Rect segment = new Rect(rect.x + i * rect.width / 3f + 5f, rect.y + 10f, segmentWidth, rect.height - 20f);
                GUI.color = completed ? new Color(0.12f, 0.64f, 0.34f, 1f) : current ? accent : new Color(0.12f, 0.15f, 0.16f, 1f);
                GUI.DrawTexture(segment, Texture2D.whiteTexture);
                GUI.color = current ? new Color(1f, 1f, 1f, 0.96f) : new Color(0.04f, 0.05f, 0.055f, 1f);
                GUI.DrawTexture(new Rect(segment.x + 8f, segment.y + 8f, segment.width - 16f, 5f), Texture2D.whiteTexture);
                GUI.color = completed ? Color.white : current ? Color.black : new Color(0.78f, 0.82f, 0.8f, 1f);
                GUI.Label(new Rect(segment.x + 10f, segment.y + 20f, segment.width - 20f, segment.height - 28f), completed ? "已校验\n键 " + required : current ? "下一步\n键 " + nextInput : "等待\n键 " + required);
            }

            if (activeTaskFeedbackTimer > 0f)
            {
                GUI.color = activeTaskFeedbackPositive ? new Color(0.18f, 0.9f, 0.42f, 0.72f) : new Color(0.95f, 0.14f, 0.08f, 0.72f);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 5f, rect.width, 5f), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }

        // --- DrawTaskStepButton (moved from main controller) ---
        private void DrawTaskStepButton(string label, int input, bool completed, bool current)
        {
            Color oldColor = GUI.color;
            GUI.color = completed ? new Color(0.16f, 0.72f, 0.36f, 1f) : current ? TaskPanelAccent(activeTaskId) : new Color(0.18f, 0.22f, 0.24f, 1f);

            if (completed)
            {
                GUILayout.Box("完成 " + label, GUILayout.Height(44f), GUILayout.ExpandWidth(true));
            }
            else if (GUILayout.Button((current ? "执行 " : "待命 ") + label, GUILayout.Height(44f), GUILayout.ExpandWidth(true)))
            {
                ResolveActiveTaskStep(input);
            }

            GUI.color = oldColor;
        }

        // --- LocalProfessionName (moved from main controller) ---
        private string LocalProfessionName()
        {
            if (players.TryGetValue(LocalClientId(), out OnlinePlayerState state))
            {
                return ProfessionName(state.Profession);
            }

            return "待分配";
        }

        // --- DrawOpeningBriefing (moved from main controller) ---
        private void DrawOpeningBriefing()
        {
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("专案简报");
            GUILayout.Label("你的身份: " + RoleName(localRole));
            GUILayout.Label("你的职责: " + LocalProfessionName());
            GUILayout.Label("地图: 九龙港区封控街区");
            GUILayout.Label("局长: 目标 10-20 分钟；20 分钟未闭合关键证据则按证据比例结算。");
            GUILayout.Label("关键机制: 大地图巡线、现场小任务、尸体报案、会议文本讨论、投票放逐、黑帮暗线通道。");
            GUILayout.Label("目标: 警方搜证和投出黑帮；黑帮制造不在场证明、破坏证据链并误导会议。");
            GUILayout.Space(6f);
            Rect routeRect = GUILayoutUtility.GetRect(360f, 62f, GUILayout.ExpandWidth(true));
            DrawOpeningRouteCards(routeRect);
            GUILayout.Label("行动倒计时: " + Mathf.CeilToInt(phaseTimer) + "s");
            GUILayout.EndVertical();
        }

        // --- DrawOpeningRouteCards (moved from main controller) ---
        private void DrawOpeningRouteCards(Rect rect)
        {
            Color oldColor = GUI.color;
            string[] labels = { "货柜码头", "监控中心", "夜市情报", "洗钱账房", "证物冷库" };
            Color[] colors =
            {
                new Color(0.18f, 0.36f, 0.32f, 1f),
                new Color(0.08f, 0.3f, 0.42f, 1f),
                new Color(0.46f, 0.14f, 0.1f, 1f),
                new Color(0.18f, 0.2f, 0.34f, 1f),
                new Color(0.34f, 0.22f, 0.42f, 1f)
            };

            float gap = 8f;
            float cardWidth = (rect.width - gap * (labels.Length - 1)) / labels.Length;

            for (int i = 0; i < labels.Length; i++)
            {
                Rect card = new Rect(rect.x + i * (cardWidth + gap), rect.y, cardWidth, rect.height);
                GUI.color = colors[i];
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(card.x + 8f, card.y + 8f, card.width - 16f, card.height - 16f), labels[i] + "\n" + OpeningRouteStatus(i));
            }

            GUI.color = oldColor;
        }

        // --- DrawTacticalMapMini (moved from main controller) ---
        private void DrawTacticalMapMini()
        {
            float mapHeight = phase == OnlineMatchPhase.Action && !tacticalMapOpen ? 92f : 132f;
            Rect rect = GUILayoutUtility.GetRect(180f, mapHeight, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "港区小地图");
            DrawMapRect(rect, false);
        }

        // --- DrawLargeMapPreview (moved from main controller) ---
        private void DrawLargeMapPreview()
        {
            float width = Mathf.Min(Screen.width * 0.74f, 1180f);
            float height = Mathf.Min(Screen.height * 0.68f, 760f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f + 26f, width, height);
            GUI.Box(rect, "九龙港区封控全图 | M/Tab 收起");
            DrawMapRect(new Rect(rect.x + 18f, rect.y + 34f, rect.width - 36f, rect.height - 52f), true);

            Rect legend = new Rect(rect.x + 22f, rect.y + rect.height - 42f, rect.width - 44f, 28f);
            GUILayout.BeginArea(legend);
            GUILayout.Label("黄点 玩家 | 青点 任务 | 红点 被破坏/尸体 | 紫点 暗线 | 蓝色区域 警方据点 | 棕红区域 黑帮高风险区");
            GUILayout.EndArea();
        }

        // --- DrawMapRect (moved from main controller) ---
        private void DrawMapRect(Rect rect, bool withLabels)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.075f, 0.08f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            DrawMiniMapCorridors(rect);

            foreach (OnlineMapService.ShipRoomSpec room in mapService.ShipRooms())
            {
                DrawMiniMapArea(rect, mapService.ScaleMapPosition(room.Center), mapService.ScaleMapSize(room.Size), room.Floor, withLabels ? room.Label : string.Empty);
            }

            for (int i = 0; i < ruleSet.UnderworldPassageCount; i++)
            {
                DrawMiniMapDot(rect, mapService.UnderworldPassagePosition(i, ruleSet.UnderworldPassageCount), new Color(0.78f, 0.2f, 0.86f, 1f), withLabels ? 8f : 5f);
            }

            foreach (OnlineTaskState task in tasks)
            {
                Color taskColor = task.Completed ? Color.green : task.Sabotaged ? Color.red : Color.cyan;
                DrawMiniMapDot(rect, task.Position, taskColor, withLabels ? 7f : 5f);

                if (withLabels)
                {
                    DrawMiniMapLabel(rect, task.Position, TaskMapCode(task.Id), taskColor);
                }
            }

            foreach (OnlineBodyState body in killSystem.bodies)
            {
                if (!body.Reported)
                {
                    DrawMiniMapDot(rect, body.Position, new Color(1f, 0.05f, 0.04f, 1f), withLabels ? 9f : 6f);
                    if (withLabels)
                    {
                        DrawMiniMapLabel(rect, body.Position, "尸", new Color(1f, 0.05f, 0.04f, 1f));
                    }
                }
            }

            foreach (OnlinePlayerState state in players.Values)
            {
                Color playerColor = state.Alive ? Color.yellow : Color.gray;
                DrawMiniMapDot(rect, state.Position, playerColor, withLabels ? 8f : 6f);

                if (withLabels)
                {
                    DrawMiniMapLabel(rect, state.Position, state.ClientId == LocalClientId() ? "你" : ShortDisplayName(state.DisplayName, 3), playerColor);
                }
            }

            GUI.color = oldColor;
        }

        // --- DrawMiniMapCorridors (moved from main controller) ---
        private void DrawMiniMapCorridors(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.22f, 0.25f, 0.26f, 1f);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(0f, -0.18f, 0f)), mapService.ScaleMapSize(new Vector3(15.5f, 1.2f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(0f, 3.65f, 0f)), mapService.ScaleMapSize(new Vector3(16.4f, 1.04f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(0.12f, -3.9f, 0f)), mapService.ScaleMapSize(new Vector3(15.4f, 1.04f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(-6.85f, 0.15f, 0f)), mapService.ScaleMapSize(new Vector3(1.08f, 8.35f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(7.05f, 0.08f, 0f)), mapService.ScaleMapSize(new Vector3(1.08f, 8.18f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(0f, 1.85f, 0f)), mapService.ScaleMapSize(new Vector3(1.08f, 3.15f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, mapService.ScaleMapPosition(new Vector3(0f, -2.35f, 0f)), mapService.ScaleMapSize(new Vector3(1.08f, 3.05f, 0f)), GUI.color, string.Empty);
            DrawMiniMapDot(rect, mapService.ScaleMapPosition(new Vector3(0f, -0.35f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 13f);
            DrawMiniMapDot(rect, mapService.ScaleMapPosition(new Vector3(-6.85f, 3.65f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, mapService.ScaleMapPosition(new Vector3(7.05f, 3.65f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, mapService.ScaleMapPosition(new Vector3(-6.85f, -3.9f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, mapService.ScaleMapPosition(new Vector3(7.05f, -3.9f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            GUI.color = oldColor;
        }

        // --- DrawMiniMapArea (moved from main controller) ---
        private void DrawMiniMapArea(Rect mapRect, Vector3 worldCenter, Vector3 worldSize, Color color, string label)
        {
            Rect area = WorldRectToMapRect(mapRect, worldCenter, worldSize);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(label))
            {
                GUI.Label(area, label);
            }

            GUI.color = oldColor;
        }

        // --- DrawMiniMapDot (moved from main controller) ---
        private void DrawMiniMapDot(Rect mapRect, Vector3 worldPosition, Color color, float size)
        {
            Vector2 point = WorldToMapPoint(mapRect, worldPosition);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        // --- DrawMiniMapLabel (moved from main controller) ---
        private void DrawMiniMapLabel(Rect mapRect, Vector3 worldPosition, string label, Color color)
        {
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            Vector2 point = WorldToMapPoint(mapRect, worldPosition);
            Rect labelRect = new Rect(point.x + 5f, point.y - 9f, 64f, 20f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.015f, 0.018f, 0.02f, 0.78f);
            GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(labelRect.x, labelRect.y, 3f, labelRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(labelRect.x + 6f, labelRect.y + 1f, labelRect.width - 8f, labelRect.height - 2f), label);
            GUI.color = oldColor;
        }

        // --- DrawVotePanel (moved from main controller) ---
        private void DrawVotePanel()
        {
            GUILayout.Space(6f);
            GUILayout.Label("会议投票");

            if (phase == OnlineMatchPhase.Meeting)
            {
                GUILayout.Label("讨论倒计时：" + Mathf.CeilToInt(phaseTimer) + "s");
            }
            else
            {
                GUILayout.Label("投票倒计时：" + Mathf.CeilToInt(phaseTimer) + "s");
            }

            ulong localClientId = LocalClientId();
            bool canVote = players.TryGetValue(localClientId, out OnlinePlayerState localState) && localState.Alive;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canVote;

            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.Alive || state.ClientId == localClientId)
                {
                    continue;
                }

                if (GUILayout.Button("投票给 " + state.DisplayName))
                {
                    SendClientAction(OnlineActionType.Vote, state.ClientId);
                }
            }

            if (GUILayout.Button("跳过投票"))
            {
                SendClientAction(OnlineActionType.SkipVote);
            }

            GUI.enabled = previousEnabled;
        }

        // --- DrawActionChatPanel (moved from main controller) ---
        private void DrawActionChatPanel()
        {
            if (chatSystem == null)
            {
                return;
            }

            float chatWidth = Mathf.Clamp(Screen.width * 0.22f, 240f, 340f);
            float chatHeight = Mathf.Clamp(Screen.height * 0.35f, 220f, 360f);
            Rect chatArea = new Rect(Screen.width - chatWidth - 18f, Screen.height - chatHeight - 18f, chatWidth, chatHeight);

            chatSystem.CurrentPhase = OnlineMatchPhase.Action;
            chatSystem.CanSend = IsLocalAlive();
            chatSystem.IsAlive = IsLocalAlive();
            chatSystem.LocalFaction = ChatSystem.RoleToFaction(LocalEffectiveRole());
            chatSystem.ProcessInputKeys();
            chatSystem.DrawChatPanel(chatArea, null);
        }

        // --- DrawMeetingScreen (moved from main controller) ---
        private void DrawMeetingScreen()
        {
            float boardWidth = Mathf.Clamp(Screen.width * 0.58f, 720f, 980f);
            float boardHeight = Mathf.Clamp(Screen.height * 0.72f, 520f, 760f);
            Rect board = new Rect((Screen.width - boardWidth) * 0.5f, (Screen.height - boardHeight) * 0.5f, boardWidth, boardHeight);
            GUILayout.BeginArea(board, GUI.skin.box);
            GUILayout.Label("九龙港城会议");
            GUILayout.Label(status);
            GUILayout.Label((phase == OnlineMatchPhase.Meeting ? "讨论倒计时 " : "投票倒计时 ") + Mathf.CeilToInt(phaseTimer) + "s | 投票 " + votes.Count + "/" + CountAlivePlayers());

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(boardWidth * 0.54f));
            DrawMeetingEvidenceStrip(boardWidth * 0.52f);
            GUILayout.Space(6f);
            DrawMeetingRosterButtons();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            float rightWidth = boardWidth * 0.4f;

            // 案情记录（上半部分）
            GUILayout.Label("案情记录");
            float intelHeight = boardHeight * 0.38f;
            intelScroll = GUILayout.BeginScrollView(intelScroll, GUILayout.Height(intelHeight));
            GUILayout.Label(BuildFocusedIntel());
            GUILayout.Space(8f);
            GUILayout.Label(BuildCaseLog());
            GUILayout.EndScrollView();

            // 聊天区域（下半部分）
            GUILayout.Space(6f);
            if (chatSystem != null)
            {
                float chatHeight = boardHeight * 0.42f;
                Rect chatArea = GUILayoutUtility.GetRect(rightWidth, chatHeight);
                chatSystem.CurrentPhase = phase;
                chatSystem.CanSend = IsLocalAlive();
                chatSystem.IsAlive = IsLocalAlive();
                chatSystem.LocalFaction = ChatSystem.RoleToFaction(LocalEffectiveRole());
                chatSystem.ProcessInputKeys();
                chatSystem.DrawChatPanel(chatArea, null);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // --- DrawMeetingEvidenceStrip (moved from main controller) ---
        private void DrawMeetingEvidenceStrip(float width)
        {
            GUILayout.Label("会议证据墙");
            Rect rect = GUILayoutUtility.GetRect(width, 124f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.055f, 0.065f, 0.07f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float progress = Mathf.Clamp01(taskService.EvidenceScore / (float)Mathf.Max(1, taskService.EvidenceTarget));
            GUI.color = new Color(0.08f, 0.62f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 16f, (rect.width - 24f) * progress, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(0.72f, 0.18f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 12f, rect.y + 40f, Mathf.Clamp01(CountUnreportedBodies() / 3f) * (rect.width - 24f), 8f), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.68f, 0.12f, 1f);

            int sabotaged = CountSabotagedTasks();
            for (int i = 0; i < sabotaged; i++)
            {
                float x = rect.x + 14f + i * 18f;
                GUI.DrawTexture(new Rect(x, rect.y + 60f, 12f, 12f), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 18f, rect.width - 24f, rect.height - 18f), "证据链 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget + "\n未报案 " + CountUnreportedBodies() + " | 被破坏任务 " + sabotaged + "\n" + BuildMeetingEvidenceDigest() + "\n会议原因: " + lastMeetingReason + "\n上轮结论: " + lastVoteOutcome);
            GUI.color = oldColor;
        }

        // --- BuildMeetingEvidenceDigest (moved from main controller) ---
        private string BuildMeetingEvidenceDigest()
        {
            OnlineTaskState keyTask = FindHighestValueOpenTask();
            string keyTaskText = keyTask.Id >= 0 ? "关键未闭合: " + keyTask.Name + " +" + TaskEvidenceValue(keyTask.Id) : "关键未闭合: 无";
            string dossier = evidenceDossier?.MeetingEvidenceDossier() ?? "";
            if (!string.IsNullOrEmpty(dossier))
                return keyTaskText + "\n\n【证据指证】\n" + dossier;
            return keyTaskText + " | 当前票型 " + BuildVoteTallySummary();
        }

        // --- BuildVoteTallySummary (moved from main controller) ---
        private string BuildVoteTallySummary()
        {
            if (votes.Count == 0)
            {
                return "无人投票";
            }

            Dictionary<ulong, int> tally = new Dictionary<ulong, int>();
            int skipVotes = 0;

            foreach (ulong targetClientId in votes.Values)
            {
                if (targetClientId == SkipVoteTarget)
                {
                    skipVotes++;
                    continue;
                }

                tally[targetClientId] = tally.TryGetValue(targetClientId, out int count) ? count + 1 : 1;
            }

            string lead = skipVotes > 0 ? "跳过 " + skipVotes : "跳过 0";

            // Mark SecretVote voters
            StringBuilder secretVoters = new StringBuilder();
            foreach (var kv in votes)
            {
                if (players.TryGetValue(kv.Key, out OnlinePlayerState voterState)
                    && ruleSet != null && ruleSet.HasAbility(voterState.Profession, AbilityType.SecretVote))
                {
                    if (secretVoters.Length > 0)
                    {
                        secretVoters.Append(" | ");
                    }
                    secretVoters.Append(voterState.DisplayName).Append(" 秘密投票");
                }
            }

            foreach (KeyValuePair<ulong, int> pair in tally)
            {
                if (players.TryGetValue(pair.Key, out OnlinePlayerState state))
                {
                    lead += " | " + state.DisplayName + " " + pair.Value;
                }
            }

            if (secretVoters.Length > 0)
            {
                lead += " | " + secretVoters.ToString();
            }

            return lead;
        }

        // --- BuildVoteTallySummary (moved from main controller) ---
        private string BuildVoteTallySummary(Dictionary<ulong, int> tally)
        {
            if (tally == null || tally.Count == 0)
            {
                return "无人得票";
            }

            StringBuilder builder = new StringBuilder();

            // Show SecretVote voters separately
            StringBuilder secretBuilder = new StringBuilder();
            foreach (var kv in votes)
            {
                if (players.TryGetValue(kv.Key, out OnlinePlayerState voterState)
                    && ruleSet != null && ruleSet.HasAbility(voterState.Profession, AbilityType.SecretVote))
                {
                    if (secretBuilder.Length > 0)
                    {
                        secretBuilder.Append(" | ");
                    }
                    secretBuilder.Append(voterState.DisplayName).Append(" 秘密投票");
                }
            }

            foreach (KeyValuePair<ulong, int> pair in tally)
            {
                if (!players.TryGetValue(pair.Key, out OnlinePlayerState state))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(state.DisplayName).Append(" ").Append(pair.Value);
            }

            if (secretBuilder.Length > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(secretBuilder);
            }

            return builder.Length == 0 ? "无人得票" : builder.ToString();
        }

        // --- DrawMeetingRosterButtons (moved from main controller) ---
        private void DrawMeetingRosterButtons()
        {
            ulong localClientId = LocalClientId();
            bool canVote = players.TryGetValue(localClientId, out OnlinePlayerState localState) && localState.Alive;
            bool previousEnabled = GUI.enabled;

            foreach (OnlinePlayerState state in players.Values)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                string voteBadge = votes.ContainsKey(state.ClientId) ? "已投" : "未投";
                GUILayout.Label(state.DisplayName + " | " + (state.Alive ? "在场" : "出局") + " | 嫌疑 " + state.Suspicion + " | " + voteBadge + " | " + ProfessionName(state.Profession), GUILayout.ExpandWidth(true));
                GUI.enabled = previousEnabled && canVote && state.Alive && state.ClientId != localClientId;

                if (GUILayout.Button("投票", GUILayout.Width(92f)))
                {
                    SendClientAction(OnlineActionType.Vote, state.ClientId);
                }

                GUI.enabled = previousEnabled;
                GUILayout.EndHorizontal();
            }

            GUI.enabled = previousEnabled && canVote;

            if (GUILayout.Button("跳过投票", GUILayout.Height(36f)))
            {
                SendClientAction(OnlineActionType.SkipVote);
            }

            GUI.enabled = previousEnabled;
        }

        // --- DrawResultScreen (moved from main controller) ---
        private void DrawResultScreen()
        {
            float width = Mathf.Clamp(Screen.width * 0.58f, 720f, 980f);
            float height = Mathf.Clamp(Screen.height * 0.62f, 460f, 680f);
            Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("行动结算");
            GUILayout.Label(resultSummary);
            GUILayout.Space(8f);
            DrawResultScoreboard(width - 42f);
            GUILayout.Space(8f);
            GUILayout.Label(BuildResultRosterLine());
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = IsHost;

            if (GUILayout.Button("重开同房间", GUILayout.Height(38f)))
            {
                RestartMatch();
            }

            GUI.enabled = previousEnabled;

            if (GUILayout.Button("返回房间", GUILayout.Height(38f)))
            {
                ReturnToLobby();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // --- DrawResultScoreboard (moved from main controller) ---
        private void DrawResultScoreboard(float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 120f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.055f, 0.065f, 0.07f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float evidenceRatio = Mathf.Clamp01(taskService.EvidenceScore / (float)Mathf.Max(1, taskService.EvidenceTarget));
            float taskRatio = Mathf.Clamp01(CountCompletedTasks() / (float)Mathf.Max(1, tasks.Count));
            float survivalRatio = Mathf.Clamp01(CountAlivePlayers() / (float)Mathf.Max(1, players.Count));

            DrawResultBar(new Rect(rect.x + 14f, rect.y + 18f, rect.width - 28f, 16f), evidenceRatio, new Color(0.08f, 0.62f, 0.82f, 1f), "证据链 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget);
            DrawResultBar(new Rect(rect.x + 14f, rect.y + 50f, rect.width - 28f, 16f), taskRatio, new Color(0.86f, 0.68f, 0.12f, 1f), "任务 " + CountCompletedTasks() + "/" + tasks.Count);
            DrawResultBar(new Rect(rect.x + 14f, rect.y + 82f, rect.width - 28f, 16f), survivalRatio, new Color(0.14f, 0.7f, 0.36f, 1f), "存活 " + CountAlivePlayers() + "/" + players.Count);
            GUI.color = oldColor;
        }

        // --- BuildResultRosterLine (moved from main controller) ---
        private string BuildResultRosterLine()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("身份公开: ");

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                builder.Append(pair.Value.DisplayName)
                    .Append("/")
                    .Append(RoleName(GetPrivateRole(pair.Key)))
                    .Append(pair.Value.Alive ? " " : "(出局) ");
            }

            return builder.ToString();
        }

        // --- BuildPlayerWorldLabel (moved from main controller) ---
        private string BuildPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            return worldBuilder.BuildPlayerWorldLabel(state, isLocal);
        }
    }
}
