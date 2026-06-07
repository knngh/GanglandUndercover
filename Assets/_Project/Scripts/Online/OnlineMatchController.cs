using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#if UNITY_EDITOR
using UnityEditor;
#endif

using GanglandUndercover;
using GanglandUndercover.Audio;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using GanglandUndercover.SocialDeduction;
using GanglandUndercover.Online.MiniGames;
using GanglandUndercover.Online.Map;
using GanglandUndercover.Online.Surveillance;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController : MonoBehaviour
    {
        private const string ClientStateMessage = "GanglandClientState";
        private const string ClientActionMessage = "GanglandClientAction";
        private const string ClientProfileMessage = "GanglandClientProfile";
        private const string ServerSnapshotMessage = "GanglandServerSnapshot";
        private const string RoleAssignMessage = "GanglandRoleAssign";
        private const string ChatSendMessage = "GanglandChatSend";
        private const string ChatBroadcastMessage = "GanglandChatBroadcast";
        private const string MapSelectMessage = "GanglandMapSelect"; // D5
        private const string WorldRootName = "Online Gangland Runtime Map v5";
        private const string QuaterniusFbxRoot = "Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit/FBX/";
        private const string RuntimeResourcesRoot = "Assets/_Project/Resources/";
        private const string AssetStoreResourceRoot = "AssetStore/";
        private const ushort DefaultPort = 7777;
        private const ulong SkipVoteTarget = ulong.MaxValue;
        private const float SnapshotIntervalSeconds = 0.08f;
        private const float MoveSpeed = 4.5f;
        private const float PlayerCollisionRadius = 0.22f;
        private const float CollisionTraceStep = 0.08f;
        private const float RoleRevealSeconds = 6.5f;
        private const ulong LocalPreviewClientId = 0UL;
        private const uint SurveillanceCameraPrefabHash = 0x47554343; // "GUCC" stable runtime prefab source hash.
        // Camera constants moved to OnlineCameraRig — use _cameraRig.Configure() for all camera setup.
        private const float PlayerAliveVisualScale = 1.12f;
        private const float PlayerDeadVisualScaleX = 1.04f;
        private const float PlayerDeadVisualScaleY = 0.52f;



        internal readonly Dictionary<ulong, OnlinePlayerState> players = new Dictionary<ulong, OnlinePlayerState>();
        private readonly List<OnlineTaskState> tasks = new List<OnlineTaskState>();
        private readonly List<string> caseLog = new List<string>();
        private readonly Dictionary<ulong, OnlineRole> privateRoles = new Dictionary<ulong, OnlineRole>();
        internal readonly Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();
        internal readonly Dictionary<ulong, float> abilityCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> ventCooldowns = new Dictionary<ulong, float>();
        private OnlineBotController _botController;

        // 击杀/尸体/报告状态已迁移到 KillSystem（单一数据源）
        private KillSystem killSystem;

        // M8.4: 对局数据采集器
        private MatchStatsCollector _statsCollector;

        // C2: 证据链指证系统
        internal EvidenceDossier evidenceDossier;
        public string MeetingEvidenceDossier => evidenceDossier?.MeetingEvidenceDossier() ?? "证据系统未就绪。";

        // C4: 内鬼隐藏目标追踪
        private readonly Dictionary<ulong, MoleObjective> _moleObjectives = new Dictionary<ulong, MoleObjective>();
        private int _meetingCount;   // 累计会议次数

        private readonly Dictionary<ulong, GameObject> playerVisuals = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, Vector3> playerVisualBaseScales = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<int, GameObject> taskVisuals = new Dictionary<int, GameObject>();
        private readonly List<OnlineSecurityCamera> surveillanceCameras = new List<OnlineSecurityCamera>();
        private readonly Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();
        private readonly List<Rect> solidObstacleRects = new List<Rect>();
        private readonly List<Rect> walkableRects = new List<Rect>();
        private readonly List<TextMesh> worldLabels = new List<TextMesh>();
        private readonly Dictionary<string, GameObject> modelPrefabCache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> runtimeMeshMaterials = new Dictionary<string, Material>();
        private Sprite roundedRectSprite;
        private Sprite circleSprite;
        private Sprite softCircleSprite;
        private Sprite diamondSprite;
        private Sprite capsuleSprite;

        internal NetworkManager networkManager;
        private UnityTransport transport;
        internal GameObject surveillanceCameraTemplate; // A1: NetworkPrefab 模板
        private UnityServiceBootstrap serviceBootstrap;
        public OnlineWorldBuilder WorldBuilder;
        private OnlineWorldBuilder worldBuilder
        {
            get
            {
                if (WorldBuilder == null)
                {
                    WorldBuilder = new OnlineWorldBuilder();

                    if (worldRoot != null)
                    {
                        WorldBuilder.Initialize(worldRoot, mapService, solidObstacleRects, walkableRects, worldLabels, ruleSet.UnderworldPassageCount);
                    }
                }

                return WorldBuilder;
            }
        }
        private GameObject worldRoot;
        private bool proximityVoiceEnabled;
        private OnlineMatchHud onlineHud;
        private OnlineSyncManager syncManager;
        private HostMigrationManager migrationManager;
        private AudioSource audioSource;
        private Vector2 rosterScroll;
        private Vector2 intelScroll;
        private string joinAddress = "127.0.0.1";
        private string relayJoinCode = string.Empty;
        private string relayJoinInput = string.Empty;
        private string relayStatus = "Relay 房间码未创建。";
        private string localPlayerName = "港区玩家";
        private string roomName = "九龙港区夜局";
        private string status = "离线。创建 Host 或加入 Client。";
        private string resultSummary = "尚未结算。";
        private string lastMeetingReason = "尚未召开会议。";
        private string lastVoteOutcome = "尚未投票。";
        private string lastEvidenceEvent = "尚未取得关键证据。";
        private string lastSabotageEvent = "尚未发生破坏。";
        private OnlineRole localRole = OnlineRole.Unassigned;
        private OnlineMatchPhase phase = OnlineMatchPhase.Lobby;
        private ChatSystem chatSystem;
        private bool localReady;
        private bool roomAutoFillAi;
        private bool revealRoleOnEject;
        [SerializeField] private bool canvasHudEnabled = true;
        [SerializeField] internal OnlineRuleSet ruleSet;
        [SerializeField] internal OnlineMapService mapService;
        [SerializeField] internal OnlineTaskService taskService;
        [SerializeField] internal MiniGames.OnlineMiniGameBridge miniGameBridge;
        [Header("M6 地图布局")]
        [SerializeField] private Map.MapLayoutData mapLayoutData;
        [SerializeField] private Map.MapLayoutData policeStationMapLayoutData;
        [SerializeField] private Map.MapLayoutData kowloonWalledCityMapLayoutData; // D4
        [SerializeField] private bool useGreyboxMode;
        [Header("M6 Kenney 美术")]
        [SerializeField] private Map.KenneySpriteCatalog kenneyCatalog;
        [SerializeField] private bool kenneyMode;
        private bool matchStarted;
        private bool localPreviewMode;
        private bool fullMapPreview = true;
        private bool tacticalMapOpen;
        private bool intelBoardOpen;
        private int roomMinPlayers;
        private int roomMaxPlayers;
        private int emergencyMeetingsLeft;
        private bool tacticalMapDisabled;         // Phase 4: Communications 破坏效果
        private bool _blackoutVisionReduced;       // Phase 4: Blackout 破坏效果
        private bool _blackoutInteractionHalved;   // Phase 4: Blackout 交互减半
        private bool _patrolAlertActive;           // Phase 4: PatrolAlert 破坏效果
        private readonly HashSet<int> _lockedRoomIndices = new HashSet<int>(); // Phase 4: Lockdown 封锁房间
        private Vector2 localInput;
        private Vector3 localPosition;
        private float clientSnapshotTimer;
        private float serverSnapshotTimer;
        private float actionCooldown;
        private float phaseTimer;
        private float emergencyCooldownTimer;
        private float aiActionGraceTimer;
        private float matchElapsedSeconds;
        private int activeTaskId = -1;
        private int activeTaskStep;
        private int activeTaskMistakes;
        private int evidenceMilestoneIndex;
        private float activeTaskCharge;
        private float activeTaskFeedbackTimer;
        private bool activeTaskStepOneDone;
        private bool activeTaskStepTwoDone;
        private bool activeTaskStepThreeDone;
        private bool activeTaskFeedbackPositive;
        private bool submittingActiveTask;
        // Task#7：现场任务接入真·Among Us 风格小游戏（连线/刷卡/记忆/扫描…共 11 种）。
        // 非空时由小游戏自建的 ScreenSpaceOverlay Canvas 接管交互，OnGUI 经典任务面板让位。
        private GanglandUndercover.SocialDeduction.MiniGames.MiniGameBase activeMiniGame;
        private OnlineCameraRig _cameraRig;
        private bool relayOperationInProgress;
        // currentCameraSubjectId moved to OnlineCameraRig; use _cameraRig.SetSubject() / _cameraRig.CurrentSubjectId

        public Dictionary<ulong, OnlinePlayerState> Players => players;
        public IReadOnlyList<OnlineTaskState> Tasks => tasks;
        public List<OnlineBodyState> Bodies => killSystem != null ? killSystem.bodies : null;
        public IReadOnlyList<string> CaseLog => caseLog;
        public OnlineRole LocalRole => localRole;
        public ulong LocalClientIdValue => LocalClientId();
        public string LocalPlayerName => localPlayerName;
        public string JoinAddress => joinAddress;
        public string RelayJoinCode => relayJoinCode;
        public string RelayJoinInput => relayJoinInput;
        public string RelayStatus => relayStatus;
        public string RoomName => roomName;
        public bool IsOnline => localPreviewMode || networkManager != null && (networkManager.IsHost || networkManager.IsClient);
        public bool IsHost => localPreviewMode || networkManager != null && networkManager.IsHost;
        public bool IsLocalPreview => localPreviewMode;
        /// <summary>当前已连接的客户端数（仅 Server/Host 有意义；用于 Relay 双进程联调断言）。</summary>
        public int ConnectedClientCount => networkManager != null ? networkManager.ConnectedClientsList.Count : 0;
        /// <summary>NGO 是否已建立监听（Host）或已连接（Client）。</summary>
        public bool IsListeningOrConnected => networkManager != null && networkManager.IsListening;
        public bool IsClientConnected => networkManager != null && networkManager.IsConnectedClient;
        /// <summary>Task#7：当前是否有现场小游戏在前台（供联机自动化断言小游戏确实接入）。</summary>
        public bool HasActiveMiniGame => activeMiniGame != null;
        /// <summary>Task#7：当前激活小游戏的类型名（WireTask/KeypadTask…），无则空串。</summary>
        public string ActiveMiniGameName => activeMiniGame != null ? activeMiniGame.GetType().Name : string.Empty;
        /// <summary>Task#7：当前正在处理的任务 Id（无则 -1）。</summary>
        public int ActiveTaskId => activeTaskId;
        public bool MatchStarted => matchStarted;
        public OnlineMatchPhase Phase => phase;
        public float PhaseTimer => phaseTimer;
        public string Status { get => status; internal set => status = value; }
        public int TaskCount => tasks.Count;
        public int BodyCount => killSystem != null ? killSystem.bodies.Count : 0;

        // ── M7.1 Relay 公开 API ───────────────────────────────
        /// <summary>Relay 状态变化事件（供 LobbyController 订阅）</summary>
        public event System.Action<string> OnRelayStatusChanged;
        public event System.Action<string> OnRelayRoomCodeReady;
        public event System.Action<bool> OnRelayConnectionChanged;

        /// <summary>LobbyController 调用：请求准备状态切换</summary>
        public void RequestReadyToggle()
        {
            localReady = !localReady;
            SendClientState(true);
        }
        public int BotCount => _botController?.BotCount ?? 0;
        public int HumanPlayerCount => CountHumanPlayers();
        public int ReadyPlayerCountValue => ReadyPlayerCount();
        public int AlivePlayerCount => CountAlivePlayers();
        public int PlayerCount => players.Count;
        public int CompletedTaskCount => CountCompletedTasks();
        public int SabotagedTaskCount => CountSabotagedTasks();
        public int UnreportedBodyCount => CountUnreportedBodies();
        public int CaseLogCount => caseLog.Count;
        public bool HasWorld => worldRoot != null;
        public bool HasCanvasHud => onlineHud != null;
        public int WorldObjectCount => CountWorldObjects();
        public int CollisionObjectCount => solidObstacleRects.Count;
        public int PhysicsColliderCount => worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<Collider2D>(true).Length;
        public int BuildingVolumeCount => CountNamedWorldObjects("2.5D 建筑体");
        public int RooftopFeatureCount => CountNamedWorldObjects("屋顶");
        public int ForegroundOccluderCount => CountNamedWorldObjects("前景遮挡层");
        public int PremiumTaskSetPieceCount => CountNamedWorldObjects("成熟任务站");
        public int OrganicRouteFeatureCount => CountNamedWorldObjects("非直角动线");
        public int MatureDockyardSetPieceCount => CountNamedWorldObjects("成熟港区设施");
        public int CommercialArtAdapterCount => CountNamedWorldObjects("资源适配层");
        public int OfficialFreeAssetSetPieceCount => CountNamedWorldObjects("官方免费素材层");
        public int DenseOfficialStreetSetPieceCount => CountNamedWorldObjects("官方免费街区密度层");
        public int TaskReadabilityMarkerCount => CountNamedWorldObjects("任务可读性");
        public int ActionViewShowcasePieceCount => CountNamedWorldObjects("行动视角样板层");
        public int VerticalSliceSetPieceCount => CountNamedWorldObjects("VerticalSlice");
        public int VerticalSliceRoomIdentityCount => CountNamedWorldObjects("VerticalSlice Room");
        public int VerticalSliceTaskMiniGameSetPieceCount => CountNamedWorldObjects("VerticalSlice Task");
        public int VerticalSliceMapOverlayCount => onlineHud == null ? 0 : onlineHud.VerticalSliceStaticMapElementCount;
        public int VerticalSliceStageOneSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1");
        public int VerticalSliceStageOneEntranceCount => CountNamedWorldObjects("VerticalSlice Stage1 Entrance");
        public int VerticalSliceStageOneFirstScreenCount => CountNamedWorldObjects("VerticalSlice Stage1 FirstScreen");
        public int VerticalSliceStageOneSightlineCount => CountNamedWorldObjects("VerticalSlice Stage1 Sightline");
        public int VerticalSliceStageOneCameraShotCount => CountNamedWorldObjects("VerticalSlice Stage1 CameraShot");
        public int VerticalSliceStageOneGameplayAnchorCount => CountNamedWorldObjects("VerticalSlice Stage1 GameplayAnchor");
        public int VerticalSliceStageOneMeetingSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1 Meeting");
        public int VerticalSliceStageOneBlackoutSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1 Blackout");
        public int VerticalSliceStageOneEditableAnchorCount => worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<VerticalSliceStageOneAnchor>(true).Length;
        public int FreeCharacterAdapterCount => CountNamedWorldObjects("FreeCharacterAdapter");
        public int StageTwoCharacterStateLayerCount => CountNamedWorldObjects("Stage2 Character");
        internal void DecrementEmergencyMeetings() { emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1); }
        internal void AddBotPlayer(ulong clientId, string displayName, Vector3 spawn, OnlineProfession profession)
        {
            players[clientId] = new OnlinePlayerState(clientId, displayName, spawn, true, true, OnlineRole.Unassigned, profession, 0, true);
        }
        internal void SetKillCooldown(ulong clientId, float value) { if (killSystem != null) killSystem.killCooldowns[clientId] = value; }
        internal void SetAbilityCooldown(ulong clientId, float value) { abilityCooldowns[clientId] = value; }
        internal bool TryGetKillCooldown(ulong clientId, out float value) { if (killSystem != null) return killSystem.killCooldowns.TryGetValue(clientId, out value); value = 0f; return false; }
        internal bool HasVoted(ulong clientId) => votes.ContainsKey(clientId);
        public float BlackoutTimer => taskService.BlackoutTimer;
        public float LockdownTimer => taskService.LockdownTimer;
        public float CommunicationJamTimer => taskService.CommunicationJamTimer;
        public float EvidenceLeakTimer => taskService.EvidenceLeakTimer;
        public float PatrolAlertTimer => taskService.PatrolAlertTimer;
        /// <summary>B3: 通用破坏计时器设置（替代反射）</summary>
        public void ApplySabotageTimer(SabotageType type)
        {
            taskService.ApplySabotageEffect(type, type.ToString());
        }
        /// <summary>D3: 当前是否在会议/投票阶段</summary>
        public bool IsMeetingPhase => phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
        public bool TacticalMapOpen => tacticalMapOpen;
        public bool IntelBoardOpen => intelBoardOpen;
        // M1 收尾：语音已移除，以下三个属性转为聊天通道状态映射
        public string VoiceStatus => chatSystem != null ? "文本聊天: " + chatSystem.CurrentChannel : "文本聊天未初始化";
        public string ActiveVoiceChannel => chatSystem != null ? chatSystem.CurrentChannel.ToString() : "None";
        public int VoiceParticipantCount => chatSystem != null ? chatSystem.MessageCount : 0;
        public bool VoiceRoutingEnabled => true; // 文本聊天始终可用
        public bool LocalTaskInputGateActive => activeTaskId >= 0;

        public void EditorForceRestartForSmokeTest()
        {
            RestartMatch();
        }

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

        /// <summary>
        /// 烟测/自动化用：淘汰所有黑帮玩家并评估胜负，确定性地把对局推进到 Result 阶段。
        /// 走真实的 EvaluateWinConditions 路径（含 VictoryBridge），返回是否成功进入结算。
        /// </summary>
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

        /// <summary>
        /// Task#7 自动化：按 taskId 打开现场小游戏（无头环境无法点击，故提供驱动钩子）。
        /// 返回当前激活的小游戏类型名（如 WireTask），未能打开则返回空串。
        /// </summary>
        public string EditorOpenTaskMiniGameForSmokeTest(int taskId)
        {
            BeginActiveTask(taskId);
            return ActiveMiniGameName;
        }

        /// <summary>
        /// Task#7 自动化：强制完成当前激活的小游戏，走与真实完成一致的 CompleteActiveTask 路径。
        /// 完成后 activeTaskId 应回到 -1。返回是否确有小游戏被完成。
        /// </summary>
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
                if (miniGameBridge == null)
                {
                    miniGameBridge = gameObject.AddComponent<MiniGames.OnlineMiniGameBridge>();
                }
            }
            miniGameBridge.BindController(this);
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

        // E4: 破坏 VFX 初始化
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

        public OnlineRuleSet ActiveRuleSet => ruleSet != null ? ruleSet : ScriptableObject.CreateInstance<OnlineRuleSet>();

        private void Awake()
        {
            EnsureCoreServices();
            EnsureRuleSet();
            BuildDefaultTasks();
            EnsureWorld();
            EnsureAudio();
            EnsureServiceBootstrap();
            EnsureNetworkStack();
            EnsureCanvasHud();
            EnsureCameraRig();
            EnsureVFX(); // E4
            syncManager = GetComponent<OnlineSyncManager>();
            EnsureMigrationManager();
            EnsureChatSystem();
            EnsureRuntimeDependencies();
            localPosition = mapService.SpawnPosition(UnityEngine.Random.Range(0, ruleSet.MaximumRoomPlayers));
            _meetingCount = 0;
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

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.M))
            {
                tacticalMapOpen = !tacticalMapOpen;
                fullMapPreview = tacticalMapOpen;
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                intelBoardOpen = !intelBoardOpen;
            }

            if (activeTaskId >= 0)
            {
                // 小游戏接管时（activeMiniGame 非空）由其自己的 UI 处理输入，
                // 不再走 OnGUI 经典蓄力面板的键盘输入。
                if (activeMiniGame == null)
                {
                    ReadActiveTaskInput();
                }
            }
            else if (activeMiniGame != null)
            {
                // 任务在别处被重置（阶段切换/死亡/会议等共 12 处把 activeTaskId 置 -1），
                // 这里统一回收悬挂的小游戏对象，无需逐点修改各重置位。
                DestroyActiveMiniGame();
            }

            if (activeTaskFeedbackTimer > 0f)
            {
                activeTaskFeedbackTimer = Mathf.Max(0f, activeTaskFeedbackTimer - Time.deltaTime);
            }

            if (!IsOnline)
            {
                return;
            }

            ReadLocalInput();
            ReadLocalActions();
            SendClientState();

            if (localPreviewMode || networkManager.IsServer)
            {
                TickHostSimulation();
            }
        }

        private void LateUpdate()
        {
            EnsureWorld();
            EnsureAudio();
            EnsureServiceBootstrap();
            EnsureCanvasHud();
            ConfigureMainCamera();
            UpdateWorldVisuals();
            TickCharacterAnimators();
        }

        private static readonly int AnimSpeedHash = Animator.StringToHash("Speed");
        private static readonly int AnimDeadHash  = Animator.StringToHash("Dead");
        private static readonly int AnimActionHash = Animator.StringToHash("Action");





        /// <summary>
        /// A1 修复：创建并注册监控摄像头 NetworkPrefab 模板。
        /// 必须在 NetworkManager.StartHost() 前调用。
        /// </summary>




        private void OnDestroy()
        {
            if (networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            UnregisterMessages();
        }








        public void SetLocalPlayerName(string value)
        {
            localPlayerName = LimitText(value, 16, "港区玩家");

            if (localPreviewMode)
            {
                StartLocalPreviewRoom();
                return;
            }

            if (IsOnline)
            {
                SendClientProfile();
            }
        }

        public void SetRoomName(string value)
        {
            roomName = LimitText(value, 20, "九龙港区夜局");

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetJoinAddress(string value)
        {
            joinAddress = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
        }

        public void SetRelayJoinInput(string value)
        {
            relayJoinInput = CleanRelayJoinInput(value);
        }

        public void SetRoomMinPlayers(int value)
        {
            roomMinPlayers = Mathf.Clamp(value, ruleSet.MinimumRoomPlayers, roomMaxPlayers);

            if (roomMaxPlayers < roomMinPlayers)
            {
                roomMaxPlayers = roomMinPlayers;
            }

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetRoomMaxPlayers(int value)
        {
            roomMaxPlayers = Mathf.Clamp(value, roomMinPlayers, ruleSet.MaximumRoomPlayers);

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetEvidenceTarget(int value)
        {
            taskService.EvidenceTarget = Mathf.Clamp(value, 34, 56);

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetAutoFillAi(bool value)
        {
            roomAutoFillAi = value;

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetRevealRoleOnEject(bool value)
        {
            revealRoleOnEject = value;

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetProximityVoiceEnabled(bool value)
        {
            proximityVoiceEnabled = value;
        }

        public void SetLocalReady(bool ready)
        {
            localReady = ready;

            if (IsOnline)
            {
                SendClientState(true);
            }
        }

        public void SetReady(bool ready)
        {
            SetLocalReady(ready);
        }

        public void ToggleLocalReady()
        {
            SetLocalReady(!localReady);
        }

        public void ToggleTacticalMap()
        {
            tacticalMapOpen = !tacticalMapOpen;
            fullMapPreview = tacticalMapOpen;
        }

        public void ToggleIntelBoard()
        {
            intelBoardOpen = !intelBoardOpen;
        }

        public void RequestHost()
        {
            StartHost();
        }

        public void RequestClient()
        {
            StartClient(joinAddress);
        }

        public void RequestRelayHost()
        {
            StartRelayHost();
        }

        public void RequestRelayClient()
        {
            StartRelayClient();
        }

        public void RequestRelayClient(string joinCode)
        {
            SetRelayJoinInput(joinCode);
            StartRelayClient();
        }

        public void RequestLocalPreview()
        {
            StartLocalPreviewRoom();
            FillBotsAndStart();
        }

        public void RequestStartMatch()
        {
            StartOnlineMatch();
        }

        public void RequestFillBotsAndStart()
        {
            FillBotsAndStart();
        }

        public void RequestRestartMatch()
        {
            RestartMatch();
        }

        public void RequestReturnToLobby()
        {
            ReturnToLobby();
        }

        public void RequestShutdown()
        {
            Shutdown();
        }

        public void LeaveRoom()
        {
            Shutdown();
            OnRelayConnectionChanged?.Invoke(false);
            OnRelayStatusChanged?.Invoke(relayStatus);
        }

        public void RequestAction(OnlineActionType actionType)
        {
            SendClientAction(actionType);
        }

        public void RequestTaskStep(int input)
        {
            if (activeTaskId >= 0)
            {
                ResolveActiveTaskStep(input);
            }
        }

        public void RequestCancelActiveTask()
        {
            if (activeTaskId < 0)
            {
                return;
            }

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

        public void RequestChargeActiveTask()
        {
            if (activeTaskId >= 0)
            {
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + Time.deltaTime * TaskChargeRate(activeTaskId));
            }
        }

        public void RequestVote(ulong targetClientId)
        {
            SendClientAction(OnlineActionType.Vote, targetClientId);
        }

        public void RequestSkipVote()
        {
            SendClientAction(OnlineActionType.SkipVote);
        }








        private void ReadLocalInput()
        {
            if (phase != OnlineMatchPhase.Action || !IsLocalAlive())
            {
                localInput = Vector2.zero;
                return;
            }

            localInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (localInput.sqrMagnitude > 1f)
            {
                localInput.Normalize();
            }
        }

        private void ReadLocalActions()
        {
            if (activeTaskId >= 0)
            {
                return;
            }

            actionCooldown -= Time.deltaTime;

            if (actionCooldown > 0f || phase == OnlineMatchPhase.Result || phase == OnlineMatchPhase.Lobby)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (ShouldOpenLocalTaskPanel())
                {
                    OnlineTaskState nearestTask = FindNearestTask(localPosition);

                    if (nearestTask.Id >= 0)
                    {
                        BeginActiveTask(nearestTask.Id);
                        actionCooldown = 0.35f;
                        return;
                    }
                }

                SendClientAction(OnlineActionType.Interact);
                actionCooldown = 0.35f;
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                SendClientAction(OnlineActionType.Kill);
                actionCooldown = 0.35f;
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                SendClientAction(OnlineActionType.Report);
                actionCooldown = 0.35f;
            }
            else if (Input.GetKeyDown(KeyCode.F))
            {
                SendClientAction(OnlineActionType.Ability);
                actionCooldown = 0.45f;
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                SendClientAction(OnlineActionType.Vent);
                actionCooldown = 0.35f;
            }
        }










        private void TickHostSimulation()
        {
            float deltaTime = Time.deltaTime;
            taskService.TickSabotageTimers(deltaTime);

            // Phase 2.4: 紧急任务触发检查与同步
            TickCriticalTaskTriggers();
            TickCriticalTaskSync();

            if (emergencyCooldownTimer > 0f)
            {
                emergencyCooldownTimer = Mathf.Max(0f, emergencyCooldownTimer - deltaTime);
            }

            if (killSystem != null)
                killSystem.TickReportCooldown(deltaTime);

            if (aiActionGraceTimer > 0f)
            {
                aiActionGraceTimer = Mathf.Max(0f, aiActionGraceTimer - deltaTime);
            }

            if (phase == OnlineMatchPhase.Opening)
            {
                phaseTimer -= deltaTime;

                if (phaseTimer <= 0f)
                {
                    phase = OnlineMatchPhase.Action;
                    phaseTimer = 0f;
                    fullMapPreview = false;
                    status = "行动开始：九龙港城进入封控搜证。";
                    AddCaseLog(status);
                    BroadcastSnapshot();
                }
            }
            else if (phase == OnlineMatchPhase.Meeting)
            {
                phaseTimer -= deltaTime;
                TickBotVoting(deltaTime);

                if (phaseTimer <= 0f)
                {
                    phase = OnlineMatchPhase.Voting;
                    phaseTimer = ruleSet.VotingSeconds;
                    status = "开始投票。";
                    AddCaseLog(status);
                    BroadcastSnapshot();
                }
            }
            else if (phase == OnlineMatchPhase.Voting)
            {
                phaseTimer -= deltaTime;
                TickBotVoting(deltaTime);

                if (phaseTimer <= 0f)
                {
                    ResolveVotes();
                }
            }

            if (phase == OnlineMatchPhase.Action)
            {
                matchElapsedSeconds += deltaTime;

                if (matchElapsedSeconds >= ruleSet.MatchHardLimitSeconds)
                {
                    ResolveTimeLimitOutcome();
                    return;
                }

                if (aiActionGraceTimer <= 0f)
                {
                    TickBotAction(deltaTime);
                }

                List<ulong> ids = new List<ulong>(players.Keys);

                foreach (ulong clientId in ids)
                {
                    OnlinePlayerState state = players[clientId];

                    if (state.Alive)
                    {
                        Vector3 direction = new Vector3(state.Input.x, state.Input.y, 0f);
                        float speedMultiplier = taskService.LockdownTimer > 0f ? 0.72f : taskService.PatrolAlertTimer > 0f && GetPrivateRole(clientId) == OnlineRole.Gang ? 0.9f : 1f;
                        // C1: 被动能力 MoveSpeedBonus (Driver 1.08x)
                        if (ruleSet != null && ruleSet.HasAbility(state.Profession, AbilityType.MoveSpeedBonus))
                            speedMultiplier *= ruleSet.GetAbilityMultiplier(state.Profession, AbilityType.MoveSpeedBonus);
                        state.Position = ResolveMapCollision(state.Position, state.Position + direction * MoveSpeed * speedMultiplier * deltaTime);
                    }
                    else
                    {
                        state.Input = Vector2.zero;
                    }

                    players[clientId] = state;

                    if (clientId == LocalClientId())
                    {
                        localPosition = state.Position;
                    }
                }
            }

            TickMoleExposureCheck();

            TickSurveillanceCameras();
            TickCooldowns(deltaTime);

            serverSnapshotTimer -= deltaTime;

            if (serverSnapshotTimer <= 0f)
            {
                serverSnapshotTimer = SnapshotIntervalSeconds;
                BroadcastSnapshot();
            }
        }



        /// <summary>
        /// 客户端收到服务器快照后，验证复原的状态是否完整可用。
        /// 检测残缺快照（如零玩家/零任务/空对局阶段）并记录警告。
        /// </summary>

        internal void StartOnlineMatch()
        {
            if ((!localPreviewMode && (networkManager == null || !networkManager.IsServer)) || players.Count < 1)
            {
                return;
            }

            if (!CanStartLobbyMatch())
            {
                status = "暂不能开局：" + BuildLobbyReadinessSummary();
                BroadcastSnapshot();
                return;
            }

            if (!roomAutoFillAi && CountHumanPlayers() < roomMinPlayers)
            {
                status = "人数不足：" + CountHumanPlayers() + "/" + roomMinPlayers + "，可开启 AI 补位或等待玩家。";
                BroadcastSnapshot();
                return;
            }

            StartOnlineMatchCore(true);
        }

        private void StartOnlineMatchCore(bool broadcast)
        {
            EnsureRuntimeDependencies();

            if (roomAutoFillAi && players.Count < roomMinPlayers)
            {
                EnsureMinimumBots();
            }

            BuildDefaultTasks();
            killSystem.bodies.Clear();
            votes.Clear();
            caseLog.Clear();
            privateRoles.Clear();
            killSystem.killCooldowns.Clear();
            abilityCooldowns.Clear();
            _botController?.ClearVoteTimers();
            killSystem.nextBodyId = 0;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            submittingActiveTask = false;
            taskService.EvidenceScore = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "专案启动，证据链待闭合。";
            lastSabotageEvent = "暂未发现破坏。";
            evidenceMilestoneIndex = 0;
            phaseTimer = 0f;
            taskService.ResetAllSabotageTimers();
            emergencyCooldownTimer = 0f;
            emergencyMeetingsLeft = ruleSet.EmergencyMeetingLimitFor(players.Count);
            aiActionGraceTimer = ruleSet.AiActionGraceSeconds;
            _cameraRig.SetSubject(LocalPreviewClientId);
            matchElapsedSeconds = 0f;
            resultSummary = "专案简报中。";
            matchStarted = true;
            phase = OnlineMatchPhase.Opening;
            phaseTimer = RoleRevealSeconds;
            fullMapPreview = true;

            List<ulong> ids = new List<ulong>(players.Keys);
            ids.Sort();

            for (int i = 0; i < ids.Count; i++)
            {
                OnlinePlayerState state = players[ids[i]];
                state.Position = FindNearestOpenPosition(mapService.SpawnPosition(i), Vector3.zero);
                state.Input = Vector2.zero;
                state.Alive = true;
                state.PublicRole = OnlineRole.Unassigned;
                state.KillCooldown = 0f;
                state.AbilityCooldown = 0f;
                state.Suspicion = 0;
                state.Ready = true;
                players[ids[i]] = state;
                killSystem.killCooldowns[ids[i]] = 0f;
                abilityCooldowns[ids[i]] = 0f;
                if (OnlineBotController.IsBotClient(ids[i]))
                    _botController.InitBotState(ids[i]);
            }

            AssignRoles(ids);
            status = "专案开始：身份已私发，准备进入九龙港城。";
            AddCaseLog(status);
            PlayCue("start");

            if (broadcast)
            {
                BroadcastSnapshot();
            }

            List<ulong> gangIds = new List<ulong>();
            List<ulong> nonGangIds = new List<ulong>();
            foreach (var kvp in players)
            {
                if (privateRoles.TryGetValue(kvp.Key, out OnlineRole role))
                {
                    if (role == OnlineRole.Gang || role == OnlineRole.Undercover)
                        gangIds.Add(kvp.Key);
                    else if (role == OnlineRole.Police)
                        nonGangIds.Add(kvp.Key);
                }
            }
            syncManager?.OnMatchStarted(gangIds, nonGangIds, tasks);
        }

        // M4.1: 可配置阵营分配
        private void AssignRoles(IList<ulong> ids)
        {
            RoleDistribution dist = ruleSet.GetRoleDistribution(ids.Count);
            List<ulong> shuffled = new List<ulong>(ids);
            Shuffle(shuffled);

            int idx = 0;
            int gangIdx = 0, undercoverIdx = 0, moleIdx = 0, policeIdx = 0;

            // 1) 黑帮
            for (int g = 0; g < dist.gang && idx < shuffled.Count; g++, idx++)
            {
                AssignSingleRole(shuffled[idx], OnlineRole.Gang, gangIdx++);
            }

            // 2) 卧底
            for (int u = 0; u < dist.undercover && idx < shuffled.Count; u++, idx++)
            {
                AssignSingleRole(shuffled[idx], OnlineRole.Undercover, undercoverIdx++);
            }

            // 3) 内鬼
            for (int m = 0; m < dist.mole && idx < shuffled.Count; m++, idx++)
            {
                AssignSingleRole(shuffled[idx], OnlineRole.Mole, moleIdx++);
            }

            // 4) 剩余为警察/市民
            for (; idx < shuffled.Count; idx++)
            {
                AssignSingleRole(shuffled[idx], OnlineRole.Police, policeIdx++);
            }
        }

        private void AssignSingleRole(ulong clientId, OnlineRole role, int roleIndex)
        {
            privateRoles[clientId] = role;
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                state.Profession = ProfessionFor(role, roleIndex);
                state.Suspicion = (role == OnlineRole.Gang || role == OnlineRole.Mole) ? 1 : 0;
                state.PublicRole = role switch
                {
                    OnlineRole.Mole       => OnlineRole.Police,
                    OnlineRole.Undercover => OnlineRole.Gang,
                    _                    => role
                };
                players[clientId] = state;
            }
            SendRole(clientId, role);
        }

        private void FillBotsAndStart()
        {
            EnsureRuntimeDependencies();
            _botController.FillBotsAndStart();
        }

        private void EnsureMinimumBots()
        {
            EnsureRuntimeDependencies();
            _botController.EnsureMinimumBots();
        }



        // ─── 聊天系统 ─────────────────────────────



        /// <summary>
        /// 捕获当前游戏状态的完整快照（供主机迁移使用）。
        /// </summary>
        public GameStateSnapshot CaptureSnapshot()
        {
            var snap = new GameStateSnapshot();

            // ── 版本标记 ──
            snap.Version = GameStateSnapshot.SNAPSHOT_VERSION;

            // ── 全局状态 ──
            snap.MatchStarted = matchStarted;
            snap.Phase = phase;
            snap.EvidenceScore = taskService.EvidenceScore;
            snap.EvidenceTarget = taskService.EvidenceTarget;
            snap.EmergencyMeetingsLeft = emergencyMeetingsLeft;
            snap.EvidenceMilestoneIndex = evidenceMilestoneIndex;
            snap.NextBodyId = killSystem.nextBodyId;
            snap.RoomMinPlayers = roomMinPlayers;
            snap.RoomMaxPlayers = roomMaxPlayers;
            snap.RoomAutoFillAi = roomAutoFillAi;
            snap.RevealRoleOnEject = revealRoleOnEject;
            snap.ProximityVoiceEnabled = proximityVoiceEnabled;
            snap.RoomName = roomName;
            snap.ResultSummary = resultSummary;
            snap.LastMeetingReason = lastMeetingReason;
            snap.LastVoteOutcome = lastVoteOutcome;
            snap.LastEvidenceEvent = lastEvidenceEvent;
            snap.LastSabotageEvent = lastSabotageEvent;
            snap.PhaseTimer = phaseTimer;
            snap.BlackoutTimer = taskService.BlackoutTimer;
            snap.LockdownTimer = taskService.LockdownTimer;
            snap.CommunicationJamTimer = taskService.CommunicationJamTimer;
            snap.EvidenceLeakTimer = taskService.EvidenceLeakTimer;
            snap.EvidenceLeakAccumulator = taskService.EvidenceLeakAccumulator;
            snap.PatrolAlertTimer = taskService.PatrolAlertTimer;
            snap.EmergencyCooldownTimer = emergencyCooldownTimer;
            snap.AiActionGraceTimer = aiActionGraceTimer;
            snap.MatchElapsedSeconds = matchElapsedSeconds;

            // ── 玩家状态 ──
            snap.Players = new List<GameStateSnapshot.SnapshotPlayerEntry>(players.Count);
            foreach (var p in players.Values)
            {
                snap.Players.Add(new GameStateSnapshot.SnapshotPlayerEntry
                {
                    ClientId = p.ClientId,
                    DisplayName = p.DisplayName,
                    Position = p.Position,
                    Input = p.Input,
                    Ready = p.Ready,
                    Alive = p.Alive,
                    IsBot = p.IsBot,
                    PublicRole = p.PublicRole,
                    Profession = p.Profession,
                    KillCooldown = killSystem.killCooldowns.TryGetValue(p.ClientId, out float kd) ? kd : 0f,
                    AbilityCooldown = abilityCooldowns.TryGetValue(p.ClientId, out float ac) ? ac : 0f,
                    Suspicion = p.Suspicion,
                });
            }

            // ── 私密角色 ──
            snap.PrivateRoles = new List<GameStateSnapshot.SnapshotRoleEntry>(privateRoles.Count);
            foreach (var kv in privateRoles)
            {
                snap.PrivateRoles.Add(new GameStateSnapshot.SnapshotRoleEntry { ClientId = kv.Key, Role = kv.Value });
            }

            // ── 任务 ──
            snap.Tasks = new List<GameStateSnapshot.SnapshotTaskEntry>(tasks.Count);
            foreach (var t in tasks)
            {
                snap.Tasks.Add(new GameStateSnapshot.SnapshotTaskEntry
                {
                    Id = t.Id, Name = t.Name, Position = t.Position,
                    Progress = t.Progress, RequiredProgress = t.RequiredProgress,
                    Completed = t.Completed, Sabotaged = t.Sabotaged,
                });
            }

            // ── 尸体 ──
            snap.Bodies = new List<GameStateSnapshot.SnapshotBodyEntry>(killSystem.bodies.Count);
            foreach (var b in killSystem.bodies)
            {
                snap.Bodies.Add(new GameStateSnapshot.SnapshotBodyEntry
                {
                    Id = b.Id, VictimClientId = b.VictimClientId,
                    Position = b.Position, Reported = b.Reported,
                });
            }

            // ── 投票 ──
            snap.Votes = new List<GameStateSnapshot.SnapshotVoteEntry>(votes.Count);
            foreach (var v in votes)
            {
                snap.Votes.Add(new GameStateSnapshot.SnapshotVoteEntry { VoterClientId = v.Key, TargetClientId = v.Value });
            }

            // ── 案卷 ──
            snap.CaseLog = new List<string>(caseLog);

            // ── 冷却 ──
            snap.KillCooldowns = CooldownsToList(killSystem.killCooldowns);
            snap.AbilityCooldowns = CooldownsToList(abilityCooldowns);
            snap.VentCooldowns = CooldownsToList(ventCooldowns);
            snap.BotThinkTimers = CooldownsToList(_botController.ThinkTimers);
            snap.BotVoteTimers = CooldownsToList(_botController.VoteTimers);

            // ── Bot 目标 ──
            snap.BotTargets = new List<GameStateSnapshot.SnapshotTargetEntry>(_botController.Targets.Count);
            foreach (var bt in _botController.Targets)
            {
                snap.BotTargets.Add(new GameStateSnapshot.SnapshotTargetEntry { ClientId = bt.Key, Target = bt.Value });
            }

            return snap;
        }

        /// <summary>
        /// 从快照恢复游戏状态（主机迁移时由新主机或客户端调用）。
        /// </summary>
        public void RestoreFromSnapshot(GameStateSnapshot snap)
        {
            // ── 版本兼容性检查 ──
            if (snap.Version != GameStateSnapshot.SNAPSHOT_VERSION)
            {
                Debug.LogWarning(
                    $"[RestoreFromSnapshot] 快照版本不匹配: 快照 v{snap.Version}, 当前 v{GameStateSnapshot.SNAPSHOT_VERSION}。" +
                    "将尽力恢复，但部分状态可能存在差异。");
            }

            if (!snap.IsValid())
            {
                Debug.LogError("[RestoreFromSnapshot] 快照完整性检查失败，恢复后的状态可能不完整。");
            }

            // ── 全局状态 ──
            matchStarted = snap.MatchStarted;
            phase = snap.Phase;
            taskService.EvidenceScore = snap.EvidenceScore;
            taskService.EvidenceTarget = snap.EvidenceTarget;
            emergencyMeetingsLeft = snap.EmergencyMeetingsLeft;
            evidenceMilestoneIndex = snap.EvidenceMilestoneIndex;
            killSystem.nextBodyId = snap.NextBodyId;
            roomMinPlayers = snap.RoomMinPlayers;
            roomMaxPlayers = snap.RoomMaxPlayers;
            roomAutoFillAi = snap.RoomAutoFillAi;
            revealRoleOnEject = snap.RevealRoleOnEject;
            proximityVoiceEnabled = snap.ProximityVoiceEnabled;
            roomName = snap.RoomName;
            resultSummary = snap.ResultSummary;
            lastMeetingReason = snap.LastMeetingReason;
            lastVoteOutcome = snap.LastVoteOutcome;
            lastEvidenceEvent = snap.LastEvidenceEvent;
            lastSabotageEvent = snap.LastSabotageEvent;
            phaseTimer = snap.PhaseTimer;
            taskService.LoadSabotageTimersFromSnapshot(
                snap.BlackoutTimer, snap.LockdownTimer, snap.CommunicationJamTimer,
                snap.EvidenceLeakTimer, snap.EvidenceLeakAccumulator, snap.PatrolAlertTimer);
            emergencyCooldownTimer = snap.EmergencyCooldownTimer;
            aiActionGraceTimer = snap.AiActionGraceTimer;
            matchElapsedSeconds = snap.MatchElapsedSeconds;

            // ── 玩家状态 ──
            players.Clear();
            foreach (var p in snap.Players)
            {
                var state = new OnlinePlayerState(p.ClientId, p.DisplayName, p.Position, p.Ready, p.Alive, p.PublicRole, p.Profession, p.Suspicion, p.IsBot)
                {
                    Input = p.Input,
                    KillCooldown = p.KillCooldown,
                    AbilityCooldown = p.AbilityCooldown,
                };
                players[p.ClientId] = state;
            }

            // ── 私密角色 ──
            privateRoles.Clear();
            foreach (var r in snap.PrivateRoles)
            {
                privateRoles[r.ClientId] = r.Role;
            }

            // ── 任务 ──
            tasks.Clear();
            foreach (var t in snap.Tasks)
            {
                tasks.Add(new OnlineTaskState(t.Id, t.Name, t.Position, t.Progress, t.RequiredProgress, t.Completed, t.Sabotaged));
            }

            // ── 尸体 ──
            killSystem.bodies.Clear();
            foreach (var b in snap.Bodies)
            {
                killSystem.bodies.Add(new OnlineBodyState(b.Id, b.VictimClientId, b.Position, b.Reported));
            }

            // ── 投票 ──
            votes.Clear();
            foreach (var v in snap.Votes)
            {
                votes[v.VoterClientId] = v.TargetClientId;
            }

            // ── 案卷 ──
            caseLog.Clear();
            caseLog.AddRange(snap.CaseLog);

            // ── 冷却 ──
            ListToCooldowns(killSystem.killCooldowns, snap.KillCooldowns);
            ListToCooldowns(abilityCooldowns, snap.AbilityCooldowns);
            ListToCooldowns(ventCooldowns, snap.VentCooldowns);
            // Bot 计时器通过 bot 控制器恢复
            if (snap.BotThinkTimers != null)
                foreach (var entry in snap.BotThinkTimers)
                    _botController.SetThinkTimer(entry.ClientId, entry.Value);
            if (snap.BotVoteTimers != null)
                foreach (var entry in snap.BotVoteTimers)
                    _botController.SetVoteTimer(entry.ClientId, entry.Value);

            // ── Bot 目标 ──
            _botController.ClearTargets();
            foreach (var bt in snap.BotTargets)
            {
                _botController.SetTarget(bt.ClientId, bt.Target);
            }

            // 更新本地位置
            ulong localId = LocalClientId();
            if (players.TryGetValue(localId, out OnlinePlayerState localState))
            {
                localPosition = localState.Position;
            }

            status = "主机迁移完成，对局已恢复。";
            AddCaseLog("主机迁移完成，新主机接管对局。");

            // 确保 UI 刷新
            if (syncManager != null)
            {
                syncManager.enabled = true;
            }
        }

        /// <summary>
        /// 强制终止游戏（主机迁移失败 / 存活玩家不足）。
        /// </summary>
        public void ForceGameOver(string resultText)
        {
            SetResult(resultText);
        }

        private static List<GameStateSnapshot.SnapshotCooldownEntry> CooldownsToList(IReadOnlyDictionary<ulong, float> dict)
        {
            var list = new List<GameStateSnapshot.SnapshotCooldownEntry>(dict.Count);
            foreach (var kv in dict)
            {
                list.Add(new GameStateSnapshot.SnapshotCooldownEntry { ClientId = kv.Key, Value = kv.Value });
            }
            return list;
        }

        private static void ListToCooldowns(Dictionary<ulong, float> dict, List<GameStateSnapshot.SnapshotCooldownEntry> list)
        {
            dict.Clear();
            foreach (var entry in list)
            {
                dict[entry.ClientId] = entry.Value;
            }
        }




        // ══════════════════════════════════════════════════════
        // D5 地图选择联机同步
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 房主设置地图类型并广播给所有客户端。
        /// 仅在 Lobby 阶段有效（地图在 StartMatch 时才建造）。
        /// </summary>
        public void SetActiveMapType(OnlineMapService.OnlineMapType type)
        {
            if (!IsHost || phase != OnlineMatchPhase.Lobby) return;

            mapService.ActiveMapType = type;

            // 广播给所有客户端
            if (networkManager != null && networkManager.IsServer)
            {
                var writer = new FastBufferWriter(16, Unity.Collections.Allocator.Temp);
                try
                {
                    writer.WriteValueSafe((int)type);
                    networkManager.CustomMessagingManager.SendNamedMessage(
                        MapSelectMessage, NetworkManager.ServerClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            Debug.Log($"[D5] Host selected map: {type}");
        }



        /// <summary>获取玩家显示名称（按 clientId 查字典）</summary>
        public string GetPlayerDisplayName(ulong clientId)
        {
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
                return state.DisplayName;
            return "玩家" + clientId;
        }


        internal void TryInteractWithTask(ulong senderClientId, OnlinePlayerState player)
        {
            OnlineTaskState nearestTask = FindNearestTask(player.Position);

            if (nearestTask.Id < 0)
            {
                status = "附近没有可互动任务。";
                BroadcastSnapshot();
                return;
            }

            OnlineRole role = GetPrivateRole(senderClientId);

            if (role == OnlineRole.Gang || role == OnlineRole.Mole)
            {
                SabotageType sabotageType = SabotageForTask(nearestTask.Id);
                nearestTask.Sabotaged = true;
                nearestTask.Completed = false;
                nearestTask.Progress = Mathf.Max(0, nearestTask.Progress - 1);
                taskService.EvidenceScore = Mathf.Max(0, taskService.EvidenceScore - SabotageEvidencePenalty(sabotageType));
                string actorLabel = role == OnlineRole.Mole ? "线人" : "黑帮";
                status = actorLabel + "秘密破坏了 " + nearestTask.Name + "。";
                lastSabotageEvent = status + " 影响: " + SabotageName(sabotageType);
                AddCaseLog(status);
                syncManager?.OnTaskSabotagedLocally(senderClientId, nearestTask.Id, sabotageType);
                ApplySabotageEffect(sabotageType, nearestTask.Name);
                AudioManager.Instance?.PlaySFX(SoundEffect.Sabotage);

                // M5.5: 证据板记录 + 嫌疑增加
                LogEvidence(EvidenceCategory.EvidenceChain,
                    $"{nearestTask.Name} 被破坏（{SabotageName(sabotageType)}），证据链-{SabotageEvidencePenalty(sabotageType)}", -1);

                // 增加附近存活玩家的嫌疑（模糊线索，不点名）
                foreach (var kv in players)
                {
                    if (!kv.Value.Alive || kv.Key == senderClientId) continue;
                    float dist = Vector2.Distance(
                        new Vector2(kv.Value.Position.x, kv.Value.Position.y),
                        new Vector2(nearestTask.Position.x, nearestTask.Position.y));
                    if (dist < 6f)
                        AddSuspicion(kv.Key, 1);
                }
            }
            else
            {
                if (senderClientId == LocalClientId() && !player.IsBot && !submittingActiveTask)
                {
                    // M5.1: 委托给小游戏桥
                    if (miniGameBridge != null && miniGameBridge.IsSpawned)
                    {
                        miniGameBridge.OpenMinigameOnClient(senderClientId, nearestTask.Id);
                    }
                    else
                    {
                        BeginActiveTask(nearestTask.Id); // 降级到旧 OnGUI 任务
                    }
                    return;
                }

                if (nearestTask.Sabotaged)
                {
                    // M5.3: 修复需要小游戏
                    if (senderClientId == LocalClientId() && !player.IsBot && miniGameBridge != null && miniGameBridge.IsSpawned)
                    {
                        miniGameBridge.OpenRepairMinigameOnClient(senderClientId, nearestTask.Id);
                    }
                    else
                    {
                        nearestTask.Sabotaged = false;
                        RepairSabotageEffect(SabotageForTask(nearestTask.Id));
                        status = nearestTask.Name + " 的破坏已修复，危机效果下降。";
                        lastSabotageEvent = status;
                        AddCaseLog(status);
                    }
                }
                else if (!nearestTask.Completed)
                {
                    int progressGain = role == OnlineRole.Undercover ? 2 : 1;
                    nearestTask.Progress = Mathf.Min(nearestTask.RequiredProgress, nearestTask.Progress + progressGain);

                    if (nearestTask.Progress >= nearestTask.RequiredProgress)
                    {
                        nearestTask.Completed = true;
                        taskService.EvidenceScore = Mathf.Min(taskService.EvidenceTarget, taskService.EvidenceScore + EvidenceGainFor(nearestTask.Id, player.Profession, role));
                        player.Suspicion = Mathf.Max(0, player.Suspicion - 1);
                        players[senderClientId] = player;
                        status = nearestTask.Name + " 完成，证据链推进。";
                        lastEvidenceEvent = status + " 当前 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget;
                        UpdateEvidenceMilestone();
                        AddCaseLog(status);
                        PlayCue("task");
                        syncManager?.OnTaskCompletedLocally(senderClientId, nearestTask.Id);
                    }
                    else
                    {
                        status = nearestTask.Name + " 进度 " + nearestTask.Progress + "/" + nearestTask.RequiredProgress + "。";
                    }
                }
            }

            SetTask(nearestTask);
            EvaluateWinConditions();
            BroadcastSnapshot();
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

        /// <summary>
        /// 按 taskId 创建并显示对应的小游戏（11 种轮转），把完成/取消回调接到既有的
        /// CompleteActiveTask（服务器提交）与取消逻辑上。任何异常都降级为经典任务面板。
        /// </summary>
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

        private void TryKill(ulong senderClientId, OnlinePlayerState player)
        {
            if (GetPrivateRole(senderClientId) != OnlineRole.Gang && GetPrivateRole(senderClientId) != OnlineRole.Mole)
            {
                status = "只有黑帮可以击倒目标。";
                BroadcastSnapshot();
                return;
            }

            if (killSystem.killCooldowns.TryGetValue(senderClientId, out float cooldown) && cooldown > 0f)
            {
                status = "击倒冷却中：" + Mathf.CeilToInt(cooldown) + "s。";
                BroadcastSnapshot();
                return;
            }

            if (!TryFindNearestVictim(player.Position, out ulong victimClientId, out OnlinePlayerState victim))
            {
                status = "附近没有可击倒目标。";
                BroadcastSnapshot();
                return;
            }

            victim.Alive = false;
            victim.Input = Vector2.zero;
            victim.KillCooldown = 0f;
            players[victimClientId] = victim;

            // 如果被击杀的是本地玩家，进入鬼魂模式
            if (victimClientId == LocalClientId())
            {
                ActivateGhostModeForLocalPlayer(victimClientId);
            }
            killSystem.bodies.Add(new OnlineBodyState(killSystem.nextBodyId++, victimClientId, victim.Position, false));

            // M3: Create 2D corpse ground marker at death position (primary kill path)
            if (worldBuilder != null && worldBuilder.Use2DBackend)
            {
                worldBuilder.CreateCorpseMarker(victim.Position);
            }
            killSystem.killCooldowns[senderClientId] = ruleSet.KillCooldownSeconds;
            player.Suspicion += 2;
            players[senderClientId] = player;
            killSystem.killCount++;
            status = "黑帮击倒了 " + victim.DisplayName + "。";
            AddCaseLog(status);
            evidenceDossier?.RegisterEvidence("击杀事件：" + victim.DisplayName + "被击倒", new Vector2(victim.Position.x, victim.Position.y), 5.0f); // C2

            // C4: 内鬼隐藏目标追踪
            if (GetPrivateRole(senderClientId) == OnlineRole.Mole)
            {
                if (!_moleObjectives.ContainsKey(senderClientId))
                    _moleObjectives[senderClientId] = new MoleObjective();
                var obj = _moleObjectives[senderClientId];
                obj.Kills++;
                _moleObjectives[senderClientId] = obj;
            }

            syncManager?.OnKilled(victimClientId, senderClientId);
            PlayCue("kill");
            AudioManager.Instance?.PlaySFX(SoundEffect.Kill);
            EvaluateWinConditions();
            BroadcastSnapshot();
        }

        internal void TryUseProfessionAbility(ulong senderClientId, OnlinePlayerState player)
        {
            if (abilityCooldowns.TryGetValue(senderClientId, out float cooldown) && cooldown > 0f)
            {
                status = "职业技能冷却中：" + Mathf.CeilToInt(cooldown) + "s。";
                BroadcastSnapshot();
                return;
            }

            OnlineRole role = GetPrivateRole(senderClientId);

            switch (player.Profession)
            {
                case OnlineProfession.Inspector:
                    RevealMostSuspiciousPlayer();
                    status = player.DisplayName + " 发起重点盘问，案情板标记最高嫌疑。";
                    break;
                case OnlineProfession.Forensics:
                    taskService.EvidenceScore = Mathf.Min(taskService.EvidenceTarget, taskService.EvidenceScore + 1);
                    status = player.DisplayName + " 快速鉴证，证据链 +1。";
                    lastEvidenceEvent = status + " 当前 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget;
                    UpdateEvidenceMilestone();
                    break;
                case OnlineProfession.Tech:
                    taskService.RepairSabotageEffect(SabotageType.Blackout);
                    RepairSabotagedTasks(1);
                    status = player.DisplayName + " 重启监控和电闸，解除一次破坏。";
                    break;
                case OnlineProfession.UndercoverAgent:
                    // C3: 证据≥75%时可以背叛黑帮（切换公开身份为警察）
                    if (taskService.EvidenceScore >= Mathf.CeilToInt(taskService.EvidenceTarget * 0.75f)
                        && player.PublicRole != OnlineRole.Police)
                    {
                        // 背叛：公开身份切警察，证据+3，黑帮全员被标记
                        player.PublicRole = OnlineRole.Police;
                        player.Suspicion -= 2;
                        taskService.EvidenceScore = Mathf.Min(taskService.EvidenceTarget, taskService.EvidenceScore + 3);
                        status = player.DisplayName + " 当众背叛黑帮！已转为警方证人，全员黑帮嫌疑+2。";
                        lastEvidenceEvent = "卧底背叛！" + status + " 证据 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget;
                        // 标记所有黑帮
                        foreach (var kv in players)
                        {
                            if (GetPrivateRole(kv.Key) == OnlineRole.Gang)
                            {
                                var s = kv.Value;
                                s.Suspicion += 2;
                                players[kv.Key] = s;
                            }
                        }
                        UpdateEvidenceMilestone();
                    }
                    else
                    {
                        taskService.EvidenceScore = Mathf.Min(taskService.EvidenceTarget, taskService.EvidenceScore + 2);
                        player.Suspicion += 2;
                        status = player.DisplayName + " 秘密上传线报，证据链 +2 但暴露风险上升。";
                        lastEvidenceEvent = status + " 当前 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget;
                    }
                    UpdateEvidenceMilestone();
                    break;
                case OnlineProfession.Enforcer:
                    if (role == OnlineRole.Gang || role == OnlineRole.Mole)
                    {
                        killSystem.killCooldowns[senderClientId] = Mathf.Max(0f, killSystem.killCooldowns.TryGetValue(senderClientId, out float killCooldown) ? killCooldown - 9f : 0f);
                        player.Suspicion += 1;
                        status = player.DisplayName + " 清理路线，击倒冷却缩短。";
                    }
                    else
                    {
                        status = player.DisplayName + " 封锁后巷，附近黑帮嫌疑上升。";
                        MarkNearbyGangSuspicion(player.Position, 1);
                    }

                    break;
                case OnlineProfession.Fixer:
                    RepairSabotagedTasks(2);
                    taskService.EvidenceScore = Mathf.Max(0, taskService.EvidenceScore - 1);
                    status = player.DisplayName + " 篡改现场，修复表象但证据链被污染。";
                    break;
                case OnlineProfession.Driver:
                    if ((role == OnlineRole.Gang || role == OnlineRole.Mole) && TryUseUnderworldPassage(ref player))
                    {
                        player.Suspicion += 1;
                        status = player.DisplayName + " 通过暗线通道换位。";
                    }
                    else
                    {
                        player.Position = FindNearestOpenPosition(player.Position + new Vector3(UnityEngine.Random.Range(-2.4f, 2.4f), UnityEngine.Random.Range(-1.8f, 1.8f), 0f), player.Position);
                        player.Suspicion += (role == OnlineRole.Gang || role == OnlineRole.Mole) ? 1 : 0;
                        status = player.DisplayName + " 走后巷快速换位。";
                    }

                    break;
                case OnlineProfession.Mole:
                    if (player.Suspicion >= 60)
                    {
                        // Betray: Mole publicly switches to Gang faction
                        player.PublicRole = OnlineRole.Gang;
                        // All non-Gang players get suspicion +2
                        foreach (var kv in players)
                        {
                            OnlineRole kvRole = GetPrivateRole(kv.Key);
                            if (kvRole != OnlineRole.Gang && kvRole != OnlineRole.Mole && kv.Value.Alive)
                            {
                                var s = kv.Value;
                                s.Suspicion += 2;
                                players[kv.Key] = s;
                            }
                        }
                        // Mole gets a one-time kill cooldown reset
                        killSystem.killCooldowns[senderClientId] = 0f;
                        player.KillCooldown = 0f;
                        // Cooldown override: use standard 13s for betrayal
                        abilityCooldowns[senderClientId] = ruleSet.AbilityCooldownSeconds;
                        player.AbilityCooldown = ruleSet.AbilityCooldownSeconds;
                        status = "内鬼暴露! " + player.DisplayName + "背叛了警方!";
                        AddCaseLog(status);
                        // Global chat message
                        chatSystem?.ReceiveMessage("system", "系统", "内鬼暴露! " + player.DisplayName + "背叛了警方!", false, Faction.Police, ChatChannel.Meeting);
                    }
                    else
                    {
                        // Sabotage Intel: secretly reduce EvidenceScore by 2
                        taskService.EvidenceScore = Mathf.Max(0, taskService.EvidenceScore - 2);
                        // All Gang players including self get suspicion -1 (covering tracks)
                        player.Suspicion = Mathf.Max(0, player.Suspicion - 1);
                        foreach (var kv in players)
                        {
                            OnlineRole kvRole = GetPrivateRole(kv.Key);
                            if ((kvRole == OnlineRole.Gang || kvRole == OnlineRole.Mole) && kv.Value.Alive && kv.Key != senderClientId)
                            {
                                var s = kv.Value;
                                s.Suspicion = Mathf.Max(0, s.Suspicion - 1);
                                players[kv.Key] = s;
                            }
                        }
                        // Override cooldown to 20s for Sabotage Intel
                        abilityCooldowns[senderClientId] = 20f;
                        player.AbilityCooldown = 20f;
                        status = player.DisplayName + " 暗中破坏情报，证据链 -2。";
                        lastEvidenceEvent = status + " 当前 " + taskService.EvidenceScore + "/" + taskService.EvidenceTarget;
                        UpdateEvidenceMilestone();
                    }

                    break;
                default:
                    if ((role == OnlineRole.Gang || role == OnlineRole.Mole) && TryUseUnderworldPassage(ref player))
                    {
                        player.Suspicion += 1;
                        status = player.DisplayName + " 通过暗线通道换位。";
                    }
                    else
                    {
                        status = player.DisplayName + " 进行现场支援。";
                    }

                    break;
            }

            // Mole case handles its own cooldown (20s for Sabotage Intel, standard for Betray)
            if (player.Profession != OnlineProfession.Mole)
            {
                abilityCooldowns[senderClientId] = ruleSet.AbilityCooldownSeconds;
                player.AbilityCooldown = ruleSet.AbilityCooldownSeconds;
            }
            players[senderClientId] = player;
            // Mole Betray already logged inside the case; avoid double-logging
            if (player.Profession != OnlineProfession.Mole || player.Suspicion < 60)
            {
                AddCaseLog(status);
            }
            PlayCue("ability");
            EvaluateWinConditions();
            BroadcastSnapshot();
        }

        // ──────────────────────────────────────────────
        //  暗线通道系统（Underworld Passage）
        //  仅 Gang/Mole 可用，冷却由 ruleSet.VentCooldownSeconds 控制；
        //  节点位置来自 OnlineMapService；逻辑见 TryUseUnderworldPassage()。
        // ──────────────────────────────────────────────

        /// <summary>
        /// M4.3: 公开紧急会议调用入口（供 EmergencyButton / HUD 使用）。
        /// 立即扣除次数、设置冷却，进入会议阶段。
        /// </summary>
        public void CallEmergencyMeeting(string callerDisplayName)
        {
            if (emergencyMeetingsLeft <= 0 || emergencyCooldownTimer > 0f) return;
            emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
            emergencyCooldownTimer = ruleSet.EmergencyCooldownSeconds;
            BeginMeeting(callerDisplayName + " 按下警署紧急铃");
            BroadcastSnapshot();
        }

        private void TryReportOrEmergency(ulong senderClientId, OnlinePlayerState player)
        {
            if (killSystem.reportCooldownTimer > 0f)
            {
                status = "报案冷却中：" + Mathf.CeilToInt(killSystem.reportCooldownTimer) + "s，请稍后再试。";
                BroadcastSnapshot();
                return;
            }
            if (TryFindNearestBody(player.Position, out int bodyIndex))
            {
                OnlineBodyState body = killSystem.bodies[bodyIndex];
                body.Reported = true;
                killSystem.bodies[bodyIndex] = body;
                AudioManager.Instance?.PlaySFX(SoundEffect.BodyReport);
                BeginMeeting(player.DisplayName + " 发现尸体并报案");

                // M5.5: 证据板记录
                LogEvidence(EvidenceCategory.Surveillance,
                    $"{player.DisplayName} 在 {body.Position} 附近发现尸体", (int)senderClientId);
                LogEvidence(EvidenceCategory.EvidenceChain, "尸体报案触发紧急会议", -1);

                BroadcastSnapshot();
                return;
            }

            if (taskService.CommunicationJamTimer > 0f)
            {
                status = "通讯干扰中，无法启动紧急会议，需修复无线电监听。";
                BroadcastSnapshot();
                return;
            }

            if (emergencyMeetingsLeft <= 0)
            {
                status = "紧急会议次数已用完，只能通过发现尸体报案。";
                BroadcastSnapshot();
                return;
            }

            if (emergencyCooldownTimer > 0f)
            {
                status = "紧急会议冷却中：" + Mathf.CeilToInt(emergencyCooldownTimer) + "s。";
                BroadcastSnapshot();
                return;
            }

            if (Vector3.Distance(player.Position, mapService.ScaleMapPosition(Vector3.zero)) <= ruleSet.ReportRange)
            {
                emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
                emergencyCooldownTimer = ruleSet.EmergencyCooldownSeconds;
                BeginMeeting(player.DisplayName + " 按下警署紧急铃");
                BroadcastSnapshot();
                return;
            }

            status = "附近没有尸体，也不在紧急铃范围内。";
            BroadcastSnapshot();
        }

        internal void BeginMeeting(string reason)
        {
            phase = OnlineMatchPhase.Meeting;
            phaseTimer = ruleSet.MeetingIntroSeconds;
            taskService.RepairSabotageEffect(SabotageType.Blackout);
            killSystem.reportCooldownTimer = ruleSet.ReportCooldownSeconds;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskStepOneDone = false;
            activeTaskStepTwoDone = false;
            activeTaskStepThreeDone = false;
            activeTaskMistakes = 0;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            votes.Clear();
            lastMeetingReason = reason;

            List<ulong> ids = new List<ulong>(players.Keys);

            foreach (ulong clientId in ids)
            {
                OnlinePlayerState state = players[clientId];
                state.Input = Vector2.zero;
                if (state.Alive)
                {
                    state.Position = MeetingSeatPositionFor(clientId);
                }

                players[clientId] = state;
            }

            status = reason + "。进入会议。";
            AddCaseLog(status);
            _meetingCount++;
            syncManager?.OnMeetingBegan(reason, phase);

            // 激活聊天系统
            if (chatSystem != null)
            {
                chatSystem.CurrentPhase = OnlineMatchPhase.Meeting;
                chatSystem.CanSend = IsLocalAlive();
                chatSystem.IsAlive = IsLocalAlive();
                chatSystem.LocalFaction = ChatSystem.RoleToFaction(LocalEffectiveRole());
            }

            PlayCue("meeting");
        }

        private Vector3 MeetingSeatPositionFor(ulong clientId)
        {
            List<ulong> seatedIds = new List<ulong>(players.Keys);
            seatedIds.Sort();
            int seatIndex = Mathf.Max(0, seatedIds.IndexOf(clientId));
            int seatCount = Mathf.Clamp(seatedIds.Count, ruleSet.MinimumPlayablePlayers, ruleSet.MaximumRoomPlayers);
            Vector3 worldSeat = mapService.MeetingSeatWorldPosition(seatIndex, seatCount);
            return FindNearestOpenPosition(worldSeat, mapService.ScaleMapPosition(Vector3.zero));
        }

        internal void ApplyVote(ulong voterClientId, ulong targetClientId)
        {
            if (phase != OnlineMatchPhase.Meeting && phase != OnlineMatchPhase.Voting)
            {
                return;
            }

            if (!players.TryGetValue(voterClientId, out OnlinePlayerState voter) || !voter.Alive)
            {
                return;
            }

            if (targetClientId != SkipVoteTarget)
            {
                if (!players.TryGetValue(targetClientId, out OnlinePlayerState target) || !target.Alive)
                {
                    return;
                }
            }

            phase = OnlineMatchPhase.Voting;
            phaseTimer = Mathf.Max(phaseTimer, 6f);
            votes[voterClientId] = targetClientId;
            // SecretVote: hide who the voter voted for
            bool hasSecretVote = ruleSet != null && ruleSet.HasAbility(voter.Profession, AbilityType.SecretVote);
            if (hasSecretVote)
            {
                status = voter.DisplayName + " 秘密投票";
            }
            else
            {
                status = voter.DisplayName + (targetClientId == SkipVoteTarget ? " 已投票跳过。" : " 已投票给 " + players[targetClientId].DisplayName + "。");
            }
            AddCaseLog(status);
            syncManager?.OnVoteCast(voterClientId, targetClientId);

            if (votes.Count >= CountAlivePlayers())
            {
                ResolveVotes();
            }
            else
            {
                BroadcastSnapshot();
            }
        }

        private void ResolveVotes()
        {
            if (phase != OnlineMatchPhase.Voting && phase != OnlineMatchPhase.Meeting)
            {
                return;
            }

            Dictionary<ulong, int> tally = new Dictionary<ulong, int>();

            foreach (ulong targetClientId in votes.Values)
            {
                if (targetClientId == SkipVoteTarget)
                {
                    continue;
                }

                tally[targetClientId] = tally.TryGetValue(targetClientId, out int count) ? count + 1 : 1;
            }

            ulong ejectedClientId = SkipVoteTarget;
            int bestVotes = 0;
            bool tied = false;

            foreach (KeyValuePair<ulong, int> pair in tally)
            {
                if (pair.Value > bestVotes)
                {
                    ejectedClientId = pair.Key;
                    bestVotes = pair.Value;
                    tied = false;
                }
                else if (pair.Value == bestVotes)
                {
                    tied = true;
                }
            }

            votes.Clear();
            phaseTimer = 0f;

            if (ejectedClientId == SkipVoteTarget || tied)
            {
                syncManager?.OnMeetingResolved(SkipVoteTarget, tied, tally);
                RemoveReportedBodies();
                phase = OnlineMatchPhase.Action;
                ApplyPostMeetingKillGrace();
                status = "投票无结果，无人出局。";
                lastVoteOutcome = status + " 票型: " + BuildVoteTallySummary(tally);
                AddCaseLog(status);
                BroadcastSnapshot();
                return;
            }

            if (players.TryGetValue(ejectedClientId, out OnlinePlayerState ejected))
            {
                OnlineRole ejectedRole = GetPrivateRole(ejectedClientId);
                ejected.Alive = false;
                ejected.Input = Vector2.zero;
                if (revealRoleOnEject)
                {
                    ejected.PublicRole = ejectedRole;
                }

                players[ejectedClientId] = ejected;
                status = revealRoleOnEject
                    ? ejected.DisplayName + " 被投出局，身份是：" + RoleName(ejected.PublicRole) + "。"
                    : ejected.DisplayName + " 被投出局，身份暂不公开。";
                lastVoteOutcome = ejected.DisplayName + " 出局 | 得票 " + bestVotes + " | 身份 " + (revealRoleOnEject ? RoleName(ejectedRole) : "未公开");
                AddCaseLog(status);
                PlayCue("vote");

                // 如果被投票淘汰的是本地玩家，进入鬼魂模式
                if (ejectedClientId == LocalClientId())
                {
                    ActivateGhostModeForLocalPlayer(ejectedClientId);
                }

                // 记录会议淘汰到 VictoryBridge + MeetingSync
                syncManager?.RegisterElimination(ejectedClientId, GetPrivateRole);
                syncManager?.OnMeetingResolved(ejectedClientId, false, tally);
            }

            RemoveReportedBodies();
            EvaluateWinConditions();

            if (phase != OnlineMatchPhase.Result)
            {
                phase = OnlineMatchPhase.Action;
                ApplyPostMeetingKillGrace();
                syncManager?.OnMeetingEnded();
            }

            BroadcastSnapshot();
        }

        internal void EvaluateWinConditions()
        {
            if (!matchStarted || phase == OnlineMatchPhase.Result)
            {
                return;
            }

            UpdateEvidenceMilestone();

            // 优先使用 OnlineVictoryBridge 双重判定（原生在线规则 + 离线 VictoryEvaluator）
            if (syncManager != null)
            {
                EvaluateResult bridgeResult = syncManager.EvaluateVictory(
                    taskService.EvidenceScore, taskService.EvidenceTarget, players,
                    GetPrivateRole, tasks, matchStarted, phase, localRole);

                if (bridgeResult.HasResult)
                {
                    SetResult(bridgeResult.ResultText);
                    return;
                }
            }

            // 兜底：在线原生快速判定（证据链 / 存活阵营）
            if (taskService.EvidenceScore >= taskService.EvidenceTarget)
            {
                SetResult("警方胜利：证据链闭合。");
                return;
            }

            int aliveGang = 0;
            int aliveNonGang = 0;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (!pair.Value.Alive)
                {
                    continue;
                }

                if (GetPrivateRole(pair.Key) == OnlineRole.Gang)
                {
                    aliveGang++;
                }
                else
                {
                    aliveNonGang++;
                }
            }

            if (aliveGang == 0 && players.Count >= 2)
            {
                SetResult("警方胜利：黑帮全部出局。");
            }
            else if (aliveGang > 0 && (aliveNonGang == 0 || (players.Count >= 4 && aliveGang >= aliveNonGang)))
            {
                SetResult("黑帮胜利：港区控制权失守。");
            }
        }

        /// <summary>
        /// 为本地玩家激活鬼魂模式。淘汰后调用：
        /// - 设置半透明渲染
        /// - 碰撞器设为 Trigger 可穿越墙壁
        /// - 可继续做任务但无法报告尸体/发起会议
        /// </summary>
        private void ActivateGhostModeForLocalPlayer(ulong clientId)
        {
            // 查找本地玩家的 SocialCharacter GameObject
            SocialCharacter[] allChars = FindObjectsByType<SocialCharacter>();
            SocialCharacter localChar = null;

            foreach (SocialCharacter sc in allChars)
            {
                // 通过名称或 OnlineClientId 匹配本地玩家
                if (sc != null && sc.IsPlayer)
                {
                    localChar = sc;
                    break;
                }
            }

            if (localChar == null)
            {
                Debug.LogWarning($"[OnlineMatchController] 无法找到 clientId={clientId} 对应的 SocialCharacter，GhostMode 未激活。");
                return;
            }

            GhostMode ghost = localChar.GetComponent<GhostMode>();
            if (ghost == null) ghost = localChar.gameObject.AddComponent<GhostMode>();
            ghost.EnterGhostMode();
            ghost.CanDoTasks = true;
            ghost.CanReportBody = false;
            ghost.GhostCanCallMeeting = false;

            AddCaseLog($"{localChar.CharacterName} 被淘汰，进入鬼魂模式，可继续帮助队友完成任务。");
            Debug.Log($"[OnlineMatchController] 本地玩家 {localChar.CharacterName} 进入鬼魂模式。");
        }

        private void ResolveTimeLimitOutcome()
        {
            if (!matchStarted || phase == OnlineMatchPhase.Result)
            {
                return;
            }

            // 优先让 VictoryBridge 做超时判定
            if (syncManager != null && syncManager.TryTimeLimitEvaluation(
                matchElapsedSeconds, ruleSet.MatchHardLimitSeconds, taskService.EvidenceScore, taskService.EvidenceTarget, tasks, out string bridgeResult))
            {
                SetResult(bridgeResult);
                return;
            }

            if (taskService.EvidenceScore >= Mathf.CeilToInt(taskService.EvidenceTarget * 0.82f) || CountCompletedTasks() >= Mathf.CeilToInt(tasks.Count * 0.72f))
            {
                SetResult("警方胜利：行动超时前已掌握关键证据。");
            }
            else
            {
                SetResult("黑帮胜利：20 分钟窗口结束，关键证据未能闭合。");
            }
        }

        private void SetResult(string resultStatus)
        {
            phase = OnlineMatchPhase.Result;
            phaseTimer = 0f;
            taskService.ResetAllSabotageTimers();
            emergencyCooldownTimer = 0f;
            aiActionGraceTimer = 0f;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            submittingActiveTask = false;
            status = resultStatus;

            // C4: 评估内鬼隐藏目标 — 存活至≤3人
            int aliveTotal = CountAlivePlayers();
            foreach (var kv in players)
            {
                if (GetPrivateRole(kv.Key) == OnlineRole.Mole && kv.Value.Alive && aliveTotal <= 3)
                {
                    if (_moleObjectives.TryGetValue(kv.Key, out var obj))
                    {
                        obj.SurvivedTilLate = true;
                        _moleObjectives[kv.Key] = obj;
                    }
                }
            }

            resultSummary = BuildResultSummary(resultStatus);
            AddCaseLog(status);
            PlayCue("result");

            // M8.4: 对局结束，采集并持久化日志
            if (_statsCollector != null)
            {
                _statsCollector.LogMatch(this);
            }

            List<ulong> ids = new List<ulong>(players.Keys);

            foreach (ulong clientId in ids)
            {
                OnlinePlayerState state = players[clientId];
                state.PublicRole = GetPrivateRole(clientId);
                state.Input = Vector2.zero;
                players[clientId] = state;
            }
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

        private void TickCooldowns(float deltaTime)
        {
            // 击杀冷却已委托给 KillSystem 管理
            if (killSystem != null)
                killSystem.TickKillCooldowns(deltaTime);

            List<ulong> keys = new List<ulong>(abilityCooldowns.Keys);

            foreach (ulong clientId in keys)
            {
                abilityCooldowns[clientId] = Mathf.Max(0f, abilityCooldowns[clientId] - deltaTime);

                if (players.TryGetValue(clientId, out OnlinePlayerState state))
                {
                    state.AbilityCooldown = abilityCooldowns[clientId];
                    players[clientId] = state;
                }
            }

            // 暗线通道冷却（统一系统，原 TickVentCooldowns）
            keys = new List<ulong>(ventCooldowns.Keys);
            foreach (ulong id in keys)
            {
                float remaining = ventCooldowns[id] - deltaTime;
                if (remaining <= 0f)
                {
                    ventCooldowns.Remove(id);
                    if (players.TryGetValue(id, out OnlinePlayerState vState))
                    {
                        vState.VentCooldown = 0f;
                        players[id] = vState;
                    }
                }
                else
                {
                    ventCooldowns[id] = remaining;
                    if (players.TryGetValue(id, out OnlinePlayerState vState))
                    {
                        vState.VentCooldown = remaining;
                        players[id] = vState;
                    }
                }
            }
        }

        private void TickBotAction(float deltaTime)
        {
            _botController.TickBotAction(deltaTime);
        }

        private void TickBotVoting(float deltaTime)
        {
            _botController.TickBotVoting(deltaTime);
        }

        private void RemoveReportedBodies()
        {
            if (killSystem != null)
                killSystem.RemoveReportedBodies();
        }

        /// <summary>
        /// M4.2: 会议结束后给所有黑帮阵营玩家施加击杀冷却宽容期，
        /// 防止"刚开会出来就秒杀"破坏节奏。
        /// </summary>
        private void ApplyPostMeetingKillGrace()
        {
            if (killSystem != null)
                killSystem.ApplyPostMeetingKillGrace(ruleSet.PostMeetingKillGraceSeconds);
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

        // ── M5.5 会议证据板与嫌疑系统 ──

        /// <summary>
        /// 证据板条目类型：会议展示三类线索。
        /// </summary>
        public enum EvidenceCategory
        {
            TaskTrail,      // 任务轨迹：谁完成了什么任务
            Surveillance,   // 监控目击：摄像头看到什么
            EvidenceChain   // 证据链片段：证据进度变化
        }

        [System.Serializable]
        public struct EvidenceBoardEntry
        {
            public EvidenceCategory Category;
            public string Text;
            public float Timestamp;
            public int RelatedClientId; // -1 表示不关联特定玩家
        }

        private List<EvidenceBoardEntry> _evidenceBoard = new List<EvidenceBoardEntry>();

        /// <summary>
        /// 记录一条证据板条目。
        /// </summary>
        public void LogEvidence(EvidenceCategory category, string text, int relatedClientId = -1)
        {
            _evidenceBoard.Add(new EvidenceBoardEntry
            {
                Category = category,
                Text = text,
                Timestamp = Time.time,
                RelatedClientId = relatedClientId
            });

            // 限制最大条目数
            while (_evidenceBoard.Count > 48)
                _evidenceBoard.RemoveAt(0);
        }

        /// <summary>
        /// 获取会议证据板数据，按三类分组。
        /// </summary>
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

        /// <summary>
        /// 清理证据板（新一局开始时调用）。
        /// </summary>
        public void ClearEvidenceBoard()
        {
            _evidenceBoard.Clear();
        }

        /// <summary>
        /// 增加指定玩家的嫌疑值。
        /// </summary>
        public void AddSuspicion(ulong clientId, int amount)
        {
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                state.Suspicion = Mathf.Clamp(state.Suspicion + amount, 0, 100);
                players[clientId] = state;
            }
        }

        /// <summary>
        /// 获取按嫌疑值降序排列的玩家 ID 列表（供会议使用）。
        /// </summary>
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

        private void RemoveMissingPlayers(HashSet<ulong> seenPlayers)
        {
            List<ulong> stalePlayers = new List<ulong>();

            foreach (ulong clientId in players.Keys)
            {
                if (!seenPlayers.Contains(clientId))
                {
                    stalePlayers.Add(clientId);
                }
            }

            foreach (ulong clientId in stalePlayers)
            {
                players.Remove(clientId);
                _botController?.RemoveBot(clientId);
                abilityCooldowns.Remove(clientId);
            }
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

        /// <summary>
        /// 统一暗线通道逻辑（合并原 OnlineVents / TryVent 系统）。
        /// 权限：仅 Gang/Mole 可用；冷却由 ruleSet.VentCooldownSeconds 控制；
        /// 节点位置来自 OnlineMapService；目标为对侧节点 (i+2)%count。
        /// </summary>

        /// <summary>
        /// 重载：供职业技能（Driver 等）调用，只传入 ref player，使用默认 senderClientId 逻辑。
        /// </summary>

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

        // ============================================================
        //  Phase 4: 破坏深化 — 房间封锁系统
        // ============================================================

        /// <summary>随机封锁 N 个房间的入口。</summary>
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

        /// <summary>检查房间是否被封锁。</summary>
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

        internal bool TryFindNearestBody(Vector3 position, out int bodyIndex)
        {
            if (killSystem != null)
                return killSystem.TryFindNearestBody(position, out bodyIndex);
            bodyIndex = -1;
            return false;
        }

        private bool IsLocalAlive()
        {
            return !players.TryGetValue(LocalClientId(), out OnlinePlayerState state) || state.Alive;
        }

        private bool TryGetLocalPlayer(out OnlinePlayerState state)
        {
            return players.TryGetValue(LocalClientId(), out state);
        }

        private ulong LocalClientId()
        {
            return localPreviewMode || networkManager == null ? LocalPreviewClientId : networkManager.LocalClientId;
        }

        /// <summary>M7.2: 返回建房间时的 Host ClientId（用于迁移时排除旧主机）</summary>
        public ulong OldHostClientId()
        {
            return relayHostClientId;
        }

        private ulong relayHostClientId;

        // ── M5.1 小游戏联机协议 — 服务器校验方法 ──

        /// <summary>
        /// 服务器校验：玩家是否有权开始指定任务的小游戏。
        /// </summary>
        public bool ValidateTaskStart(ulong clientId, int taskId, out string reason)
        {
            reason = string.Empty;

            if (!players.TryGetValue(clientId, out OnlinePlayerState player))
            {
                reason = "玩家不存在";
                return false;
            }

            if (!player.Alive)
            {
                reason = "玩家已死亡";
                return false;
            }

            OnlineTaskState task = GetTask(taskId);
            if (task.Id < 0)
            {
                reason = "任务不存在";
                return false;
            }

            if (task.Completed)
            {
                reason = "任务已完成";
                return false;
            }

            // 距离校验
            float dist = Vector2.Distance(
                new Vector2(player.Position.x, player.Position.y),
                new Vector2(task.Position.x, task.Position.y));
            if (dist > 2.5f)
            {
                reason = "距离任务点太远";
                return false;
            }

            return true;
        }

        /// <summary>
        /// M5.3: 服务器校验：玩家是否有权开始修复破坏。
        /// </summary>
        public bool ValidateRepairStart(ulong clientId, int taskId, out string reason)
        {
            if (!ValidateTaskStart(clientId, taskId, out string baseReason))
            {
                // 修复允许 sabotaged 任务
                OnlineTaskState task = GetTask(taskId);
                if (!task.Sabotaged)
                {
                    reason = baseReason;
                    return false;
                }
            }

            OnlineTaskState t = GetTask(taskId);
            if (!t.Sabotaged)
            {
                reason = "任务未被破坏";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// 标记任务为「进行中」，防止多人同时操作同一任务。
        /// </summary>
        public void MarkTaskActive(ulong clientId, int taskId)
        {
            if (activeTaskUsers == null) activeTaskUsers = new Dictionary<int, ulong>();
            activeTaskUsers[taskId] = clientId;
        }

        /// <summary>
        /// 释放任务锁定。
        /// </summary>
        public void ReleaseTask(ulong clientId, int taskId)
        {
            if (activeTaskUsers != null && activeTaskUsers.TryGetValue(taskId, out ulong owner) && owner == clientId)
            {
                activeTaskUsers.Remove(taskId);
            }
        }

        /// <summary>
        /// 服务器二次校验并记录任务完成。
        /// </summary>
        public bool ValidateAndCompleteTask(ulong clientId, int taskId, out string error)
        {
            error = string.Empty;

            if (!players.TryGetValue(clientId, out OnlinePlayerState player))
            {
                error = "玩家不存在";
                return false;
            }

            if (!player.Alive)
            {
                error = "玩家已死亡";
                return false;
            }

            OnlineTaskState task = GetTask(taskId);
            if (task.Id < 0)
            {
                error = "任务不存在";
                return false;
            }

            if (task.Completed)
            {
                error = "任务已完成";
                return false;
            }

            if (task.Sabotaged)
            {
                error = "任务已被破坏";
                return false;
            }

            // 记录完成
            SetTask(new OnlineTaskState(task.Id, task.Name, task.Position,
                task.RequiredProgress, task.RequiredProgress, true, task.Sabotaged));

            // 证据收益
            int gain = 0;
            status = $"{player.DisplayName} 完成了任务：{task.Name}。";
            if (!localPreviewMode)
            {
                OnlineRole role = privateRoles.TryGetValue(clientId, out OnlineRole r) ? r : OnlineRole.Police;
                OnlineProfession prof = player.Profession;
                gain = EvidenceGainFor(taskId, prof, role);
                taskService.AddEvidence(gain, status);
                lastEvidenceEvent = taskService.LastEvidenceEvent;
            }

            ReleaseTask(clientId, taskId);
            syncManager?.OnTaskCompletedLocally(clientId, taskId);
            EvaluateWinConditions();
            BroadcastSnapshot();

            AddCaseLog(status);

            // M5.5: 证据板记录
            LogEvidence(EvidenceCategory.TaskTrail, $"{player.DisplayName} 完成 {task.Name}", (int)clientId);
            LogEvidence(EvidenceCategory.EvidenceChain, $"证据链 +{gain}，当前 {EvidenceScore}/{EvidenceTarget}", -1);
            AddSuspicion(clientId, -2); // 完成任务降低嫌疑

            return true;
        }

        private Dictionary<int, ulong> activeTaskUsers;

        /// <summary>
        /// M5.3: 服务器校验并完成破坏修复。
        /// </summary>
        public bool ValidateAndRepairTask(ulong clientId, int taskId, out string error)
        {
            error = string.Empty;

            if (!players.TryGetValue(clientId, out OnlinePlayerState player))
            {
                error = "玩家不存在";
                return false;
            }

            if (!player.Alive)
            {
                error = "玩家已死亡";
                return false;
            }

            OnlineTaskState task = GetTask(taskId);
            if (task.Id < 0)
            {
                error = "任务不存在";
                return false;
            }

            if (!task.Sabotaged)
            {
                error = "任务未被破坏";
                return false;
            }

            // 修复
            SabotageType sabotageType = SabotageForTask(taskId);
            SetTask(new OnlineTaskState(task.Id, task.Name, task.Position,
                task.Progress, task.RequiredProgress, false, false));
            RepairSabotageEffect(sabotageType);
            ReleaseTask(clientId, taskId);

            status = player.DisplayName + " 修复了 " + task.Name + " 的破坏。";
            lastSabotageEvent = status;
            AddCaseLog(status);
            BroadcastSnapshot();

            return true;
        }

        /// <summary>
        /// M5.4 公开方法：判断玩家是否属于黑帮阵营（Gang/Undercover/Mole）。
        /// 用于监控系统红灯判定，不下发私密身份。
        /// </summary>
        public bool IsGangFaction(ulong clientId)
        {
            if (privateRoles.TryGetValue(clientId, out OnlineRole role))
            {
                return role == OnlineRole.Gang || role == OnlineRole.Undercover || role == OnlineRole.Mole;
            }
            return false;
        }

        private int CountAlivePlayers()
        {
            int count = 0;

            foreach (OnlinePlayerState state in players.Values)
            {
                if (state.Alive)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountAliveRole(OnlineRole role)
        {
            int count = 0;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (pair.Value.Alive && GetPrivateRole(pair.Key) == role)
                {
                    count++;
                }
            }

            return count;
        }

        internal OnlineRole GetPrivateRole(ulong clientId)
        {
            return privateRoles.TryGetValue(clientId, out OnlineRole role) ? role : OnlineRole.Police;
        }

        private OnlineRole LocalEffectiveRole()
        {
            ulong localClientId = LocalClientId();

            if (privateRoles.TryGetValue(localClientId, out OnlineRole role))
            {
                return role;
            }

            return localRole == OnlineRole.Unassigned ? OnlineRole.Police : localRole;
        }

        private int CountHumanPlayers()
        {
            int count = 0;

            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.IsBot)
                {
                    count++;
                }
            }

            return count;
        }

        private int ReadyPlayerCount()
        {
            int count = 0;

            foreach (OnlinePlayerState state in players.Values)
            {
                if (state.Ready)
                {
                    count++;
                }
            }

            return count;
        }

        private bool CanStartLobbyMatch()
        {
            if (phase != OnlineMatchPhase.Lobby)
            {
                return false;
            }

            if (roomAutoFillAi)
            {
                return players.Count >= roomMinPlayers;
            }

            return CountHumanPlayers() >= roomMinPlayers;
        }

        private int EvidenceGainFor(int taskId, OnlineProfession profession, OnlineRole role)
        {
            int gain = TaskEvidenceValue(taskId);

            if (profession == OnlineProfession.Forensics)
            {
                gain++;
            }

            // C1: Tech EvidenceChainBonus 1.3x
            if (ruleSet != null && ruleSet.HasAbility(profession, AbilityType.EvidenceChainBonus))
                gain = Mathf.RoundToInt(gain * ruleSet.GetAbilityMultiplier(profession, AbilityType.EvidenceChainBonus));

            if (role == OnlineRole.Undercover || profession == OnlineProfession.UndercoverAgent)
            {
                gain++;
            }

            return Mathf.Clamp(gain, 1, 5);
        }

        private static int TaskEvidenceValue(int taskId)
        {
            switch (taskId)
            {
                case 0:
                case 3:
                case 11:
                case 15:
                case 16:
                case 21:
                case 22:
                case 26:
                    return 2;
                case 4:
                case 8:
                case 18:
                case 24:
                case 27:
                    return 3;
                default:
                    return 1;
            }
        }


        private static string FormatMatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
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

        private int EmergencyMeetingLimitFor(int playerCount)
        {
            return ruleSet.EmergencyMeetingLimitFor(playerCount);
        }






        private int CountUnreportedBodies()
        {
            if (killSystem != null)
                return killSystem.CountUnreportedBodies();
            return 0;
        }

        private int CountCompletedTasks()
        {
            int completed = 0;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Completed)
                {
                    completed++;
                }
            }

            return completed;
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















        private static int TaskTemplateMode(int taskId)
        {
            switch (taskId)
            {
                case 0:
                case 6:
                case 13:
                case 21:
                    return 0;
                case 1:
                case 10:
                case 20:
                case 23:
                    return 1;
                case 2:
                case 7:
                case 12:
                case 14:
                case 24:
                    return 2;
                case 3:
                case 9:
                case 15:
                case 19:
                    return 3;
                case 4:
                case 11:
                case 16:
                case 22:
                    return 4;
                default:
                    return 5;
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

        private static Color TaskPanelAccent(int taskId)
        {
            return OnlineWorldBuilder.TaskPanelAccent(taskId);
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



        private static string TaskPanelInstruction(int taskId)
        {
            switch (taskId)
            {
                case 0:
                    return "切换摄像头、锁定可疑动线、导出录像。";
                case 1:
                case 23:
                    return "核对封条号、扫描货柜、同步查验记录。";
                case 2:
                case 14:
                case 24:
                    return "对齐断路器、按住充电、恢复港区供电。";
                case 3:
                case 15:
                    return "放置样本、校准光谱、生成鉴证报告。";
                case 4:
                case 16:
                case 22:
                    return "翻账本、标记异常、冻结可疑现金流。";
                case 5:
                case 27:
                    return "递送情报、控制暴露、稳住接头安全。";
                case 6:
                case 13:
                case 21:
                    return "调频、过滤噪声、恢复无线电通道。";
                case 7:
                case 12:
                    return "刷卡、解除门禁、记录出入日志。";
                case 8:
                case 18:
                case 26:
                    return "巡线、补充目击、锁定撤离路线。";
                case 9:
                case 19:
                    return "搜查诊所、对照病历、追痕提证。";
                case 10:
                    return "顺线走访货场，补强路线证据。";
                case 11:
                    return "核对财务流向，锁定异常资金。";
                case 17:
                    return "执行巡逻打卡，压制高风险街口。";
                case 20:
                    return "读懂鱼档暗号，辨识黑市交易。";
                case 25:
                    return "排查后巷摩托，封死逃逸支线。";
                default:
                    return "完成现场校验并提交证据链。";
            }
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






        // ══════════════════════════════════════════════════════
        // M6.1 监控摄像头生成 & 灰盒地图建造器
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 在地图中实例化监控摄像头 NetworkBehaviour。
        /// 从 OnlineMapService.SurveillanceZones() 获取布点数据 → 生成 OnlineSecurityCamera。
        /// </summary>



        /// <summary>
        /// C1: Periodic check for Mole auto-exposure.
        /// If a Mole player's Suspicion >= 90, automatically expose them:
        /// set PublicRole to Gang and broadcast a chat notification.
        /// </summary>
        private void TickMoleExposureCheck()
        {
            if (phase != OnlineMatchPhase.Action || !matchStarted)
            {
                return;
            }

            List<ulong> ids = new List<ulong>(players.Keys);
            foreach (ulong clientId in ids)
            {
                OnlinePlayerState state = players[clientId];
                if (state.Profession != OnlineProfession.Mole || !state.Alive)
                {
                    continue;
                }

                // Only trigger if Mole hasn't already been exposed (PublicRole still Police)
                if (state.Suspicion >= 90 && state.PublicRole == OnlineRole.Police)
                {
                    state.PublicRole = OnlineRole.Gang;
                    players[clientId] = state;
                    string msg = "内鬼" + state.DisplayName + "被警方锁定，身份暴露!";
                    status = msg;
                    AddCaseLog(status);
                    chatSystem?.ReceiveMessage("system", "系统", msg, false, Faction.Police, ChatChannel.Meeting);
                    PlayCue("meeting");
                    BroadcastSnapshot();
                }
            }
        }

        private void TickSurveillanceCameras()
        {
            if (surveillanceCameras.Count == 0)
            {
                return;
            }

            foreach (OnlineSecurityCamera camera in surveillanceCameras)
            {
                if (camera != null)
                {
                    camera.ServerTick(players);
                }
            }
        }

        /// <summary>
        /// 使用 GreyboxMapBuilder 叠加灰盒地图。
        /// 灰盒地图不替换现有视觉层，而是叠加一层简单的几何体用于玩法测试。
        ///
        /// M8.1: 支持根据 OnlineMapService.ActiveMapType 选择港区或警署地图。
        /// </summary>

        /// <summary>
        /// 使用 KenneySpriteDecorator 为所有房间铺设 2D 建筑 Sprite。
        /// 不替换灰盒——仅叠加视觉层。
        /// </summary>
















































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

        private TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize)
        {
            return worldBuilder.CreateWorldLabelAt(text, position, characterSize);
        }

        private TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize)
        {
            return worldBuilder.CreateWorldLabel(parent, text, localPosition, characterSize);
        }

        private static void BillboardLabel(Transform labelTransform)
        {
            OnlineWorldBuilder.BillboardLabel(labelTransform);
        }




        private static void SetTextMeshVisible(TextMesh label, bool visible)
        {
            OnlineWorldBuilder.SetTextMeshVisible(label, visible);
        }

        private static void RemoveStaleVisuals<T>(Dictionary<T, GameObject> visuals, HashSet<T> seen)
        {
            OnlineWorldBuilder.RemoveStaleVisuals(visuals, seen);
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

        private static int SortingOrderForZ(float z)
        {
            return OnlineWorldBuilder.SortingOrderForZ(z);
        }

        private static int SortingOrderForLocalZ(float z)
        {
            return OnlineWorldBuilder.SortingOrderForLocalZ(z);
        }

        private void EnsureAudio()
        {
            if (audioSource != null)
            {
                return;
            }

            audioSource = gameObject.GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.42f;
            audioClips["start"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Medieval City", "Gangland Start", 440f, 0.16f);
            audioClips["task"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Secret door", "Gangland Task", 660f, 0.12f);
            audioClips["ability"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Metal impact 5", "Gangland Ability", 520f, 0.14f);
            audioClips["blackout"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Thunder strikes 30 second- Loop", "Gangland Blackout", 160f, 0.22f);
            audioClips["kill"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Bloody punch", "Gangland Knockdown", 120f, 0.2f);
            audioClips["meeting"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Hand Gun 1", "Gangland Meeting", 320f, 0.18f);
            audioClips["vote"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Hand Gun 2", "Gangland Vote", 380f, 0.12f);
            audioClips["result"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Explosion 1", "Gangland Result", 740f, 0.2f);
        }

        private AudioClip LoadAudioClipOrFallback(string resourcePath, string clipName, float frequency, float duration)
        {
            AudioClip clip = Resources.Load<AudioClip>(NormalizeResourcePath(resourcePath));

            if (clip != null)
            {
                return clip;
            }

            return CreateToneClip(clipName, frequency, duration);
        }

        private void PlayCue(string cueName)
        {
            EnsureAudio();

            if (audioSource != null && audioClips.TryGetValue(cueName, out AudioClip clip) && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private static AudioClip CreateToneClip(string clipName, float frequency, float duration)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = 1f - i / (float)sampleCount;
                samples[i] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.28f * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void RestartMatch()
        {
            if (networkManager != null && networkManager.IsServer)
            {
                StartOnlineMatchCore(true);
                return;
            }

            if ((localPreviewMode || !IsOnline) && players.Count > 0)
            {
                StartOnlineMatchCore(false);
            }
        }

        private void ReturnToLobby()
        {
            if (networkManager != null && !networkManager.IsServer)
            {
                status = "等待 Host 返回房间。";
                return;
            }

            phase = OnlineMatchPhase.Lobby;
            matchStarted = false;
            fullMapPreview = true;
            localRole = OnlineRole.Unassigned;
            privateRoles.Clear();
            votes.Clear();
            killSystem.bodies.Clear();
            killSystem.killCooldowns.Clear();
            abilityCooldowns.Clear();
            _botController?.Clear();
            migrationManager?.ResetState();
            BuildDefaultTasks();
            taskService.EvidenceScore = 0;
            evidenceMilestoneIndex = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
            taskService.ResetAllSabotageTimers();
            emergencyCooldownTimer = 0f;
            aiActionGraceTimer = 0f;
            evidenceMilestoneIndex = 0;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            submittingActiveTask = false;
            emergencyMeetingsLeft = 0;
            phaseTimer = 0f;
            resultSummary = "尚未结算。";
            matchElapsedSeconds = 0f;

            List<ulong> ids = new List<ulong>(players.Keys);

            foreach (ulong clientId in ids)
            {
                OnlinePlayerState state = players[clientId];
                state.Alive = true;
                state.Ready = state.IsBot;
                state.PublicRole = OnlineRole.Unassigned;
                state.KillCooldown = 0f;
                state.AbilityCooldown = 0f;
                state.Suspicion = 0;
                state.Input = Vector2.zero;
                players[clientId] = state;
            }

            status = "已返回房间，可调整规则或重开。";
            AddCaseLog(status);
            BroadcastSnapshot();
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

        /// <summary>
        /// M8.2: 根据角色和序号分配职业。
        /// Mole 分配警察职业以维持掩护；Gang 分配 Enforcer/Fixer；Undercover 分配 UndercoverAgent/Driver。
        /// </summary>
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

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, items.Count);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }

    public struct OnlinePlayerState
    {
        public OnlinePlayerState(ulong clientId, string displayName, Vector3 position, bool ready, bool alive, OnlineRole publicRole, OnlineProfession profession, int suspicion, bool isBot = false)
        {
            ClientId = clientId;
            DisplayName = displayName;
            Position = position;
            Input = Vector2.zero;
            Ready = ready;
            Alive = alive;
            PublicRole = publicRole;
            Profession = profession;
            KillCooldown = 0f;
            AbilityCooldown = 0f;
            VentCooldown = 0f;
            Suspicion = suspicion;
            IsBot = isBot;
            IsGhost = false;
            CharacterAnimator = null;
            SocialChar = null;
            Character2DDirectionIndicator = null;
            HasPendingAction = false;
        }

        public ulong ClientId;
        public string DisplayName;
        public Vector3 Position;
        public Vector2 Input;
        public bool Ready;
        public bool Alive;
        public bool IsGhost;
        public bool IsBot;
        public OnlineRole PublicRole;
        public OnlineProfession Profession;
        public float KillCooldown;
        public float AbilityCooldown;
        public float VentCooldown;
        public int Suspicion;
        public Animator CharacterAnimator;
        public SocialDeduction.SocialCharacter SocialChar;
        /// <summary>M3: 2D direction indicator GameObject — rotated to match movement direction in orthographic top-down view.</summary>
        public GameObject Character2DDirectionIndicator;
        public bool HasPendingAction;
    }

    public struct OnlineTaskState
    {
        public OnlineTaskState(int id, string name, Vector3 position, int progress, int requiredProgress, bool completed, bool sabotaged)
        {
            Id = id;
            Name = name;
            Position = position;
            Progress = progress;
            RequiredProgress = requiredProgress;
            Completed = completed;
            Sabotaged = sabotaged;
        }

        public int Id;
        public string Name;
        public Vector3 Position;
        public int Progress;
        public int RequiredProgress;
        public bool Completed;
        public bool Sabotaged;
    }

    public struct OnlineBodyState
    {
        public OnlineBodyState(int id, ulong victimClientId, Vector3 position, bool reported)
        {
            Id = id;
            VictimClientId = victimClientId;
            Position = position;
            Reported = reported;
        }

        public int Id;
        public ulong VictimClientId;
        public Vector3 Position;
        public bool Reported;
    }

    public enum OnlineMatchPhase
    {
        Lobby,
        Opening,
        Action,
        Meeting,
        Voting,
        Result
    }

    public enum OnlineActionType
    {
        Interact,
        Report,
        Kill,
        Vote,
        SkipVote,
        Ability,
        Vent
    }

    /// <summary>C4 内鬼隐藏目标追踪</summary>
    public struct MoleObjective
    {
        public int Kills;
        public int Sabotages;
        public bool SurvivedTilLate; // 存活到≤3人
    }
}
