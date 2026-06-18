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
        private const string SurveillanceCameraPrefabResourcePath = "Network/OnlineSecurityCamera";
        private const string MiniGameBridgePrefabResourcePath = "Network/OnlineMiniGameBridge";
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
        internal GameObject miniGameBridgeTemplate;
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
        private readonly Dictionary<ulong, float> serverChatLastSendTimes = new Dictionary<ulong, float>();
        private bool localReady;
        private bool roomAutoFillAi;
        private bool revealRoleOnEject;
        [SerializeField] private bool canvasHudEnabled = true;
        [SerializeField] internal OnlineRuleSet ruleSet;
        [SerializeField] internal OnlineMapService mapService;
        [SerializeField] internal OnlineTaskService taskService;
        [SerializeField] internal MiniGames.OnlineMiniGameBridge miniGameBridge;
        [Header("── T1 服务组件 ──")]
        [SerializeField] public Services.VotingService votingService;
        [SerializeField] public Services.SabotageService sabotageService;
        [SerializeField] public Services.EvidenceService evidenceService;
        [SerializeField] public Services.MeetingService meetingService;
        [SerializeField] public SimpleGameEventBus gameEventBus;
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
        private bool disconnectedNetworkSession;
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
        private Vector2 forcedLocalInputForSmokeTest;
        private Vector3 localPosition;
        private float forcedLocalInputTimer;
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

        // T1 A2: 接口引用（与现有字段并存，供服务组件使用）
        /// <summary>World builder as <see cref="IWorldBuilder"/> interface.</summary>
        public GanglandUndercover.Online.World.IWorldBuilder WorldBuilderService => WorldBuilder;

        /// <summary>Map service as <see cref="IMapService"/> interface.</summary>
        public IMapService MapServiceProvider => mapService;

        /// <summary>Audio manager as <see cref="IAudioService"/> interface.</summary>
        public IAudioService AudioServiceInstance => AudioManager.Instance;

        /// <summary>Chat system as <see cref="IChatService"/> interface.</summary>
        public IChatService ChatServiceProvider => chatSystem;

        public ulong LocalClientIdValue => LocalClientId();
        public bool IsOnline => localPreviewMode || networkManager != null && (networkManager.IsHost || networkManager.IsClient);
        public bool HasDisconnectedNetworkSession => disconnectedNetworkSession;
        /// <summary>当前已连接的客户端数（仅 Server/Host 有意义；用于 Relay 双进程联调断言）。</summary>
        /// <summary>NGO 是否已建立监听（Host）或已连接（Client）。</summary>
        /// <summary>Task#7：当前是否有现场小游戏在前台（供联机自动化断言小游戏确实接入）。</summary>
        /// <summary>Task#7：当前激活小游戏的类型名（WireTask/KeypadTask…），无则空串。</summary>
        public string ActiveMiniGameName => activeMiniGame != null ? activeMiniGame.GetType().Name : string.Empty;
        /// <summary>Task#7：当前正在处理的任务 Id（无则 -1）。</summary>

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
        public int HumanPlayerCount => CountHumanPlayers();
        public int ReadyPlayerCountValue => ReadyPlayerCount();
        public int AlivePlayerCount => CountAlivePlayers();
        public int CompletedTaskCount => CountCompletedTasks();
        public int SabotagedTaskCount => CountSabotagedTasks();
        public int UnreportedBodyCount => CountUnreportedBodies();
        public int WorldObjectCount => CountWorldObjects();
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
        public int KeyLandmarkVisualCount => CountNamedWorldObjects("关键地标");
        public int TaskEventFeedbackMarkerCount => CountNamedWorldObjects("事件反馈");
        public int VerticalSliceSetPieceCount => CountNamedWorldObjects("VerticalSlice");
        public int VerticalSliceRoomIdentityCount => CountNamedWorldObjects("VerticalSlice Room");
        public int VerticalSliceTaskMiniGameSetPieceCount => CountNamedWorldObjects("VerticalSlice Task");
        public int VerticalSliceStageOneSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1");
        public int VerticalSliceStageOneEntranceCount => CountNamedWorldObjects("VerticalSlice Stage1 Entrance");
        public int VerticalSliceStageOneFirstScreenCount => CountNamedWorldObjects("VerticalSlice Stage1 FirstScreen");
        public int VerticalSliceStageOneSightlineCount => CountNamedWorldObjects("VerticalSlice Stage1 Sightline");
        public int VerticalSliceStageOneCameraShotCount => CountNamedWorldObjects("VerticalSlice Stage1 CameraShot");
        public int VerticalSliceStageOneGameplayAnchorCount => CountNamedWorldObjects("VerticalSlice Stage1 GameplayAnchor");
        public int VerticalSliceStageOneMeetingSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1 Meeting");
        public int VerticalSliceStageOneBlackoutSetPieceCount => CountNamedWorldObjects("VerticalSlice Stage1 Blackout");
        public int VerticalSliceStageOneEditableAnchorCount => worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<VerticalSliceStageOneAnchor>(true).Length;
        public int LimeZuFirstScreenSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuFirstScreenSpriteElementCount;
        public int LimeZuTaskMiniGameSetPieceSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuTaskMiniGameSetPieceSpriteElementCount;
        public int LimeZuTaskStationSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuTaskStationSpriteElementCount;
        public int LimeZuLandmarkSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuLandmarkSpriteElementCount;
        public int LimeZuTaskEventFeedbackSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuTaskEventFeedbackSpriteElementCount;
        public int LimeZuRoomPropSpriteElementCount => WorldBuilder == null ? 0 : WorldBuilder.LimeZuRoomPropSpriteElementCount;
        public int KillSceneVfxCount => CountNamedWorldObjects("Stage2 Kill VFX");
        public int BlackoutVfxCount => CountNamedWorldObjects("Blackout VFX");
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
        /// <summary>B3: 通用破坏计时器设置（替代反射）</summary>
        public void ApplySabotageTimer(SabotageType type)
        {
            taskService.ApplySabotageEffect(type, type.ToString());
        }
        /// <summary>D3: 当前是否在会议/投票阶段</summary>
        // M1 收尾：语音已移除，以下三个属性转为聊天通道状态映射
        public string ActiveVoiceChannel => chatSystem != null ? chatSystem.CurrentChannel.ToString() : "None";

        public void EditorForceRestartForSmokeTest()
        {
            RestartMatch();
        }
















        /// <summary>
        /// 烟测/自动化用：淘汰所有黑帮玩家并评估胜负，确定性地把对局推进到 Result 阶段。
        /// 走真实的 EvaluateWinConditions 路径（含 VictoryBridge），返回是否成功进入结算。
        /// </summary>

        /// <summary>
        /// Task#7 自动化：按 taskId 打开现场小游戏（无头环境无法点击，故提供驱动钩子）。
        /// 返回当前激活的小游戏类型名（如 WireTask），未能打开则返回空串。
        /// </summary>

        /// <summary>
        /// Task#7 自动化：强制完成当前激活的小游戏，走与真实完成一致的 CompleteActiveTask 路径。
        /// 完成后 activeTaskId 应回到 -1。返回是否确有小游戏被完成。
        /// </summary>









        // E4: 破坏 VFX 初始化




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
            CleanupJoinedLobbySession();
            CleanupPublishedLobbySession();

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
            localPlayerName = OnlineMatchUtils.LimitText(value, 16, "港区玩家");

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
            roomName = OnlineMatchUtils.LimitText(value, 20, "九龙港区夜局");

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
            relayJoinInput = OnlineMatchUtils.CleanRelayJoinInput(value);
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
            taskService.EvidenceTarget = Mathf.Clamp(value, ruleSet.MinEvidenceTarget, ruleSet.MaxEvidenceTarget);

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
                activeTaskCharge = Mathf.Min(1f, activeTaskCharge + Time.deltaTime * OnlineMatchUtils.TaskChargeRate(activeTaskId));
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

            if (forcedLocalInputTimer > 0f)
            {
                forcedLocalInputTimer = Mathf.Max(0f, forcedLocalInputTimer - Time.deltaTime);
                localInput = forcedLocalInputForSmokeTest;
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
                if (ShouldOpenLocalTaskPanel() && (miniGameBridge == null || !miniGameBridge.IsSpawned))
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
                    phaseTimer = ruleSet.VotingSecondsFor(players.Count);
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
            serverChatLastSendTimes.Clear();
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
            ApplyFirstKillDelay();
            status = "专案开始：身份已私发，准备进入九龙港城。";
            AddCaseLog(status);
            PlayCue("start");
            SetPublishedLobbySessionLocked(true);

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
                    if (role == OnlineRole.Gang || role == OnlineRole.Mole)
                        gangIds.Add(kvp.Key);
                    else if (role == OnlineRole.Police || role == OnlineRole.Undercover)
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
            OnlineMatchUtils.Shuffle(shuffled);

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

        private void ApplyFirstKillDelay()
        {
            float delay = ruleSet.FirstKillMinDelaySecondsFor(players.Count);

            foreach (KeyValuePair<ulong, OnlineRole> pair in privateRoles)
            {
                if (pair.Value != OnlineRole.Gang && pair.Value != OnlineRole.Mole)
                {
                    continue;
                }

                killSystem.killCooldowns[pair.Key] = delay;

                if (players.TryGetValue(pair.Key, out OnlinePlayerState state))
                {
                    state.KillCooldown = delay;
                    players[pair.Key] = state;
                }
            }
        }

        private void AssignSingleRole(ulong clientId, OnlineRole role, int roleIndex)
        {
            privateRoles[clientId] = role;
            if (players.TryGetValue(clientId, out OnlinePlayerState state))
            {
                state.Profession = OnlineMatchUtils.ProfessionFor(role, roleIndex);
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
            snap.KillCooldowns = OnlineMatchUtils.CooldownsToList(killSystem.killCooldowns);
            snap.AbilityCooldowns = OnlineMatchUtils.CooldownsToList(abilityCooldowns);
            snap.VentCooldowns = OnlineMatchUtils.CooldownsToList(ventCooldowns);
            snap.BotThinkTimers = OnlineMatchUtils.CooldownsToList(_botController.ThinkTimers);
            snap.BotVoteTimers = OnlineMatchUtils.CooldownsToList(_botController.VoteTimers);

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
            OnlineMatchUtils.ListToCooldowns(killSystem.killCooldowns, snap.KillCooldowns);
            OnlineMatchUtils.ListToCooldowns(abilityCooldowns, snap.AbilityCooldowns);
            OnlineMatchUtils.ListToCooldowns(ventCooldowns, snap.VentCooldowns);
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
                    networkManager.CustomMessagingManager.SendNamedMessageToAll(MapSelectMessage, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }

            Debug.Log($"[D5] Host selected map: {type}");
        }



        /// <summary>获取玩家显示名称（按 clientId 查字典）</summary>


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
                SabotageType sabotageType = OnlineMatchUtils.SabotageForTask(nearestTask.Id);
                nearestTask.Sabotaged = true;
                nearestTask.Completed = false;
                nearestTask.Progress = Mathf.Max(0, nearestTask.Progress - 1);
                taskService.EvidenceScore = Mathf.Max(0, taskService.EvidenceScore - OnlineMatchUtils.SabotageEvidencePenalty(sabotageType));
                string actorLabel = role == OnlineRole.Mole ? "线人" : "黑帮";
                status = actorLabel + "秘密破坏了 " + nearestTask.Name + "。";
                lastSabotageEvent = status + " 影响: " + OnlineMatchUtils.SabotageName(sabotageType);
                AddCaseLog(status);
                syncManager?.OnTaskSabotagedLocally(senderClientId, nearestTask.Id, sabotageType);
                ApplySabotageEffect(sabotageType, nearestTask.Name);
                AudioManager.Instance?.PlaySFX(SoundEffect.Sabotage);

                // M5.5: 证据板记录 + 嫌疑增加
                LogEvidence(EvidenceCategory.EvidenceChain,
                    $"{nearestTask.Name} 被破坏（{OnlineMatchUtils.SabotageName(sabotageType)}），证据链-{OnlineMatchUtils.SabotageEvidencePenalty(sabotageType)}", -1);

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
                if (!player.IsBot && !submittingActiveTask && miniGameBridge != null && miniGameBridge.IsSpawned)
                {
                    if (nearestTask.Sabotaged)
                    {
                        miniGameBridge.OpenRepairMinigameOnClient(senderClientId, nearestTask.Id);
                    }
                    else if (!nearestTask.Completed)
                    {
                        miniGameBridge.OpenMinigameOnClient(senderClientId, nearestTask.Id);
                    }

                    return;
                }

                if (senderClientId == LocalClientId() && !player.IsBot && !submittingActiveTask)
                {
                    BeginActiveTask(nearestTask.Id); // 降级到旧 OnGUI 任务
                    return;
                }

                if (nearestTask.Sabotaged)
                {
                    nearestTask.Sabotaged = false;
                    RepairSabotageEffect(OnlineMatchUtils.SabotageForTask(nearestTask.Id));
                    status = nearestTask.Name + " 的破坏已修复，危机效果下降。";
                    lastSabotageEvent = status;
                    AddCaseLog(status);
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


        /// <summary>
        /// 按 taskId 创建并显示对应的小游戏（11 种轮转），把完成/取消回调接到既有的
        /// CompleteActiveTask（服务器提交）与取消逻辑上。任何异常都降级为经典任务面板。
        /// </summary>










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
            killSystem.killCooldowns[senderClientId] = ruleSet.KillCooldownFor(players.Count);
            player.Suspicion += 2;
            players[senderClientId] = player;
            killSystem.killCount++;
            status = "黑帮击倒了 " + victim.DisplayName + "。";
            AddCaseLog(status);
            evidenceDossier?.RegisterEvidence("击杀事件：" + victim.DisplayName + "被击倒", new Vector2(victim.Position.x, victim.Position.y), 5.0f); // C2

            // C2-1: Wire EvidenceChain — register kill as bloodstain evidence
            RegisterKillEvidence(new Vector2(victim.Position.x, victim.Position.y));

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
                        // 标记所有黑帮侧（Gang + Mole）
                        foreach (var kv in players)
                        {
                            OnlineRole kvRole = GetPrivateRole(kv.Key);
                            if (kvRole == OnlineRole.Gang || kvRole == OnlineRole.Mole)
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
            emergencyCooldownTimer = ruleSet.EmergencyCooldownSecondsFor(players.Count);
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

            if (Vector3.Distance(player.Position, mapService.ScaleMapPosition(Vector3.zero)) <= ruleSet.ReportRangeFor(players.Count))
            {
                emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
                emergencyCooldownTimer = ruleSet.EmergencyCooldownSecondsFor(players.Count);
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
            phaseTimer = ruleSet.MeetingIntroSecondsFor(players.Count);
            taskService.RepairSabotageEffect(SabotageType.Blackout);
            killSystem.reportCooldownTimer = ruleSet.ReportCooldownSecondsFor(players.Count);
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
            // C2-2: Register as evidence accusation for vote weight bonus
            if (targetClientId != SkipVoteTarget)
                AccusePlayer(voterClientId, targetClientId);
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
            PlayCue("vote");

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

            // C2-2: Apply evidence chain accusation weight bonus (+2 per mid-strength evidence chain)
            var accusedList = new List<ulong>(tally.Keys);
            foreach (var accused in accusedList)
            {
                int bonus = GetAccusationWeightBonus(accused);
                if (bonus > 0) tally[accused] += bonus;
            }

            ulong ejectedClientId = SkipVoteTarget;
            int bestVotes = 0;
            bool tied = false;

            if (skipVotes > 0)
            {
                bestVotes = skipVotes;
            }

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
                lastVoteOutcome = status + " 票型: " + BuildVoteTallySummary(tally, skipVotes);
                AddCaseLog(status);
                syncManager?.OnMeetingEnded();
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
                    ? ejected.DisplayName + " 被投出局，身份是：" + OnlineMatchUtils.RoleName(ejected.PublicRole) + "。"
                    : ejected.DisplayName + " 被投出局，身份暂不公开。";
                lastVoteOutcome = ejected.DisplayName + " 出局 | 得票 " + bestVotes + " | 身份 " + (revealRoleOnEject ? OnlineMatchUtils.RoleName(ejectedRole) : "未公开");
                AddCaseLog(status);
                PlayCue("eliminated");

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
            syncManager?.OnMeetingEnded();

            if (phase != OnlineMatchPhase.Result)
            {
                phase = OnlineMatchPhase.Action;
                ApplyPostMeetingKillGrace();
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

            // 阵营归属：黑帮侧 = Gang + Mole，警方侧 = Police + Undercover
            int aliveGangSide = 0;
            int alivePoliceSide = 0;
            int aliveGang = 0;
            int aliveNonGangSide = 0;
            int aliveUndercover = 0;
            int totalAlive = 0;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (!pair.Value.Alive)
                {
                    continue;
                }

                totalAlive++;
                OnlineRole role = GetPrivateRole(pair.Key);
                if (role == OnlineRole.Gang || role == OnlineRole.Mole)
                {
                    aliveGangSide++;
                    if (role == OnlineRole.Gang) aliveGang++;
                }
                else
                {
                    alivePoliceSide++;
                    aliveNonGangSide++;
                    if (role == OnlineRole.Undercover) aliveUndercover++;
                }
            }

            // 卧底特殊胜利：优先于阵营全灭判定
            if (aliveUndercover == 1 && totalAlive == 1)
            {
                SetResult("卧底胜利：港区暗线完美收网。");
            }
            else if (aliveGangSide == 0 && totalAlive >= 1)
            {
                SetResult("警方胜利：黑帮全部出局。");
            }
            else if (alivePoliceSide == 0 && totalAlive >= 1)
            {
                SetResult("黑帮胜利：警方阵营全部出局。");
            }
            else if (aliveGang > 0 && (aliveNonGangSide == 0 || (totalAlive >= 4 && aliveGang >= aliveNonGangSide)))
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
                killSystem.ApplyPostMeetingKillGrace(ruleSet.PostMeetingKillGraceSecondsFor(players.Count));
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

        /// <summary>
        /// 获取按嫌疑值降序排列的玩家 ID 列表（供会议使用）。
        /// </summary>

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




        /// <summary>
        /// 统一暗线通道逻辑（合并原 OnlineVents / TryVent 系统）。
        /// 权限：仅 Gang/Mole 可用；冷却由 ruleSet.VentCooldownSeconds 控制；
        /// 节点位置来自 OnlineMapService；目标为对侧节点 (i+2)%count。
        /// </summary>

        /// <summary>
        /// 重载：供职业技能（Driver 等）调用，只传入 ref player，使用默认 senderClientId 逻辑。
        /// </summary>



        // ============================================================
        //  Phase 4: 破坏深化 — 房间封锁系统
        // ============================================================

        /// <summary>随机封锁 N 个房间的入口。</summary>

        /// <summary>检查房间是否被封锁。</summary>

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
            if (!TryValidateTaskActor(clientId, taskId, out _, out OnlineTaskState task, out reason))
                return false;

            if (task.Completed)
            {
                reason = "任务已完成";
                return false;
            }

            if (task.Sabotaged)
            {
                reason = "任务已被破坏";
                return false;
            }

            if (IsTaskLockedByAnotherPlayer(clientId, taskId, out reason))
                return false;

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// M5.3: 服务器校验：玩家是否有权开始修复破坏。
        /// </summary>
        public bool ValidateRepairStart(ulong clientId, int taskId, out string reason)
        {
            if (!TryValidateTaskActor(clientId, taskId, out _, out OnlineTaskState task, out reason))
                return false;

            if (!task.Sabotaged)
            {
                reason = "任务未被破坏";
                return false;
            }

            if (IsTaskLockedByAnotherPlayer(clientId, taskId, out reason))
                return false;

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
            if (!TryValidateTaskActor(clientId, taskId, out OnlinePlayerState player, out OnlineTaskState task, out error))
                return false;

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

            if (!IsTaskActiveFor(clientId, taskId, out error))
                return false;

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

            // C3: Wire identity mechanics — undercover/mole intelligence accumulation
            OnlineRole privateRole = GetPrivateRole(clientId);
            if (privateRole == OnlineRole.Undercover)
                AccumulateUndercoverIntel(clientId, 1);
            if (privateRole == OnlineRole.Mole)
                AccumulateMoleIntel(clientId, 1);

            // C2-1: Wire EvidenceChain — register task evidence for meeting chain
            RegisterTaskEvidence(taskId, new Vector2(task.Position.x, task.Position.y), clientId);

            return true;
        }

        private Dictionary<int, ulong> activeTaskUsers;

        /// <summary>
        /// M5.3: 服务器校验并完成破坏修复。
        /// </summary>
        public bool ValidateAndRepairTask(ulong clientId, int taskId, out string error)
        {
            if (!TryValidateTaskActor(clientId, taskId, out OnlinePlayerState player, out OnlineTaskState task, out error))
                return false;

            if (!task.Sabotaged)
            {
                error = "任务未被破坏";
                return false;
            }

            if (!IsTaskActiveFor(clientId, taskId, out error))
                return false;

            // 修复
            SabotageType sabotageType = OnlineMatchUtils.SabotageForTask(taskId);
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

        private bool TryValidateTaskActor(ulong clientId, int taskId, out OnlinePlayerState player, out OnlineTaskState task, out string reason)
        {
            player = default;
            task = default;
            reason = string.Empty;

            if (phase != OnlineMatchPhase.Action)
            {
                reason = "当前阶段不能处理任务";
                return false;
            }

            if (!players.TryGetValue(clientId, out player))
            {
                reason = "玩家不存在";
                return false;
            }

            if (!player.Alive)
            {
                reason = "玩家已死亡";
                return false;
            }

            task = GetTask(taskId);
            if (task.Id < 0)
            {
                reason = "任务不存在";
                return false;
            }

            float range = ruleSet != null ? Mathf.Max(2.5f, ruleSet.InteractionRange) : 2.5f;
            float dist = Vector2.Distance(
                new Vector2(player.Position.x, player.Position.y),
                new Vector2(task.Position.x, task.Position.y));
            if (dist > range)
            {
                reason = "距离任务点太远";
                return false;
            }

            return true;
        }

        private bool IsTaskLockedByAnotherPlayer(ulong clientId, int taskId, out string reason)
        {
            reason = string.Empty;
            if (activeTaskUsers != null
                && activeTaskUsers.TryGetValue(taskId, out ulong owner)
                && owner != clientId)
            {
                reason = "任务由其他玩家处理中";
                return true;
            }

            return false;
        }

        private bool IsTaskActiveFor(ulong clientId, int taskId, out string reason)
        {
            reason = string.Empty;
            if (activeTaskUsers == null || !activeTaskUsers.TryGetValue(taskId, out ulong owner))
            {
                reason = "任务未开始";
                return false;
            }

            if (owner != clientId)
            {
                reason = "任务由其他玩家处理中";
                return false;
            }

            return true;
        }

        /// <summary>
        /// M5.4 公开方法：判断玩家是否属于黑帮阵营（Gang/Mole）。
        /// 用于监控系统红灯判定，不下发私密身份。
        /// </summary>
        public bool IsGangFaction(ulong clientId)
        {
            if (privateRoles.TryGetValue(clientId, out OnlineRole role))
            {
                return role == OnlineRole.Gang || role == OnlineRole.Mole;
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
            int gain = OnlineMatchUtils.TaskEvidenceValue(taskId);

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




























































































        private TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize)
        {
            return worldBuilder.CreateWorldLabelAt(text, position, characterSize);
        }

        private TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize)
        {
            return worldBuilder.CreateWorldLabel(parent, text, localPosition, characterSize);
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
                    completed++;
            }
            return completed;
        }

        private static string BuildRelayLobbySummary(
            string relayStatusValue,
            string relayJoinCodeValue,
            string relayJoinInputValue,
            bool operationInProgress,
            bool isOnline,
            bool isHost,
            bool isClientConnected,
            int connectedClientCount)
        {
            return OnlineMatchUtils.BuildRelayLobbySummary(
                relayStatusValue, relayJoinCodeValue, relayJoinInputValue,
                operationInProgress, isOnline, isHost, isClientConnected, connectedClientCount);
        }

        private static bool TryResolveSoundEffectCue(string cueName, out SoundEffect effect)
        {
            return OnlineMatchUtils.TryResolveSoundEffectCue(cueName, out effect);
        }

        private static AudioClip CreateToneClip(string clipName, float frequency, float duration)
        {
            return OnlineMatchUtils.CreateToneClip(clipName, frequency, duration);
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
            audioClips["eliminated"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Hand Gun 2", "Gangland Eliminated", 300f, 0.16f);
            audioClips["result"] = LoadAudioClipOrFallback("AssetStore/Free Pack/Explosion 1", "Gangland Result", 740f, 0.2f);
        }

        private AudioClip LoadAudioClipOrFallback(string resourcePath, string clipName, float frequency, float duration)
        {
            AudioClip clip = Resources.Load<AudioClip>(OnlineWorldBuilder.NormalizeResourcePath(resourcePath));

            if (clip != null)
            {
                return clip;
            }

            return OnlineMatchUtils.CreateToneClip(clipName, frequency, duration);
        }

        private void PlayCue(string cueName)
        {
            EnsureAudio();

            if (OnlineMatchUtils.TryResolveSoundEffectCue(cueName, out SoundEffect effect))
            {
                AudioManager.Instance?.PlaySFX(effect);
            }

            if (audioSource != null && audioClips.TryGetValue(cueName, out AudioClip clip) && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
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
            serverChatLastSendTimes.Clear();
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
            SetPublishedLobbySessionLocked(false);
            BroadcastSnapshot();
        }
















        /// <summary>
        /// M8.2: 根据角色和序号分配职业。
        /// Mole 分配警察职业以维持掩护；Gang 分配 Enforcer/Fixer；Undercover 分配 UndercoverAgent/Driver。
        /// </summary>













    }
}
