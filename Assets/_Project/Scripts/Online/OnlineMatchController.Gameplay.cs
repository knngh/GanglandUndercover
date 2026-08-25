using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using GanglandUndercover.Audio;
using GanglandUndercover.Core;
using GanglandUndercover.Online.MiniGames;
using UnityEngine.Rendering;

namespace GanglandUndercover.Online
{
    /// <summary>B1: Extracted methods</summary>
    public sealed partial class OnlineMatchController
    {
        public Dictionary<ulong, OnlinePlayerState> Players => players;
        public IReadOnlyList<OnlineTaskState> Tasks => tasks;
        public bool IsTaskVisibleToLocalPlayer(OnlineTaskState task)
        {
            return IsTaskVisibleToPlayer(LocalClientId(), task);
        }

        public bool HasLocalKillTarget => TryGetLocalKillTarget(out _);

        private bool TryGetLocalKillTarget(out ulong targetClientId)
        {
            targetClientId = SkipVoteTarget;
            ulong localClientId = LocalClientId();
            if (!players.TryGetValue(localClientId, out OnlinePlayerState localState) || !localState.Alive)
            {
                return false;
            }

            return TryFindNearestVictim(localClientId, localState.Position, out targetClientId, out _);
        }

        internal bool IsTaskVisibleToPlayer(ulong clientId, OnlineTaskState task)
        {
            TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
            return task.Sabotaged
                || taskSync == null
                || !taskSync.HasAssignments
                || taskSync.IsTaskAssignedTo(clientId, task.Id);
        }

        internal OnlineTaskState TaskForPlayer(ulong clientId, OnlineTaskState task)
        {
            TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
            return taskSync != null && taskSync.HasAssignments
                ? taskSync.TaskForPlayer(clientId, task)
                : task;
        }

        public OnlineTaskState TaskForLocalPlayer(OnlineTaskState task)
        {
            return TaskForPlayer(LocalClientId(), task);
        }

        public int PersonalTaskCount
        {
            get
            {
                TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
                return taskSync != null && taskSync.HasAssignments
                    ? taskSync.AssignedCountFor(LocalClientId())
                    : tasks.Count;
            }
        }

        public int PersonalCompletedTaskCount
        {
            get
            {
                TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
                return taskSync != null && taskSync.HasAssignments
                    ? taskSync.CompletedCountFor(LocalClientId())
                    : CountCompletedTasks();
            }
        }

        public int TeamTaskCount
        {
            get
            {
                TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
                return taskSync != null && taskSync.HasAssignments
                    ? taskSync.TotalAssignedCount()
                    : tasks.Count;
            }
        }

        public int TeamCompletedTaskCount
        {
            get
            {
                TaskSync taskSync = syncManager != null ? syncManager.TaskSync : null;
                return taskSync != null && taskSync.HasAssignments
                    ? taskSync.CompletedCount(tasks)
                    : CountCompletedTasks();
            }
        }
        internal List<GameStateSnapshot.SnapshotTaskAssignmentEntry> TaskSyncAssignmentsSnapshot()
        {
            return syncManager != null && syncManager.TaskSync != null
                ? syncManager.TaskSync.ExportAssignments()
                : new List<GameStateSnapshot.SnapshotTaskAssignmentEntry>();
        }

        internal void LoadTaskSyncAssignments(IReadOnlyList<GameStateSnapshot.SnapshotTaskAssignmentEntry> assignments)
        {
            syncManager?.TaskSync?.LoadAssignments(assignments);
        }
        public List<OnlineBodyState> Bodies => killSystem != null ? killSystem.bodies : null;
        public IReadOnlyList<string> CaseLog => caseLog;
        public OnlineRole LocalRole => localRole;
        public string LocalPlayerName => localPlayerName;
        public string JoinAddress => joinAddress;
        public string RelayJoinCode => relayJoinCode;
        public string RelayJoinInput => relayJoinInput;
        public string RelayStatus => relayStatus;
        public string RelayLobbySummary => OnlineMatchUtils.BuildRelayLobbySummary(
            relayStatus,
            relayJoinCode,
            relayJoinInput,
            relayOperationInProgress,
            IsOnline,
            IsHost,
            IsClientConnected,
            ConnectedClientCount);
        public string RoomName => roomName;
        public bool IsHost => localPreviewMode || networkManager != null && networkManager.IsHost;
        public bool IsLocalPreview => localPreviewMode;
        public int ConnectedClientCount => networkManager != null ? networkManager.ConnectedClientsList.Count : 0;
        public bool IsListeningOrConnected => networkManager != null && networkManager.IsListening;
        public bool IsClientConnected => networkManager != null && networkManager.IsConnectedClient;
        public bool HasActiveMiniGame => minigameService != null && minigameService.HasActiveMiniGame;
        public int ActiveTaskId => minigameService != null ? minigameService.ActiveTaskId : -1;
        public bool MatchStarted => matchStarted;
        public OnlineMatchPhase Phase => phase;
        public float PhaseTimer => phaseTimer;
        public string Status { get => status; internal set => status = value; }
        public int TaskCount => tasks.Count;
        public int BodyCount => killSystem != null ? killSystem.bodies.Count : 0;
        public int BotCount => _botController?.BotCount ?? 0;
        public int PlayerCount => players.Count;
        public int CaseLogCount => caseLog.Count;
        public bool HasWorld => worldRoot != null;
        public bool HasCanvasHud => onlineHud != null;
        public int CollisionObjectCount => solidObstacleRects.Count;
        public int VerticalSliceMapOverlayCount => onlineHud == null ? 0 : onlineHud.VerticalSliceStaticMapElementCount;
        public float BlackoutTimer => taskService.BlackoutTimer;
        public float LockdownTimer => taskService.LockdownTimer;
        public float CommunicationJamTimer => taskService.CommunicationJamTimer;
        public float EvidenceLeakTimer => taskService.EvidenceLeakTimer;
        public float PatrolAlertTimer => taskService.PatrolAlertTimer;
        public bool IsMeetingPhase => phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
        public bool TacticalMapOpen => tacticalMapOpen;
        public bool IntelBoardOpen => intelBoardOpen;
        public string VoiceStatus => chatSystem != null ? "聊天: " + ChatSystem.ChannelDisplayName(chatSystem.CurrentChannel) : "聊天未初始化";
        public int VoiceParticipantCount => chatSystem != null ? chatSystem.MessageCount : 0;
        public bool VoiceRoutingEnabled => true; // 文本聊天始终可用
        public bool LocalTaskInputGateActive => minigameService != null && minigameService.HasActiveTask;
        public void EditorSkipOpeningForSmokeTest()
        {
            if (phase == OnlineMatchPhase.Opening)
            {
                phase = OnlineMatchPhase.Action;
                phaseTimer = 0f;
                fullMapPreview = false;
                tacticalMapOpen = false;
                intelBoardOpen = false;
                status = "编辑器烟测跳过专案简报。";
            }
        }
        public Vector3 EditorResolveCollisionForSmokeTest(Vector3 from, Vector3 requested)
        {
            return ResolveMapCollision(from, requested);
        }
        public bool EditorIsWalkableForSmokeTest(Vector3 position)
        {
            return IsWalkable(position);
        }
        public void EditorToggleTacticalMapForSmokeTest()
        {
            tacticalMapOpen = true;
            fullMapPreview = true;
        }
        public bool EditorConfigureActionCameraForSmokeTest()
        {
            tacticalMapOpen = false;
            fullMapPreview = false;
            intelBoardOpen = false;
            minigameService?.Reset();
            phase = OnlineMatchPhase.Action;
            _cameraRig.ResetConfiguration();
            ConfigureMainCamera();
            return Camera.main != null && Camera.main.orthographic;    // M3: camera is always orthographic now
        }
        public bool EditorForceActionPreviewForSmokeTest()
        {
            // The smoke view represents the playable local room, so the HUD must
            // exercise the same online/compact state as an actual preview match.
            if (!localPreviewMode)
            {
                localPreviewMode = true;
            }

            Vector3 showcasePosition = FindActionPreviewStartPosition();

            if (players.Count == 0)
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(LocalPreviewClientId, "烟测玩家", showcasePosition, true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            ulong localClientId = LocalClientId();

            if (players.TryGetValue(localClientId, out OnlinePlayerState localState))
            {
                localState.Position = showcasePosition;
                localState.Input = Vector2.zero;
                localState.Alive = true;
                localState.Ready = true;
                players[localClientId] = localState;
            }

            localRole = LocalEffectiveRole();
            phase = OnlineMatchPhase.Action;
            phaseTimer = 0f;
            fullMapPreview = false;
            tacticalMapOpen = false;
            intelBoardOpen = false;
            minigameService?.Reset();
            taskService.ResetAllSabotageTimers();
            _cameraRig.SetSubject(localClientId);
            _cameraRig.ResetConfiguration();
            status = "编辑器演示视角：已进入九龙港区行动画面。";

            if (onlineHud != null)
            {
                onlineHud.Bind(this);
            }

            ConfigureMainCamera();
            return Camera.main != null && Camera.main.orthographic;    // M3: camera is always orthographic now
        }
        public bool EditorSeedChatSafetyMessageForSmokeTest()
        {
            EnsureChatSystem();
            chatSystem.ReceiveMessage("qa-player", "质检玩家", "这是一条待处理聊天", false, Faction.Police, ChatChannel.Meeting);
            return chatSystem.MessageCount > 0;
        }
        public bool EditorForceStageOneOpeningShotForSmokeTest()
        {
            if (players.Count == 0)
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(LocalPreviewClientId, "烟测玩家", mapService.ScaleMapPosition(new Vector3(-1.18f, -0.72f, 0f)), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            phase = OnlineMatchPhase.Opening;
            matchStarted = true;
            fullMapPreview = true;
            tacticalMapOpen = false;
            intelBoardOpen = false;
            minigameService?.Reset();
            taskService.ResetAllSabotageTimers();
            _cameraRig.ResetConfiguration();
            ConfigureMainCamera();
            return Camera.main != null && Camera.main.orthographic;
        }
        public bool EditorForceStageOneBlackoutShotForSmokeTest()
        {
            ulong localClientId = LocalClientId();
            Vector3 blackoutPosition = mapService.ScaleMapPosition(new Vector3(8.72f, 4.8f, 0f));

            if (players.Count == 0 || !players.ContainsKey(localClientId))
            {
                players[localClientId] = new OnlinePlayerState(localClientId, "烟测玩家", blackoutPosition, true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }
            else
            {
                OnlinePlayerState state = players[localClientId];
                state.Position = blackoutPosition;
                state.Alive = true;
                players[localClientId] = state;
            }

            phase = OnlineMatchPhase.Action;
            matchStarted = true;
            fullMapPreview = false;
            tacticalMapOpen = false;
            intelBoardOpen = false;
            minigameService?.Reset();
            taskService.ApplySabotageEffect(SabotageType.Blackout, "编辑器预览");
            _cameraRig.SetSubject(localClientId);
            _cameraRig.ResetConfiguration();
            ConfigureMainCamera();
            // 停电状态下相机会缩到 BlackoutSize（而非 ActionSize），这才是停电镜头配置成功的证据。
            return Camera.main != null && Camera.main.orthographic && Mathf.Abs(Camera.main.orthographicSize - OnlineCameraRig.BlackoutSize) < 0.5f;
        }
        public void EditorRefreshWorldVisualsForSmokeTest()
        {
            EnsureWorld();
            EnsureCanvasHud();
            UpdateWorldVisuals();

            if (onlineHud != null)
            {
                onlineHud.Bind(this);
            }
        }
        public bool EditorForceLocal2DWalkAnimationForSmokeTest()
        {
            EnsureLocalPreviewActionState();
            ulong clientId = LocalClientId();
            return EditorForce2DWalkAnimationForClient(clientId, Vector2.right);
        }
        public bool EditorForceRemote2DWalkAnimationForSmokeTest()
        {
            EnsureLocalPreviewActionState();

            ulong remoteClientId = ulong.MaxValue;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (pair.Key != LocalClientId())
                {
                    remoteClientId = pair.Key;
                    break;
                }
            }

            if (remoteClientId == ulong.MaxValue)
            {
                return false;
            }

            return EditorForce2DWalkAnimationForClient(remoteClientId, Vector2.left);
        }
        private void EnsureLocalPreviewActionState()
        {
            EnsureRuntimeDependencies();
            EnsureWorld();
            EnsureCanvasHud();

            if (!localPreviewMode)
            {
                localPreviewMode = true;
            }

            if (players.Count == 0)
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(
                    LocalPreviewClientId,
                    "烟测玩家",
                    FindActionPreviewStartPosition(),
                    true,
                    true,
                    OnlineRole.Unassigned,
                    OnlineProfession.Inspector,
                    0,
                    false);
            }

            if (players.Count < ruleSet.MinimumPlayablePlayers)
            {
                EnsureMinimumBots();
            }

            phase = OnlineMatchPhase.Action;
            matchStarted = true;
            fullMapPreview = false;
            tacticalMapOpen = false;
            intelBoardOpen = false;
            minigameService?.Reset();
        }
        private bool EditorForce2DWalkAnimationForClient(ulong clientId, Vector2 input)
        {
            if (!players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                return false;
            }

            Vector2 direction = input.sqrMagnitude > 0.001f ? input.normalized : Vector2.right;
            state.Alive = true;
            state.Input = direction;
            state.Position += new Vector3(direction.x, direction.y, 0f) * 0.42f;
            players[clientId] = state;

            if (clientId == LocalClientId())
            {
                forcedLocalInputForSmokeTest = direction;
                forcedLocalInputTimer = 0.75f;
                localInput = direction;
                localPosition = state.Position;
            }

            UpdateWorldVisuals();
            return true;
        }
        public void EditorTriggerTaskForSmokeTest(int taskId, bool asGang)
        {
            const ulong smokeClientId = ulong.MaxValue;
            bool hadPlayer = players.TryGetValue(smokeClientId, out OnlinePlayerState previousPlayer);
            bool hadRole = privateRoles.TryGetValue(smokeClientId, out OnlineRole previousRole);
            bool wasMatchStarted = matchStarted;

            try
            {
                players[smokeClientId] = new OnlinePlayerState(smokeClientId, "烟测玩家", mapService.TaskPositionFor(taskId), true, true, OnlineRole.Unassigned, asGang ? OnlineProfession.Enforcer : OnlineProfession.Inspector, 0, true);
                privateRoles[smokeClientId] = asGang ? OnlineRole.Gang : OnlineRole.Police;

                // The smoke hook validates task effects without allowing its temporary
                // role to alter the live roster's victory calculation.
                matchStarted = false;
                TryInteractWithTask(smokeClientId, players[smokeClientId]);
            }
            finally
            {
                matchStarted = wasMatchStarted;

                if (hadPlayer)
                {
                    players[smokeClientId] = previousPlayer;
                }
                else
                {
                    players.Remove(smokeClientId);
                }

                if (hadRole)
                {
                    privateRoles[smokeClientId] = previousRole;
                }
                else
                {
                    privateRoles.Remove(smokeClientId);
                }
            }
        }
        public void EditorOpenTaskPanelForSmokeTest(int taskId)
        {
            ulong clientId = LocalClientId();
            players[clientId] = new OnlinePlayerState(clientId, "烟测玩家", mapService.TaskPositionFor(taskId), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            privateRoles[clientId] = OnlineRole.Police;
            localRole = OnlineRole.Police;
            BeginActiveTask(taskId);
            onlineHud?.EditorRefreshForSmokeTest();
        }
        public void EditorForceMeetingForSmokeTest()
        {
            if (!localPreviewMode)
            {
                localPreviewMode = true;
            }

            if (players.Count < ruleSet.MinimumPlayablePlayers)
            {
                EnsureMinimumBots();
            }

            matchStarted = true;
            localRole = LocalEffectiveRole();
            BeginMeeting("编辑器烟测触发会议");

            if (onlineHud != null)
            {
                onlineHud.Bind(this);
            }
        }
        public bool EditorForceVoteStateForSmokeTest()
        {
            if (phase != OnlineMatchPhase.Meeting && phase != OnlineMatchPhase.Voting)
            {
                EditorForceMeetingForSmokeTest();
            }

            if (phase == OnlineMatchPhase.Meeting)
            {
                phase = OnlineMatchPhase.Voting;
                phaseTimer = ruleSet.VotingSecondsFor(players.Count);
            }

            ulong voterClientId = SkipVoteTarget;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (pair.Value.Alive)
                {
                    voterClientId = pair.Key;
                    break;
                }
            }

            if (voterClientId == SkipVoteTarget)
            {
                return false;
            }

            ApplyVote(voterClientId, SkipVoteTarget);
            UpdateWorldVisuals();
            return StageTwoActiveVoteFeedbackCount > 0;
        }
        public bool EditorForceDownedStateForSmokeTest()
        {
            EnsureRuntimeDependencies();
            EnsureWorld();
            EnsureCanvasHud();

            if (!localPreviewMode)
            {
                localPreviewMode = true;
            }

            if (!players.ContainsKey(LocalPreviewClientId))
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(
                    LocalPreviewClientId,
                    "烟测玩家",
                    FindActionPreviewStartPosition(),
                    true,
                    true,
                    OnlineRole.Unassigned,
                    OnlineProfession.Inspector,
                    0,
                    false);
            }

            if (players.Count < ruleSet.MinimumPlayablePlayers)
            {
                EnsureMinimumBots();
            }

            ulong victimClientId = ulong.MaxValue;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (pair.Key != LocalClientId())
                {
                    victimClientId = pair.Key;
                    break;
                }
            }

            if (victimClientId == ulong.MaxValue)
            {
                return false;
            }

            OnlinePlayerState victim = players[victimClientId];
            victim.Alive = false;
            victim.Input = Vector2.zero;
            players[victimClientId] = victim;

            phase = OnlineMatchPhase.Action;
            matchStarted = true;
            _cameraRig.SetSubject(LocalClientId());
            EnsureSmokeDeathVisualState(victimClientId);
            UpdateWorldVisuals();
            return StageTwoActiveDownedStateCount > 0 && StageTwoForensicSceneCount > 0 && BodyCount > 0;
        }
        private void EnsureSmokeDeathVisualState(ulong victimClientId)
        {
            if (!players.TryGetValue(victimClientId, out OnlinePlayerState victim) || killSystem == null)
            {
                return;
            }

            int bodyIndex = -1;

            for (int i = 0; i < killSystem.bodies.Count; i++)
            {
                OnlineBodyState body = killSystem.bodies[i];

                if (!body.Reported && body.VictimClientId == victimClientId)
                {
                    bodyIndex = i;
                    break;
                }
            }

            if (bodyIndex < 0)
            {
                bodyIndex = killSystem.bodies.Count;
                killSystem.bodies.Add(new OnlineBodyState(killSystem.nextBodyId++, victimClientId, victim.Position, false));
            }

            OnlineBodyState forcedBody = killSystem.bodies[bodyIndex];

            if (worldBuilder != null && worldBuilder.Use2DBackend && CountNamedWorldObjects("CorpseMarker") == 0)
            {
                worldBuilder.CreateCorpseMarker(forcedBody.Position);
            }

            if (!playerVisuals.TryGetValue(victimClientId, out GameObject victimVisual) || victimVisual == null)
            {
                victimVisual = CreatePlayerVisual(victim);
                playerVisuals[victimClientId] = victimVisual;
                playerVisualBaseScales[victimClientId] = victimVisual != null ? victimVisual.transform.localScale : Vector3.one;
            }

            if (victimVisual != null)
            {
                if (victimVisual.transform.Find("Stage2 Downed chalk silhouette") == null
                    || victimVisual.transform.Find("Stage2 Downed personal item") == null)
                {
                    worldBuilder.CreateStageTwoCharacterStateLayer(victimVisual);
                }

                victimVisual.transform.position = victim.Position + new Vector3(0f, 0f, 0.12f);
                UpdatePlayerStageTwoStateLayer(victimVisual, victim, false);
                SetChildActive(victimVisual, "Stage2 Downed chalk silhouette", true);
                SetChildActive(victimVisual, "Stage2 Downed personal item", true);
            }

            killSystem.UpdateBodyVisuals();

            if (!killSystem.bodyVisuals.TryGetValue(forcedBody.Id, out GameObject bodyVisual) || bodyVisual == null)
            {
                killSystem.CreateBodyVisualFor(forcedBody);
            }

            if (killSystem.bodyVisuals.TryGetValue(forcedBody.Id, out bodyVisual) && bodyVisual != null)
            {
                bodyVisual.SetActive(true);
                bodyVisual.transform.position = forcedBody.Position + new Vector3(0f, 0f, 0.11f);
            }
        }
        public bool EditorForceResultForSmokeTest()
        {
            if (!matchStarted)
            {
                return false;
            }

            List<ulong> ids = new List<ulong>(players.Keys);
            foreach (ulong clientId in ids)
            {
                OnlineRole role = GetPrivateRole(clientId);
                if (role == OnlineRole.Gang || role == OnlineRole.Mole)
                {
                    OnlinePlayerState gangState = players[clientId];
                    gangState.Alive = false;
                    gangState.Input = Vector2.zero;
                    players[clientId] = gangState;
                }
            }

            EvaluateWinConditions();
            return phase == OnlineMatchPhase.Result;
        }
        public string EditorOpenTaskMiniGameForSmokeTest(int taskId)
        {
            BeginActiveTask(taskId);
            return ActiveMiniGameName;
        }
        public bool EditorForceCompleteActiveMiniGameForSmokeTest()
        {
            if (minigameService == null || !minigameService.HasActiveMiniGame)
            {
                return false;
            }

            // 模拟真实小游戏完成回调；经典任务面板的蓄力校验不适用于
            // 已由 MiniGameBase 接管的富交互任务。
            var activeGame = minigameService.ActiveMiniGame;
            activeGame.OnComplete?.Invoke(activeGame);
            return minigameService.ActiveTaskId < 0;
        }
        public void EditorStartLocalPlayablePreview()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EditorRebuildRuntimeWorld();
            Shutdown();
            StartLocalPreviewRoom();
            FillBotsAndStart();

            if (phase == OnlineMatchPhase.Opening)
            {
                EditorSkipOpeningForSmokeTest();
            }

            aiActionGraceTimer = ruleSet.PreviewAiActionGraceSeconds;
            status = "本地可玩局已启动：AI 正在巡场，开局缓冲中。";
            AddCaseLog(status);
            fullMapPreview = false;
            tacticalMapOpen = false;
            intelBoardOpen = false;
        }
        public void EditorRebuildRuntimeWorld()
        {
            DestroyRuntimeWorld();
            EnsureWorld();
        }
        public void EditorBuildStageOneAuthoringWorldForBake()
        {
            BuildDefaultTasks();
            DestroyRuntimeWorld();
            EnsureWorld();
        }
        private Vector3 FindActionPreviewStartPosition()
        {
            Vector3[] preferred =
            {
                mapService.ScaleMapPosition(new Vector3(-0.62f, -1.02f, 0f)),
                mapService.ScaleMapPosition(new Vector3(-0.92f, -0.82f, 0f)),
                mapService.ScaleMapPosition(new Vector3(-1.42f, -0.62f, 0f)),
                mapService.ScaleMapPosition(new Vector3(-1.42f, -0.18f, 0f)),
                mapService.ScaleMapPosition(new Vector3(0f, -0.82f, 0f)),
                mapService.ScaleMapPosition(new Vector3(-0.25f, 1.68f, 0f))
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                if (IsWalkable(preferred[i]))
                {
                    return preferred[i];
                }
            }

            return FindNearestOpenPosition(mapService.ScaleMapPosition(new Vector3(-1.42f, -0.62f, 0f)), Vector3.zero);
        }
        private void EnsureRuleSet()
        {
            EnsureCoreServices();

            roomAutoFillAi = ruleSet.RoomAutoFillAi;
            revealRoleOnEject = ruleSet.RevealRoleOnEject;
            proximityVoiceEnabled = ruleSet.ProximityVoiceEnabled;
            roomMinPlayers = ruleSet.DefaultRoomMinPlayers;
            roomMaxPlayers = ruleSet.DefaultRoomMaxPlayers;
            // lobby/未入座阶段用默认证据目标；按人数缩放只在确实有玩家后进行，
            // 否则 0 人会被缩放成下限值，破坏 10-20 分钟局时设计。
            taskService.EvidenceTarget = players.Count > 0
                ? ruleSet.ScaledEvidenceTarget(players.Count)
                : ruleSet.DefaultEvidenceTarget;
        }
        private void EnsureCoreServices()
        {
            if (ruleSet == null)
            {
                ruleSet = ScriptableObject.CreateInstance<OnlineRuleSet>();
            }

            if (mapService == null)
            {
                mapService = GetComponent<OnlineMapService>();

                if (mapService == null)
                {
                    mapService = gameObject.AddComponent<OnlineMapService>();
                }
            }

            if (taskService == null)
            {
                taskService = GetComponent<OnlineTaskService>();

                if (taskService == null)
                {
                    taskService = gameObject.AddComponent<OnlineTaskService>();
                }
            }

            taskService.Initialize(ruleSet, mapService);
            EnsureT1Services();
            EnsureMiniGameBridge();
            EnsureCameraRig();
            EnsureCriticalTaskSystem();
        }

        /// <summary>
        /// 确保 T1 服务组件存在并已初始化。
        /// </summary>
        private void EnsureT1Services()
        {
            // 事件总线
            if (gameEventBus == null)
            {
                gameEventBus = GetComponent<SimpleGameEventBus>();
                if (gameEventBus == null)
                {
                    gameEventBus = gameObject.AddComponent<SimpleGameEventBus>();
                }
            }

            // VotingService
            if (votingService == null)
            {
                votingService = GetComponent<Services.VotingService>();
                if (votingService == null)
                {
                    votingService = gameObject.AddComponent<Services.VotingService>();
                }
            }
            votingService.Initialize(this, gameEventBus);
            votingService.BindVotes(votes);

            // MeetingService
            if (meetingService == null)
            {
                meetingService = GetComponent<Services.MeetingService>();
                if (meetingService == null)
                {
                    meetingService = gameObject.AddComponent<Services.MeetingService>();
                }
            }
            meetingService.Initialize(this, gameEventBus, votingService);

            // EvidenceService
            if (evidenceService == null)
            {
                evidenceService = GetComponent<Services.EvidenceService>();
                if (evidenceService == null)
                {
                    evidenceService = gameObject.AddComponent<Services.EvidenceService>();
                }
            }
            evidenceService.Initialize(this, gameEventBus);

            // 绑定 EvidenceService 到 taskService（taskService 的证据属性委托到 EvidenceService）
            taskService?.BindEvidenceService(evidenceService);

            // SabotageService
            if (sabotageService == null)
            {
                sabotageService = GetComponent<Services.SabotageService>();
                if (sabotageService == null)
                {
                    sabotageService = gameObject.AddComponent<Services.SabotageService>();
                }
            }
            sabotageService.Initialize(this, gameEventBus);

            // 绑定 SabotageService 到 taskService（taskService 的计时器属性委托到 SabotageService）
            taskService?.BindSabotageService(sabotageService);

            // MinigameService
            if (minigameService == null)
            {
                minigameService = GetComponent<Services.MinigameService>();
                if (minigameService == null)
                {
                    minigameService = gameObject.AddComponent<Services.MinigameService>();
                }
                minigameService.OnTaskCompleted += OnMinigameTaskCompleted;
                minigameService.OnStatusChanged += msg => { status = msg; };
            }
        }
        private void EnsureMiniGameBridge()
        {
            if (miniGameBridge == null)
            {
                miniGameBridge = GetComponent<MiniGames.OnlineMiniGameBridge>();
            }

            if (miniGameBridge != null)
            {
                miniGameBridge.BindController(this);
            }
        }
        internal void BindNetworkMiniGameBridge(MiniGames.OnlineMiniGameBridge bridge)
        {
            if (bridge == null)
            {
                return;
            }

            if (networkManager != null && bridge.NetworkManager != null && bridge.NetworkManager != networkManager)
            {
                return;
            }

            if (miniGameBridge == null || bridge.IsSpawned || !miniGameBridge.IsSpawned)
            {
                miniGameBridge = bridge;
            }

            bridge.BindController(this);
        }
        private void EnsureCameraRig()
        {
            if (_cameraRig == null)
            {
                _cameraRig = GetComponent<OnlineCameraRig>();
                if (_cameraRig == null)
                {
                    _cameraRig = gameObject.AddComponent<OnlineCameraRig>();
                }
            }
        }
        private void EnsureVFX()
        {
            if (sabotageVFX != null) return;
            var go = new GameObject("SabotageVFX");
            go.transform.SetParent(transform, false);
            sabotageVFX = go.AddComponent<GanglandUndercover.Art.SabotageVFX>();
            sabotageVFX.Bind(this);
        }
        private void EnsureRuntimeDependencies()
        {
            if (syncManager == null)
            {
                syncManager = GetComponent<OnlineSyncManager>();
            }

            EnsureBotController();
            EnsureKillSystem();

            if (_statsCollector == null)
            {
                _statsCollector = new MatchStatsCollector();
            }

            if (evidenceDossier == null)
            {
                evidenceDossier = new EvidenceDossier(this);
            }

            if (snapshotService == null)
            {
                snapshotService = new MatchSnapshotService(this);
            }
        }
        private void EnsureBotController()
        {
            if (_botController == null)
            {
                _botController = new OnlineBotController(this);
            }
        }
        private void EnsureKillSystem()
        {
            if (killSystem == null)
            {
                killSystem = GetComponent<KillSystem>();
                if (killSystem == null)
                {
                    killSystem = FindAnyObjectByType<KillSystem>();
                }
                if (killSystem == null)
                {
                    killSystem = gameObject.AddComponent<KillSystem>();
                }
            }

            killSystem.Bind(this);
        }
        private void Reset()
        {
            if (Application.isPlaying)
            {
                return;
            }

            EnsureCoreServices();
            EnsureRuleSet();
            BuildDefaultTasks();
            EnsureWorld();
            EnsureCanvasHud();
            EnsureRuntimeDependencies();
        }
        private void EnsureMinimumBots()
        {
            EnsureRuntimeDependencies();
            _botController.EnsureMinimumBots();
        }
        public string GetPlayerDisplayName(ulong clientId)
        {
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
                return state.DisplayName;
            return "玩家" + clientId;
        }
        // ====== 活动任务 / 迷你游戏（委托到 MinigameService） ======

        private void BeginActiveTask(int taskId)
        {
            OnlineTaskState task = GetTask(taskId);
            if (task.Id < 0) return;

            minigameService.Begin(taskId);
            status = "开始处理任务：" + task.Name + "。";
            AddCaseLog(status);
        }

        private void DestroyActiveMiniGame()
        {
            minigameService?.DestroyMiniGame();
        }

        private void ReadActiveTaskInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                minigameService.Cancel();
                return;
            }

            if (Input.GetKey(KeyCode.Space))
            {
                minigameService.AddCharge(Time.deltaTime);
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                minigameService.ResolveStep(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                minigameService.ResolveStep(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                minigameService.ResolveStep(3);
            }

            minigameService.CheckAndComplete();
        }

        private void ResolveActiveTaskStep(int input)
        {
            minigameService.ResolveStep(input);
        }

        private void CompleteActiveTask()
        {
            minigameService.CheckAndComplete();
        }

        /// <summary>MinigameService.OnTaskCompleted 事件回调：发送网络 Interact 动作。</summary>
        private void OnMinigameTaskCompleted(int taskId)
        {
            if (phase == OnlineMatchPhase.Action)
            {
                minigameService.SetSubmitting(true);
                SendClientAction(OnlineActionType.Interact);
                minigameService.SetSubmitting(false);
            }
        }

        private bool ShouldOpenLocalTaskPanel()
        {
            if (phase != OnlineMatchPhase.Action || !players.TryGetValue(LocalClientId(), out OnlinePlayerState localState) || !localState.Alive)
            {
                return false;
            }

            OnlineRole role = LocalEffectiveRole();

            if (role == OnlineRole.Gang)
            {
                return false;
            }

            OnlineTaskState nearestTask = FindNearestTask(localState.Position);
            nearestTask = TaskForPlayer(LocalClientId(), nearestTask);
            return nearestTask.Id >= 0
                && IsTaskVisibleToPlayer(LocalClientId(), nearestTask)
                && (!nearestTask.Completed || nearestTask.Sabotaged);
        }
        private void UpdateEvidenceMilestone()
        {
            // EvidenceService 已在 AddEvidence 时自动更新里程碑。
            // 这里仅做同步到 controller 镜像字段 + 案情日志。
            if (evidenceService == null) return;

            int milestone = evidenceService.EvidenceMilestoneIndex;
            if (milestone <= evidenceMilestoneIndex) return;

            evidenceMilestoneIndex = milestone;
            lastEvidenceEvent = evidenceService.LastEvidenceEvent;
            AddCaseLog(lastEvidenceEvent);
        }
        public void AddCaseLog(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            caseLog.Add(entry);

            while (caseLog.Count > ruleSet.MaxCaseLogEntries)
            {
                caseLog.RemoveAt(0);
            }
        }
        public Dictionary<EvidenceCategory, List<string>> GetMeetingEvidence()
        {
            var result = new Dictionary<EvidenceCategory, List<string>>
            {
                { EvidenceCategory.TaskTrail, new List<string>() },
                { EvidenceCategory.Surveillance, new List<string>() },
                { EvidenceCategory.EvidenceChain, new List<string>() }
            };

            foreach (var entry in _evidenceBoard)
            {
                result[entry.Category].Add(entry.Text);
            }

            return result;
        }
        public void AddSuspicion(ulong clientId, int amount)
        {
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                state.Suspicion = Mathf.Clamp(state.Suspicion + amount, 0, 100);
                players[clientId] = state;
            }
        }
        public List<ulong> GetPlayersBySuspicion()
        {
            List<ulong> result = new List<ulong>();
            List<(ulong id, int susp)> suspects = new List<(ulong, int)>();

            foreach (var kv in players)
            {
                if (kv.Value.Alive)
                    suspects.Add((kv.Key, kv.Value.Suspicion));
            }

            suspects.Sort((a, b) => b.susp.CompareTo(a.susp));
            foreach (var pair in suspects)
                result.Add(pair.id);

            return result;
        }
        private void RepairSabotagedTasks(int maxCount)
        {
            int repaired = 0;

            for (int i = 0; i < tasks.Count && repaired < maxCount; i++)
            {
                OnlineTaskState task = tasks[i];

                if (!task.Sabotaged)
                {
                    continue;
                }

                task.Sabotaged = false;
                RepairSabotageEffect(OnlineMatchUtils.SabotageForTask(task.Id));
                tasks[i] = task;
                repaired++;
            }
        }
        private bool ApplySabotageEffect(SabotageType sabotageType, string taskName, ulong initiatorId = 0)
        {
            if (!taskService.ApplySabotageEffect(sabotageType, taskName, initiatorId))
            {
                return false;
            }

            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    status = "黑帮切断电闸，港区进入黑灯。";
                    AddCaseLog(status);
                    PlayCue("blackout");
                    // Phase 4: 视野缩小 + 交互范围减半
                    _blackoutVisionReduced = true;
                    _blackoutInteractionHalved = true;
                    break;
                case SabotageType.Lockdown:
                    status = taskName + " 引发门禁封锁，部分路线被迫绕行。";
                    AddCaseLog(status);
                    // Phase 4: 随机封锁3个房间
                    LockRandomRooms(3);
                    break;
                case SabotageType.Communications:
                    status = taskName + " 被干扰，紧急会议暂时无法呼叫。";
                    AddCaseLog(status);
                    // Phase 4: 小地图禁用
                    tacticalMapDisabled = true;
                    emergencyCooldownTimer = Mathf.Max(emergencyCooldownTimer, 30f);
                    SyncMeetingServiceFromController();
                    break;
                case SabotageType.EvidenceLeak:
                    status = taskName + " 泄露证据，证据链持续受损。";
                    AddCaseLog(status);
                    // Phase 4: 证据分每秒-1 (由 taskService.TickSabotageTimers 已有)
                    break;
                case SabotageType.PatrolAlert:
                    MarkNearbyGangSuspicion(mapService.ScaleMapPosition(Vector3.zero), 1);
                    status = taskName + " 触发巡逻警戒，靠近指挥区的嫌疑上升。";
                    AddCaseLog(status);
                    // Phase 4: 激活额外巡逻路线
                    _patrolAlertActive = true;
                    break;
            }

            return true;
        }
        private void RepairSabotageEffect(SabotageType sabotageType)
        {
            taskService.RepairSabotageEffect(sabotageType);
            // Phase 4: 清除深化效果
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    _blackoutVisionReduced = false;
                    _blackoutInteractionHalved = false;
                    break;
                case SabotageType.Lockdown:
                    UnlockAllRooms();
                    break;
                case SabotageType.Communications:
                    tacticalMapDisabled = false;
                    break;
                case SabotageType.PatrolAlert:
                    _patrolAlertActive = false;
                    break;
            }
        }
        private void RevealMostSuspiciousPlayer()
        {
            ulong bestClientId = SkipVoteTarget;
            int bestSuspicion = -1;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (!pair.Value.Alive || pair.Value.Suspicion <= bestSuspicion)
                {
                    continue;
                }

                bestClientId = pair.Key;
                bestSuspicion = pair.Value.Suspicion;
            }

            if (bestClientId != SkipVoteTarget && players.TryGetValue(bestClientId, out OnlinePlayerState suspect))
            {
                suspect.Suspicion += 1;
                players[bestClientId] = suspect;
            }
        }
        private void MarkNearbyGangSuspicion(Vector3 position, int amount)
        {
            List<ulong> ids = new List<ulong>(players.Keys);

            foreach (ulong clientId in ids)
            {
                OnlinePlayerState state = players[clientId];

                if (!state.Alive || GetPrivateRole(clientId) != OnlineRole.Gang)
                {
                    continue;
                }

                if (Vector3.Distance(position, state.Position) <= 2.2f)
                {
                    state.Suspicion += amount;
                    players[clientId] = state;
                }
            }
        }
        private void LockRandomRooms(int count)
        {
            if (mapService == null) return;
            var rooms = mapService.ShipRooms();
            if (rooms == null || rooms.Length == 0) return;

            _lockedRoomIndices.Clear();
            int attempts = 0;
            while (_lockedRoomIndices.Count < count && attempts < 20)
            {
                int idx = UnityEngine.Random.Range(0, rooms.Length);
                _lockedRoomIndices.Add(idx);
                attempts++;
            }
        }
        public bool IsRoomLocked(int roomIndex) => _lockedRoomIndices.Contains(roomIndex);

        /// <summary>解除所有房间封锁。</summary>
        private void UnlockAllRooms() => _lockedRoomIndices.Clear();

        /// <summary>Blackout 视野缩小倍数。</summary>
        public float BlackoutVisionMultiplier => _blackoutVisionReduced && !IsDarkVisionActive(LocalClientId()) ? 0.4f : 1f;

        /// <summary>Blackout 交互范围倍率。</summary>
        public float BlackoutInteractionMultiplier => _blackoutInteractionHalved ? 0.5f : 1f;

        /// <summary>通讯干扰是否激活（小地图禁用）。</summary>
        public bool IsCommunicationsJammed => tacticalMapDisabled;

        /// <summary>巡逻警报是否激活。</summary>
        public bool IsPatrolAlertActive => _patrolAlertActive;

        internal bool TryFindNearestVictim(ulong attackerClientId, Vector3 position, out ulong victimClientId, out OnlinePlayerState victim)
        {
            if (killSystem != null)
                return killSystem.TryFindNearestVictim(attackerClientId, position, out victimClientId, out victim);
            victimClientId = SkipVoteTarget;
            victim = default;
            return false;
        }
        private int CountSabotagedTasks()
        {
            int sabotaged = 0;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Sabotaged)
                {
                    sabotaged++;
                }
            }

            return sabotaged;
        }
        private Rect WorldRectToMapRect(Rect mapRect, Vector3 worldCenter, Vector3 worldSize)
        {
            Vector2 center = WorldToMapPoint(mapRect, worldCenter);
            float width = worldSize.x / (mapService.MapHalfWidth * 2f) * mapRect.width;
            float height = worldSize.y / (mapService.MapHalfHeight * 2f) * mapRect.height;
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }
        private Vector2 WorldToMapPoint(Rect mapRect, Vector3 worldPosition)
        {
            float x = Mathf.InverseLerp(-mapService.MapHalfWidth, mapService.MapHalfWidth, worldPosition.x);
            float y = Mathf.InverseLerp(-mapService.MapHalfHeight, mapService.MapHalfHeight, worldPosition.y);
            return new Vector2(mapRect.x + x * mapRect.width, mapRect.y + (1f - y) * mapRect.height);
        }
        private OnlineTaskState FindHighestValueOpenTask()
        {
            OnlineTaskState best = new OnlineTaskState(-1, "无", Vector3.zero, 0, 1, false, false);
            int bestValue = -1;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Completed && !task.Sabotaged)
                {
                    continue;
                }

                int value = OnlineMatchUtils.TaskEvidenceValue(task.Id) + (task.Sabotaged ? 2 : 0);

                if (value > bestValue)
                {
                    best = task;
                    bestValue = value;
                }
            }

            return best;
        }
        private void UpdatePlayerStageTwoStateLayer(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            bool inMeeting = phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
            bool actionPhase = phase == OnlineMatchPhase.Action;
            bool moving = state.Alive && state.Input.sqrMagnitude > 0.02f;
            bool nearBody = IsNearUnreportedBody(state.Position);
            bool interacting = state.Alive && minigameService != null && minigameService.HasActiveTask && isLocal;
            bool hasVoted = votes.ContainsKey(state.ClientId);
            Color accent = OnlineWorldBuilder.PlayerAccentColor(state);

            SetChildActive(visual, "Stage2 Character interaction radius", isLocal && actionPhase && state.Alive && !tacticalMapOpen);
            SetChildActive(visual, "Stage2 VoiceRadius action proximity", actionPhase && state.Alive && proximityVoiceEnabled && IsNearCameraSubject(state.Position));
            SetChildActive(visual, "Stage2 Downed chalk silhouette", !state.Alive);
            SetChildActive(visual, "Stage2 Downed personal item", !state.Alive);
            SetChildActive(visual, "Stage2 Character facing wedge", state.Alive && !inMeeting);
            SetChildActive(visual, "Stage2 Character action hand prop", interacting);
            SetChildActive(visual, "Stage2 Character report beacon", state.Alive && actionPhase && CountUnreportedBodies() > 0 && nearBody);
            SetChildActive(visual, "Stage2 Report proximity ping", state.Alive && actionPhase && CountUnreportedBodies() > 0 && nearBody);
            SetChildActive(visual, "Stage2 Meeting seated pad", inMeeting && state.Alive);
            SetChildActive(visual, "Stage2 Meeting vote tablet", inMeeting && state.Alive);
            SetChildActive(visual, "Stage2 Meeting voice mic", inMeeting && state.Alive);
            SetChildActive(visual, "Stage2 Vote locked marker", inMeeting && state.Alive && hasVoted);
            SetChildActive(visual, "Stage2 Character footstep L", moving && !inMeeting);
            SetChildActive(visual, "Stage2 Character footstep R", moving && !inMeeting);

            StageTwoCharacterRig rig = visual.GetComponent<StageTwoCharacterRig>();

            if (rig != null)
            {
                rig.ApplyRuntimeState(state.Alive, moving, interacting, nearBody, inMeeting, hasVoted);
            }

            Transform voiceRadius = visual.transform.Find("Stage2 VoiceRadius action proximity");

            if (voiceRadius != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f + state.ClientId * 0.43f) * 0.035f;
                voiceRadius.localScale = new Vector3(2.35f * pulse, 1.36f * pulse, 0.05f);
            }

            Transform interactionRadius = visual.transform.Find("Stage2 Character interaction radius");

            if (interactionRadius != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 5.6f) * 0.025f;
                interactionRadius.localScale = new Vector3(ruleSet.InteractionRange * 2f * pulse, ruleSet.InteractionRange * 1.18f * pulse, 0.05f);
            }

            Transform facingWedge = visual.transform.Find("Stage2 Character facing wedge");

            if (facingWedge != null)
            {
                Vector2 facing = state.Input.sqrMagnitude > 0.02f ? state.Input.normalized : Vector2.up;
                float facingAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
                facingWedge.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                OnlineWorldBuilder.SetColor(facingWedge.gameObject, isLocal ? new Color(0.95f, 0.82f, 0.12f, 1f) : OnlineWorldBuilder.Darken(accent, 0.9f));
            }

            Transform handProp = visual.transform.Find("Stage2 Character action hand prop");

            if (handProp != null)
            {
                handProp.localRotation = Quaternion.Euler(0f, 0f, -18f + Mathf.Sin(Time.time * 8f) * 8f);
            }

            Transform reportPing = visual.transform.Find("Stage2 Report proximity ping");

            if (reportPing != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 7.2f + state.ClientId * 0.31f) * 0.14f;
                reportPing.localScale = new Vector3(0.32f * pulse, 0.18f * pulse, 0.05f);
            }

            Transform meetingMic = visual.transform.Find("Stage2 Meeting voice mic");

            if (meetingMic != null)
            {
                meetingMic.localRotation = Quaternion.Euler(0f, 0f, -8f + Mathf.Sin(Time.time * 3.6f + state.ClientId * 0.4f) * 5f);
            }

            Transform voteMarker = visual.transform.Find("Stage2 Vote locked marker");

            if (voteMarker != null)
            {
                voteMarker.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
            }
        }
        private bool IsNearUnreportedBody(Vector3 position)
        {
            for (int i = 0; i < killSystem.bodies.Count; i++)
            {
                OnlineBodyState body = killSystem.bodies[i];

                if (!body.Reported && Vector3.Distance(position, body.Position) <= ruleSet.ReportRangeFor(players.Count) * 1.4f)
                {
                    return true;
                }
            }

            return false;
        }
        private Vector3 ScaleMapPosition(Vector3 position)
        {
            return mapService.ScaleMapPosition(position);
        }
        private Vector3 ScaleMapSize(Vector3 size)
        {
            return mapService.ScaleMapSize(size);
        }
        private OnlineTaskState FindRecommendedTask(Vector3 position)
        {
            OnlineTaskState best = new OnlineTaskState(-1, "无待办任务", position, 0, 1, true, false);
            float bestScore = float.MaxValue;
            ulong localClientId = LocalClientId();

            foreach (OnlineTaskState task in tasks)
            {
                OnlineTaskState playerTask = TaskForPlayer(localClientId, task);
                if (!IsTaskVisibleToPlayer(localClientId, playerTask) || playerTask.Completed && !playerTask.Sabotaged)
                {
                    continue;
                }

                float score = Vector3.Distance(position, playerTask.Position) + (playerTask.Sabotaged ? -8f : 0f);

                if (score < bestScore)
                {
                    best = playerTask;
                    bestScore = score;
                }
            }

            return best;
        }
        private OnlineTaskState FindNearestTask(Vector3 position)
        {
            OnlineTaskState best = new OnlineTaskState(-1, string.Empty, Vector3.zero, 0, 1, false, false);
            float bestDistance = ruleSet.InteractionRange;

            foreach (OnlineTaskState task in tasks)
            {
                float distance = Vector3.Distance(position, task.Position);

                if (distance <= bestDistance)
                {
                    best = task;
                    bestDistance = distance;
                }
            }

            return best;
        }
        private OnlineTaskState GetTask(int taskId)
        {
            foreach (OnlineTaskState task in tasks)
            {
                if (task.Id == taskId)
                {
                    return task;
                }
            }

            return new OnlineTaskState(-1, "未知任务", Vector3.zero, 0, 1, false, false);
        }
        private Sprite TaskVisualSprite(int taskId)
        {
            switch (taskId)
            {
                case 0: case 6: case 13: case 21: return circleSprite;
                case 1: case 10: case 23: return capsuleSprite;
                case 2: case 14: case 24: return diamondSprite;
                case 3: case 15: case 18: return softCircleSprite;
                case 4: case 11: case 16: case 22: return roundedRectSprite;
                case 5: case 27: return capsuleSprite;
                case 7: case 12: return roundedRectSprite;
                case 8: case 19: case 26: return diamondSprite;
                case 9: case 20: case 25: return circleSprite;
                default: return roundedRectSprite;
            }
        }
        private Vector3 ResolveMapCollision(Vector3 from, Vector3 requested)
        {
            Vector3 clamped = mapService.ClampToOnlineMap(requested);

            if (IsWalkable(clamped))
            {
                return clamped;
            }

            Vector3 horizontal = mapService.ClampToOnlineMap(new Vector3(clamped.x, from.y, 0f));
            if (IsWalkable(horizontal))
            {
                return horizontal;
            }

            Vector3 vertical = mapService.ClampToOnlineMap(new Vector3(from.x, clamped.y, 0f));
            if (IsWalkable(vertical))
            {
                return vertical;
            }

            return FindNearestOpenPosition(clamped, from);
        }
        private Vector3 FindNearestOpenPosition(Vector3 desired, Vector3 fallback)
        {
            Vector3 clamped = mapService.ClampToOnlineMap(desired);

            if (IsWalkable(clamped))
            {
                return clamped;
            }

            if (fallback != Vector3.zero)
            {
                Vector3 safeFallback = mapService.ClampToOnlineMap(fallback);
                if (IsWalkable(safeFallback))
                {
                    return safeFallback;
                }
            }

            for (int ring = 1; ring <= 10; ring++)
            {
                float radius = CollisionTraceStep * ring * 4f;

                for (int i = 0; i < 16; i++)
                {
                    float angle = i / 16f * Mathf.PI * 2f;
                    Vector3 candidate = mapService.ClampToOnlineMap(clamped + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);

                    if (IsWalkable(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return mapService.ClampToOnlineMap(fallback == Vector3.zero ? Vector3.zero : fallback);
        }
        private Vector3 GetLocalPlayerPosition()
        {
            return players.TryGetValue(LocalClientId(), out OnlinePlayerState state) ? state.Position : localPosition;
        }
        private Vector3 GetPlayerPosition(ulong clientId)
        {
            return players.TryGetValue(clientId, out OnlinePlayerState state) ? state.Position : Vector3.zero;
        }
        private bool IsWalkable(Vector3 position)
        {
            if (position.x < -mapService.MapHalfWidth + PlayerCollisionRadius
                || position.x > mapService.MapHalfWidth - PlayerCollisionRadius
                || position.y < -mapService.MapHalfHeight + PlayerCollisionRadius
                || position.y > mapService.MapHalfHeight - PlayerCollisionRadius)
            {
                return false;
            }

            if (walkableRects.Count > 0 && !IsInsideWalkableArea(position))
            {
                return false;
            }

            foreach (Rect obstacle in solidObstacleRects)
            {
                if (OnlineMatchUtils.CircleIntersectsRect(position, PlayerCollisionRadius, obstacle))
                {
                    return false;
                }
            }

            return true;
        }
        private bool IsInsideWalkableArea(Vector3 position)
        {
            for (int i = 0; i < walkableRects.Count; i++)
            {
                if (walkableRects[i].Contains(new Vector2(position.x, position.y)))
                {
                    return true;
                }
            }

            return false;
        }
        private int CountWorldObjects()
        {
            return worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<Transform>(true).Length - 1;
        }
        private int CountNamedWorldObjects(string prefix)
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;

            foreach (Transform child in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
        private int CountConfiguredStageTwoRigs()
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;

            foreach (StageTwoCharacterRig rig in worldRoot.GetComponentsInChildren<StageTwoCharacterRig>(true))
            {
                if (rig != null && rig.HasRequiredRuntimeSlots)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
