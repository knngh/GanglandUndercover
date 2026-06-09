using System;
using System.Collections.Generic;
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
        public List<OnlineBodyState> Bodies => killSystem != null ? killSystem.bodies : null;
        public IReadOnlyList<string> CaseLog => caseLog;
        public OnlineRole LocalRole => localRole;
        public string LocalPlayerName => localPlayerName;
        public string JoinAddress => joinAddress;
        public string RelayJoinCode => relayJoinCode;
        public string RelayJoinInput => relayJoinInput;
        public string RelayStatus => relayStatus;
        public string RoomName => roomName;
        public bool IsHost => localPreviewMode || networkManager != null && networkManager.IsHost;
        public bool IsLocalPreview => localPreviewMode;
        public int ConnectedClientCount => networkManager != null ? networkManager.ConnectedClientsList.Count : 0;
        public bool IsListeningOrConnected => networkManager != null && networkManager.IsListening;
        public bool IsClientConnected => networkManager != null && networkManager.IsConnectedClient;
        public bool HasActiveMiniGame => activeMiniGame != null;
        public int ActiveTaskId => activeTaskId;
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
        public string VoiceStatus => chatSystem != null ? "文本聊天: " + chatSystem.CurrentChannel : "文本聊天未初始化";
        public int VoiceParticipantCount => chatSystem != null ? chatSystem.MessageCount : 0;
        public bool VoiceRoutingEnabled => true; // 文本聊天始终可用
        public bool LocalTaskInputGateActive => activeTaskId >= 0;
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
            activeTaskId = -1;
            phase = OnlineMatchPhase.Action;
            _cameraRig.ResetConfiguration();
            ConfigureMainCamera();
            return Camera.main != null && Camera.main.orthographic;    // M3: camera is always orthographic now
        }
        public bool EditorForceActionPreviewForSmokeTest()
        {
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
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskFeedbackTimer = 0f;
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
            activeTaskId = -1;
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
            activeTaskId = -1;
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
            activeTaskId = -1;
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
            ulong clientId = 0;
            players[clientId] = new OnlinePlayerState(clientId, "烟测玩家", mapService.TaskPositionFor(taskId), true, true, OnlineRole.Unassigned, asGang ? OnlineProfession.Enforcer : OnlineProfession.Inspector, 0, false);
            privateRoles[clientId] = asGang ? OnlineRole.Gang : OnlineRole.Police;
            TryInteractWithTask(clientId, players[clientId]);
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
                if (GetPrivateRole(clientId) == OnlineRole.Gang)
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
            if (activeMiniGame == null)
            {
                return false;
            }

            OnActiveMiniGameComplete();
            return true;
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
            EnsureMiniGameBridge();
            EnsureCameraRig();
            EnsureCriticalTaskSystem();
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
        private void BeginActiveTask(int taskId)
        {
            OnlineTaskState task = GetTask(taskId);

            if (task.Id < 0)
            {
                return;
            }

            activeTaskId = taskId;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;

            // Task#7：优先打开 Among Us 风格小游戏（成功后走与经典面板一致的服务器提交路径）。
            // 打开失败（如缺 UI 环境）时 activeMiniGame 保持 null，自动回退到 OnGUI 经典面板。
            TryOpenActiveMiniGame(taskId);

            status = "开始处理任务：" + task.Name + "。";
            AddCaseLog(status);
        }
        private void TryOpenActiveMiniGame(int taskId)
        {
            DestroyActiveMiniGame();

            try
            {
                GanglandUndercover.SocialDeduction.MiniGames.MiniGameBase mini =
                    OnlineMiniGameBridge.CreateDefaultMinigame(taskId, transform);
                if (mini == null)
                {
                    return;
                }

                mini.OnComplete = _ => OnActiveMiniGameComplete();
                mini.OnCancel = _ => OnActiveMiniGameCancel();
                mini.Show();
                activeMiniGame = mini;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[OnlineMatch] 小游戏打开失败，回退经典任务面板：" + e.Message);
                DestroyActiveMiniGame();
            }
        }
        private void OnActiveMiniGameComplete()
        {
            // CompleteActiveTask 会把 activeTaskId 置 -1 并提交现场结果；
            // Update 下一帧检测到 activeTaskId<0 即回收 activeMiniGame（延迟销毁更安全）。
            CompleteActiveTask();
        }
        private void OnActiveMiniGameCancel()
        {
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            status = "已退出任务面板。";
        }
        private void DestroyActiveMiniGame()
        {
            if (activeMiniGame == null)
            {
                return;
            }

            GanglandUndercover.SocialDeduction.MiniGames.MiniGameBase mini = activeMiniGame;
            activeMiniGame = null;
            try
            {
                mini.Hide();
            }
            catch (Exception)
            {
                // Hide 的清理失败不应阻断对象销毁。
            }

            if (mini != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(mini.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(mini.gameObject);
                }
            }
        }
        private void ReadActiveTaskInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                activeTaskId = -1;
                activeTaskStep = 0;
                activeTaskCharge = 0f;
                activeTaskStepOneDone = false;
                activeTaskStepTwoDone = false;
                activeTaskStepThreeDone = false;
                activeTaskMistakes = 0;
                activeTaskFeedbackTimer = 0f;
                activeTaskFeedbackPositive = false;
                status = "已退出任务面板。";
                return;
            }

            if (Input.GetKey(KeyCode.Space))
            {
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + Time.deltaTime * TaskChargeRate(activeTaskId));
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ResolveActiveTaskStep(1);
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ResolveActiveTaskStep(2);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ResolveActiveTaskStep(3);
            }

            if (activeTaskCharge >= 1f && activeTaskStepOneDone && activeTaskStepTwoDone && activeTaskStepThreeDone)
            {
                CompleteActiveTask();
            }
        }
        private void ResolveActiveTaskStep(int input)
        {
            if (input == CorrectTaskStepInput(activeTaskId, activeTaskStep))
            {
                activeTaskStep++;
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + 0.28f);

                if (activeTaskStep == 1)
                {
                    activeTaskStepOneDone = true;
                }
                else if (activeTaskStep == 2)
                {
                    activeTaskStepTwoDone = true;
                }
                else
                {
                    activeTaskStepThreeDone = true;
                }

                status = "任务校验 " + Mathf.Min(activeTaskStep, 3) + "/3 通过。";
                activeTaskFeedbackTimer = 0.42f;
                activeTaskFeedbackPositive = true;
                return;
            }

            activeTaskCharge = Mathf.Max(0f, activeTaskCharge - 0.18f);
            activeTaskMistakes++;
            status = "校验不匹配，进度回退。";
            activeTaskFeedbackTimer = 0.55f;
            activeTaskFeedbackPositive = false;

            if (activeTaskMistakes >= 3)
            {
                activeTaskMistakes = 0;
                activeTaskCharge = 0f;
                status = "连续错误触发复核，任务进度清零重校。";
            }
        }
        private static int CorrectTaskStepInput(int taskId, int step)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0:
                    return new[] { 1, 3, 2 }[Mathf.Clamp(step, 0, 2)];
                case 1:
                    return new[] { 2, 1, 3 }[Mathf.Clamp(step, 0, 2)];
                case 2:
                    return new[] { 3, 2, 1 }[Mathf.Clamp(step, 0, 2)];
                case 3:
                    return new[] { 1, 2, 3 }[Mathf.Clamp(step, 0, 2)];
                case 4:
                    return new[] { 2, 3, 1 }[Mathf.Clamp(step, 0, 2)];
                default:
                    return new[] { 3, 1, 2 }[Mathf.Clamp(step, 0, 2)];
            }
        }
        private static float TaskChargeRate(int taskId)
        {
            switch (TaskTemplateMode(taskId))
            {
                case 0:
                    return 0.58f;
                case 1:
                    return 0.72f;
                case 2:
                    return 0.68f;
                case 3:
                    return 0.56f;
                case 4:
                    return 0.76f;
                default:
                    return 0.62f;
            }
        }
        private void CompleteActiveTask()
        {
            int taskId = activeTaskId;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;

            if (phase == OnlineMatchPhase.Action)
            {
                submittingActiveTask = true;
                SendClientAction(OnlineActionType.Interact);
                submittingActiveTask = false;
            }

            status = "任务操作完成，已提交现场结果。";
        }
        private bool ShouldOpenLocalTaskPanel()
        {
            if (phase != OnlineMatchPhase.Action || !players.TryGetValue(LocalClientId(), out OnlinePlayerState localState) || !localState.Alive)
            {
                return false;
            }

            OnlineRole role = LocalEffectiveRole();

            if (role == OnlineRole.Gang || role == OnlineRole.Mole)
            {
                return false;
            }

            OnlineTaskState nearestTask = FindNearestTask(localState.Position);
            return nearestTask.Id >= 0 && (!nearestTask.Completed || nearestTask.Sabotaged);
        }
        private void UpdateEvidenceMilestone()
        {
            int milestone = EvidenceMilestoneFor(taskService.EvidenceScore, taskService.EvidenceTarget);

            if (milestone <= evidenceMilestoneIndex)
            {
                return;
            }

            evidenceMilestoneIndex = milestone;

            switch (milestone)
            {
                case 1:
                    lastEvidenceEvent = "证据链达成 25%，已锁定第一批路线。";
                    break;
                case 2:
                    lastEvidenceEvent = "证据链达成 50%，会议可重点追问高嫌疑目标。";
                    break;
                case 3:
                    lastEvidenceEvent = "证据链达成 75%，警方接近结案，黑帮必须制造破坏。";
                    break;
                default:
                    lastEvidenceEvent = "证据链闭合，进入结案判定。";
                    break;
            }

            AddCaseLog(lastEvidenceEvent);
        }
        private static int EvidenceMilestoneFor(int score, int target)
        {
            if (target <= 0)
            {
                return 0;
            }

            float ratio = score / (float)target;

            if (ratio >= 1f)
            {
                return 4;
            }

            if (ratio >= 0.75f)
            {
                return 3;
            }

            if (ratio >= 0.5f)
            {
                return 2;
            }

            return ratio >= 0.25f ? 1 : 0;
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
                RepairSabotageEffect(SabotageForTask(task.Id));
                tasks[i] = task;
                repaired++;
            }
        }
        private void ApplySabotageEffect(SabotageType sabotageType, string taskName)
        {
            taskService.ApplySabotageEffect(sabotageType, taskName);
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
        public float BlackoutVisionMultiplier => _blackoutVisionReduced ? 0.4f : 1f;

        /// <summary>Blackout 交互范围倍率。</summary>
        public float BlackoutInteractionMultiplier => _blackoutInteractionHalved ? 0.5f : 1f;

        /// <summary>通讯干扰是否激活（小地图禁用）。</summary>
        public bool IsCommunicationsJammed => tacticalMapDisabled;

        /// <summary>巡逻警报是否激活。</summary>
        public bool IsPatrolAlertActive => _patrolAlertActive;

        internal bool TryFindNearestVictim(Vector3 position, out ulong victimClientId, out OnlinePlayerState victim)
        {
            if (killSystem != null)
                return killSystem.TryFindNearestVictim(position, out victimClientId, out victim);
            victimClientId = SkipVoteTarget;
            victim = default;
            return false;
        }
        private static SabotageType SabotageForTask(int taskId)
        {
            switch (taskId)
            {
                case 2:
                case 14:
                    return SabotageType.Blackout;
                case 7:
                case 12:
                    return SabotageType.Lockdown;
                case 6:
                case 13:
                    return SabotageType.Communications;
                case 3:
                case 11:
                case 16:
                    return SabotageType.EvidenceLeak;
                case 4:
                case 10:
                case 17:
                case 24:
                case 26:
                    return SabotageType.PatrolAlert;
                case 20:
                case 21:
                case 27:
                    return SabotageType.Communications;
                case 22:
                case 23:
                case 25:
                    return SabotageType.EvidenceLeak;
                default:
                    return SabotageType.None;
            }
        }
        private static int SabotageEvidencePenalty(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.EvidenceLeak:
                    return 2;
                case SabotageType.Blackout:
                case SabotageType.Lockdown:
                case SabotageType.Communications:
                    return 1;
                default:
                    return 0;
            }
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
        private static string EvidenceMilestoneName(int milestone)
        {
            switch (milestone)
            {
                case 1:
                    return "初步锁线";
                case 2:
                    return "重点盘问";
                case 3:
                    return "接近结案";
                case 4:
                    return "证据闭合";
                default:
                    return "摸排中";
            }
        }
        private OnlineTaskState FindRecommendedTask(Vector3 position)
        {
            OnlineTaskState best = tasks.Count > 0 ? tasks[0] : new OnlineTaskState(-1, "无任务", Vector3.zero, 0, 1, false, false);
            float bestScore = float.MaxValue;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Completed && !task.Sabotaged)
                {
                    continue;
                }

                float score = Vector3.Distance(position, task.Position) + (task.Sabotaged ? -8f : 0f);

                if (score < bestScore)
                {
                    best = task;
                    bestScore = score;
                }
            }

            return best;
        }
        private static string SabotageName(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    return "黑灯";
                case SabotageType.Lockdown:
                    return "封锁";
                case SabotageType.Communications:
                    return "断讯";
                case SabotageType.EvidenceLeak:
                    return "泄证";
                case SabotageType.PatrolAlert:
                    return "巡逻";
                default:
                    return "普通";
            }
        }
        private static string TaskPanelTemplateTitle(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "监控追踪";
                case 1:
                case 10:
                case 23:
                    return "货柜查验";
                case 2:
                case 14:
                case 24:
                    return "电力修复";
                case 3:
                case 15:
                    return "证物鉴证";
                case 4:
                case 11:
                case 16:
                case 22:
                    return "档案账本";
                case 5:
                case 27:
                    return "接头安全";
                case 6:
                case 13:
                case 21:
                    return "通讯监听";
                case 7:
                case 12:
                    return "门禁封控";
                case 8:
                case 18:
                case 26:
                    return "巡线取证";
                case 9:
                case 19:
                    return "诊所搜查";
                case 17:
                    return "街口执勤";
                case 20:
                    return "鱼档暗号";
                case 25:
                    return "后巷排查";
                default:
                    return "现场任务";
            }
        }
        private static string TaskPanelTemplateSubtitle(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "多屏比对 / 导出线索";
                case 1:
                case 10:
                case 23:
                    return "封条核验 / 货单比对";
                case 2:
                case 14:
                case 24:
                    return "断路恢复 / 电网重启";
                case 3:
                case 15:
                    return "样本扫描 / 证据归档";
                case 4:
                case 11:
                case 16:
                case 22:
                    return "账目追踪 / 异常冻结";
                case 5:
                case 27:
                    return "短接传递 / 风险控制";
                case 6:
                case 13:
                case 21:
                    return "锁频过滤 / 信号回收";
                case 7:
                case 12:
                    return "刷卡开闸 / 通道清理";
                case 8:
                case 18:
                case 26:
                    return "路线校验 / 目击补强";
                case 9:
                case 19:
                    return "现场搜查 / 痕迹比对";
                case 17:
                    return "巡逻打卡 / 风险压制";
                case 20:
                    return "暗号识别 / 交易追踪";
                case 25:
                    return "摩托排查 / 后路封锁";
                default:
                    return "证据推进 / 风险判断";
            }
        }
        private static string TaskPanelFooter(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "监控面板优先看路线";
                case 1:
                case 23:
                    return "货柜越多，假线索越容易藏";
                case 2:
                case 14:
                    return "电力恢复会重开部分视野";
                case 4:
                case 16:
                case 22:
                    return "账本任务更容易拉高证据链";
                case 6:
                case 13:
                case 21:
                    return "通讯越乱，黑帮越容易行动";
                case 7:
                case 12:
                    return "门禁任务适合配合追捕";
                case 8:
                case 18:
                case 26:
                    return "巡线任务会给路线压力";
                default:
                    return "完成后会推进整局节奏";
            }
        }
        private static void DrawTaskScreenGrid(Rect rect)
        {
            Color oldColor = GUI.color;

            for (int i = 0; i < 6; i++)
            {
                float column = i % 3;
                float row = i / 3;
                Rect screen = new Rect(rect.x + 18f + column * (rect.width - 56f) / 3f, rect.y + 14f + row * 42f, (rect.width - 78f) / 3f, 30f);
                GUI.color = i % 2 == 0 ? new Color(0.06f, 0.42f, 0.52f, 1f) : new Color(0.08f, 0.22f, 0.28f, 1f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
                GUI.color = new Color(0.1f, 0.9f, 0.95f, 1f);
                GUI.DrawTexture(new Rect(screen.x + 8f, screen.y + 8f, screen.width * 0.62f, 3f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(screen.x + 8f, screen.y + 17f, screen.width * 0.42f, 3f), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }
        private static void DrawTaskSealScanner(Rect rect)
        {
            Color oldColor = GUI.color;
            Rect belt = new Rect(rect.x + 20f, rect.y + rect.height * 0.48f, rect.width - 40f, 18f);
            GUI.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            GUI.DrawTexture(belt, Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.72f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 44f, rect.y + 22f, rect.width - 88f, 18f), Texture2D.whiteTexture);

            for (int i = 0; i < 5; i++)
            {
                GUI.color = i <= 2 ? new Color(0.1f, 0.72f, 0.84f, 1f) : new Color(0.34f, 0.36f, 0.34f, 1f);
                GUI.DrawTexture(new Rect(rect.x + 46f + i * 54f, rect.y + 66f, 34f, 14f), Texture2D.whiteTexture);
            }

            GUI.color = oldColor;
        }
        private static void DrawTaskBreakerWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            float startX = rect.x + rect.width * 0.28f;

            for (int i = 0; i < 4; i++)
            {
                Rect slot = new Rect(startX + i * 58f, rect.y + 20f, 18f, rect.height - 42f);
                GUI.color = new Color(0.12f, 0.16f, 0.18f, 1f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture);
                GUI.color = i == 2 ? new Color(0.9f, 0.1f, 0.06f, 1f) : new Color(0.16f, 0.72f, 0.32f, 1f);
                GUI.DrawTexture(new Rect(slot.x - 10f, slot.y + 18f + i * 7f, 38f, 10f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.92f, 0.74f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 28f, rect.y + rect.height - 34f, rect.width - 56f, 4f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        private static void DrawTaskEvidenceTray(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.74f, 0.78f, 0.72f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 32f, rect.y + 26f, rect.width - 64f, rect.height - 52f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 48f, rect.y + 42f, rect.width - 96f, rect.height - 84f), Texture2D.whiteTexture);
            GUI.color = new Color(0.08f, 0.68f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 58f, rect.y + 54f, rect.width - 116f, 5f), Texture2D.whiteTexture);
            GUI.color = new Color(0.82f, 0.14f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.38f, rect.y + 68f, 46f, 14f), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.76f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.56f, rect.y + 72f, 34f, 10f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        private static void DrawTaskLedgerWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.16f, 0.12f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, rect.height - 36f), Texture2D.whiteTexture);
            GUI.color = new Color(0.86f, 0.76f, 0.54f, 1f);

            for (int i = 0; i < 5; i++)
            {
                float y = rect.y + 28f + i * 15f;
                GUI.DrawTexture(new Rect(rect.x + 42f, y, rect.width * 0.42f, 4f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width * 0.62f, y, rect.width * 0.22f, 4f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.12f, 0.62f, 0.28f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.54f, rect.y + 72f, 72f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(0.92f, 0.12f, 0.08f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.26f, rect.y + 54f, 52f, 10f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        private static void DrawTaskRouteWidget(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.11f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 18f, rect.y + 18f, rect.width - 36f, rect.height - 36f), Texture2D.whiteTexture);
            GUI.color = new Color(0.42f, 0.62f, 0.66f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float x = rect.x + 54f + i * (rect.width - 120f) / 3f;
                GUI.DrawTexture(new Rect(x, rect.y + 28f, 7f, rect.height - 54f), Texture2D.whiteTexture);
            }

            GUI.color = new Color(0.9f, 0.7f, 0.1f, 1f);
            GUI.DrawTexture(new Rect(rect.x + 58f, rect.y + 76f, rect.width - 116f, 5f), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.72f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.34f, rect.y + 46f, 44f, 12f), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.08f, 0.06f, 1f);
            GUI.DrawTexture(new Rect(rect.x + rect.width * 0.68f, rect.y + 72f, 38f, 12f), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        private static void DrawProgressBar(Rect rect, float progress, Color fillColor)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.08f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(0f, rect.width - 4f) * Mathf.Clamp01(progress), Mathf.Max(0f, rect.height - 4f)), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }
        private static string TaskMapCode(int taskId)
        {
            string title = TaskPanelTemplateTitle(taskId);

            if (string.IsNullOrEmpty(title))
            {
                return "T" + taskId;
            }

            return "T" + taskId + " " + ShortDisplayName(title, 2);
        }
        private static string ShortDisplayName(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string safeValue = value.Trim();
            return safeValue.Length <= maxLength ? safeValue : safeValue.Substring(0, maxLength);
        }
        private static string OpeningRouteStatus(int index)
        {
            switch (index)
            {
                case 0:
                    return "货柜/巡线";
                case 1:
                    return "录像/通话";
                case 2:
                    return "线人/暗号";
                case 3:
                    return "账本/赃款";
                default:
                    return "鉴证/结案";
            }
        }
        private static void ApplyHudSkin()
        {
            int baseSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 72f), 12, 15);
            GUI.skin.label.fontSize = baseSize;
            GUI.skin.button.fontSize = baseSize;
            GUI.skin.textField.fontSize = baseSize;
            GUI.skin.toggle.fontSize = baseSize;
            GUI.skin.box.fontSize = baseSize;
            GUI.skin.label.wordWrap = true;
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

                int value = TaskEvidenceValue(task.Id) + (task.Sabotaged ? 2 : 0);

                if (value > bestValue)
                {
                    best = task;
                    bestValue = value;
                }
            }

            return best;
        }
        private static void DrawResultBar(Rect rect, float ratio, Color color, string label)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y - 1f, rect.width - 16f, rect.height + 4f), label);
            GUI.color = oldColor;
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
                case 0:
                case 6:
                case 13:
                case 21:
                    return circleSprite;
                case 1:
                case 10:
                case 23:
                    return capsuleSprite;
                case 2:
                case 14:
                case 24:
                    return diamondSprite;
                case 3:
                case 15:
                case 18:
                    return softCircleSprite;
                case 4:
                case 11:
                case 16:
                case 22:
                    return roundedRectSprite;
                case 5:
                case 27:
                    return capsuleSprite;
                case 7:
                case 12:
                    return roundedRectSprite;
                case 8:
                case 19:
                case 26:
                    return diamondSprite;
                case 9:
                case 20:
                case 25:
                    return circleSprite;
                default:
                    return roundedRectSprite;
            }
        }
        private static Color Darken(Color color, float multiplier)
        {
            return OnlineWorldBuilder.Darken(color, multiplier);
        }
        private static void Remove3DCollider(GameObject prop)
        {
            OnlineWorldBuilder.Remove3DCollider(prop);
        }
        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
        }
        private static void AttachPhysicsCollider(GameObject prop, Vector3 designScale, bool round)
        {
            OnlineWorldBuilder.AttachPhysicsCollider(prop, designScale, round);
        }
        private static Color FallbackColorForModel(string relativeFbxPath)
        {
            return OnlineWorldBuilder.FallbackColorForModel(relativeFbxPath);
        }
        private static string NormalizeResourcePath(string resourcePath)
        {
            return OnlineWorldBuilder.NormalizeResourcePath(resourcePath);
        }
        private static GameObject InstantiateModelPrefab(GameObject prefab)
        {
            return OnlineWorldBuilder.InstantiateModelPrefab(prefab);
        }
        private static void AlignModelBounds(GameObject model, Vector3 targetPosition)
        {
            OnlineWorldBuilder.AlignModelBounds(model, targetPosition);
        }
        private static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            return OnlineWorldBuilder.TryGetRendererBounds(model, out bounds);
        }
        private static void ConfigureModelRenderers(GameObject model, bool preserveMaterials)
        {
            OnlineWorldBuilder.ConfigureModelRenderers(model, preserveMaterials);
        }
        private static Color ReadMaterialColor(Material material, Color fallback)
        {
            return OnlineWorldBuilder.ReadMaterialColor(material, fallback);
        }
        private static void SetMaterialColor(Material material, Color color)
        {
            OnlineWorldBuilder.SetMaterialColor(material, color);
        }
        private static Vector3 RotateOffset(Vector3 offset, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
        }
        private static Sprite CreateRoundedRectSprite(string spriteName, int size, int radius)
        {
            return OnlineWorldBuilder.CreateRoundedRectSprite(spriteName, size, radius);
        }
        private static Sprite CreateCircleSprite(string spriteName, int size, bool softEdge)
        {
            return OnlineWorldBuilder.CreateCircleSprite(spriteName, size, softEdge);
        }
        private static Sprite CreateDiamondSprite(string spriteName, int size)
        {
            return OnlineWorldBuilder.CreateDiamondSprite(spriteName, size);
        }
        private static void BillboardLabel(Transform labelTransform)
        {
            OnlineWorldBuilder.BillboardLabel(labelTransform);
        }
        private static void SetTextMeshVisible(TextMesh label, bool visible)
        {
            OnlineWorldBuilder.SetTextMeshVisible(label, visible);
        }
        private static void SetColor(GameObject target, Color color)
        {
            OnlineWorldBuilder.SetColor(target, color);
        }
        private static void SetPlayerVisualColors(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            OnlineWorldBuilder.SetPlayerVisualColors(visual, state, isLocal);
        }
        private void UpdatePlayerStageTwoStateLayer(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            bool inMeeting = phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
            bool actionPhase = phase == OnlineMatchPhase.Action;
            bool moving = state.Alive && state.Input.sqrMagnitude > 0.02f;
            bool nearBody = IsNearUnreportedBody(state.Position);
            bool interacting = state.Alive && activeTaskId >= 0 && isLocal;
            bool hasVoted = votes.ContainsKey(state.ClientId);
            Color accent = PlayerAccentColor(state);

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
                SetColor(facingWedge.gameObject, isLocal ? new Color(0.95f, 0.82f, 0.12f, 1f) : Darken(accent, 0.9f));
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

                if (!body.Reported && Vector3.Distance(position, body.Position) <= ruleSet.ReportRange * 1.4f)
                {
                    return true;
                }
            }

            return false;
        }
        private static Transform FindChildTransform(Transform root, params string[] names)
        {
            return OnlineWorldBuilder.FindChildTransform(root, names);
        }
        private static void SetSortingFromZ(GameObject target)
        {
            OnlineWorldBuilder.SetSortingFromZ(target);
        }
        private static string LimitText(string value, int maxLength, string fallback)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

            if (safeValue.Length > maxLength)
            {
                safeValue = safeValue.Substring(0, maxLength);
            }

            return safeValue;
        }
        private static string CleanRelayJoinInput(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string safeValue = value.Trim().ToUpperInvariant();

            if (safeValue.Length > 12)
            {
                safeValue = safeValue.Substring(0, 12);
            }

            return safeValue;
        }
        private static Vector3 TaskScale(int taskId)
        {
            return OnlineWorldBuilder.TaskScale(taskId);
        }
        private static Color PlayerAccentColor(OnlinePlayerState state)
        {
            return OnlineWorldBuilder.PlayerAccentColor(state);
        }
        private static Color PlayerColor(OnlinePlayerState state, bool isLocal)
        {
            return OnlineWorldBuilder.PlayerColor(state, isLocal);
        }
        private static string TaskNameFor(int id)
        {
            return OnlineWorldBuilder.TaskNameFor(id);
        }
        private static string TaskDistrictName(int id)
        {
            return OnlineWorldBuilder.TaskDistrictName(id);
        }
        private Vector3 ScaleMapPosition(Vector3 position)
        {
            return mapService.ScaleMapPosition(position);
        }
        private Vector3 ScaleMapSize(Vector3 size)
        {
            return mapService.ScaleMapSize(size);
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
        private static OnlineProfession ProfessionFor(OnlineRole role, int index)
        {
            // 内鬼：公开为警察，分配警察职业维持掩护
            if (role == OnlineRole.Mole)
            {
                OnlineProfession[] moleCoverProfessions =
                {
                    OnlineProfession.Tech,       // 技术员可访问监控最不易暴露
                    OnlineProfession.Forensics,
                    OnlineProfession.Inspector,
                };
                return moleCoverProfessions[index % moleCoverProfessions.Length];
            }

            // 黑帮：打手/清道夫
            if (role == OnlineRole.Gang)
            {
                OnlineProfession[] gangProfessions =
                {
                    OnlineProfession.Enforcer,
                    OnlineProfession.Fixer,
                };
                return gangProfessions[index % gangProfessions.Length];
            }

            // 卧底：卧底特工/车手
            if (role == OnlineRole.Undercover)
            {
                OnlineProfession[] undercoverProfessions =
                {
                    OnlineProfession.UndercoverAgent,
                    OnlineProfession.Driver,
                };
                return undercoverProfessions[index % undercoverProfessions.Length];
            }

            // 警察：督察/法医/技术员
            OnlineProfession[] policeProfessions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Forensics,
                OnlineProfession.Tech
            };
            return policeProfessions[index % policeProfessions.Length];
        }
        private static string RoleName(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Police:
                    return "警方";
                case OnlineRole.Undercover:
                    return "卧底";
                case OnlineRole.Gang:
                    return "黑帮";
                case OnlineRole.Mole:
                    return "线人";
                default:
                    return "未分配";
            }
        }
        private static string ProfessionName(OnlineProfession profession)
        {
            switch (profession)
            {
                case OnlineProfession.Inspector:
                    return "督察";
                case OnlineProfession.Forensics:
                    return "法证";
                case OnlineProfession.Tech:
                    return "技术";
                case OnlineProfession.UndercoverAgent:
                    return "卧底";
                case OnlineProfession.Enforcer:
                    return "打手";
                case OnlineProfession.Fixer:
                    return "善后";
                case OnlineProfession.Driver:
                    return "车手";
                case OnlineProfession.Mole:
                    return "内鬼";
                default:
                    return "未知";
            }
        }
        private static string PhaseName(OnlineMatchPhase matchPhase)
        {
            switch (matchPhase)
            {
                case OnlineMatchPhase.Lobby:
                    return "房间";
                case OnlineMatchPhase.Opening:
                    return "简报";
                case OnlineMatchPhase.Action:
                    return "行动";
                case OnlineMatchPhase.Meeting:
                    return "会议";
                case OnlineMatchPhase.Voting:
                    return "投票";
                case OnlineMatchPhase.Result:
                    return "结算";
                default:
                    return "未知";
            }
        }
        private static int TaskRequiredProgress(int taskId)
        {
            return OnlineTaskService.TaskRequiredProgress(taskId);
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
                if (CircleIntersectsRect(position, PlayerCollisionRadius, obstacle))
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
        private static bool CircleIntersectsRect(Vector3 center, float radius, Rect rect)
        {
            float nearestX = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
            float nearestY = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
            float dx = center.x - nearestX;
            float dy = center.y - nearestY;
            return dx * dx + dy * dy < radius * radius;
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
