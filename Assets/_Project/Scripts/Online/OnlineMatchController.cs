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

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController : MonoBehaviour
    {
        private const string ClientStateMessage = "GanglandClientState";
        private const string ClientActionMessage = "GanglandClientAction";
        private const string ClientProfileMessage = "GanglandClientProfile";
        private const string ServerSnapshotMessage = "GanglandServerSnapshot";
        private const string RoleAssignMessage = "GanglandRoleAssign";
        private const string WorldRootName = "Online Gangland Runtime Map v5";
        private const string QuaterniusFbxRoot = "Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit/FBX/";
        private const string RuntimeResourcesRoot = "Assets/_Project/Resources/";
        private const string AssetStoreResourceRoot = "AssetStore/";
        private const ushort DefaultPort = 7777;
        private const ulong SkipVoteTarget = ulong.MaxValue;
        private const float SnapshotIntervalSeconds = 0.08f;
        private const float MoveSpeed = 4.5f;
        private const float InteractionRange = 1.08f;
        private const float KillRange = 0.9f;
        private const float ReportRange = 1.25f;
        private const float PlayerCollisionRadius = 0.22f;
        private const float CollisionTraceStep = 0.08f;
        private const float UnderworldTransitRange = 1.15f;
        private const float KillCooldownSeconds = 34f;
        private const float RoleRevealSeconds = 6.5f;
        private const float MeetingIntroSeconds = 35f;
        private const float VotingSeconds = 55f;
        private const float BlackoutSeconds = 28f;
        private const float LockdownSeconds = 32f;
        private const float CommunicationJamSeconds = 30f;
        private const float EvidenceLeakSeconds = 36f;
        private const float PatrolAlertSeconds = 30f;
        private const float EmergencyCooldownSeconds = 75f;
        private const float AiActionGraceSeconds = 22f;
        private const float PreviewAiActionGraceSeconds = 55f;
        private const float MatchTargetMinSeconds = 600f;
        private const float MatchHardLimitSeconds = 1200f;
        private const float MapHalfWidth = 24.0f;
        private const float MapHalfHeight = 14.0f;
        private const float DesignScaleX = 2.0f;
        private const float DesignScaleY = 1.85f;
        private const float DesignMapHalfWidth = MapHalfWidth / DesignScaleX;
        private const float DesignMapHalfHeight = MapHalfHeight / DesignScaleY;
        private const int MinimumPlayablePlayers = 5;
        private const int MinimumRoomPlayers = 4;
        private const int MaximumRoomPlayers = 10;
        private const int DefaultRoomMinPlayers = 8;
        private const int DefaultRoomMaxPlayers = 10;
        private const int DefaultEvidenceTarget = 44;
        private const int UnderworldPassageCount = 4;
        private const ulong LocalPreviewClientId = 0UL;
        private const ulong BotClientIdBase = 900000UL;
        private const float BotThinkMinSeconds = 1.2f;
        private const float BotThinkMaxSeconds = 3.4f;
        private const float BotInteractDistance = 0.45f;
        private const int MaxCaseLogEntries = 8;
        private const float AbilityCooldownSeconds = 13f;
        private const float PreviewCameraSize = 13.4f;
        private const float ActionCameraSize = 4.25f;
        private const float BlackoutCameraSize = 3.05f;
        private const float TaskCameraSize = 4.1f;
        private const float ActionCameraYOffset = -4.42f;
        private const float ActionCameraZOffset = -6.85f;
        private const float PreviewCameraYOffset = -13.6f;
        private const float PreviewCameraZOffset = -16.2f;
        private const float ActionCameraFieldOfView = 42f;
        private const float PreviewCameraFieldOfView = 52f;
        private const float ActionCameraLookAheadY = 0.88f;
        private const float ActionCameraLookHeight = 0.42f;
        private const float PlayerAliveVisualScale = 1.12f;
        private const float PlayerDeadVisualScaleX = 1.04f;
        private const float PlayerDeadVisualScaleY = 0.52f;
        private const float VoiceRetrySeconds = 4.0f;
        private const float VoicePositionUpdateSeconds = 0.12f;

        private enum MapEntrance
        {
            North,
            South,
            East,
            West
        }

        private readonly struct ShipRoomSpec
        {
            public ShipRoomSpec(string name, string label, Vector3 center, Vector3 size, Color floor, MapEntrance entrance)
            {
                Name = name;
                Label = label;
                Center = center;
                Size = size;
                Floor = floor;
                Entrance = entrance;
            }

            public readonly string Name;
            public readonly string Label;
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public readonly Color Floor;
            public readonly MapEntrance Entrance;
        }

        private enum SabotageType
        {
            None,
            Blackout,
            Lockdown,
            Communications,
            EvidenceLeak,
            PatrolAlert
        }

        private readonly Dictionary<ulong, OnlinePlayerState> players = new Dictionary<ulong, OnlinePlayerState>();
        private readonly List<OnlineTaskState> tasks = new List<OnlineTaskState>();
        private readonly List<OnlineBodyState> bodies = new List<OnlineBodyState>();
        private readonly List<string> caseLog = new List<string>();
        private readonly Dictionary<ulong, OnlineRole> privateRoles = new Dictionary<ulong, OnlineRole>();
        private readonly Dictionary<ulong, ulong> votes = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, float> killCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> abilityCooldowns = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> botThinkTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, float> botVoteTimers = new Dictionary<ulong, float>();
        private readonly Dictionary<ulong, Vector3> botTargets = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<ulong, GameObject> playerVisuals = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, Vector3> playerVisualBaseScales = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<int, GameObject> taskVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> bodyVisuals = new Dictionary<int, GameObject>();
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

        private NetworkManager networkManager;
        private UnityTransport transport;
        private UnityServiceBootstrap serviceBootstrap;
        private GameObject worldRoot;
        private OnlineMatchHud onlineHud;
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
        private bool localReady;
        private bool roomAutoFillAi = true;
        private bool revealRoleOnEject = true;
        private bool proximityVoiceEnabled = true;
        [SerializeField] private bool canvasHudEnabled = true;
        private bool matchStarted;
        private bool localPreviewMode;
        private bool fullMapPreview = true;
        private bool tacticalMapOpen;
        private bool intelBoardOpen;
        private int evidenceScore;
        private int evidenceTarget = DefaultEvidenceTarget;
        private int roomMinPlayers = DefaultRoomMinPlayers;
        private int roomMaxPlayers = DefaultRoomMaxPlayers;
        private int nextBodyId;
        private int evidenceMilestoneIndex;
        private int activeTaskId = -1;
        private int activeTaskStep;
        private int emergencyMeetingsLeft;
        private Vector2 localInput;
        private Vector3 localPosition;
        private float clientSnapshotTimer;
        private float serverSnapshotTimer;
        private float actionCooldown;
        private float phaseTimer;
        private float blackoutTimer;
        private float lockdownTimer;
        private float communicationJamTimer;
        private float evidenceLeakTimer;
        private float evidenceLeakAccumulator;
        private float patrolAlertTimer;
        private float emergencyCooldownTimer;
        private float aiActionGraceTimer;
        private float activeTaskCharge;
        private float matchElapsedSeconds;
        private bool cameraWasConfigured;
        private bool submittingActiveTask;
        private bool activeTaskStepOneDone;
        private bool activeTaskStepTwoDone;
        private bool activeTaskStepThreeDone;
        private int activeTaskMistakes;
        private float activeTaskFeedbackTimer;
        private float nextVoiceRetryTime;
        private float nextVoicePositionUpdateTime;
        private bool activeTaskFeedbackPositive;
        private bool voiceJoinInProgress;
        private bool voiceLeaveInProgress;
        private bool relayOperationInProgress;
        private ulong currentCameraSubjectId = LocalPreviewClientId;
        private string desiredVoiceChannel = string.Empty;
        private string pendingVoiceChannel = string.Empty;

        public IReadOnlyDictionary<ulong, OnlinePlayerState> Players => players;
        public IReadOnlyList<OnlineTaskState> Tasks => tasks;
        public IReadOnlyList<OnlineBodyState> Bodies => bodies;
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
        public bool MatchStarted => matchStarted;
        public OnlineMatchPhase Phase => phase;
        public float PhaseTimer => phaseTimer;
        public string Status => status;
        public int TaskCount => tasks.Count;
        public int BodyCount => bodies.Count;
        public int BotCount => CountBotPlayers();
        public int HumanPlayerCount => CountHumanPlayers();
        public int ReadyPlayerCountValue => ReadyPlayerCount();
        public int AlivePlayerCount => CountAlivePlayers();
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
        public int StageTwoActiveMeetingPoseCount => CountActiveNamedWorldObjects("Stage2 Meeting");
        public int StageTwoActiveDownedStateCount => CountActiveNamedWorldObjects("Stage2 Downed");
        public int StageTwoActiveVoiceRadiusCount => CountActiveNamedWorldObjects("Stage2 VoiceRadius");
        public int StageTwoActiveReportFeedbackCount => CountActiveNamedWorldObjects("Stage2 Report");
        public int StageTwoActiveVoteFeedbackCount => CountActiveNamedWorldObjects("Stage2 Vote");
        public int StageTwoForensicSceneCount => CountNamedWorldObjects("Stage2 Forensic");
        public int StageTwoRuntimeRigCount => worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<StageTwoCharacterRig>(true).Length;
        public int StageTwoConfiguredRigCount => CountConfiguredStageTwoRigs();
        public int TaskMiniGameCanvasElementCount => onlineHud == null ? 0 : onlineHud.TaskMiniGameCanvasElementCount;
        public int MeetingSeatCanvasElementCount => onlineHud == null ? 0 : onlineHud.MeetingSeatCanvasElementCount;
        public bool CanvasHudLayoutComplete => onlineHud != null && onlineHud.HasCompleteLayout;
        public int LargePortVistaCount => CountNamedWorldObjects("大场景港区层");
        public int UnderworldPassageNodeCount => worldRoot == null ? 0 : CountNamedWorldObjects("暗线节点");
        public bool HasRuntimeAudio => audioSource != null;
        public int RoomMinPlayers => roomMinPlayers;
        public int RoomMaxPlayers => roomMaxPlayers;
        public int MinimumRoomPlayersValue => MinimumRoomPlayers;
        public int MaximumRoomPlayersValue => MaximumRoomPlayers;
        public int EvidenceScore => evidenceScore;
        public int EvidenceTarget => evidenceTarget;
        public float MatchElapsedSeconds => matchElapsedSeconds;
        public float MapHalfWidthValue => MapHalfWidth;
        public float MapHalfHeightValue => MapHalfHeight;
        public int TargetMatchMinutesMin => Mathf.RoundToInt(MatchTargetMinSeconds / 60f);
        public int TargetMatchMinutesMax => Mathf.RoundToInt(MatchHardLimitSeconds / 60f);
        public string ResultSummary => resultSummary;
        public bool AutoFillAi => roomAutoFillAi;
        public bool RevealRoleOnEject => revealRoleOnEject;
        public bool ProximityVoiceEnabled => proximityVoiceEnabled;
        public bool LocalReady => localReady;
        public bool CanStartMatch => CanStartLobbyMatch();
        public bool RelayOperationInProgress => relayOperationInProgress;
        public int EmergencyMeetingsLeft => emergencyMeetingsLeft;
        public float BlackoutTimer => blackoutTimer;
        public float LockdownTimer => lockdownTimer;
        public float CommunicationJamTimer => communicationJamTimer;
        public float EvidenceLeakTimer => evidenceLeakTimer;
        public float PatrolAlertTimer => patrolAlertTimer;
        public bool TacticalMapOpen => tacticalMapOpen;
        public bool IntelBoardOpen => intelBoardOpen;
        public string VoiceStatus => serviceBootstrap == null ? "Vivox 未挂载。" : serviceBootstrap.VoiceStatus;
        public string ActiveVoiceChannel => serviceBootstrap == null ? string.Empty : serviceBootstrap.ActiveVoiceChannel;
        public int VoiceParticipantCount => serviceBootstrap == null ? 0 : serviceBootstrap.ActiveVoiceParticipantCount;
        public bool VoiceRoutingEnabled => proximityVoiceEnabled || phase == OnlineMatchPhase.Action || phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
        public bool LocalTaskInputGateActive => activeTaskId >= 0;
        public string MatchPressureSummary => BuildMatchPressureSummary();
        public string LobbyReadinessSummary => BuildLobbyReadinessSummary();
        public string LobbyRoadmap => BuildPhaseRoadmap();
        public string LocalObjectiveSummary => BuildLocalObjectiveSummary();
        public string LocalProfessionDisplayName => LocalProfessionName();
        public string PhaseDisplayName => PhaseName(phase);
        public string MatchTimeText => FormatMatchTime(matchElapsedSeconds);
        public string HazardSummary => BuildHazardSummary();
        public string LocalActionHint => BuildLocalActionHint();
        public string VoiceHudLine => BuildVoiceHudLine();
        public string FocusedIntelText => BuildFocusedIntel();
        public string TaskListText => BuildTaskList();
        public string CaseLogText => BuildCaseLog();
        public string PlayerListText => BuildPlayerList();
        public string ReleaseReadinessText => BuildReleaseReadiness();
        public string MeetingEvidenceDigest => BuildMeetingEvidenceDigest();
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
        public int TacticalMapLabelCount => tasks.Count + ShipRooms().Length + players.Count + bodies.Count + UnderworldPassageCount;
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
                players[0] = new OnlinePlayerState(0, "玩家0", SpawnPosition(0), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            EnsureMinimumBots();
            StartOnlineMatchCore(false);
        }

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
            cameraWasConfigured = false;
            ConfigureMainCamera();
            return Camera.main != null && !Camera.main.orthographic;
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
            blackoutTimer = 0f;
            currentCameraSubjectId = localClientId;
            cameraWasConfigured = false;
            status = "编辑器演示视角：已进入九龙港区行动画面。";

            if (onlineHud != null)
            {
                onlineHud.Bind(this);
            }

            ConfigureMainCamera();
            return Camera.main != null && !Camera.main.orthographic;
        }

        public bool EditorForceStageOneOpeningShotForSmokeTest()
        {
            if (players.Count == 0)
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(LocalPreviewClientId, "烟测玩家", ScaleMapPosition(new Vector3(-1.18f, -0.72f, 0f)), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            phase = OnlineMatchPhase.Opening;
            matchStarted = true;
            fullMapPreview = true;
            tacticalMapOpen = false;
            intelBoardOpen = false;
            activeTaskId = -1;
            blackoutTimer = 0f;
            cameraWasConfigured = false;
            ConfigureMainCamera();
            return Camera.main != null && Camera.main.orthographic;
        }

        public bool EditorForceStageOneBlackoutShotForSmokeTest()
        {
            ulong localClientId = LocalClientId();
            Vector3 blackoutPosition = ScaleMapPosition(new Vector3(8.72f, 4.8f, 0f));

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
            blackoutTimer = BlackoutSeconds;
            currentCameraSubjectId = localClientId;
            cameraWasConfigured = false;
            ConfigureMainCamera();
            return Camera.main != null && !Camera.main.orthographic && Mathf.Abs(Camera.main.fieldOfView - ActionCameraFieldOfView) < 0.5f;
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
            players[clientId] = new OnlinePlayerState(clientId, "烟测玩家", TaskPositionFor(taskId), true, true, OnlineRole.Unassigned, asGang ? OnlineProfession.Enforcer : OnlineProfession.Inspector, 0, false);
            privateRoles[clientId] = asGang ? OnlineRole.Gang : OnlineRole.Police;
            TryInteractWithTask(clientId, players[clientId]);
        }

        public void EditorOpenTaskPanelForSmokeTest(int taskId)
        {
            ulong clientId = LocalClientId();
            players[clientId] = new OnlinePlayerState(clientId, "烟测玩家", TaskPositionFor(taskId), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
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

            if (players.Count < MinimumPlayablePlayers)
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
            if (!localPreviewMode)
            {
                localPreviewMode = true;
            }

            if (players.Count < MinimumPlayablePlayers)
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

            if (bodies.Count == 0)
            {
                bodies.Add(new OnlineBodyState(nextBodyId++, victimClientId, victim.Position, false));
            }

            phase = OnlineMatchPhase.Action;
            matchStarted = true;
            currentCameraSubjectId = LocalClientId();
            UpdateWorldVisuals();
            return StageTwoActiveDownedStateCount > 0 && StageTwoForensicSceneCount > 0 && BodyCount > 0;
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

            aiActionGraceTimer = PreviewAiActionGraceSeconds;
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
#endif

        private Vector3 FindActionPreviewStartPosition()
        {
            Vector3[] preferred =
            {
                ScaleMapPosition(new Vector3(-0.62f, -1.02f, 0f)),
                ScaleMapPosition(new Vector3(-0.92f, -0.82f, 0f)),
                ScaleMapPosition(new Vector3(-1.42f, -0.62f, 0f)),
                ScaleMapPosition(new Vector3(-1.42f, -0.18f, 0f)),
                ScaleMapPosition(new Vector3(0f, -0.82f, 0f)),
                ScaleMapPosition(new Vector3(-0.25f, 1.68f, 0f))
            };

            for (int i = 0; i < preferred.Length; i++)
            {
                if (IsWalkable(preferred[i]))
                {
                    return preferred[i];
                }
            }

            return FindNearestOpenPosition(ScaleMapPosition(new Vector3(-1.42f, -0.62f, 0f)), Vector3.zero);
        }

        private void Awake()
        {
            BuildDefaultTasks();
            EnsureWorld();
            EnsureAudio();
            EnsureServiceBootstrap();
            EnsureNetworkStack();
            EnsureCanvasHud();
            localPosition = SpawnPosition(UnityEngine.Random.Range(0, MaximumRoomPlayers));
        }

        private void Reset()
        {
            if (Application.isPlaying)
            {
                return;
            }

            BuildDefaultTasks();
            EnsureWorld();
            EnsureCanvasHud();
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
                ReadActiveTaskInput();
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
            TickVoiceRouting();
            ConfigureMainCamera();
            UpdateWorldVisuals();
        }

        private void OnGUI()
        {
            if (canvasHudEnabled && onlineHud != null)
            {
                return;
            }

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
            GUILayout.Label("阶段: " + PhaseName(phase) + " | 局时: " + FormatMatchTime(matchElapsedSeconds) + "/20:00 | 证据链: " + evidenceScore + "/" + evidenceTarget + " | 危机: " + BuildHazardSummary());
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

        private void OnDrawGizmos()
        {
            foreach (OnlineTaskState task in tasks)
            {
                Gizmos.color = task.Completed ? Color.green : task.Sabotaged ? Color.red : Color.cyan;
                Gizmos.DrawCube(task.Position, new Vector3(0.35f, 0.35f, 0.05f));
            }

            foreach (OnlineBodyState body in bodies)
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

        private void EnsureNetworkStack()
        {
            networkManager = FindAnyObjectByType<NetworkManager>();

            if (networkManager == null)
            {
                GameObject networkObject = new GameObject("NetworkManager");
                networkManager = networkObject.AddComponent<NetworkManager>();
                transport = networkObject.AddComponent<UnityTransport>();
                networkManager.NetworkConfig = new NetworkConfig();
                networkManager.NetworkConfig.NetworkTransport = transport;

                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(networkObject);
                }
                else
                {
                    networkObject.transform.SetParent(transform, false);
                }
            }
            else
            {
                transport = networkManager.GetComponent<UnityTransport>();

                if (transport == null)
                {
                    transport = networkManager.gameObject.AddComponent<UnityTransport>();
                }

                if (networkManager.NetworkConfig == null)
                {
                    networkManager.NetworkConfig = new NetworkConfig();
                }

                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        }

        private void EnsureServiceBootstrap()
        {
            if (serviceBootstrap != null)
            {
                return;
            }

            serviceBootstrap = GetComponent<UnityServiceBootstrap>();

            if (serviceBootstrap == null)
            {
                serviceBootstrap = gameObject.AddComponent<UnityServiceBootstrap>();
            }
        }

        private void EnsureCanvasHud()
        {
            if (onlineHud != null)
            {
                return;
            }

            OnlineMatchHud existingHud = GetComponentInChildren<OnlineMatchHud>(true);

            if (existingHud != null)
            {
                onlineHud = existingHud;
                onlineHud.Bind(this);
                return;
            }

            GameObject hudObject = new GameObject("Online Match HUD");
            hudObject.transform.SetParent(transform, false);
            onlineHud = hudObject.AddComponent<OnlineMatchHud>();
            onlineHud.Bind(this);
        }

        private void TickVoiceRouting()
        {
            if (!Application.isPlaying || serviceBootstrap == null)
            {
                return;
            }

            if (!IsOnline)
            {
                desiredVoiceChannel = string.Empty;
                RequestLeaveVoiceChannels();
                return;
            }

            bool positional = ShouldUsePositionalVoice();
            desiredVoiceChannel = BuildDesiredVoiceChannel();

            if (string.IsNullOrWhiteSpace(desiredVoiceChannel))
            {
                RequestLeaveVoiceChannels();
                return;
            }

            if (!voiceJoinInProgress
                && Time.time >= nextVoiceRetryTime
                && (serviceBootstrap.ActiveVoiceChannel != desiredVoiceChannel || serviceBootstrap.ActiveVoiceChannelIsPositional != positional))
            {
                pendingVoiceChannel = desiredVoiceChannel;
                _ = JoinVoiceChannelAsync(desiredVoiceChannel, positional);
            }

            if (positional && serviceBootstrap.ActiveVoiceChannel == desiredVoiceChannel && Time.time >= nextVoicePositionUpdateTime)
            {
                serviceBootstrap.UpdatePositionalVoice(LocalVoicePosition());
                nextVoicePositionUpdateTime = Time.time + VoicePositionUpdateSeconds;
            }
        }

        private async Task JoinVoiceChannelAsync(string channelName, bool positional)
        {
            voiceJoinInProgress = true;
            nextVoiceRetryTime = Time.time + VoiceRetrySeconds;

            try
            {
                bool joined = await serviceBootstrap.JoinVoiceChannelAsync(channelName, localPlayerName, positional);

                if (joined && pendingVoiceChannel == channelName)
                {
                    AddCaseLog(positional ? "行动近距离语音已接入。" : "全员语音频道已接入。");
                }
            }
            finally
            {
                voiceJoinInProgress = false;
            }
        }

        private void RequestLeaveVoiceChannels()
        {
            if (serviceBootstrap == null || voiceLeaveInProgress || string.IsNullOrWhiteSpace(serviceBootstrap.ActiveVoiceChannel))
            {
                return;
            }

            _ = LeaveVoiceChannelsAsync();
        }

        private async Task LeaveVoiceChannelsAsync()
        {
            voiceLeaveInProgress = true;

            try
            {
                await serviceBootstrap.LeaveVoiceChannelsAsync();
            }
            finally
            {
                voiceLeaveInProgress = false;
            }
        }

        private bool ShouldUsePositionalVoice()
        {
            return phase == OnlineMatchPhase.Action && proximityVoiceEnabled && IsLocalAlive();
        }

        private string BuildDesiredVoiceChannel()
        {
            string roomKey = StableRoomKey(roomName);

            switch (phase)
            {
                case OnlineMatchPhase.Lobby:
                    return "gangland-" + roomKey + "-lobby";
                case OnlineMatchPhase.Opening:
                    return "gangland-" + roomKey + "-briefing";
                case OnlineMatchPhase.Action:
                    return IsLocalAlive()
                        ? "gangland-" + roomKey + "-action"
                        : "gangland-" + roomKey + "-ghost";
                case OnlineMatchPhase.Meeting:
                case OnlineMatchPhase.Voting:
                    return "gangland-" + roomKey + "-meeting";
                case OnlineMatchPhase.Result:
                    return "gangland-" + roomKey + "-result";
                default:
                    return string.Empty;
            }
        }

        private Vector3 LocalVoicePosition()
        {
            if (players.TryGetValue(LocalClientId(), out OnlinePlayerState state))
            {
                return state.Position;
            }

            return localPosition;
        }

        private static string StableRoomKey(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                string source = string.IsNullOrWhiteSpace(value) ? "gangland-room" : value;

                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private void OnDestroy()
        {
            if (networkManager == null)
            {
                RequestLeaveVoiceChannels();
                return;
            }

            RequestLeaveVoiceChannels();
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            UnregisterMessages();
        }

        private void StartHost()
        {
            try
            {
                relayJoinCode = string.Empty;
                relayStatus = "使用直连 Host。";
                ConfigureTransport("0.0.0.0");
                RegisterMessages();

                if (networkManager.StartHost())
                {
                    localPreviewMode = false;
                    status = "Host 已创建。等待玩家 Ready。";
                    AddCaseLog(status);
                    UpsertLocalPlayer();
                    SendClientProfile();
                    PlayCue("start");
                    BroadcastSnapshot();
                }
                else
                {
                    StartLocalPreviewRoom();
                    status = "Host 创建失败，已切换本地试玩模式。";
                    AddCaseLog(status);
                }
            }
            catch (Exception exception)
            {
                StartLocalPreviewRoom();
                status = "Host 启动异常，已切换本地试玩模式：" + exception.GetType().Name;
                AddCaseLog(status);
            }
        }

        private void StartClient(string address)
        {
            string safeAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            relayJoinCode = string.Empty;
            relayStatus = "使用直连 Client。";
            ConfigureTransport(safeAddress);
            RegisterMessages();

            if (networkManager.StartClient())
            {
                status = "Client 正在连接 " + safeAddress + "。";
                AddCaseLog(status);
            }
            else
            {
                status = "Client 加入失败。";
            }
        }

        private async void StartRelayHost()
        {
            if (relayOperationInProgress)
            {
                return;
            }

            relayOperationInProgress = true;
            relayStatus = "Relay 正在创建房间码。";
            status = relayStatus;

            try
            {
                EnsureServiceBootstrap();
                EnsureNetworkStack();
                await serviceBootstrap.InitializeAsync();

                if (!CanUseRelay(out string reason))
                {
                    relayStatus = reason;
                    status = reason;
                    AddCaseLog(reason);
                    return;
                }

                int maxConnections = Mathf.Clamp(roomMaxPlayers - 1, 1, MaximumRoomPlayers - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                relayJoinCode = (await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId) ?? string.Empty).Trim().ToUpperInvariant();
                transport.UseWebSockets = false;
                transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
                RegisterMessages();

                if (networkManager.StartHost())
                {
                    localPreviewMode = false;
                    relayStatus = "Relay 房间码: " + relayJoinCode;
                    status = "Relay Host 已创建。分享房间码 " + relayJoinCode + "。";
                    AddCaseLog(status);
                    UpsertLocalPlayer();
                    SendClientProfile();
                    PlayCue("start");
                    BroadcastSnapshot();
                }
                else
                {
                    relayStatus = "Relay Host 启动失败。";
                    status = relayStatus;
                    AddCaseLog(status);
                }
            }
            catch (Exception exception)
            {
                relayJoinCode = string.Empty;
                relayStatus = "Relay 创建失败：" + exception.Message;
                status = relayStatus;
                AddCaseLog(status);
            }
            finally
            {
                relayOperationInProgress = false;
            }
        }

        private async void StartRelayClient()
        {
            if (relayOperationInProgress)
            {
                return;
            }

            string safeJoinCode = (relayJoinInput ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(safeJoinCode))
            {
                relayStatus = "请输入 Relay 房间码。";
                status = relayStatus;
                return;
            }

            relayOperationInProgress = true;
            relayStatus = "Relay 正在加入 " + safeJoinCode + "。";
            status = relayStatus;

            try
            {
                EnsureServiceBootstrap();
                EnsureNetworkStack();
                await serviceBootstrap.InitializeAsync();

                if (!CanUseRelay(out string reason))
                {
                    relayStatus = reason;
                    status = reason;
                    AddCaseLog(reason);
                    return;
                }

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(safeJoinCode);
                relayJoinCode = safeJoinCode;
                transport.UseWebSockets = false;
                transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));
                RegisterMessages();

                if (networkManager.StartClient())
                {
                    relayStatus = "Relay 已发送加入请求: " + safeJoinCode;
                    status = relayStatus;
                    AddCaseLog(status);
                }
                else
                {
                    relayStatus = "Relay Client 启动失败。";
                    status = relayStatus;
                }
            }
            catch (Exception exception)
            {
                relayStatus = "Relay 加入失败：" + exception.Message;
                status = relayStatus;
                AddCaseLog(status);
            }
            finally
            {
                relayOperationInProgress = false;
            }
        }

        private bool CanUseRelay(out string reason)
        {
            if (serviceBootstrap == null)
            {
                reason = "Unity Services 未挂载，Relay 暂不可用。";
                return false;
            }

            if (!serviceBootstrap.CloudProjectBound)
            {
                reason = "Unity Cloud Project 未绑定，Relay 暂不可用。";
                return false;
            }

            if (!serviceBootstrap.ServicesReady || !serviceBootstrap.AuthenticationReady || !serviceBootstrap.RelayReady)
            {
                reason = "Relay 未就绪：" + serviceBootstrap.ServiceReadinessSummary;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void StartLocalPreviewRoom()
        {
            localPreviewMode = true;
            localReady = true;
            localPlayerName = LimitText(localPlayerName, 16, "港区玩家");

            if (!players.ContainsKey(LocalPreviewClientId))
            {
                players[LocalPreviewClientId] = new OnlinePlayerState(LocalPreviewClientId, localPlayerName, FindNearestOpenPosition(localPosition, Vector3.zero), true, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }
            else
            {
                OnlinePlayerState state = players[LocalPreviewClientId];
                state.DisplayName = localPlayerName;
                state.Ready = true;
                state.IsBot = false;
                players[LocalPreviewClientId] = state;
            }

            killCooldowns[LocalPreviewClientId] = 0f;
            abilityCooldowns[LocalPreviewClientId] = 0f;
            status = "本地试玩房间已创建。";
            AddCaseLog(status);
            PlayCue("start");
        }

        private void Shutdown()
        {
            UnregisterMessages();

            if (networkManager != null && networkManager.IsListening)
            {
                try
                {
                    networkManager.Shutdown();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Gangland network shutdown skipped: " + exception.GetType().Name);
                }
            }

            players.Clear();
            bodies.Clear();
            caseLog.Clear();
            votes.Clear();
            privateRoles.Clear();
            killCooldowns.Clear();
            abilityCooldowns.Clear();
            botThinkTimers.Clear();
            botVoteTimers.Clear();
            botTargets.Clear();
            localRole = OnlineRole.Unassigned;
            phase = OnlineMatchPhase.Lobby;
            localPreviewMode = false;
            localReady = false;
            matchStarted = false;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            submittingActiveTask = false;
            evidenceScore = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
            evidenceMilestoneIndex = 0;
            phaseTimer = 0f;
            blackoutTimer = 0f;
            lockdownTimer = 0f;
            communicationJamTimer = 0f;
            evidenceLeakTimer = 0f;
            evidenceLeakAccumulator = 0f;
            patrolAlertTimer = 0f;
            emergencyCooldownTimer = 0f;
            aiActionGraceTimer = 0f;
            currentCameraSubjectId = LocalPreviewClientId;
            desiredVoiceChannel = string.Empty;
            pendingVoiceChannel = string.Empty;
            voiceJoinInProgress = false;
            RequestLeaveVoiceChannels();
            resultSummary = "尚未结算。";
            status = "已离开房间。";
            relayJoinCode = string.Empty;
            relayStatus = "Relay 房间码未创建。";
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
            roomMinPlayers = Mathf.Clamp(value, MinimumRoomPlayers, roomMaxPlayers);

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
            roomMaxPlayers = Mathf.Clamp(value, roomMinPlayers, MaximumRoomPlayers);

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetEvidenceTarget(int value)
        {
            evidenceTarget = Mathf.Clamp(value, 34, 56);

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

            if (IsOnline && IsHost)
            {
                BroadcastSnapshot();
            }
        }

        public void SetLocalReady(bool ready)
        {
            localReady = ready;

            if (IsOnline)
            {
                SendClientState(true);
            }
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

        private void ConfigureTransport(string address)
        {
            transport.UseWebSockets = false;
            transport.UseEncryption = false;
            transport.SetConnectionData(address, DefaultPort);
        }

        private void RegisterMessages()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            UnregisterMessages();
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientStateMessage, ReceiveClientState);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientActionMessage, ReceiveClientAction);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ClientProfileMessage, ReceiveClientProfile);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ServerSnapshotMessage, ReceiveServerSnapshot);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RoleAssignMessage, ReceiveRoleAssign);
        }

        private void UnregisterMessages()
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientStateMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientActionMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ClientProfileMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ServerSnapshotMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RoleAssignMessage);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (networkManager.IsServer)
            {
                Vector3 spawn = SpawnPosition(players.Count);
                players[clientId] = new OnlinePlayerState(clientId, "玩家" + clientId, spawn, false, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
                killCooldowns[clientId] = 0f;
                abilityCooldowns[clientId] = 0f;
                BroadcastSnapshot();
            }

            if (clientId == LocalClientId())
            {
                UpsertLocalPlayer();
                SendClientProfile();
                status = networkManager.IsHost ? "Host 在线。" : "Client 已连接。";
                AddCaseLog(status);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            players.Remove(clientId);
            privateRoles.Remove(clientId);
            votes.Remove(clientId);
            killCooldowns.Remove(clientId);
            abilityCooldowns.Remove(clientId);
            botThinkTimers.Remove(clientId);
            botVoteTimers.Remove(clientId);
            botTargets.Remove(clientId);

            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                if (bodies[i].VictimClientId == clientId)
                {
                    bodies.RemoveAt(i);
                }
            }

            if (networkManager != null && networkManager.IsServer)
            {
                AddCaseLog("玩家" + clientId + " 已离开房间。");
                EvaluateWinConditions();
                BroadcastSnapshot();
            }
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
        }

        private void SendClientAction(OnlineActionType actionType, ulong targetClientId = SkipVoteTarget)
        {
            if (localPreviewMode)
            {
                ApplyClientAction(LocalPreviewClientId, actionType, targetClientId);
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            if (networkManager.IsHost)
            {
                ApplyClientAction(networkManager.LocalClientId, actionType, targetClientId);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(32, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe((int)actionType);
            writer.WriteValueSafe(targetClientId);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientActionMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendClientState(bool force = false)
        {
            if (localPreviewMode)
            {
                ApplyClientState(LocalPreviewClientId, localPosition, localInput, localReady);
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            clientSnapshotTimer -= Time.deltaTime;

            if (!force && clientSnapshotTimer > 0f)
            {
                return;
            }

            clientSnapshotTimer = SnapshotIntervalSeconds;

            if (networkManager.IsHost)
            {
                ApplyClientState(networkManager.LocalClientId, localPosition, localInput, localReady);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(localPosition);
            writer.WriteValueSafe(localInput);
            writer.WriteValueSafe(localReady);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientStateMessage, NetworkManager.ServerClientId, writer);
        }

        private void SendClientProfile()
        {
            if (localPreviewMode)
            {
                ApplyClientProfile(LocalPreviewClientId, LimitText(localPlayerName, 16, "港区玩家"));
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsClient)
            {
                return;
            }

            string safeName = LimitText(localPlayerName, 16, "港区玩家");
            localPlayerName = safeName;

            if (networkManager.IsHost)
            {
                ApplyClientProfile(networkManager.LocalClientId, safeName);
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(safeName);
            networkManager.CustomMessagingManager.SendNamedMessage(ClientProfileMessage, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveClientState(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out Vector3 position);
            reader.ReadValueSafe(out Vector2 input);
            reader.ReadValueSafe(out bool ready);
            ApplyClientState(senderClientId, position, input, ready);
        }

        private void ReceiveClientProfile(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out string displayName);
            ApplyClientProfile(senderClientId, displayName);
        }

        private void ReceiveClientAction(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int actionValue);
            reader.ReadValueSafe(out ulong targetClientId);
            ApplyClientAction(senderClientId, (OnlineActionType)actionValue, targetClientId);
        }

        private void ApplyClientAction(ulong senderClientId, OnlineActionType actionType, ulong targetClientId)
        {
            if ((!localPreviewMode && (networkManager == null || !networkManager.IsServer)) || !players.TryGetValue(senderClientId, out OnlinePlayerState player))
            {
                return;
            }

            if (actionType == OnlineActionType.Vote || actionType == OnlineActionType.SkipVote)
            {
                ApplyVote(senderClientId, actionType == OnlineActionType.SkipVote ? SkipVoteTarget : targetClientId);
                return;
            }

            if (phase == OnlineMatchPhase.Lobby || phase == OnlineMatchPhase.Opening || phase == OnlineMatchPhase.Result || !player.Alive)
            {
                return;
            }

            if (actionType == OnlineActionType.Report)
            {
                TryReportOrEmergency(senderClientId, player);
                return;
            }

            if (phase != OnlineMatchPhase.Action)
            {
                return;
            }

            if (actionType == OnlineActionType.Kill)
            {
                TryKill(senderClientId, player);
                return;
            }

            if (actionType == OnlineActionType.Interact)
            {
                TryInteractWithTask(senderClientId, player);
                return;
            }

            if (actionType == OnlineActionType.Ability)
            {
                TryUseProfessionAbility(senderClientId, player);
            }
        }

        private void ApplyClientState(ulong senderClientId, Vector3 position, Vector2 input, bool ready)
        {
            OnlinePlayerState state = players.TryGetValue(senderClientId, out OnlinePlayerState existing)
                ? existing
                : new OnlinePlayerState(senderClientId, "玩家" + senderClientId, position, ready, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);

            if (!matchStarted || phase == OnlineMatchPhase.Lobby)
            {
                state.Position = ClampToOnlineMap(position);
            }

            state.Input = state.Alive && phase == OnlineMatchPhase.Action ? input : Vector2.zero;
            state.Ready = ready;
            players[senderClientId] = state;
        }

        private void ApplyClientProfile(ulong senderClientId, string displayName)
        {
            string safeName = LimitText(displayName, 16, "港区玩家");

            if (players.TryGetValue(senderClientId, out OnlinePlayerState state))
            {
                state.DisplayName = safeName;
                state.IsBot = false;
                players[senderClientId] = state;
            }
            else
            {
                players[senderClientId] = new OnlinePlayerState(senderClientId, safeName, SpawnPosition(players.Count), false, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }

            status = safeName + " 已进入房间。";
            AddCaseLog(status);
            BroadcastSnapshot();
        }

        private void TickHostSimulation()
        {
            float deltaTime = Time.deltaTime;

            if (blackoutTimer > 0f)
            {
                blackoutTimer = Mathf.Max(0f, blackoutTimer - deltaTime);
            }

            if (lockdownTimer > 0f)
            {
                lockdownTimer = Mathf.Max(0f, lockdownTimer - deltaTime);
            }

            if (communicationJamTimer > 0f)
            {
                communicationJamTimer = Mathf.Max(0f, communicationJamTimer - deltaTime);
            }

            if (evidenceLeakTimer > 0f)
            {
                evidenceLeakTimer = Mathf.Max(0f, evidenceLeakTimer - deltaTime);
                if (phase == OnlineMatchPhase.Action)
                {
                    evidenceLeakAccumulator += deltaTime;

                    if (evidenceLeakAccumulator >= 5f)
                    {
                        evidenceScore = Mathf.Max(0, evidenceScore - 1);
                        evidenceLeakAccumulator = 0f;
                    }
                }
            }
            else
            {
                evidenceLeakAccumulator = 0f;
            }

            if (patrolAlertTimer > 0f)
            {
                patrolAlertTimer = Mathf.Max(0f, patrolAlertTimer - deltaTime);
            }

            if (emergencyCooldownTimer > 0f)
            {
                emergencyCooldownTimer = Mathf.Max(0f, emergencyCooldownTimer - deltaTime);
            }

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
                    phaseTimer = VotingSeconds;
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

                if (matchElapsedSeconds >= MatchHardLimitSeconds)
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
                        float speedMultiplier = lockdownTimer > 0f ? 0.72f : patrolAlertTimer > 0f && GetPrivateRole(clientId) == OnlineRole.Gang ? 0.9f : 1f;
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

            TickCooldowns(deltaTime);

            serverSnapshotTimer -= deltaTime;

            if (serverSnapshotTimer <= 0f)
            {
                serverSnapshotTimer = SnapshotIntervalSeconds;
                BroadcastSnapshot();
            }
        }

        private void BroadcastSnapshot()
        {
            if (localPreviewMode)
            {
                return;
            }

            if (networkManager == null || networkManager.CustomMessagingManager == null || !networkManager.IsServer)
            {
                return;
            }

            if (!networkManager.IsListening && !networkManager.IsClient && !networkManager.IsServer)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(8192, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(matchStarted);
            writer.WriteValueSafe((int)phase);
            writer.WriteValueSafe(evidenceScore);
            writer.WriteValueSafe(evidenceTarget);
            writer.WriteValueSafe(emergencyMeetingsLeft);
            writer.WriteValueSafe(roomMinPlayers);
            writer.WriteValueSafe(roomMaxPlayers);
            writer.WriteValueSafe(roomAutoFillAi);
            writer.WriteValueSafe(revealRoleOnEject);
            writer.WriteValueSafe(proximityVoiceEnabled);
            writer.WriteValueSafe(roomName);
            writer.WriteValueSafe(resultSummary);
            writer.WriteValueSafe(lastMeetingReason);
            writer.WriteValueSafe(lastVoteOutcome);
            writer.WriteValueSafe(lastEvidenceEvent);
            writer.WriteValueSafe(lastSabotageEvent);
            writer.WriteValueSafe(evidenceMilestoneIndex);
            writer.WriteValueSafe(phaseTimer);
            writer.WriteValueSafe(blackoutTimer);
            writer.WriteValueSafe(lockdownTimer);
            writer.WriteValueSafe(communicationJamTimer);
            writer.WriteValueSafe(evidenceLeakTimer);
            writer.WriteValueSafe(patrolAlertTimer);
            writer.WriteValueSafe(emergencyCooldownTimer);
            writer.WriteValueSafe(matchElapsedSeconds);
            writer.WriteValueSafe(players.Count);

            foreach (OnlinePlayerState state in players.Values)
            {
                float killCooldown = killCooldowns.TryGetValue(state.ClientId, out float cooldown) ? cooldown : 0f;
                float abilityCooldown = abilityCooldowns.TryGetValue(state.ClientId, out float abilityCooldownValue) ? abilityCooldownValue : 0f;
                writer.WriteValueSafe(state.ClientId);
                writer.WriteValueSafe(state.DisplayName);
                writer.WriteValueSafe(state.Position);
                writer.WriteValueSafe(state.Ready);
                writer.WriteValueSafe(state.Alive);
                writer.WriteValueSafe(state.IsBot);
                writer.WriteValueSafe((int)state.PublicRole);
                writer.WriteValueSafe((int)state.Profession);
                writer.WriteValueSafe(state.Suspicion);
                writer.WriteValueSafe(killCooldown);
                writer.WriteValueSafe(abilityCooldown);
            }

            writer.WriteValueSafe(tasks.Count);

            foreach (OnlineTaskState task in tasks)
            {
                writer.WriteValueSafe(task.Id);
                writer.WriteValueSafe(task.Position);
                writer.WriteValueSafe(task.Progress);
                writer.WriteValueSafe(task.RequiredProgress);
                writer.WriteValueSafe(task.Completed);
                writer.WriteValueSafe(task.Sabotaged);
            }

            writer.WriteValueSafe(bodies.Count);

            foreach (OnlineBodyState body in bodies)
            {
                writer.WriteValueSafe(body.Id);
                writer.WriteValueSafe(body.VictimClientId);
                writer.WriteValueSafe(body.Position);
                writer.WriteValueSafe(body.Reported);
            }

            writer.WriteValueSafe(votes.Count);

            foreach (KeyValuePair<ulong, ulong> vote in votes)
            {
                writer.WriteValueSafe(vote.Key);
                writer.WriteValueSafe(vote.Value);
            }

            writer.WriteValueSafe(caseLog.Count);

            foreach (string entry in caseLog)
            {
                writer.WriteValueSafe(entry);
            }

            networkManager.CustomMessagingManager.SendNamedMessageToAll(ServerSnapshotMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        private void ReceiveServerSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager != null && networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out bool snapshotMatchStarted);
            reader.ReadValueSafe(out int phaseValue);
            reader.ReadValueSafe(out int snapshotEvidenceScore);
            reader.ReadValueSafe(out int snapshotEvidenceTarget);
            reader.ReadValueSafe(out int snapshotEmergencyMeetingsLeft);
            reader.ReadValueSafe(out int snapshotRoomMinPlayers);
            reader.ReadValueSafe(out int snapshotRoomMaxPlayers);
            reader.ReadValueSafe(out bool snapshotAutoFillAi);
            reader.ReadValueSafe(out bool snapshotRevealRoleOnEject);
            reader.ReadValueSafe(out bool snapshotProximityVoice);
            reader.ReadValueSafe(out string snapshotRoomName);
            reader.ReadValueSafe(out string snapshotResultSummary);
            reader.ReadValueSafe(out string snapshotLastMeetingReason);
            reader.ReadValueSafe(out string snapshotLastVoteOutcome);
            reader.ReadValueSafe(out string snapshotLastEvidenceEvent);
            reader.ReadValueSafe(out string snapshotLastSabotageEvent);
            reader.ReadValueSafe(out int snapshotEvidenceMilestoneIndex);
            reader.ReadValueSafe(out float snapshotPhaseTimer);
            reader.ReadValueSafe(out float snapshotBlackoutTimer);
            reader.ReadValueSafe(out float snapshotLockdownTimer);
            reader.ReadValueSafe(out float snapshotCommunicationJamTimer);
            reader.ReadValueSafe(out float snapshotEvidenceLeakTimer);
            reader.ReadValueSafe(out float snapshotPatrolAlertTimer);
            reader.ReadValueSafe(out float snapshotEmergencyCooldownTimer);
            reader.ReadValueSafe(out float snapshotMatchElapsedSeconds);
            reader.ReadValueSafe(out int count);
            matchStarted = snapshotMatchStarted;
            phase = (OnlineMatchPhase)phaseValue;
            evidenceScore = snapshotEvidenceScore;
            evidenceTarget = snapshotEvidenceTarget;
            emergencyMeetingsLeft = snapshotEmergencyMeetingsLeft;
            roomMinPlayers = snapshotRoomMinPlayers;
            roomMaxPlayers = snapshotRoomMaxPlayers;
            roomAutoFillAi = snapshotAutoFillAi;
            revealRoleOnEject = snapshotRevealRoleOnEject;
            proximityVoiceEnabled = snapshotProximityVoice;
            roomName = snapshotRoomName;
            resultSummary = snapshotResultSummary;
            lastMeetingReason = snapshotLastMeetingReason;
            lastVoteOutcome = snapshotLastVoteOutcome;
            lastEvidenceEvent = snapshotLastEvidenceEvent;
            lastSabotageEvent = snapshotLastSabotageEvent;
            evidenceMilestoneIndex = snapshotEvidenceMilestoneIndex;
            phaseTimer = snapshotPhaseTimer;
            blackoutTimer = snapshotBlackoutTimer;
            lockdownTimer = snapshotLockdownTimer;
            communicationJamTimer = snapshotCommunicationJamTimer;
            evidenceLeakTimer = snapshotEvidenceLeakTimer;
            patrolAlertTimer = snapshotPatrolAlertTimer;
            emergencyCooldownTimer = snapshotEmergencyCooldownTimer;
            matchElapsedSeconds = snapshotMatchElapsedSeconds;
            status = "同步在线局：" + PhaseName(phase) + "。";

            HashSet<ulong> seenPlayers = new HashSet<ulong>();

            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out string displayName);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool ready);
                reader.ReadValueSafe(out bool alive);
                reader.ReadValueSafe(out bool isBot);
                reader.ReadValueSafe(out int roleValue);
                reader.ReadValueSafe(out int professionValue);
                reader.ReadValueSafe(out int suspicion);
                reader.ReadValueSafe(out float killCooldown);
                reader.ReadValueSafe(out float abilityCooldown);

                OnlinePlayerState state = players.TryGetValue(clientId, out OnlinePlayerState existing)
                    ? existing
                    : new OnlinePlayerState(clientId, displayName, position, ready, alive, (OnlineRole)roleValue, (OnlineProfession)professionValue, suspicion, isBot);

                state.DisplayName = displayName;
                state.Position = position;
                state.Ready = ready;
                state.Alive = alive;
                state.IsBot = isBot;
                state.PublicRole = (OnlineRole)roleValue;
                state.Profession = (OnlineProfession)professionValue;
                state.Suspicion = suspicion;
                state.KillCooldown = killCooldown;
                state.AbilityCooldown = abilityCooldown;
                players[clientId] = state;
                seenPlayers.Add(clientId);

                if (clientId == LocalClientId())
                {
                    localPosition = position;
                }
            }

            RemoveMissingPlayers(seenPlayers);

            reader.ReadValueSafe(out int taskCount);
            tasks.Clear();

            for (int i = 0; i < taskCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out int progress);
                reader.ReadValueSafe(out int requiredProgress);
                reader.ReadValueSafe(out bool completed);
                reader.ReadValueSafe(out bool sabotaged);
                tasks.Add(new OnlineTaskState(id, TaskNameFor(id), position, progress, requiredProgress, completed, sabotaged));
            }

            reader.ReadValueSafe(out int bodyCount);
            bodies.Clear();

            for (int i = 0; i < bodyCount; i++)
            {
                reader.ReadValueSafe(out int id);
                reader.ReadValueSafe(out ulong victimClientId);
                reader.ReadValueSafe(out Vector3 position);
                reader.ReadValueSafe(out bool reported);
                bodies.Add(new OnlineBodyState(id, victimClientId, position, reported));
            }

            reader.ReadValueSafe(out int voteCount);
            votes.Clear();

            for (int i = 0; i < voteCount; i++)
            {
                reader.ReadValueSafe(out ulong voterClientId);
                reader.ReadValueSafe(out ulong targetClientId);
                votes[voterClientId] = targetClientId;
            }

            reader.ReadValueSafe(out int caseLogCount);
            caseLog.Clear();

            for (int i = 0; i < caseLogCount; i++)
            {
                reader.ReadValueSafe(out string entry);
                caseLog.Add(entry);
            }
        }

        private void StartOnlineMatch()
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
            if (roomAutoFillAi && players.Count < roomMinPlayers)
            {
                EnsureMinimumBots();
            }

            BuildDefaultTasks();
            bodies.Clear();
            votes.Clear();
            caseLog.Clear();
            privateRoles.Clear();
            killCooldowns.Clear();
            abilityCooldowns.Clear();
            botVoteTimers.Clear();
            nextBodyId = 0;
            activeTaskId = -1;
            activeTaskStep = 0;
            activeTaskCharge = 0f;
            activeTaskFeedbackTimer = 0f;
            activeTaskFeedbackPositive = false;
            submittingActiveTask = false;
            evidenceScore = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "专案启动，证据链待闭合。";
            lastSabotageEvent = "暂未发现破坏。";
            evidenceMilestoneIndex = 0;
            phaseTimer = 0f;
            blackoutTimer = 0f;
            lockdownTimer = 0f;
            communicationJamTimer = 0f;
            evidenceLeakTimer = 0f;
            evidenceLeakAccumulator = 0f;
            patrolAlertTimer = 0f;
            emergencyCooldownTimer = 0f;
            emergencyMeetingsLeft = EmergencyMeetingLimitFor(players.Count);
            aiActionGraceTimer = AiActionGraceSeconds;
            currentCameraSubjectId = LocalPreviewClientId;
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
                state.Position = FindNearestOpenPosition(SpawnPosition(i), Vector3.zero);
                state.Input = Vector2.zero;
                state.Alive = true;
                state.PublicRole = OnlineRole.Unassigned;
                state.KillCooldown = 0f;
                state.AbilityCooldown = 0f;
                state.Suspicion = 0;
                state.Ready = true;
                players[ids[i]] = state;
                killCooldowns[ids[i]] = 0f;
                abilityCooldowns[ids[i]] = 0f;
                botTargets[ids[i]] = PickBotTarget(ids[i]);
                botThinkTimers[ids[i]] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
            }

            AssignRoles(ids);
            status = "专案开始：身份已私发，准备进入九龙港城。";
            AddCaseLog(status);
            PlayCue("start");

            if (broadcast)
            {
                BroadcastSnapshot();
            }
        }

        private void AssignRoles(IList<ulong> ids)
        {
            List<ulong> shuffled = new List<ulong>(ids);
            Shuffle(shuffled);

            for (int i = 0; i < shuffled.Count; i++)
            {
                OnlineRole role = OnlineRole.Police;

                if (i == 0 && shuffled.Count >= 2)
                {
                    role = OnlineRole.Gang;
                }
                else if (i == 1 && shuffled.Count >= 5)
                {
                    role = OnlineRole.Undercover;
                }
                else if (i == 2 && shuffled.Count >= 8)
                {
                    role = OnlineRole.Gang;
                }

                ulong clientId = shuffled[i];
                privateRoles[clientId] = role;
                if (players.TryGetValue(clientId, out OnlinePlayerState state))
                {
                    state.Profession = ProfessionFor(role, i);
                    state.Suspicion = role == OnlineRole.Gang ? 1 : 0;
                    players[clientId] = state;
                }

                SendRole(clientId, role);
            }
        }

        private void FillBotsAndStart()
        {
            if ((!localPreviewMode && (networkManager == null || !networkManager.IsServer)) || phase != OnlineMatchPhase.Lobby)
            {
                return;
            }

            EnsureMinimumBots();
            StartOnlineMatch();
        }

        private void EnsureMinimumBots()
        {
            int targetCount = Mathf.Clamp(roomMinPlayers, MinimumPlayablePlayers, roomMaxPlayers);
            int index = 0;

            while (players.Count < targetCount)
            {
                ulong clientId = BotClientIdBase + (ulong)index;
                index++;

                if (players.ContainsKey(clientId))
                {
                    continue;
                }

                string displayName = BotName(index);
                Vector3 spawn = SpawnPosition(players.Count);
                players[clientId] = new OnlinePlayerState(clientId, displayName, spawn, true, true, OnlineRole.Unassigned, BotProfession(index), 0, true);
                killCooldowns[clientId] = 0f;
                abilityCooldowns[clientId] = 0f;
                botThinkTimers[clientId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
                botTargets[clientId] = PickBotTarget(clientId);
            }

            status = "已补齐 AI 玩家，可直接开始完整本地局。";
            BroadcastSnapshot();
        }

        private void SendRole(ulong clientId, OnlineRole role)
        {
            if (clientId == LocalClientId())
            {
                localRole = role;
                status = "收到身份：" + RoleName(localRole);
            }

            if (localPreviewMode || IsBotClient(clientId) || networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using FastBufferWriter writer = new FastBufferWriter(16, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe((int)role);
            networkManager.CustomMessagingManager.SendNamedMessage(RoleAssignMessage, clientId, writer);
        }

        private void ReceiveRoleAssign(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int roleValue);
            localRole = (OnlineRole)roleValue;
            status = "收到身份：" + RoleName(localRole);
        }

        private void UpsertLocalPlayer()
        {
            if (localPreviewMode)
            {
                StartLocalPreviewRoom();
                return;
            }

            if (networkManager == null || !networkManager.IsClient)
            {
                return;
            }

            ulong clientId = LocalClientId();

            if (players.TryGetValue(clientId, out OnlinePlayerState existing))
            {
                existing.Position = localPosition;
                existing.Ready = localReady;
                existing.IsBot = false;
                existing.DisplayName = LimitText(localPlayerName, 16, "港区玩家");
                players[clientId] = existing;
            }
            else
            {
                players[clientId] = new OnlinePlayerState(clientId, LimitText(localPlayerName, 16, "港区玩家"), localPosition, localReady, true, OnlineRole.Unassigned, OnlineProfession.Inspector, 0, false);
            }
        }

        private void TryInteractWithTask(ulong senderClientId, OnlinePlayerState player)
        {
            OnlineTaskState nearestTask = FindNearestTask(player.Position);

            if (nearestTask.Id < 0)
            {
                status = "附近没有可互动任务。";
                BroadcastSnapshot();
                return;
            }

            OnlineRole role = GetPrivateRole(senderClientId);

            if (role == OnlineRole.Gang)
            {
                SabotageType sabotageType = SabotageForTask(nearestTask.Id);
                nearestTask.Sabotaged = true;
                nearestTask.Completed = false;
                nearestTask.Progress = Mathf.Max(0, nearestTask.Progress - 1);
                evidenceScore = Mathf.Max(0, evidenceScore - SabotageEvidencePenalty(sabotageType));
                status = "黑帮破坏了 " + nearestTask.Name + "。";
                lastSabotageEvent = status + " 影响: " + SabotageName(sabotageType);
                AddCaseLog(status);
                ApplySabotageEffect(sabotageType, nearestTask.Name);
            }
            else
            {
                if (senderClientId == LocalClientId() && !player.IsBot && !submittingActiveTask)
                {
                    BeginActiveTask(nearestTask.Id);
                    return;
                }

                if (nearestTask.Sabotaged)
                {
                    nearestTask.Sabotaged = false;
                    RepairSabotageEffect(SabotageForTask(nearestTask.Id));
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
                        evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + EvidenceGainFor(nearestTask.Id, player.Profession, role));
                        player.Suspicion = Mathf.Max(0, player.Suspicion - 1);
                        players[senderClientId] = player;
                        status = nearestTask.Name + " 完成，证据链推进。";
                        lastEvidenceEvent = status + " 当前 " + evidenceScore + "/" + evidenceTarget;
                        UpdateEvidenceMilestone();
                        AddCaseLog(status);
                        PlayCue("task");
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
            status = "开始处理任务：" + task.Name + "。";
            AddCaseLog(status);
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

            if (role == OnlineRole.Gang)
            {
                return false;
            }

            OnlineTaskState nearestTask = FindNearestTask(localState.Position);
            return nearestTask.Id >= 0 && (!nearestTask.Completed || nearestTask.Sabotaged);
        }

        private void TryKill(ulong senderClientId, OnlinePlayerState player)
        {
            if (GetPrivateRole(senderClientId) != OnlineRole.Gang)
            {
                status = "只有黑帮可以击倒目标。";
                BroadcastSnapshot();
                return;
            }

            if (killCooldowns.TryGetValue(senderClientId, out float cooldown) && cooldown > 0f)
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
            bodies.Add(new OnlineBodyState(nextBodyId++, victimClientId, victim.Position, false));
            killCooldowns[senderClientId] = KillCooldownSeconds;
            player.Suspicion += 2;
            players[senderClientId] = player;
            status = "黑帮击倒了 " + victim.DisplayName + "。";
            AddCaseLog(status);
            PlayCue("kill");
            EvaluateWinConditions();
            BroadcastSnapshot();
        }

        private void TryUseProfessionAbility(ulong senderClientId, OnlinePlayerState player)
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
                    evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + 1);
                    status = player.DisplayName + " 快速鉴证，证据链 +1。";
                    lastEvidenceEvent = status + " 当前 " + evidenceScore + "/" + evidenceTarget;
                    UpdateEvidenceMilestone();
                    break;
                case OnlineProfession.Tech:
                    blackoutTimer = 0f;
                    RepairSabotagedTasks(1);
                    status = player.DisplayName + " 重启监控和电闸，解除一次破坏。";
                    break;
                case OnlineProfession.UndercoverAgent:
                    evidenceScore = Mathf.Min(evidenceTarget, evidenceScore + 2);
                    player.Suspicion += 2;
                    status = player.DisplayName + " 秘密上传线报，证据链 +2 但暴露风险上升。";
                    lastEvidenceEvent = status + " 当前 " + evidenceScore + "/" + evidenceTarget;
                    UpdateEvidenceMilestone();
                    break;
                case OnlineProfession.Enforcer:
                    if (role == OnlineRole.Gang)
                    {
                        killCooldowns[senderClientId] = Mathf.Max(0f, killCooldowns.TryGetValue(senderClientId, out float killCooldown) ? killCooldown - 9f : 0f);
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
                    evidenceScore = Mathf.Max(0, evidenceScore - 1);
                    status = player.DisplayName + " 篡改现场，修复表象但证据链被污染。";
                    break;
                case OnlineProfession.Driver:
                    if (role == OnlineRole.Gang && TryUseUnderworldPassage(ref player))
                    {
                        player.Suspicion += 1;
                        status = player.DisplayName + " 通过暗线通道换位。";
                    }
                    else
                    {
                        player.Position = FindNearestOpenPosition(player.Position + new Vector3(UnityEngine.Random.Range(-2.4f, 2.4f), UnityEngine.Random.Range(-1.8f, 1.8f), 0f), player.Position);
                        player.Suspicion += role == OnlineRole.Gang ? 1 : 0;
                        status = player.DisplayName + " 走后巷快速换位。";
                    }

                    break;
                default:
                    if (role == OnlineRole.Gang && TryUseUnderworldPassage(ref player))
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

            abilityCooldowns[senderClientId] = AbilityCooldownSeconds;
            player.AbilityCooldown = AbilityCooldownSeconds;
            players[senderClientId] = player;
            AddCaseLog(status);
            PlayCue("ability");
            EvaluateWinConditions();
            BroadcastSnapshot();
        }

        private void TryReportOrEmergency(ulong senderClientId, OnlinePlayerState player)
        {
            if (TryFindNearestBody(player.Position, out int bodyIndex))
            {
                OnlineBodyState body = bodies[bodyIndex];
                body.Reported = true;
                bodies[bodyIndex] = body;
                BeginMeeting(player.DisplayName + " 发现尸体并报案");
                BroadcastSnapshot();
                return;
            }

            if (communicationJamTimer > 0f)
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

            if (Vector3.Distance(player.Position, ScaleMapPosition(Vector3.zero)) <= ReportRange)
            {
                emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
                emergencyCooldownTimer = EmergencyCooldownSeconds;
                BeginMeeting(player.DisplayName + " 按下警署紧急铃");
                BroadcastSnapshot();
                return;
            }

            status = "附近没有尸体，也不在紧急铃范围内。";
            BroadcastSnapshot();
        }

        private void BeginMeeting(string reason)
        {
            phase = OnlineMatchPhase.Meeting;
            phaseTimer = MeetingIntroSeconds;
            blackoutTimer = 0f;
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
            PlayCue("meeting");
        }

        private Vector3 MeetingSeatPositionFor(ulong clientId)
        {
            List<ulong> seatedIds = new List<ulong>(players.Keys);
            seatedIds.Sort();

            int seatIndex = Mathf.Max(0, seatedIds.IndexOf(clientId));
            int seatCount = Mathf.Clamp(seatedIds.Count, MinimumPlayablePlayers, MaximumRoomPlayers);
            float angle = seatIndex / (float)seatCount * Mathf.PI * 2f + Mathf.PI * 0.5f;
            Vector3 designSeat = new Vector3(Mathf.Cos(angle) * 1.18f, -0.35f + Mathf.Sin(angle) * 0.78f, 0f);
            return FindNearestOpenPosition(ScaleMapPosition(designSeat), ScaleMapPosition(Vector3.zero));
        }

        private void ApplyVote(ulong voterClientId, ulong targetClientId)
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
            status = voter.DisplayName + (targetClientId == SkipVoteTarget ? " 已投票跳过。" : " 已投票给 " + players[targetClientId].DisplayName + "。");
            AddCaseLog(status);

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
                RemoveReportedBodies();
                phase = OnlineMatchPhase.Action;
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
            }

            RemoveReportedBodies();
            EvaluateWinConditions();

            if (phase != OnlineMatchPhase.Result)
            {
                phase = OnlineMatchPhase.Action;
            }

            BroadcastSnapshot();
        }

        private void EvaluateWinConditions()
        {
            if (!matchStarted || phase == OnlineMatchPhase.Result)
            {
                return;
            }

            UpdateEvidenceMilestone();

            if (evidenceScore >= evidenceTarget)
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

        private void ResolveTimeLimitOutcome()
        {
            if (!matchStarted || phase == OnlineMatchPhase.Result)
            {
                return;
            }

            if (evidenceScore >= Mathf.CeilToInt(evidenceTarget * 0.82f) || CountCompletedTasks() >= Mathf.CeilToInt(tasks.Count * 0.72f))
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
            blackoutTimer = 0f;
            lockdownTimer = 0f;
            communicationJamTimer = 0f;
            evidenceLeakTimer = 0f;
            evidenceLeakAccumulator = 0f;
            patrolAlertTimer = 0f;
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
            resultSummary = BuildResultSummary(resultStatus);
            AddCaseLog(status);
            PlayCue("result");

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
            int milestone = EvidenceMilestoneFor(evidenceScore, evidenceTarget);

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
            List<ulong> keys = new List<ulong>(killCooldowns.Keys);

            foreach (ulong clientId in keys)
            {
                killCooldowns[clientId] = Mathf.Max(0f, killCooldowns[clientId] - deltaTime);

                if (players.TryGetValue(clientId, out OnlinePlayerState state))
                {
                    state.KillCooldown = killCooldowns[clientId];
                    players[clientId] = state;
                }
            }

            keys = new List<ulong>(abilityCooldowns.Keys);

            foreach (ulong clientId in keys)
            {
                abilityCooldowns[clientId] = Mathf.Max(0f, abilityCooldowns[clientId] - deltaTime);

                if (players.TryGetValue(clientId, out OnlinePlayerState state))
                {
                    state.AbilityCooldown = abilityCooldowns[clientId];
                    players[clientId] = state;
                }
            }
        }

        private void TickBotAction(float deltaTime)
        {
            List<ulong> botIds = new List<ulong>();

            foreach (OnlinePlayerState state in players.Values)
            {
                if (state.IsBot && state.Alive)
                {
                    botIds.Add(state.ClientId);
                }
            }

            foreach (ulong botId in botIds)
            {
                OnlinePlayerState bot = players[botId];
                botThinkTimers[botId] = botThinkTimers.TryGetValue(botId, out float timer) ? timer - deltaTime : 0f;

                if (TryFindNearestBody(bot.Position, out int bodyIndex) && UnityEngine.Random.value < 0.45f)
                {
                    OnlineBodyState body = bodies[bodyIndex];
                    body.Reported = true;
                    bodies[bodyIndex] = body;
                    players[botId] = bot;
                    BeginMeeting(bot.DisplayName + " 发现尸体并报案");
                    return;
                }

                OnlineRole role = GetPrivateRole(botId);

                if (botThinkTimers[botId] <= 0f)
                {
                    botThinkTimers[botId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);

                    if (role == OnlineRole.Gang)
                    {
                        if (UnityEngine.Random.value < 0.08f)
                        {
                            TryUseProfessionAbility(botId, bot);
                            return;
                        }

                        if (killCooldowns.TryGetValue(botId, out float cooldown) && cooldown <= 0f && TryFindNearestVictim(bot.Position, out ulong victimClientId, out OnlinePlayerState victim))
                        {
                            victim.Alive = false;
                            victim.Input = Vector2.zero;
                            players[victimClientId] = victim;
                            bodies.Add(new OnlineBodyState(nextBodyId++, victimClientId, victim.Position, false));
                            killCooldowns[botId] = KillCooldownSeconds;
                            status = bot.DisplayName + " 在黑灯巷口击倒了 " + victim.DisplayName + "。";
                            AddCaseLog(status);
                            EvaluateWinConditions();
                            BroadcastSnapshot();
                            return;
                        }

                        if (UnityEngine.Random.value < 0.36f)
                        {
                            botTargets[botId] = PickSabotageTarget();
                        }
                    }
                    else if (UnityEngine.Random.value < 0.2f && communicationJamTimer <= 0f && emergencyMeetingsLeft > 0 && Vector3.Distance(bot.Position, ScaleMapPosition(Vector3.zero)) <= ReportRange)
                    {
                        emergencyMeetingsLeft = Mathf.Max(0, emergencyMeetingsLeft - 1);
                        emergencyCooldownTimer = EmergencyCooldownSeconds;
                        BeginMeeting(bot.DisplayName + " 按下警署紧急铃");
                        BroadcastSnapshot();
                        return;
                    }
                    else
                    {
                        if (UnityEngine.Random.value < 0.1f)
                        {
                            TryUseProfessionAbility(botId, bot);
                            return;
                        }

                        botTargets[botId] = PickEvidenceTarget();
                    }
                }

                Vector3 target = botTargets.TryGetValue(botId, out Vector3 currentTarget) ? currentTarget : PickBotTarget(botId);
                Vector3 delta = target - bot.Position;
                Vector2 direction = new Vector2(delta.x, delta.y);

                if (direction.magnitude <= BotInteractDistance)
                {
                    bot.Input = Vector2.zero;
                    players[botId] = bot;

                    if (role == OnlineRole.Gang || UnityEngine.Random.value < 0.76f)
                    {
                        TryInteractWithTask(botId, bot);
                    }

                    botTargets[botId] = PickBotTarget(botId);
                    botThinkTimers[botId] = UnityEngine.Random.Range(BotThinkMinSeconds, BotThinkMaxSeconds);
                }
                else
                {
                    bot.Input = direction.normalized;
                    players[botId] = bot;
                }
            }
        }

        private void TickBotVoting(float deltaTime)
        {
            List<ulong> botIds = new List<ulong>();

            foreach (OnlinePlayerState state in players.Values)
            {
                if (state.IsBot && state.Alive && !votes.ContainsKey(state.ClientId))
                {
                    botIds.Add(state.ClientId);
                }
            }

            foreach (ulong botId in botIds)
            {
                botVoteTimers[botId] = botVoteTimers.TryGetValue(botId, out float timer)
                    ? timer - deltaTime
                    : UnityEngine.Random.Range(1.2f, 4.5f);

                if (botVoteTimers[botId] > 0f)
                {
                    continue;
                }

                ApplyVote(botId, PickBotVoteTarget(botId));
                botVoteTimers[botId] = UnityEngine.Random.Range(2f, 5f);
            }
        }

        private void RemoveReportedBodies()
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                if (bodies[i].Reported)
                {
                    bodies.RemoveAt(i);
                }
            }
        }

        private void AddCaseLog(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            caseLog.Add(entry);

            while (caseLog.Count > MaxCaseLogEntries)
            {
                caseLog.RemoveAt(0);
            }
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
                botThinkTimers.Remove(clientId);
                botVoteTimers.Remove(clientId);
                botTargets.Remove(clientId);
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
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    blackoutTimer = BlackoutSeconds;
                    status = "黑帮切断电闸，港区进入黑灯。";
                    AddCaseLog(status);
                    PlayCue("blackout");
                    break;
                case SabotageType.Lockdown:
                    lockdownTimer = LockdownSeconds;
                    status = taskName + " 引发门禁封锁，部分路线被迫绕行。";
                    AddCaseLog(status);
                    break;
                case SabotageType.Communications:
                    communicationJamTimer = CommunicationJamSeconds;
                    status = taskName + " 被干扰，紧急会议暂时无法呼叫。";
                    AddCaseLog(status);
                    break;
                case SabotageType.EvidenceLeak:
                    evidenceLeakTimer = EvidenceLeakSeconds;
                    status = taskName + " 泄露证据，证据链持续受损。";
                    AddCaseLog(status);
                    break;
                case SabotageType.PatrolAlert:
                    patrolAlertTimer = PatrolAlertSeconds;
                    MarkNearbyGangSuspicion(ScaleMapPosition(Vector3.zero), 1);
                    status = taskName + " 触发巡逻警戒，靠近指挥区的嫌疑上升。";
                    AddCaseLog(status);
                    break;
            }
        }

        private void RepairSabotageEffect(SabotageType sabotageType)
        {
            switch (sabotageType)
            {
                case SabotageType.Blackout:
                    blackoutTimer = 0f;
                    break;
                case SabotageType.Lockdown:
                    lockdownTimer = 0f;
                    break;
                case SabotageType.Communications:
                    communicationJamTimer = 0f;
                    break;
                case SabotageType.EvidenceLeak:
                    evidenceLeakTimer = 0f;
                    break;
                case SabotageType.PatrolAlert:
                    patrolAlertTimer = 0f;
                    break;
            }
        }

        private bool TryUseUnderworldPassage(ref OnlinePlayerState player)
        {
            Vector3 current = player.Position;

            for (int i = 0; i < UnderworldPassageCount; i++)
            {
                Vector3 node = UnderworldPassagePosition(i);

                if (Vector3.Distance(current, node) > UnderworldTransitRange)
                {
                    continue;
                }

                int exitIndex = (i + 2) % UnderworldPassageCount;
                Vector3 exit = UnderworldPassagePosition(exitIndex);
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.32f, 0.32f), UnityEngine.Random.Range(-0.24f, 0.24f), 0f);
                player.Position = FindNearestOpenPosition(exit + offset, exit);
                return true;
            }

            return false;
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

        private bool TryFindNearestVictim(Vector3 position, out ulong victimClientId, out OnlinePlayerState victim)
        {
            victimClientId = SkipVoteTarget;
            victim = default;
            float bestDistance = KillRange;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                OnlinePlayerState candidate = pair.Value;

                if (!candidate.Alive || GetPrivateRole(pair.Key) == OnlineRole.Gang)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, candidate.Position);

                if (distance <= bestDistance)
                {
                    victimClientId = pair.Key;
                    victim = candidate;
                    bestDistance = distance;
                }
            }

            return victimClientId != SkipVoteTarget;
        }

        private bool TryFindNearestBody(Vector3 position, out int bodyIndex)
        {
            bodyIndex = -1;
            float bestDistance = ReportRange;

            for (int i = 0; i < bodies.Count; i++)
            {
                OnlineBodyState body = bodies[i];

                if (body.Reported)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, body.Position);

                if (distance <= bestDistance)
                {
                    bodyIndex = i;
                    bestDistance = distance;
                }
            }

            return bodyIndex >= 0;
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

        private OnlineRole GetPrivateRole(ulong clientId)
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

        private int CountBotPlayers()
        {
            int count = 0;

            foreach (OnlinePlayerState state in players.Values)
            {
                if (state.IsBot)
                {
                    count++;
                }
            }

            return count;
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

            if (role == OnlineRole.Undercover || profession == OnlineProfession.UndercoverAgent)
            {
                gain++;
            }

            return Mathf.Clamp(gain, 1, 4);
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

        private string BuildResultSummary(string resultStatus)
        {
            int alive = CountAlivePlayers();
            int completedTasks = 0;
            int sabotageCount = 0;

            foreach (OnlineTaskState task in tasks)
            {
                if (task.Completed)
                {
                    completedTasks++;
                }

                if (task.Sabotaged)
                {
                    sabotageCount++;
                }
            }

            return resultStatus
                + "\n用时 " + FormatMatchTime(matchElapsedSeconds)
                + " | 存活 " + alive + "/" + players.Count
                + " | 完成任务 " + completedTasks + "/" + tasks.Count
                + " | 破坏残留 " + sabotageCount
                + " | 尸体 " + bodies.Count
                + "\n可直接重开同房间，保留玩家与规则配置。";
        }

        private static string FormatMatchTime(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
        }

        private Vector3 PickBotTarget(ulong botId)
        {
            if (GetPrivateRole(botId) == OnlineRole.Gang)
            {
                return UnityEngine.Random.value < 0.55f ? PickNearestLivingNonGang(botId) : PickSabotageTarget();
            }

            return PickEvidenceTarget();
        }

        private Vector3 PickEvidenceTarget()
        {
            List<OnlineTaskState> options = new List<OnlineTaskState>();

            foreach (OnlineTaskState task in tasks)
            {
                if (!task.Completed || task.Sabotaged)
                {
                    options.Add(task);
                }
            }

            if (options.Count == 0)
            {
                return ScaleMapPosition(Vector3.zero);
            }

            return options[UnityEngine.Random.Range(0, options.Count)].Position;
        }

        private Vector3 PickSabotageTarget()
        {
            if (tasks.Count == 0)
            {
                return ScaleMapPosition(Vector3.zero);
            }

            if (UnityEngine.Random.value < 0.4f)
            {
                foreach (OnlineTaskState task in tasks)
                {
                    if (task.Id == 2)
                    {
                        return task.Position;
                    }
                }
            }

            return tasks[UnityEngine.Random.Range(0, tasks.Count)].Position;
        }

        private Vector3 PickNearestLivingNonGang(ulong botId)
        {
            if (!players.TryGetValue(botId, out OnlinePlayerState bot))
            {
                return PickSabotageTarget();
            }

            Vector3 best = PickSabotageTarget();
            float bestDistance = float.MaxValue;

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (!pair.Value.Alive || pair.Key == botId || GetPrivateRole(pair.Key) == OnlineRole.Gang)
                {
                    continue;
                }

                float distance = Vector3.Distance(bot.Position, pair.Value.Position);

                if (distance < bestDistance)
                {
                    best = pair.Value.Position;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private ulong PickBotVoteTarget(ulong voterClientId)
        {
            List<ulong> suspects = new List<ulong>();
            OnlineRole voterRole = GetPrivateRole(voterClientId);

            foreach (KeyValuePair<ulong, OnlinePlayerState> pair in players)
            {
                if (!pair.Value.Alive || pair.Key == voterClientId)
                {
                    continue;
                }

                OnlineRole targetRole = GetPrivateRole(pair.Key);

                if (voterRole == OnlineRole.Gang && targetRole != OnlineRole.Gang)
                {
                    suspects.Add(pair.Key);
                }
                else if (voterRole != OnlineRole.Gang && targetRole == OnlineRole.Gang && UnityEngine.Random.value < 0.62f)
                {
                    suspects.Add(pair.Key);
                }
                else if (UnityEngine.Random.value < 0.28f)
                {
                    suspects.Add(pair.Key);
                }
            }

            if (suspects.Count == 0 || UnityEngine.Random.value < 0.18f)
            {
                return SkipVoteTarget;
            }

            return suspects[UnityEngine.Random.Range(0, suspects.Count)];
        }

        private static bool IsBotClient(ulong clientId)
        {
            return clientId >= BotClientIdBase;
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

        private static int EmergencyMeetingLimitFor(int playerCount)
        {
            return Mathf.Clamp(playerCount / 3, 1, 3);
        }

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

        private string BuildTaskList()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("港区任务 | 调查组推进任务，黑帮可伪装靠近并破坏");
            builder.AppendLine("局时: " + FormatMatchTime(matchElapsedSeconds) + "/20:00");
            builder.AppendLine("证据链: " + evidenceScore + "/" + evidenceTarget);
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

            foreach (OnlineBodyState body in bodies)
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

        private string BuildFocusedIntel()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("局时 " + FormatMatchTime(matchElapsedSeconds) + "/20:00 | 证据链 " + evidenceScore + "/" + evidenceTarget + " | 会议 " + emergencyMeetingsLeft);
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

        private void DrawModePillars()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("完整局结构");
            GUILayout.Label("开局: 房间 Ready、身份私发、全图预览。");
            GUILayout.Label("行动: 大地图巡场、小任务、黑帮破坏、暗线换位、尸体报案。");
            GUILayout.Label("会议: 全员语音讨论、投票放逐、结算身份和胜负。");
            GUILayout.EndVertical();
        }

        private int CountUnreportedBodies()
        {
            int activeBodies = 0;

            foreach (OnlineBodyState body in bodies)
            {
                if (!body.Reported)
                {
                    activeBodies++;
                }
            }

            return activeBodies;
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

        private string BuildHazardSummary()
        {
            List<string> hazards = new List<string>();

            if (blackoutTimer > 0f)
            {
                hazards.Add("黑灯 " + Mathf.CeilToInt(blackoutTimer));
            }

            if (lockdownTimer > 0f)
            {
                hazards.Add("封锁 " + Mathf.CeilToInt(lockdownTimer));
            }

            if (communicationJamTimer > 0f)
            {
                hazards.Add("断讯 " + Mathf.CeilToInt(communicationJamTimer));
            }

            if (evidenceLeakTimer > 0f)
            {
                hazards.Add("泄证 " + Mathf.CeilToInt(evidenceLeakTimer));
            }

            if (patrolAlertTimer > 0f)
            {
                hazards.Add("巡逻 " + Mathf.CeilToInt(patrolAlertTimer));
            }

            return hazards.Count == 0 ? "无" : string.Join(" / ", hazards);
        }

        private string BuildMatchPressureSummary()
        {
            float evidenceRatio = evidenceScore / (float)Mathf.Max(1, evidenceTarget);
            float taskRatio = CountCompletedTasks() / (float)Mathf.Max(1, tasks.Count);
            float timeRatio = Mathf.Clamp01(matchElapsedSeconds / MatchHardLimitSeconds);
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

            OnlineTaskState recommended = FindRecommendedTask(LocalCameraTarget());
            return "完成任务、报案、投出黑帮；当前推荐 " + recommended.Name + "。";
        }

        private string BuildNextEvidenceMilestoneHint()
        {
            int nextMilestone = Mathf.Clamp(evidenceMilestoneIndex + 1, 1, 4);
            float targetRatio = nextMilestone == 1 ? 0.25f : nextMilestone == 2 ? 0.5f : nextMilestone == 3 ? 0.75f : 1f;
            int nextScore = Mathf.CeilToInt(evidenceTarget * targetRatio);

            if (evidenceScore >= evidenceTarget)
            {
                return "证据已闭合";
            }

            return "下阶段还差 " + Mathf.Max(0, nextScore - evidenceScore) + " 证";
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

            if (Vector3.Distance(localState.Position, ScaleMapPosition(Vector3.zero)) <= ReportRange)
            {
                return "你在紧急铃旁，剩余会议 " + emergencyMeetingsLeft + "，断讯/冷却会阻止开会。";
            }

            OnlineTaskState target = FindRecommendedTask(localState.Position);
            return "推荐前往: " + target.Name + " | 距离 " + Vector3.Distance(localState.Position, target.Position).ToString("F1");
        }

        private string BuildVoiceHudLine()
        {
            if (serviceBootstrap == null)
            {
                return "语音: Vivox 未挂载";
            }

            string mode = phase == OnlineMatchPhase.Action
                ? proximityVoiceEnabled && IsLocalAlive() ? "行动近距离" : "行动频道"
                : phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting ? "会议全员" : "房间语音";
            string channel = string.IsNullOrWhiteSpace(serviceBootstrap.ActiveVoiceChannel) ? "连接中" : serviceBootstrap.ActiveVoiceChannel;
            return "语音: " + mode + " | " + channel + " | " + serviceBootstrap.VoiceStatus;
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

        private static bool IsNearUnderworldPassage(Vector3 position)
        {
            for (int i = 0; i < UnderworldPassageCount; i++)
            {
                if (Vector3.Distance(position, UnderworldPassagePosition(i)) <= UnderworldTransitRange)
                {
                    return true;
                }
            }

            return false;
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
            builder.AppendLine("服务: Unity Services 初始化/匿名登录/Vivox 准备，等待 Cloud Project 绑定后启用");
            builder.AppendLine("音频: 运行时生成提示音，覆盖开局、任务、破坏、击倒、会议、投票、结算");
            builder.AppendLine("资源: " + CommercialArtAdapterCount + " 个资源适配层 | " + LargePortVistaCount + " 个大场景港区层");
            builder.AppendLine("大厅: " + BuildLobbyReadinessSummary());
            builder.AppendLine(BuildPhaseRoadmap());
            return builder.ToString();
        }

        private string BuildServiceStatus()
        {
            if (serviceBootstrap == null)
            {
                return "未挂载。";
            }

            string player = string.IsNullOrEmpty(serviceBootstrap.PlayerId) ? string.Empty : " | Player " + serviceBootstrap.PlayerId;
            return serviceBootstrap.ServiceReadinessSummary + player;
        }

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

        private void DrawRoomSettings()
        {
            GUILayout.Space(8f);
            GUILayout.Label("房间设置");
            roomName = LimitText(GUILayout.TextField(roomName), 20, "九龙港区夜局");
            GUILayout.BeginHorizontal();
            GUILayout.Label("最少人数 " + roomMinPlayers, GUILayout.Width(110f));
            roomMinPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(roomMinPlayers, MinimumRoomPlayers, MaximumRoomPlayers));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("最大人数 " + roomMaxPlayers, GUILayout.Width(110f));
            roomMaxPlayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(roomMaxPlayers, roomMinPlayers, MaximumRoomPlayers));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("证据目标 " + evidenceTarget, GUILayout.Width(110f));
            evidenceTarget = Mathf.RoundToInt(GUILayout.HorizontalSlider(evidenceTarget, 34, 56));
            GUILayout.EndHorizontal();
            GUILayout.Label("目标局长: " + TargetMatchMinutesMin + "-" + TargetMatchMinutesMax + " 分钟 | 击倒冷却 " + Mathf.RoundToInt(KillCooldownSeconds) + "s | 会议 " + Mathf.RoundToInt(MeetingIntroSeconds + VotingSeconds) + "s");
            roomAutoFillAi = GUILayout.Toggle(roomAutoFillAi, "人数不足时 AI 补位");
            revealRoleOnEject = GUILayout.Toggle(revealRoleOnEject, "投出局时公开身份");
            proximityVoiceEnabled = GUILayout.Toggle(proximityVoiceEnabled, "近距离语音规则");
        }

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
            GUILayout.Label(relayStatus);
        }

        private void DrawRoomHeader()
        {
            GUILayout.Label("房间: " + CountHumanPlayers() + " 真人 / " + CountBotPlayers() + " AI | " + roomMinPlayers + "-" + roomMaxPlayers + " 人");
            GUILayout.Label("规则: " + (roomAutoFillAi ? "AI 补位" : "真人优先") + " | " + (revealRoleOnEject ? "出局公开身份" : "身份隐藏") + " | " + (proximityVoiceEnabled ? "近距离语音" : "会议语音"));

            if (!string.IsNullOrWhiteSpace(relayJoinCode))
            {
                GUILayout.Label("Relay 房间码: " + relayJoinCode);
            }

            if (IsHost && phase == OnlineMatchPhase.Lobby)
            {
                DrawRoomSettings();
            }
        }

        private void DrawCompactActionHud()
        {
            float topBarWidth = Mathf.Clamp(Screen.width * 0.34f, 360f, 540f);
            float topBarHeight = 68f;
            Rect topBar = new Rect(16f, 14f, topBarWidth, topBarHeight);
            GUILayout.BeginArea(topBar, GUI.skin.box);
            GUILayout.Label("九龙港城行动 | " + RoleName(localRole) + " / " + LocalProfessionName());
            GUILayout.Label("证据 " + evidenceScore + "/" + evidenceTarget + " | 任务 " + CountCompletedTasks() + "/" + tasks.Count + " | 存活 " + CountAlivePlayers() + "/" + players.Count + " | 会议 " + emergencyMeetingsLeft + " | " + BuildHazardSummary() + (aiActionGraceTimer > 0f ? " | 缓冲 " + Mathf.CeilToInt(aiActionGraceTimer) + "s" : string.Empty));
            GUILayout.Label("阶段 " + EvidenceMilestoneName(evidenceMilestoneIndex) + " | " + BuildNextEvidenceMilestoneHint());
            GUILayout.Label(BuildVoiceHudLine());
            GUILayout.EndArea();

            float promptWidth = Mathf.Clamp(Screen.width * 0.34f, 420f, 560f);
            Rect promptRect = new Rect((Screen.width - promptWidth) * 0.5f, Screen.height - 66f, promptWidth, 48f);
            GUILayout.BeginArea(promptRect, GUI.skin.box);
            GUILayout.Label(BuildLocalActionHint() + " | WASD/E/Q/R/F/M/I");
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
            float ratio = Mathf.Clamp01(1f - abilityCooldown / AbilityCooldownSeconds);
            Rect bar = GUILayoutUtility.GetRect(rect.width - 18f, 12f);
            DrawProgressBar(bar, ratio, ratio >= 1f ? new Color(0.12f, 0.74f, 0.36f, 1f) : new Color(0.08f, 0.42f, 0.72f, 1f));
            GUILayout.Label(ratio >= 1f ? "F 可用" : "冷却 " + Mathf.CeilToInt(abilityCooldown) + "s");
            GUILayout.EndArea();
        }

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

        private void DrawActiveTaskPanel()
        {
            if (activeTaskId < 0)
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
            switch (taskId)
            {
                case 0:
                    return new Color(0.12f, 0.7f, 0.94f, 1f);
                case 1:
                case 10:
                case 23:
                    return new Color(0.92f, 0.72f, 0.16f, 1f);
                case 2:
                case 14:
                case 24:
                    return new Color(0.14f, 0.82f, 0.32f, 1f);
                case 3:
                case 15:
                    return new Color(0.82f, 0.84f, 0.92f, 1f);
                case 4:
                case 11:
                case 16:
                case 22:
                    return new Color(0.86f, 0.6f, 0.12f, 1f);
                case 5:
                case 27:
                    return new Color(0.72f, 0.2f, 0.82f, 1f);
                case 6:
                case 13:
                case 21:
                    return new Color(0.72f, 0.86f, 0.18f, 1f);
                case 7:
                case 12:
                    return new Color(0.92f, 0.42f, 0.12f, 1f);
                case 8:
                case 18:
                case 26:
                    return new Color(0.42f, 0.76f, 0.94f, 1f);
                case 9:
                case 19:
                    return new Color(0.92f, 0.48f, 0.74f, 1f);
                case 17:
                    return new Color(0.58f, 0.9f, 0.36f, 1f);
                case 20:
                    return new Color(0.94f, 0.84f, 0.2f, 1f);
                case 25:
                    return new Color(0.8f, 0.34f, 0.26f, 1f);
                default:
                    return new Color(0.08f, 0.62f, 0.82f, 1f);
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

        private string LocalProfessionName()
        {
            if (players.TryGetValue(LocalClientId(), out OnlinePlayerState state))
            {
                return ProfessionName(state.Profession);
            }

            return "待分配";
        }

        private void DrawOpeningBriefing()
        {
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("专案简报");
            GUILayout.Label("你的身份: " + RoleName(localRole));
            GUILayout.Label("你的职责: " + LocalProfessionName());
            GUILayout.Label("地图: 九龙港区封控街区");
            GUILayout.Label("局长: 目标 10-20 分钟；20 分钟未闭合关键证据则按证据比例结算。");
            GUILayout.Label("关键机制: 大地图巡线、现场小任务、尸体报案、会议语音、投票放逐、黑帮暗线通道。");
            GUILayout.Label("目标: 警方搜证和投出黑帮；黑帮制造不在场证明、破坏证据链并误导会议。");
            GUILayout.Space(6f);
            Rect routeRect = GUILayoutUtility.GetRect(360f, 62f, GUILayout.ExpandWidth(true));
            DrawOpeningRouteCards(routeRect);
            GUILayout.Label("行动倒计时: " + Mathf.CeilToInt(phaseTimer) + "s");
            GUILayout.EndVertical();
        }

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

        private void DrawTacticalMapMini()
        {
            float mapHeight = phase == OnlineMatchPhase.Action && !tacticalMapOpen ? 92f : 132f;
            Rect rect = GUILayoutUtility.GetRect(180f, mapHeight, GUILayout.ExpandWidth(true));
            GUI.Box(rect, "港区小地图");
            DrawMapRect(rect, false);
        }

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

        private void DrawMapRect(Rect rect, bool withLabels)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.06f, 0.075f, 0.08f, 0.92f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            DrawMiniMapCorridors(rect);

            foreach (ShipRoomSpec room in ShipRooms())
            {
                DrawMiniMapArea(rect, ScaleMapPosition(room.Center), ScaleMapSize(room.Size), room.Floor, withLabels ? room.Label : string.Empty);
            }

            for (int i = 0; i < UnderworldPassageCount; i++)
            {
                DrawMiniMapDot(rect, UnderworldPassagePosition(i), new Color(0.78f, 0.2f, 0.86f, 1f), withLabels ? 8f : 5f);
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

            foreach (OnlineBodyState body in bodies)
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

        private static void DrawMiniMapCorridors(Rect rect)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.22f, 0.25f, 0.26f, 1f);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(0f, -0.18f, 0f)), ScaleMapSize(new Vector3(15.5f, 1.2f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(0f, 3.65f, 0f)), ScaleMapSize(new Vector3(16.4f, 1.04f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(0.12f, -3.9f, 0f)), ScaleMapSize(new Vector3(15.4f, 1.04f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(-6.85f, 0.15f, 0f)), ScaleMapSize(new Vector3(1.08f, 8.35f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(7.05f, 0.08f, 0f)), ScaleMapSize(new Vector3(1.08f, 8.18f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(0f, 1.85f, 0f)), ScaleMapSize(new Vector3(1.08f, 3.15f, 0f)), GUI.color, string.Empty);
            DrawMiniMapArea(rect, ScaleMapPosition(new Vector3(0f, -2.35f, 0f)), ScaleMapSize(new Vector3(1.08f, 3.05f, 0f)), GUI.color, string.Empty);
            DrawMiniMapDot(rect, ScaleMapPosition(new Vector3(0f, -0.35f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 13f);
            DrawMiniMapDot(rect, ScaleMapPosition(new Vector3(-6.85f, 3.65f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, ScaleMapPosition(new Vector3(7.05f, 3.65f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, ScaleMapPosition(new Vector3(-6.85f, -3.9f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            DrawMiniMapDot(rect, ScaleMapPosition(new Vector3(7.05f, -3.9f, 0f)), new Color(0.54f, 0.6f, 0.62f, 1f), 9f);
            GUI.color = oldColor;
        }

        private static void DrawMiniMapArea(Rect mapRect, Vector3 worldCenter, Vector3 worldSize, Color color, string label)
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

        private static void DrawMiniMapDot(Rect mapRect, Vector3 worldPosition, Color color, float size)
        {
            Vector2 point = WorldToMapPoint(mapRect, worldPosition);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawMiniMapLabel(Rect mapRect, Vector3 worldPosition, string label, Color color)
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

        private static Rect WorldRectToMapRect(Rect mapRect, Vector3 worldCenter, Vector3 worldSize)
        {
            Vector2 center = WorldToMapPoint(mapRect, worldCenter);
            float width = worldSize.x / (MapHalfWidth * 2f) * mapRect.width;
            float height = worldSize.y / (MapHalfHeight * 2f) * mapRect.height;
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        private static Vector2 WorldToMapPoint(Rect mapRect, Vector3 worldPosition)
        {
            float x = Mathf.InverseLerp(-MapHalfWidth, MapHalfWidth, worldPosition.x);
            float y = Mathf.InverseLerp(-MapHalfHeight, MapHalfHeight, worldPosition.y);
            return new Vector2(mapRect.x + x * mapRect.width, mapRect.y + (1f - y) * mapRect.height);
        }

        private Vector3 LocalCameraTarget()
        {
            if (players.TryGetValue(currentCameraSubjectId, out OnlinePlayerState subject) && subject.Alive)
            {
                return subject.Position;
            }

            if (players.TryGetValue(LocalClientId(), out OnlinePlayerState state))
            {
                return state.Position;
            }

            return localPosition;
        }

        private ulong PickOpeningCameraSubject(ulong fallbackClientId)
        {
            if (players.TryGetValue(fallbackClientId, out OnlinePlayerState localState) && localState.Alive && localState.Input.sqrMagnitude > 0.02f)
            {
                return fallbackClientId;
            }

            ulong bestClientId = fallbackClientId;
            float bestDistance = float.MaxValue;
            Vector3 anchor = ScaleMapPosition(new Vector3(-4.8f, 1.65f, 0f));

            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.Alive)
                {
                    continue;
                }

                float distance = Vector3.Distance(state.Position, anchor);

                if (distance < bestDistance)
                {
                    bestClientId = state.ClientId;
                    bestDistance = distance;
                }
            }

            return bestClientId;
        }

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
            DrawVoiceRoomStrip();
            GUILayout.Space(6f);
            DrawMeetingRosterButtons();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("案情记录");
            intelScroll = GUILayout.BeginScrollView(intelScroll);
            GUILayout.Label(BuildFocusedIntel());
            GUILayout.Space(8f);
            GUILayout.Label(BuildCaseLog());
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawMeetingEvidenceStrip(float width)
        {
            GUILayout.Label("会议证据墙");
            Rect rect = GUILayoutUtility.GetRect(width, 124f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.055f, 0.065f, 0.07f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float progress = Mathf.Clamp01(evidenceScore / (float)Mathf.Max(1, evidenceTarget));
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
            GUI.Label(new Rect(rect.x + 12f, rect.y + 18f, rect.width - 24f, rect.height - 18f), "证据链 " + evidenceScore + "/" + evidenceTarget + "\n未报案 " + CountUnreportedBodies() + " | 被破坏任务 " + sabotaged + "\n" + BuildMeetingEvidenceDigest() + "\n会议原因: " + lastMeetingReason + "\n上轮结论: " + lastVoteOutcome);
            GUI.color = oldColor;
        }

        private string BuildMeetingEvidenceDigest()
        {
            OnlineTaskState keyTask = FindHighestValueOpenTask();
            string keyTaskText = keyTask.Id >= 0 ? "关键未闭合: " + keyTask.Name + " +" + TaskEvidenceValue(keyTask.Id) : "关键未闭合: 无";
            return keyTaskText + " | 当前票型 " + BuildVoteTallySummary();
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

            foreach (KeyValuePair<ulong, int> pair in tally)
            {
                if (players.TryGetValue(pair.Key, out OnlinePlayerState state))
                {
                    lead += " | " + state.DisplayName + " " + pair.Value;
                }
            }

            return lead;
        }

        private string BuildVoteTallySummary(Dictionary<ulong, int> tally)
        {
            if (tally == null || tally.Count == 0)
            {
                return "无人得票";
            }

            StringBuilder builder = new StringBuilder();

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

            return builder.Length == 0 ? "无人得票" : builder.ToString();
        }

        private void DrawVoiceRoomStrip()
        {
            GUILayout.Label("会议语音频道：全员开放 | " + BuildVoiceHudLine());
            GUILayout.BeginHorizontal();

            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.Alive)
                {
                    continue;
                }

                Color oldColor = GUI.color;
                GUI.color = votes.ContainsKey(state.ClientId) ? new Color(0.16f, 0.72f, 0.36f, 1f) : new Color(0.24f, 0.28f, 0.3f, 1f);
                GUILayout.Box(state.DisplayName.Length > 4 ? state.DisplayName.Substring(0, 4) : state.DisplayName, GUILayout.Height(28f), GUILayout.MinWidth(48f));
                GUI.color = oldColor;
            }

            GUILayout.EndHorizontal();
        }

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

        private void DrawResultScoreboard(float width)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 120f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.055f, 0.065f, 0.07f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float evidenceRatio = Mathf.Clamp01(evidenceScore / (float)Mathf.Max(1, evidenceTarget));
            float taskRatio = Mathf.Clamp01(CountCompletedTasks() / (float)Mathf.Max(1, tasks.Count));
            float survivalRatio = Mathf.Clamp01(CountAlivePlayers() / (float)Mathf.Max(1, players.Count));

            DrawResultBar(new Rect(rect.x + 14f, rect.y + 18f, rect.width - 28f, 16f), evidenceRatio, new Color(0.08f, 0.62f, 0.82f, 1f), "证据链 " + evidenceScore + "/" + evidenceTarget);
            DrawResultBar(new Rect(rect.x + 14f, rect.y + 50f, rect.width - 28f, 16f), taskRatio, new Color(0.86f, 0.68f, 0.12f, 1f), "任务 " + CountCompletedTasks() + "/" + tasks.Count);
            DrawResultBar(new Rect(rect.x + 14f, rect.y + 82f, rect.width - 28f, 16f), survivalRatio, new Color(0.14f, 0.7f, 0.36f, 1f), "存活 " + CountAlivePlayers() + "/" + players.Count);
            GUI.color = oldColor;
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

        private void BuildDefaultTasks()
        {
            tasks.Clear();
            for (int id = 0; id <= 19; id++)
            {
                tasks.Add(new OnlineTaskState(id, TaskNameFor(id), TaskPositionFor(id), 0, TaskRequiredProgress(id), false, false));
            }

            for (int id = 20; id <= 27; id++)
            {
                tasks.Add(new OnlineTaskState(id, TaskNameFor(id), TaskPositionFor(id), 0, TaskRequiredProgress(id), false, false));
            }
        }

        private OnlineTaskState FindNearestTask(Vector3 position)
        {
            OnlineTaskState best = new OnlineTaskState(-1, string.Empty, Vector3.zero, 0, 1, false, false);
            float bestDistance = InteractionRange;

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

        private void SetTask(OnlineTaskState updated)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].Id == updated.Id)
                {
                    tasks[i] = updated;
                    return;
                }
            }
        }

        private void EnsureWorld()
        {
            EnsureRuntimeSprites();

            if (worldRoot != null)
            {
                return;
            }

            DestroyStaleWorldRoots();
            solidObstacleRects.Clear();
            walkableRects.Clear();
            worldLabels.Clear();
            worldRoot = new GameObject(WorldRootName);
            worldRoot.transform.SetParent(transform, false);
            ConfigureSceneLighting();
            CreateSocialDeductionShipMap();
            CreateNeonLight("会议舱顶灯", new Vector3(0f, 0f, 1.1f), new Color(0.35f, 0.75f, 1f, 1f), 1.8f, 6.4f);
            CreateNeonLight("证物库紫外灯", new Vector3(-8.6f, -5.05f, 1.05f), new Color(0.54f, 0.32f, 1f, 1f), 1.1f, 4.2f);
            CreateNeonLight("电力舱冷光", new Vector3(8.85f, 5.25f, 1.15f), new Color(0.3f, 0.8f, 1f, 1f), 1.35f, 4.6f);
            CreateEmergencyBell();
        }

        private void DestroyRuntimeWorld()
        {
            if (worldRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(worldRoot);
                }
                else
                {
                    DestroyImmediate(worldRoot);
                }
            }

            worldRoot = null;
            solidObstacleRects.Clear();
            walkableRects.Clear();
            worldLabels.Clear();
            taskVisuals.Clear();
            playerVisuals.Clear();
            playerVisualBaseScales.Clear();
            bodyVisuals.Clear();
            modelPrefabCache.Clear();
            runtimeMeshMaterials.Clear();
            DestroyStaleWorldRoots();
        }

        private void DestroyStaleWorldRoots()
        {
            List<GameObject> staleRoots = new List<GameObject>();

            foreach (Transform child in transform)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.name == WorldRootName || child.name.StartsWith("Online Hong Kong Port Map", StringComparison.Ordinal) || child.name.StartsWith("Online Gangland Runtime Map", StringComparison.Ordinal))
                {
                    staleRoots.Add(child.gameObject);
                }
            }

            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
                {
                    continue;
                }

                if (candidate.name == WorldRootName
                    || candidate.name.StartsWith("Online Hong Kong Port Map", StringComparison.Ordinal)
                    || candidate.name.StartsWith("Online Gangland Runtime Map", StringComparison.Ordinal))
                {
                    staleRoots.Add(candidate.gameObject);
                }
            }

            for (int i = 0; i < staleRoots.Count; i++)
            {
                if (staleRoots[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(staleRoots[i]);
                }
                else
                {
                    DestroyImmediate(staleRoots[i]);
                }
            }
        }

        private void ConfigureMainCamera()
        {
            if (Camera.main == null)
            {
                return;
            }

            Camera camera = Camera.main;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);
            bool preview = tacticalMapOpen || phase == OnlineMatchPhase.Lobby || phase == OnlineMatchPhase.Opening || phase == OnlineMatchPhase.Result;
            float targetSize = preview ? PreviewCameraSize : activeTaskId >= 0 ? TaskCameraSize : blackoutTimer > 0f ? BlackoutCameraSize : ActionCameraSize;
            Vector3 target = preview ? Vector3.zero : LocalCameraTarget();
            float yOffset = preview ? PreviewCameraYOffset : ActionCameraYOffset;
            float zOffset = preview ? PreviewCameraZOffset : ActionCameraZOffset;
            Vector3 desiredPosition = new Vector3(target.x, target.y + yOffset, zOffset);

            if (camera.orthographic != preview)
            {
                cameraWasConfigured = false;
            }

            camera.orthographic = preview;

            if (preview)
            {
                camera.fieldOfView = PreviewCameraFieldOfView;
                camera.orthographicSize = cameraWasConfigured ? Mathf.Lerp(camera.orthographicSize, targetSize, Time.deltaTime * 4f) : targetSize;
            }
            else
            {
                camera.fieldOfView = cameraWasConfigured ? Mathf.Lerp(camera.fieldOfView, ActionCameraFieldOfView, Time.deltaTime * 4f) : ActionCameraFieldOfView;
                desiredPosition += new Vector3(0f, 0.18f, 0.15f);
            }

            Vector3 lookTarget = preview ? target : target + new Vector3(0f, ActionCameraLookAheadY, ActionCameraLookHeight);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);
            camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, desiredRotation, cameraWasConfigured ? Time.deltaTime * 4.5f : 1f);
            camera.transform.position = Vector3.Lerp(camera.transform.position, desiredPosition, cameraWasConfigured ? Time.deltaTime * 4.8f : 1f);
            cameraWasConfigured = true;
        }

        private void UpdateWorldVisuals()
        {
            UpdateTaskVisuals();
            UpdatePlayerVisuals();
            UpdateBodyVisuals();
            UpdateAreaLabelVisibility();
            BillboardWorldLabels();
        }

        private void UpdateTaskVisuals()
        {
            HashSet<int> seen = new HashSet<int>();

            foreach (OnlineTaskState task in tasks)
            {
                seen.Add(task.Id);

                if (!taskVisuals.TryGetValue(task.Id, out GameObject visual) || visual == null)
                {
                    visual = CreateTaskVisual(task);
                    taskVisuals[task.Id] = visual;
                }

                visual.transform.position = task.Position + new Vector3(0f, 0f, 0.1f);
                SetTaskVisualState(visual, task);
                SetSortingFromZ(visual);
            }

            RemoveStaleVisuals(taskVisuals, seen);
        }

        private void UpdatePlayerVisuals()
        {
            HashSet<ulong> seen = new HashSet<ulong>();
            ulong localClientId = LocalClientId();
            currentCameraSubjectId = localClientId;

            foreach (OnlinePlayerState state in players.Values)
            {
                seen.Add(state.ClientId);

                if (!playerVisuals.TryGetValue(state.ClientId, out GameObject visual) || visual == null)
                {
                    visual = CreatePlayerVisual(state);
                    playerVisuals[state.ClientId] = visual;
                    playerVisualBaseScales[state.ClientId] = visual != null ? visual.transform.localScale : Vector3.one;
                }

                bool isLocalPlayer = state.ClientId == localClientId;
                visual.transform.position = state.Position + new Vector3(0f, 0f, state.Alive ? 0.32f : 0.12f);
                Vector3 baseScale = playerVisualBaseScales.TryGetValue(state.ClientId, out Vector3 cachedScale) ? cachedScale : visual.transform.localScale;
                visual.transform.localScale = state.Alive
                    ? baseScale
                    : new Vector3(baseScale.x * 0.92f, baseScale.y * 0.48f, baseScale.z);
                AnimatePlayerVisual(visual, state);
                SetPlayerVisualColors(visual, state, isLocalPlayer);
                UpdatePlayerStageTwoStateLayer(visual, state, isLocalPlayer);
                SetSortingFromZ(visual);

                TextMesh[] labels = visual.GetComponentsInChildren<TextMesh>(true);

                for (int i = 0; i < labels.Length; i++)
                {
                    TextMesh label = labels[i];
                    label.text = BuildPlayerWorldLabel(state, isLocalPlayer);
                    bool showLabel = ShouldShowPlayerWorldLabel(state, isLocalPlayer) && IsNearCameraSubject(state.Position);
                    SetTextMeshVisible(label, showLabel);
                    BillboardLabel(label.transform);
                }
            }

            RemoveStalePlayerVisuals(seen);
        }

        private void RemoveStalePlayerVisuals(HashSet<ulong> seen)
        {
            List<ulong> stale = new List<ulong>();

            foreach (KeyValuePair<ulong, GameObject> pair in playerVisuals)
            {
                if (!seen.Contains(pair.Key) || pair.Value == null)
                {
                    stale.Add(pair.Key);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                ulong clientId = stale[i];

                if (playerVisuals.TryGetValue(clientId, out GameObject visual) && visual != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(visual);
                    }
                    else
                    {
                        DestroyImmediate(visual);
                    }
                }

                playerVisuals.Remove(clientId);
                playerVisualBaseScales.Remove(clientId);
            }
        }

        private void UpdateBodyVisuals()
        {
            HashSet<int> seen = new HashSet<int>();

            foreach (OnlineBodyState body in bodies)
            {
                if (body.Reported)
                {
                    continue;
                }

                seen.Add(body.Id);

                if (!bodyVisuals.TryGetValue(body.Id, out GameObject visual) || visual == null)
                {
                    visual = CreateBodyVisual(body);
                    bodyVisuals[body.Id] = visual;
                }

                visual.transform.position = body.Position + new Vector3(0f, 0f, 0.11f);
                SetSortingFromZ(visual);
            }

            RemoveStaleVisuals(bodyVisuals, seen);
        }

        private void AnimatePlayerVisual(GameObject visual, OnlinePlayerState state)
        {
            if (visual == null)
            {
                return;
            }

            float speed = state.Input.magnitude;
            bool inMeeting = phase == OnlineMatchPhase.Meeting || phase == OnlineMatchPhase.Voting;
            Vector2 facing = state.Input.sqrMagnitude > 0.02f ? state.Input.normalized : Vector2.up;
            float facingAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
            float bob = speed > 0.05f ? Mathf.Sin(Time.time * 10f + state.ClientId * 0.37f) * 0.035f : 0f;
            float meetingBob = inMeeting && state.Alive ? Mathf.Sin(Time.time * 3.2f + state.ClientId * 0.53f) * 0.012f : 0f;
            Transform body = visual.transform.Find("Body Volume");
            Transform helmet = visual.transform.Find("Helmet Volume");
            Transform armL = visual.transform.Find("Arm L");
            Transform armR = visual.transform.Find("Arm R");
            Transform bootL = visual.transform.Find("Boot L");
            Transform bootR = visual.transform.Find("Boot R");
            Transform facingLight = visual.transform.Find("Facing Light");

            if (body != null)
            {
                body.localPosition = state.Alive ? new Vector3(0f, -0.08f + bob + meetingBob, 0.22f) : new Vector3(0f, -0.22f, 0.08f);
                body.localRotation = state.Alive
                    ? Quaternion.Euler(90f, 0f, inMeeting ? 0f : Mathf.Clamp(state.Input.x, -1f, 1f) * -10f)
                    : Quaternion.Euler(90f, 0f, 82f);
            }

            if (helmet != null)
            {
                helmet.localPosition = state.Alive ? new Vector3(0.04f, 0.2f + bob * 0.6f + meetingBob, 0.52f) : new Vector3(0.24f, -0.18f, 0.13f);
                helmet.localScale = state.Alive ? new Vector3(0.38f, 0.32f, 0.32f) : new Vector3(0.28f, 0.18f, 0.14f);
            }

            if (armL != null)
            {
                armL.localPosition = state.Alive ? new Vector3(-0.26f, -0.08f, 0.28f) : new Vector3(-0.26f, -0.14f, 0.1f);
                armL.localRotation = state.Alive ? Quaternion.Euler(90f, 0f, 12f + bob * 210f) : Quaternion.Euler(90f, 0f, 74f);
            }

            if (armR != null)
            {
                armR.localPosition = state.Alive ? new Vector3(0.26f, -0.08f, 0.28f) : new Vector3(0.08f, -0.22f, 0.11f);
                armR.localRotation = state.Alive ? Quaternion.Euler(90f, 0f, -12f - bob * 210f) : Quaternion.Euler(90f, 0f, 100f);
            }

            if (bootL != null)
            {
                bootL.localPosition = state.Alive ? new Vector3(-0.11f, -0.5f - bob * 1.2f, 0.16f) : new Vector3(-0.18f, -0.46f, 0.08f);
                bootL.localRotation = state.Alive ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(90f, 0f, 82f);
            }

            if (bootR != null)
            {
                bootR.localPosition = state.Alive ? new Vector3(0.11f, -0.5f + bob * 1.2f, 0.16f) : new Vector3(0.28f, -0.34f, 0.08f);
                bootR.localRotation = state.Alive ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(90f, 0f, 82f);
            }

            if (facingLight != null)
            {
                facingLight.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
                facingLight.gameObject.SetActive(state.Alive && !inMeeting);
            }
        }

        private void UpdateAreaLabelVisibility()
        {
            bool visible = tacticalMapOpen || phase == OnlineMatchPhase.Lobby || phase == OnlineMatchPhase.Opening || phase == OnlineMatchPhase.Result;

            for (int i = worldLabels.Count - 1; i >= 0; i--)
            {
                TextMesh label = worldLabels[i];

                if (label == null)
                {
                    worldLabels.RemoveAt(i);
                    continue;
                }

                SetTextMeshVisible(label, visible);
            }
        }

        private void BillboardWorldLabels()
        {
            foreach (TextMesh label in worldLabels)
            {
                if (label == null)
                {
                    continue;
                }

                BillboardLabel(label.transform);
            }
        }

        private GameObject CreateTaskVisual(OnlineTaskState task)
        {
            GameObject taskObject = new GameObject("Online Task " + task.Name);
            taskObject.transform.SetParent(worldRoot.transform, false);
            taskObject.transform.localScale = Vector3.one;
            Vector3 scaledTaskScale = TaskScale(task.Id);
            float width = Mathf.Max(0.34f, scaledTaskScale.x);
            float depth = Mathf.Max(0.24f, scaledTaskScale.y * 0.72f);
            Color accent = TaskPanelAccent(task.Id);
            Color darkBase = new Color(0.055f, 0.072f, 0.078f, 1f);

            CreatePropChild(taskObject.transform, "Task Glow", new Vector3(0f, 0f, -0.09f), new Vector3(width * 1.42f, depth * 1.45f, 0.06f), new Color(accent.r, accent.g, accent.b, 0.18f), PrimitiveType.Sphere);
            CreateMeshBoxChild(taskObject.transform, "Task Pedestal", new Vector3(0f, 0f, 0.08f), new Vector3(width, depth, 0.22f), darkBase);
            CreateMeshBoxChild(taskObject.transform, "Task Raised Console", new Vector3(0f, depth * 0.18f, 0.28f), new Vector3(width * 0.76f, depth * 0.45f, 0.28f), Darken(accent, 0.42f));
            CreateMeshBoxChild(taskObject.transform, "Task Screen Glass", new Vector3(0f, depth * 0.42f, 0.47f), new Vector3(width * 0.62f, 0.035f, 0.2f), new Color(0.08f, 0.78f, 0.92f, 1f));
            CreatePropChild(taskObject.transform, "Task Beacon", new Vector3(width * 0.38f, -depth * 0.22f, 0.46f), new Vector3(0.14f, 0.14f, 0.12f), new Color(0.95f, 0.88f, 0.22f, 1f), PrimitiveType.Cylinder);
            CreatePropChild(taskObject.transform, "Task Marker", new Vector3(0f, depth * 0.66f, 0.36f), new Vector3(width * 0.38f, 0.06f, 0.06f), accent, PrimitiveType.Cube);
            CreateTaskEquipment(taskObject.transform, task.Id);
            SetTaskVisualState(taskObject, task);
            return taskObject;
        }

        private void SetTaskVisualState(GameObject visual, OnlineTaskState task)
        {
            Color accent = task.Completed ? new Color(0.14f, 0.74f, 0.36f, 1f) : task.Sabotaged ? new Color(0.86f, 0.1f, 0.08f, 1f) : TaskPanelAccent(task.Id);
            Transform marker = visual.transform.Find("Task Marker");
            Transform screen = visual.transform.Find("Task Screen Glass");
            Transform beacon = visual.transform.Find("Task Beacon");
            Transform console = visual.transform.Find("Task Raised Console");

            if (marker != null)
            {
                SetColor(marker.gameObject, accent);
            }

            if (screen != null)
            {
                SetColor(screen.gameObject, task.Sabotaged ? new Color(0.95f, 0.14f, 0.08f, 1f) : task.Completed ? new Color(0.16f, 0.92f, 0.5f, 1f) : new Color(0.08f, 0.78f, 0.92f, 1f));
            }

            if (beacon != null)
            {
                SetColor(beacon.gameObject, task.Sabotaged ? new Color(1f, 0.12f, 0.05f, 1f) : new Color(0.95f, 0.88f, 0.22f, 1f));
            }

            if (console != null)
            {
                SetColor(console.gameObject, Darken(accent, task.Completed ? 0.5f : 0.42f));
            }
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

        private void CreateTaskEquipment(Transform parent, int taskId)
        {
            Color screen = new Color(0.04f, 0.62f, 0.82f, 1f);
            Color darkMetal = new Color(0.05f, 0.07f, 0.08f, 1f);
            Color warning = new Color(0.92f, 0.74f, 0.08f, 1f);

            switch (taskId)
            {
                case 0:
                    CreatePropChild(parent, "CCTV Monitor A", new Vector3(-0.22f, 0.18f, 0.24f), new Vector3(0.22f, 0.05f, 0.16f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "CCTV Monitor B", new Vector3(0.04f, 0.18f, 0.24f), new Vector3(0.22f, 0.05f, 0.16f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "CCTV Monitor C", new Vector3(0.3f, 0.18f, 0.24f), new Vector3(0.22f, 0.05f, 0.16f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "CCTV Keyboard", new Vector3(0.04f, -0.12f, 0.22f), new Vector3(0.42f, 0.08f, 0.06f), darkMetal, PrimitiveType.Cube);
                    break;
                case 1:
                    CreatePropChild(parent, "Container Seal Tape", new Vector3(0f, 0.26f, 0.18f), new Vector3(0.72f, 0.06f, 0.08f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Inspection Clamp L", new Vector3(-0.42f, 0f, 0.22f), new Vector3(0.08f, 0.5f, 0.12f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Inspection Clamp R", new Vector3(0.42f, 0f, 0.22f), new Vector3(0.08f, 0.5f, 0.12f), darkMetal, PrimitiveType.Cube);
                    break;
                case 2:
                    CreatePropChild(parent, "Breaker Handle", new Vector3(0.14f, 0f, 0.26f), new Vector3(0.08f, 0.42f, 0.08f), new Color(0.8f, 0.12f, 0.08f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Breaker Spark A", new Vector3(-0.18f, 0.1f, 0.28f), new Vector3(0.12f, 0.04f, 0.08f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Breaker Spark B", new Vector3(-0.1f, -0.12f, 0.28f), new Vector3(0.08f, 0.04f, 0.08f), warning, PrimitiveType.Cube);
                    break;
                case 3:
                    CreatePropChild(parent, "Evidence Scan Tray", new Vector3(0f, -0.02f, 0.22f), new Vector3(0.52f, 0.34f, 0.08f), new Color(0.86f, 0.88f, 0.82f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Evidence Scan Beam", new Vector3(0f, 0.22f, 0.3f), new Vector3(0.46f, 0.05f, 0.1f), screen, PrimitiveType.Cube);
                    break;
                case 4:
                    CreatePropChild(parent, "Upload Screen", new Vector3(0f, 0.16f, 0.26f), new Vector3(0.38f, 0.06f, 0.22f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Upload Antenna", new Vector3(0.26f, 0.02f, 0.32f), new Vector3(0.04f, 0.04f, 0.28f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Archive Drive", new Vector3(-0.24f, -0.16f, 0.24f), new Vector3(0.18f, 0.16f, 0.1f), darkMetal, PrimitiveType.Cube);
                    break;
                case 5:
                    CreatePropChild(parent, "Recorder", new Vector3(0f, 0.02f, 0.24f), new Vector3(0.22f, 0.14f, 0.08f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Interview Chair A", new Vector3(-0.36f, 0f, 0.16f), new Vector3(0.14f, 0.22f, 0.12f), new Color(0.44f, 0.18f, 0.1f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Interview Chair B", new Vector3(0.36f, 0f, 0.16f), new Vector3(0.14f, 0.22f, 0.12f), new Color(0.44f, 0.18f, 0.1f, 1f), PrimitiveType.Cube);
                    break;
                case 6:
                    CreatePropChild(parent, "Radio Mast", new Vector3(0.22f, 0.04f, 0.34f), new Vector3(0.04f, 0.04f, 0.38f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Radio Dial A", new Vector3(-0.18f, -0.1f, 0.24f), new Vector3(0.1f, 0.1f, 0.06f), screen, PrimitiveType.Cylinder);
                    CreatePropChild(parent, "Radio Waveform", new Vector3(0.02f, 0.18f, 0.26f), new Vector3(0.38f, 0.05f, 0.1f), screen, PrimitiveType.Cube);
                    break;
                case 7:
                    CreatePropChild(parent, "Access Gate L", new Vector3(-0.35f, 0.1f, 0.2f), new Vector3(0.06f, 0.42f, 0.2f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Access Gate R", new Vector3(0.35f, 0.1f, 0.2f), new Vector3(0.06f, 0.42f, 0.2f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Card Reader", new Vector3(0f, -0.18f, 0.26f), new Vector3(0.22f, 0.1f, 0.12f), screen, PrimitiveType.Cube);
                    break;
                case 8:
                    CreatePropChild(parent, "Witness Binoculars", new Vector3(0f, 0.04f, 0.26f), new Vector3(0.32f, 0.1f, 0.1f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Tripod Stem", new Vector3(0f, -0.18f, 0.22f), new Vector3(0.05f, 0.28f, 0.05f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Photo Tag", new Vector3(0.26f, 0.18f, 0.22f), new Vector3(0.16f, 0.08f, 0.08f), new Color(0.86f, 0.86f, 0.78f, 1f), PrimitiveType.Cube);
                    break;
                case 9:
                    CreatePropChild(parent, "Clinic Tray", new Vector3(0f, 0.02f, 0.24f), new Vector3(0.44f, 0.22f, 0.08f), new Color(0.86f, 0.86f, 0.8f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Sample Tube A", new Vector3(-0.12f, 0.12f, 0.32f), new Vector3(0.05f, 0.05f, 0.18f), screen, PrimitiveType.Cylinder);
                    CreatePropChild(parent, "Sample Tube B", new Vector3(0.12f, 0.12f, 0.32f), new Vector3(0.05f, 0.05f, 0.18f), new Color(0.68f, 0.18f, 0.18f, 1f), PrimitiveType.Cylinder);
                    break;
                case 10:
                    CreatePropChild(parent, "Patrol Clipboard", new Vector3(-0.18f, 0.08f, 0.24f), new Vector3(0.18f, 0.24f, 0.06f), new Color(0.8f, 0.74f, 0.56f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Dock Rope Knot", new Vector3(0.2f, -0.05f, 0.22f), new Vector3(0.18f, 0.12f, 0.1f), new Color(0.46f, 0.34f, 0.18f, 1f), PrimitiveType.Sphere);
                    break;
                case 11:
                    CreatePropChild(parent, "Ledger Screen", new Vector3(0f, 0.18f, 0.26f), new Vector3(0.38f, 0.06f, 0.2f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Cash Stack A", new Vector3(-0.18f, -0.12f, 0.24f), new Vector3(0.16f, 0.1f, 0.06f), new Color(0.18f, 0.52f, 0.24f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Cash Stack B", new Vector3(0.08f, -0.14f, 0.24f), new Vector3(0.16f, 0.1f, 0.06f), new Color(0.18f, 0.52f, 0.24f, 1f), PrimitiveType.Cube);
                    break;
                case 12:
                    CreatePropChild(parent, "Shutter Motor", new Vector3(-0.2f, 0.12f, 0.24f), new Vector3(0.18f, 0.18f, 0.12f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Shutter Chain", new Vector3(0.16f, 0.02f, 0.28f), new Vector3(0.05f, 0.38f, 0.06f), warning, PrimitiveType.Cube);
                    break;
                case 13:
                    CreatePropChild(parent, "Signal Dish", new Vector3(0f, 0.12f, 0.28f), new Vector3(0.24f, 0.08f, 0.24f), screen, PrimitiveType.Cylinder);
                    CreatePropChild(parent, "Comms Cable", new Vector3(0.24f, -0.08f, 0.24f), new Vector3(0.3f, 0.04f, 0.05f), darkMetal, PrimitiveType.Cube);
                    break;
                case 14:
                    CreatePropChild(parent, "Generator Body", new Vector3(0f, 0f, 0.22f), new Vector3(0.42f, 0.24f, 0.16f), new Color(0.22f, 0.26f, 0.16f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Generator Coil", new Vector3(0.28f, 0f, 0.26f), new Vector3(0.1f, 0.1f, 0.1f), warning, PrimitiveType.Cylinder);
                    break;
                case 15:
                    CreatePropChild(parent, "Ballistics Tray", new Vector3(0f, 0f, 0.24f), new Vector3(0.46f, 0.2f, 0.08f), new Color(0.82f, 0.82f, 0.76f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Bullet Tag A", new Vector3(-0.14f, 0.06f, 0.3f), new Vector3(0.08f, 0.04f, 0.06f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Bullet Tag B", new Vector3(0.16f, -0.04f, 0.3f), new Vector3(0.08f, 0.04f, 0.06f), warning, PrimitiveType.Cube);
                    break;
                case 16:
                    CreatePropChild(parent, "Cash Counter", new Vector3(0f, 0.1f, 0.25f), new Vector3(0.38f, 0.16f, 0.12f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Counter Display", new Vector3(0f, 0.24f, 0.32f), new Vector3(0.24f, 0.04f, 0.08f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Money Bundle", new Vector3(0.18f, -0.08f, 0.3f), new Vector3(0.16f, 0.1f, 0.06f), new Color(0.18f, 0.52f, 0.24f, 1f), PrimitiveType.Cube);
                    break;
                case 17:
                    CreatePropChild(parent, "Checkpoint Pole", new Vector3(0f, 0.02f, 0.32f), new Vector3(0.05f, 0.05f, 0.34f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Checkpoint Scanner", new Vector3(0.18f, 0.04f, 0.24f), new Vector3(0.16f, 0.12f, 0.08f), screen, PrimitiveType.Cube);
                    break;
                case 18:
                    CreatePropChild(parent, "Plate Camera", new Vector3(0f, 0.12f, 0.26f), new Vector3(0.28f, 0.1f, 0.12f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Plate Frame", new Vector3(0f, -0.1f, 0.24f), new Vector3(0.34f, 0.08f, 0.06f), new Color(0.82f, 0.82f, 0.72f, 1f), PrimitiveType.Cube);
                    break;
                case 19:
                    CreatePropChild(parent, "Medical Tablet", new Vector3(0f, 0.1f, 0.26f), new Vector3(0.28f, 0.06f, 0.18f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Patient File", new Vector3(-0.18f, -0.08f, 0.24f), new Vector3(0.18f, 0.12f, 0.05f), new Color(0.82f, 0.76f, 0.58f, 1f), PrimitiveType.Cube);
                    break;
                case 20:
                    CreatePropChild(parent, "Fish Stall Code Board", new Vector3(0f, 0.14f, 0.26f), new Vector3(0.42f, 0.08f, 0.16f), new Color(0.82f, 0.72f, 0.22f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Seafood Crate", new Vector3(-0.22f, -0.12f, 0.23f), new Vector3(0.18f, 0.16f, 0.08f), new Color(0.18f, 0.46f, 0.54f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Hidden Note", new Vector3(0.2f, -0.1f, 0.26f), new Vector3(0.14f, 0.08f, 0.04f), warning, PrimitiveType.Cube);
                    break;
                case 21:
                    CreatePropChild(parent, "Voice Recorder", new Vector3(0f, 0.02f, 0.24f), new Vector3(0.34f, 0.16f, 0.08f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Wave Screen", new Vector3(0f, 0.2f, 0.3f), new Vector3(0.42f, 0.05f, 0.1f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Tape Reel", new Vector3(0.24f, -0.1f, 0.28f), new Vector3(0.1f, 0.1f, 0.06f), screen, PrimitiveType.Cylinder);
                    break;
                case 22:
                    CreatePropChild(parent, "Money Bag", new Vector3(0f, -0.02f, 0.26f), new Vector3(0.26f, 0.26f, 0.12f), new Color(0.18f, 0.46f, 0.2f, 1f), PrimitiveType.Sphere);
                    CreatePropChild(parent, "Evidence Tag", new Vector3(0.22f, 0.14f, 0.28f), new Vector3(0.14f, 0.08f, 0.04f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Seal Strap", new Vector3(0f, -0.02f, 0.34f), new Vector3(0.32f, 0.05f, 0.04f), warning, PrimitiveType.Cube);
                    break;
                case 23:
                    CreatePropChild(parent, "Cold Storage Door", new Vector3(0f, 0.08f, 0.26f), new Vector3(0.5f, 0.08f, 0.2f), new Color(0.58f, 0.72f, 0.78f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Temperature Strip", new Vector3(0f, 0.24f, 0.32f), new Vector3(0.32f, 0.04f, 0.05f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Frozen Box", new Vector3(-0.2f, -0.12f, 0.24f), new Vector3(0.18f, 0.16f, 0.08f), new Color(0.72f, 0.86f, 0.9f, 1f), PrimitiveType.Cube);
                    break;
                case 24:
                    CreatePropChild(parent, "Drone Body", new Vector3(0f, 0f, 0.28f), new Vector3(0.22f, 0.14f, 0.08f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Drone Rotor L", new Vector3(-0.26f, 0.16f, 0.3f), new Vector3(0.18f, 0.04f, 0.05f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Drone Rotor R", new Vector3(0.26f, 0.16f, 0.3f), new Vector3(0.18f, 0.04f, 0.05f), screen, PrimitiveType.Cube);
                    CreatePropChild(parent, "Drone Dock", new Vector3(0f, -0.16f, 0.22f), new Vector3(0.42f, 0.1f, 0.06f), warning, PrimitiveType.Cube);
                    break;
                case 25:
                    CreatePropChild(parent, "Motor Plate", new Vector3(0f, 0.08f, 0.26f), new Vector3(0.42f, 0.08f, 0.1f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Tyre Track A", new Vector3(-0.18f, -0.1f, 0.22f), new Vector3(0.18f, 0.05f, 0.05f), new Color(0.02f, 0.02f, 0.02f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Tyre Track B", new Vector3(0.18f, -0.1f, 0.22f), new Vector3(0.18f, 0.05f, 0.05f), new Color(0.02f, 0.02f, 0.02f, 1f), PrimitiveType.Cube);
                    break;
                case 26:
                    CreatePropChild(parent, "Route Map", new Vector3(0f, 0.1f, 0.26f), new Vector3(0.38f, 0.2f, 0.06f), new Color(0.78f, 0.74f, 0.56f, 1f), PrimitiveType.Cube);
                    CreatePropChild(parent, "Pin A", new Vector3(-0.12f, 0.14f, 0.32f), new Vector3(0.06f, 0.06f, 0.04f), new Color(0.9f, 0.1f, 0.06f, 1f), PrimitiveType.Cylinder);
                    CreatePropChild(parent, "Pin B", new Vector3(0.14f, 0f, 0.32f), new Vector3(0.06f, 0.06f, 0.04f), new Color(0.08f, 0.35f, 0.9f, 1f), PrimitiveType.Cylinder);
                    break;
                case 27:
                    CreatePropChild(parent, "Safehouse Door Lock", new Vector3(0f, 0.08f, 0.26f), new Vector3(0.28f, 0.18f, 0.08f), darkMetal, PrimitiveType.Cube);
                    CreatePropChild(parent, "Security Chain", new Vector3(0f, -0.08f, 0.28f), new Vector3(0.36f, 0.04f, 0.05f), warning, PrimitiveType.Cube);
                    CreatePropChild(parent, "Witness Folder", new Vector3(-0.22f, 0.14f, 0.24f), new Vector3(0.16f, 0.1f, 0.04f), new Color(0.82f, 0.76f, 0.58f, 1f), PrimitiveType.Cube);
                    break;
                default:
                    CreatePropChild(parent, "Task Panel", new Vector3(0f, 0f, 0.24f), new Vector3(0.32f, 0.18f, 0.08f), screen, PrimitiveType.Cube);
                    break;
            }
        }

        private GameObject CreatePlayerVisual(OnlinePlayerState state)
        {
            GameObject playerObject = CreateSpriteObject("Online Player " + state.ClientId, softCircleSprite, new Color(1f, 1f, 1f, 0f));
            playerObject.transform.SetParent(worldRoot.transform, false);
            playerObject.transform.localScale = new Vector3(1.34f, 1.34f, 1f);
            CreatePropChild(playerObject.transform, "Local Ring", new Vector3(0f, -0.42f, -0.36f), new Vector3(0.72f, 0.42f, 0.06f), new Color(0.08f, 0.72f, 0.95f, 0.62f), PrimitiveType.Cylinder);
            CreatePropChild(playerObject.transform, "Local Arrow", new Vector3(0f, 0.46f, 0.12f), new Vector3(0.22f, 0.12f, 0.06f), new Color(0.95f, 0.82f, 0.12f, 1f), PrimitiveType.Cube);
            CreateSpriteChild(playerObject.transform, "Silhouette Outline", roundedRectSprite, new Vector3(0f, -0.08f, 0.01f), new Vector3(0.56f, 0.74f, 0.18f), new Color(0f, 0f, 0f, 0.58f));
            CreatePropChild(playerObject.transform, "Shadow", new Vector3(0f, -0.36f, -0.38f), new Vector3(0.62f, 0.16f, 0.1f), new Color(0f, 0f, 0f, 0.36f), PrimitiveType.Cylinder);
            CreateMeshPrimitiveChild(playerObject.transform, "Body Volume", PrimitiveType.Capsule, new Vector3(0f, -0.08f, 0.22f), new Vector3(0.34f, 0.34f, 0.62f), PlayerColor(state, false), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(playerObject.transform, "Helmet Volume", PrimitiveType.Sphere, new Vector3(0.04f, 0.2f, 0.52f), new Vector3(0.38f, 0.32f, 0.32f), PlayerColor(state, false), Quaternion.identity);
            CreateMeshBoxChild(playerObject.transform, "Visor Volume", new Vector3(0.13f, 0.42f, 0.56f), new Vector3(0.28f, 0.04f, 0.12f), new Color(0.58f, 0.9f, 1f, 1f));
            CreateMeshBoxChild(playerObject.transform, "Pack Volume", new Vector3(-0.32f, -0.04f, 0.28f), new Vector3(0.14f, 0.32f, 0.34f), Darken(PlayerAccentColor(state), 0.7f));
            CreateMeshPrimitiveChild(playerObject.transform, "Arm L", PrimitiveType.Capsule, new Vector3(-0.26f, -0.08f, 0.28f), new Vector3(0.09f, 0.09f, 0.36f), PlayerAccentColor(state), Quaternion.Euler(90f, 0f, 12f));
            CreateMeshPrimitiveChild(playerObject.transform, "Arm R", PrimitiveType.Capsule, new Vector3(0.26f, -0.08f, 0.28f), new Vector3(0.09f, 0.09f, 0.36f), PlayerAccentColor(state), Quaternion.Euler(90f, 0f, -12f));
            CreateMeshPrimitiveChild(playerObject.transform, "Boot L", PrimitiveType.Capsule, new Vector3(-0.11f, -0.5f, 0.16f), new Vector3(0.1f, 0.1f, 0.22f), Darken(PlayerAccentColor(state), 0.52f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(playerObject.transform, "Boot R", PrimitiveType.Capsule, new Vector3(0.11f, -0.5f, 0.16f), new Vector3(0.1f, 0.1f, 0.22f), Darken(PlayerAccentColor(state), 0.52f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxChild(playerObject.transform, "Facing Light", new Vector3(0.18f, 0.38f, 0.44f), new Vector3(0.08f, 0.035f, 0.08f), new Color(0.92f, 1f, 1f, 1f));
            CreateMeshBoxChild(playerObject.transform, "Badge Plate", new Vector3(0.0f, 0.22f, 0.43f), new Vector3(0.18f, 0.035f, 0.08f), new Color(0.92f, 0.78f, 0.2f, 1f));
            CreateMeshBoxChild(playerObject.transform, "Shoulder L", new Vector3(-0.22f, 0.1f, 0.42f), new Vector3(0.12f, 0.06f, 0.08f), Darken(PlayerAccentColor(state), 0.82f));
            CreateMeshBoxChild(playerObject.transform, "Shoulder R", new Vector3(0.22f, 0.1f, 0.42f), new Vector3(0.12f, 0.06f, 0.08f), Darken(PlayerAccentColor(state), 0.82f));
            CreateSpriteChild(playerObject.transform, "Backpack", capsuleSprite, new Vector3(-0.31f, -0.04f, 0.07f), new Vector3(0.2f, 0.42f, 0.1f), Darken(PlayerAccentColor(state), 0.75f));
            CreateSpriteChild(playerObject.transform, "Torso", capsuleSprite, new Vector3(0f, -0.12f, 0.09f), new Vector3(0.46f, 0.6f, 0.18f), PlayerAccentColor(state));
            CreateSpriteChild(playerObject.transform, "Coat", capsuleSprite, new Vector3(0f, -0.04f, 0.16f), new Vector3(0.5f, 0.56f, 0.11f), PlayerColor(state, false));
            CreateSpriteChild(playerObject.transform, "Leg L", capsuleSprite, new Vector3(-0.13f, -0.47f, 0.05f), new Vector3(0.13f, 0.22f, 0.12f), Darken(PlayerAccentColor(state), 0.6f));
            CreateSpriteChild(playerObject.transform, "Leg R", capsuleSprite, new Vector3(0.13f, -0.47f, 0.05f), new Vector3(0.13f, 0.22f, 0.12f), Darken(PlayerAccentColor(state), 0.6f));
            CreateSpriteChild(playerObject.transform, "Visor", capsuleSprite, new Vector3(0.1f, 0.2f, 0.24f), new Vector3(0.34f, 0.18f, 0.08f), new Color(0.58f, 0.9f, 1f, 1f));
            CreateSpriteChild(playerObject.transform, "Visor Shine", capsuleSprite, new Vector3(0.18f, 0.26f, 0.28f), new Vector3(0.14f, 0.05f, 0.04f), new Color(0.92f, 1f, 1f, 1f));
            CreateProfessionAccessory(playerObject.transform, state);
            CreateStageTwoCharacterStateLayer(playerObject.transform, state);
            CreateStageTwoCharacterRig(playerObject, state);
            CreateFreeCharacterAdapter(playerObject.transform, state);
            CreateWorldLabel(playerObject.transform, state.DisplayName, new Vector3(0f, 0.92f, -0.34f), 0.038f);
            return playerObject;
        }

        private void CreateStageTwoCharacterRig(GameObject playerObject, OnlinePlayerState state)
        {
            StageTwoCharacterRig rig = playerObject.GetComponent<StageTwoCharacterRig>();

            if (rig == null)
            {
                rig = playerObject.AddComponent<StageTwoCharacterRig>();
            }

            rig.Configure("runtime-" + state.Profession, FreeCharacterPrefabPath(state));
            rig.BodyRoot = FindChildTransform(playerObject.transform, "Body Volume", "Torso", "Coat");
            rig.HeadRoot = FindChildTransform(playerObject.transform, "Helmet Volume", "Visor", "Visor Volume");
            rig.LeftArm = FindChildTransform(playerObject.transform, "Arm L", "Shoulder L");
            rig.RightArm = FindChildTransform(playerObject.transform, "Arm R", "Shoulder R");
            rig.LeftFoot = FindChildTransform(playerObject.transform, "Boot L", "Leg L");
            rig.RightFoot = FindChildTransform(playerObject.transform, "Boot R", "Leg R");
            rig.StateRoot = FindChildTransform(playerObject.transform, "Stage2 Character interaction radius", "Stage2 Meeting seated pad", "Stage2 Downed chalk silhouette");
        }

        private void CreateStageTwoCharacterStateLayer(Transform parent, OnlinePlayerState state)
        {
            Color accent = PlayerAccentColor(state);
            CreateSpriteChild(parent, "Stage2 Character interaction radius", circleSprite, new Vector3(0f, -0.18f, -0.44f), new Vector3(InteractionRange * 2f, InteractionRange * 1.18f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.12f));
            CreateSpriteChild(parent, "Stage2 VoiceRadius action proximity", circleSprite, new Vector3(0f, -0.18f, -0.46f), new Vector3(2.35f, 1.36f, 0.05f), new Color(0.08f, 0.7f, 0.9f, 0.1f));
            CreateSpriteChild(parent, "Stage2 Downed chalk silhouette", roundedRectSprite, new Vector3(0.04f, -0.24f, -0.42f), new Vector3(0.86f, 0.28f, 0.06f), new Color(0.9f, 0.88f, 0.74f, 0.48f));
            CreateMeshBoxChild(parent, "Stage2 Downed personal item", new Vector3(-0.28f, -0.36f, 0.16f), new Vector3(0.12f, 0.035f, 0.1f), new Color(0.92f, 0.76f, 0.18f, 1f), -12f);
            CreateMeshBoxChild(parent, "Stage2 Character facing wedge", new Vector3(0f, 0.58f, 0.18f), new Vector3(0.16f, 0.035f, 0.09f), new Color(0.94f, 0.86f, 0.18f, 1f));
            CreateMeshBoxChild(parent, "Stage2 Character action hand prop", new Vector3(0.34f, -0.16f, 0.32f), new Vector3(0.12f, 0.04f, 0.16f), new Color(0.86f, 0.82f, 0.62f, 1f), -18f);
            CreateMeshBoxChild(parent, "Stage2 Character report beacon", new Vector3(0f, 0.66f, 0.68f), new Vector3(0.28f, 0.035f, 0.08f), new Color(0.92f, 0.16f, 0.08f, 1f));
            CreateSpriteChild(parent, "Stage2 Report proximity ping", circleSprite, new Vector3(0f, 0.28f, 0.44f), new Vector3(0.32f, 0.18f, 0.05f), new Color(0.95f, 0.18f, 0.1f, 0.36f));
            CreateMeshPrimitiveChild(parent, "Stage2 Meeting seated pad", PrimitiveType.Cylinder, new Vector3(0f, -0.5f, -0.34f), new Vector3(0.32f, 0.035f, 0.22f), new Color(0.08f, 0.32f, 0.42f, 0.92f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxChild(parent, "Stage2 Meeting vote tablet", new Vector3(0.24f, -0.2f, 0.28f), new Vector3(0.18f, 0.035f, 0.12f), new Color(0.95f, 0.72f, 0.12f, 1f), 12f);
            CreateMeshBoxChild(parent, "Stage2 Meeting voice mic", new Vector3(-0.24f, -0.12f, 0.3f), new Vector3(0.08f, 0.035f, 0.16f), new Color(0.08f, 0.72f, 0.86f, 1f), -8f);
            CreateSpriteChild(parent, "Stage2 Vote locked marker", diamondSprite, new Vector3(0.24f, 0.1f, 0.36f), new Vector3(0.12f, 0.12f, 0.05f), new Color(0.2f, 0.9f, 0.38f, 0.9f));
            CreateMeshBoxChild(parent, "Stage2 Character footstep L", new Vector3(-0.18f, -0.76f, -0.3f), new Vector3(0.14f, 0.035f, 0.045f), Darken(accent, 0.72f), -10f);
            CreateMeshBoxChild(parent, "Stage2 Character footstep R", new Vector3(0.18f, -0.7f, -0.3f), new Vector3(0.14f, 0.035f, 0.045f), Darken(accent, 0.72f), 10f);
        }

        private void CreateProfessionAccessory(Transform parent, OnlinePlayerState state)
        {
            switch (state.Profession)
            {
                case OnlineProfession.Inspector:
                    CreateSpriteChild(parent, "Badge", diamondSprite, new Vector3(0.18f, 0.08f, 0.2f), new Vector3(0.08f, 0.08f, 0.05f), new Color(0.95f, 0.78f, 0.18f, 1f));
                    CreateSpriteChild(parent, "Radio", capsuleSprite, new Vector3(-0.22f, 0.05f, 0.2f), new Vector3(0.07f, 0.12f, 0.05f), new Color(0.03f, 0.05f, 0.06f, 1f));
                    break;
                case OnlineProfession.Forensics:
                    CreateSpriteChild(parent, "Glove L", capsuleSprite, new Vector3(-0.33f, -0.28f, 0.18f), new Vector3(0.1f, 0.1f, 0.05f), new Color(0.72f, 0.95f, 0.9f, 1f));
                    CreateSpriteChild(parent, "Sample Case", roundedRectSprite, new Vector3(0.28f, -0.34f, 0.18f), new Vector3(0.16f, 0.12f, 0.05f), new Color(0.82f, 0.82f, 0.76f, 1f));
                    break;
                case OnlineProfession.Tech:
                    CreateSpriteChild(parent, "Tech Tablet", roundedRectSprite, new Vector3(0.0f, -0.04f, 0.23f), new Vector3(0.2f, 0.12f, 0.05f), new Color(0.04f, 0.72f, 0.86f, 1f));
                    break;
                case OnlineProfession.UndercoverAgent:
                    CreateSpriteChild(parent, "Hidden Wire", capsuleSprite, new Vector3(0.0f, 0.08f, 0.22f), new Vector3(0.06f, 0.2f, 0.04f), new Color(0.58f, 0.42f, 0.86f, 1f));
                    break;
                case OnlineProfession.Enforcer:
                    CreateSpriteChild(parent, "Chain", capsuleSprite, new Vector3(0.0f, 0.14f, 0.22f), new Vector3(0.22f, 0.04f, 0.04f), new Color(0.9f, 0.76f, 0.24f, 1f));
                    break;
                case OnlineProfession.Fixer:
                    CreateSpriteChild(parent, "Ledger", roundedRectSprite, new Vector3(0.24f, -0.02f, 0.22f), new Vector3(0.14f, 0.18f, 0.05f), new Color(0.74f, 0.62f, 0.38f, 1f));
                    break;
                case OnlineProfession.Driver:
                    CreateSpriteChild(parent, "Cap", capsuleSprite, new Vector3(0f, 0.5f, 0.22f), new Vector3(0.28f, 0.08f, 0.06f), new Color(0.08f, 0.08f, 0.1f, 1f));
                    break;
            }
        }

        private GameObject CreateBodyVisual(OnlineBodyState body)
        {
            GameObject bodyObject = CreateSpriteObject("Online Body " + body.VictimClientId, roundedRectSprite, new Color(0.65f, 0.04f, 0.04f, 1f));
            bodyObject.transform.SetParent(worldRoot.transform, false);
            bodyObject.transform.localScale = new Vector3(0.42f, 0.22f, 0.12f);
            CreatePropChild(bodyObject.transform, "Body Chalk", new Vector3(0f, 0f, -0.04f), new Vector3(0.68f, 0.42f, 0.08f), new Color(0.9f, 0.9f, 0.8f, 0.28f), PrimitiveType.Cylinder);
            CreatePropChild(bodyObject.transform, "Stage2 Forensic evidence card", new Vector3(-0.36f, 0.14f, 0.08f), new Vector3(0.12f, 0.08f, 0.06f), new Color(0.95f, 0.78f, 0.16f, 1f), PrimitiveType.Cube);
            CreatePropChild(bodyObject.transform, "Stage2 Forensic blood marker", new Vector3(0.28f, -0.12f, 0.06f), new Vector3(0.16f, 0.04f, 0.05f), new Color(0.86f, 0.04f, 0.03f, 0.9f), PrimitiveType.Cube);
            CreatePropChild(bodyObject.transform, "Stage2 Forensic police tape A", new Vector3(0f, 0.34f, 0.08f), new Vector3(0.72f, 0.035f, 0.05f), new Color(0.95f, 0.72f, 0.08f, 1f), PrimitiveType.Cube);
            CreatePropChild(bodyObject.transform, "Stage2 Forensic police tape B", new Vector3(0f, -0.34f, 0.08f), new Vector3(0.72f, 0.035f, 0.05f), new Color(0.95f, 0.72f, 0.08f, 1f), PrimitiveType.Cube);
            CreateSpriteChild(bodyObject.transform, "Stage2 Report body radius", circleSprite, new Vector3(0f, 0f, -0.08f), new Vector3(ReportRange * 2f, ReportRange * 1.22f, 0.05f), new Color(0.95f, 0.2f, 0.12f, 0.14f));
            CreateWorldLabel(bodyObject.transform, "报案", new Vector3(0f, 0.52f, -0.14f), 0.055f);
            CreateWorldLabel(bodyObject.transform, "尸体", new Vector3(0f, 0.32f, -0.12f), 0.06f);
            return bodyObject;
        }

        private void CreateFloor()
        {
            CreateProp("港区街区外暗区", new Vector3(0f, 0f, -0.34f), new Vector3(26.2f, 16.8f, 0.08f), new Color(0.025f, 0.032f, 0.034f, 1f));
            CreateShapeProp("港区不规则边界底板", softCircleSprite, new Vector3(0f, 0f, -0.31f), new Vector3(23.8f, 14.5f, 0.08f), new Color(0.082f, 0.098f, 0.102f, 1f));
            CreateProp("港区主干道暗面", new Vector3(0f, -0.1f, -0.3f), new Vector3(24.0f, 8.6f, 0.08f), new Color(0.094f, 0.112f, 0.116f, 1f));
            CreateProp("港区北侧仓储街块", new Vector3(0f, 4.7f, -0.305f), new Vector3(22.5f, 4.7f, 0.08f), new Color(0.086f, 0.104f, 0.11f, 1f));
            CreateProp("港区南侧封控街块", new Vector3(0f, -5.2f, -0.305f), new Vector3(22.2f, 3.9f, 0.08f), new Color(0.086f, 0.104f, 0.11f, 1f));
            CreateProp("北侧港区围挡", new Vector3(0f, DesignMapHalfHeight, 0.02f), new Vector3(24.0f, 0.24f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("南侧港区围挡", new Vector3(0f, -DesignMapHalfHeight, 0.02f), new Vector3(24.0f, 0.24f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("西侧港区围挡", new Vector3(-DesignMapHalfWidth, 0f, 0.02f), new Vector3(0.24f, 15.0f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
            CreateProp("东侧港区围挡", new Vector3(DesignMapHalfWidth, 0f, 0.02f), new Vector3(0.24f, 15.0f, 0.32f), new Color(0.035f, 0.043f, 0.048f, 1f));
        }

        private void CreateRoadNetwork()
        {
            Color mainCorridor = new Color(0.2f, 0.22f, 0.23f, 1f);
            Color branchCorridor = new Color(0.16f, 0.18f, 0.19f, 1f);
            Color serviceCorridor = new Color(0.13f, 0.16f, 0.17f, 1f);
            Color trim = new Color(0.42f, 0.48f, 0.5f, 1f);
            Color guide = new Color(0.74f, 0.65f, 0.24f, 1f);

            CreateCorridorSegment("会议中心圆舱", new Vector3(0f, -0.08f, -0.17f), new Vector3(2.15f, 1.45f, 0.08f), mainCorridor, true);
            CreateCorridorSegment("西中段弯廊", new Vector3(-3.85f, 0.08f, -0.18f), new Vector3(6.35f, 1.04f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("东中段弯廊", new Vector3(4.15f, -0.15f, -0.18f), new Vector3(6.75f, 1.04f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("西北弯廊", new Vector3(-6.95f, 3.78f, -0.18f), new Vector3(7.2f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东上弯廊", new Vector3(5.15f, 3.98f, -0.18f), new Vector3(7.9f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西南弯廊", new Vector3(-6.25f, -3.72f, -0.18f), new Vector3(7.4f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东南弯廊", new Vector3(4.9f, -3.58f, -0.18f), new Vector3(7.2f, 0.98f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西侧舱梯", new Vector3(-7.18f, 1.45f, -0.18f), new Vector3(1.02f, 5.2f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("西下舱梯", new Vector3(-7.0f, -3.25f, -0.18f), new Vector3(1.02f, 4.5f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("中心竖向短舱", new Vector3(-0.12f, 2.0f, -0.17f), new Vector3(1.12f, 4.65f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("中心南向短舱", new Vector3(0.18f, -3.16f, -0.17f), new Vector3(1.12f, 4.4f, 0.08f), mainCorridor, false);
            CreateCorridorSegment("东侧舱梯", new Vector3(7.12f, 1.18f, -0.18f), new Vector3(1.02f, 5.5f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("东下舱梯", new Vector3(7.35f, -2.6f, -0.18f), new Vector3(1.02f, 4.3f, 0.08f), branchCorridor, false);
            CreateCorridorSegment("金融偏置短廊", new Vector3(4.25f, 1.1f, -0.17f), new Vector3(0.78f, 4.85f, 0.08f), serviceCorridor, false);
            CreateCorridorSegment("指挥舱入口短廊", new Vector3(0.35f, -4.48f, -0.17f), new Vector3(3.75f, 0.78f, 0.08f), serviceCorridor, false);
            CreateCorridorSegment("电房转角舱", new Vector3(8.82f, 4.18f, -0.17f), new Vector3(1.64f, 0.86f, 0.08f), serviceCorridor, true);
            CreateCorridorSegment("证物库转角舱", new Vector3(-7.0f, -4.42f, -0.17f), new Vector3(1.15f, 1.62f, 0.08f), serviceCorridor, true);

            CreateRotatedProp("西上斜向连接舱", new Vector3(-3.82f, 2.38f, -0.16f), new Vector3(4.2f, 0.64f, 0.08f), branchCorridor, 13f);
            CreateRotatedProp("东上斜向连接舱", new Vector3(2.7f, 2.4f, -0.16f), new Vector3(4.1f, 0.64f, 0.08f), branchCorridor, -11f);
            CreateRotatedProp("西下斜向连接舱", new Vector3(-3.6f, -2.1f, -0.16f), new Vector3(4.35f, 0.64f, 0.08f), branchCorridor, -10f);
            CreateRotatedProp("东下斜向连接舱", new Vector3(3.1f, -2.02f, -0.16f), new Vector3(4.15f, 0.64f, 0.08f), branchCorridor, 12f);

            CreateCorridorNode("中央圆节点", new Vector3(0f, 0f, -0.08f), 0.72f, trim);
            CreateCorridorNode("西北圆节点", new Vector3(-7f, 4.15f, -0.08f), 0.54f, trim);
            CreateCorridorNode("东北圆节点", new Vector3(7.25f, 4.15f, -0.08f), 0.54f, trim);
            CreateCorridorNode("西南圆节点", new Vector3(-7f, -3.65f, -0.08f), 0.54f, trim);
            CreateCorridorNode("东南圆节点", new Vector3(7.25f, -3.65f, -0.08f), 0.54f, trim);
            CreateCorridorNode("金融岔口圆节点", new Vector3(4.45f, 0.18f, -0.08f), 0.42f, trim);
            CreateCorridorNode("指挥入口圆节点", new Vector3(0.18f, -4.38f, -0.08f), 0.46f, trim);

            CreateRotatedProp("主走廊导向线 A", new Vector3(-4.8f, 0.08f, -0.07f), new Vector3(4.7f, 0.055f, 0.09f), guide, 2f);
            CreateRotatedProp("主走廊导向线 B", new Vector3(4.85f, -0.14f, -0.07f), new Vector3(5.1f, 0.055f, 0.09f), guide, -2f);
            CreateRotatedProp("北走廊导向线 A", new Vector3(-6.1f, 3.78f, -0.07f), new Vector3(4.8f, 0.05f, 0.09f), guide, 1f);
            CreateRotatedProp("北走廊导向线 B", new Vector3(5.9f, 3.98f, -0.07f), new Vector3(5.4f, 0.05f, 0.09f), guide, -1f);
            CreateRotatedProp("南走廊导向线 A", new Vector3(-5.8f, -3.72f, -0.07f), new Vector3(5.1f, 0.05f, 0.09f), guide, -1.5f);
            CreateRotatedProp("南走廊导向线 B", new Vector3(5.25f, -3.58f, -0.07f), new Vector3(5.0f, 0.05f, 0.09f), guide, 1.5f);
        }

        private void CreateCorridorSegment(string corridorName, Vector3 center, Vector3 size, Color color, bool roundNode)
        {
            GameObject segment = roundNode
                ? CreateShapeProp(corridorName, circleSprite, center, size, color)
                : CreateProp(corridorName, center, size, color);
            segment.transform.SetAsFirstSibling();
            RegisterWalkableArea(center, size);
        }

        private void CreateCorridorNode(string nodeName, Vector3 center, float radius, Color color)
        {
            GameObject node = CreateShapeProp(nodeName, circleSprite, center, new Vector3(radius, radius, 0.08f), color);
            node.transform.SetAsFirstSibling();
            CreateShapeProp(nodeName + " 内圈", circleSprite, center + new Vector3(0f, 0f, 0.02f), new Vector3(radius * 0.62f, radius * 0.62f, 0.08f), new Color(0.18f, 0.22f, 0.23f, 1f));
            RegisterWalkableArea(center, new Vector3(radius * 1.9f, radius * 1.9f, 0.08f));
        }

        private void CreateCorridorTrim(string corridorName, Vector3 center, Vector3 size, Color trimColor, bool horizontal)
        {
            if (horizontal)
            {
                CreateRoad(corridorName + " 上沿", center + new Vector3(0f, size.y * 0.5f - 0.06f, 0.02f), new Vector3(size.x, 0.05f, 0.08f), trimColor);
                CreateRoad(corridorName + " 下沿", center + new Vector3(0f, -size.y * 0.5f + 0.06f, 0.02f), new Vector3(size.x, 0.05f, 0.08f), trimColor);
                return;
            }

            CreateRoad(corridorName + " 左沿", center + new Vector3(-size.x * 0.5f + 0.06f, 0f, 0.02f), new Vector3(0.05f, size.y, 0.08f), trimColor);
            CreateRoad(corridorName + " 右沿", center + new Vector3(size.x * 0.5f - 0.06f, 0f, 0.02f), new Vector3(0.05f, size.y, 0.08f), trimColor);
        }

        private void CreateMapStructureLayer()
        {
            Color wall = new Color(0.055f, 0.065f, 0.068f, 1f);
            Color trim = new Color(0.48f, 0.48f, 0.42f, 1f);
            Color door = new Color(0.88f, 0.66f, 0.12f, 1f);

            CreateRoomFrame("西码头货柜场", new Vector3(-9.3f, 5.35f, 0.09f), new Vector3(4.25f, 2.05f, 0.24f), wall, trim, MapEntrance.South);
            CreateRoomFrame("海关查验区", new Vector3(-5.0f, 5.35f, 0.09f), new Vector3(2.95f, 2.05f, 0.24f), wall, trim, MapEntrance.South);
            CreateRoomFrame("监控室", new Vector3(-9.35f, 1.85f, 0.09f), new Vector3(2.85f, 1.85f, 0.24f), wall, trim, MapEntrance.East);
            CreateRoomFrame("茶餐厅", new Vector3(-4.8f, 1.65f, 0.09f), new Vector3(2.85f, 1.8f, 0.24f), wall, trim, MapEntrance.East);
            CreateRoomFrame("夜市主街", new Vector3(-1.0f, 2.75f, 0.09f), new Vector3(4.0f, 2.05f, 0.24f), wall, trim, MapEntrance.South);
            CreateRoomFrame("金融楼", new Vector3(4.75f, 2.75f, 0.09f), new Vector3(3.3f, 2.05f, 0.24f), wall, trim, MapEntrance.West);
            CreateRoomFrame("电房", new Vector3(8.85f, 5.25f, 0.09f), new Vector3(2.7f, 2.05f, 0.24f), wall, trim, MapEntrance.South);
            CreateRoomFrame("天台通道", new Vector3(8.95f, 1.65f, 0.09f), new Vector3(2.65f, 1.8f, 0.24f), wall, trim, MapEntrance.West);
            CreateRoomFrame("指挥车广场", new Vector3(0f, -5.35f, 0.09f), new Vector3(4.25f, 1.85f, 0.24f), wall, trim, MapEntrance.North);
            CreateRoomFrame("证物库", new Vector3(-8.6f, -5.05f, 0.09f), new Vector3(3.25f, 1.9f, 0.24f), wall, trim, MapEntrance.East);
            CreateRoomFrame("后巷排档", new Vector3(5.6f, -1.55f, 0.09f), new Vector3(3.45f, 2.1f, 0.24f), wall, trim, MapEntrance.West);
            CreateRoomFrame("地下诊所", new Vector3(6.15f, -5.05f, 0.09f), new Vector3(3.35f, 1.9f, 0.24f), wall, trim, MapEntrance.North);

            CreateRoadDetailLayer();
            CreateSharedCityProps();

            CreateDoorMarker("码头门禁黄线", new Vector3(-9.3f, 4.28f, 0.13f), new Vector3(1.0f, 0.07f, 0.08f), door);
            CreateDoorMarker("海关排队黄线", new Vector3(-5.0f, 4.28f, 0.13f), new Vector3(0.9f, 0.07f, 0.08f), door);
            CreateDoorMarker("监控室门灯", new Vector3(-7.82f, 1.85f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), door);
            CreateDoorMarker("茶餐厅门灯", new Vector3(-3.28f, 1.65f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), door);
            CreateDoorMarker("夜市入口灯带", new Vector3(-1.0f, 1.62f, 0.13f), new Vector3(1.2f, 0.07f, 0.08f), new Color(0.96f, 0.22f, 0.36f, 1f));
            CreateDoorMarker("金融楼门灯", new Vector3(3.02f, 2.75f, 0.13f), new Vector3(0.08f, 0.82f, 0.08f), new Color(0.32f, 0.72f, 1f, 1f));
            CreateDoorMarker("电房警戒门", new Vector3(8.85f, 4.16f, 0.13f), new Vector3(0.92f, 0.07f, 0.08f), door);
            CreateDoorMarker("天台铁门", new Vector3(7.55f, 1.65f, 0.13f), new Vector3(0.08f, 0.72f, 0.08f), trim);
            CreateDoorMarker("指挥广场入口", new Vector3(0f, -4.38f, 0.13f), new Vector3(1.0f, 0.07f, 0.08f), new Color(0.28f, 0.52f, 1f, 1f));
            CreateDoorMarker("证物库门禁", new Vector3(-6.88f, -5.05f, 0.13f), new Vector3(0.08f, 0.78f, 0.08f), door);
            CreateDoorMarker("后巷入口灯", new Vector3(3.82f, -1.55f, 0.13f), new Vector3(0.08f, 0.78f, 0.08f), new Color(0.88f, 0.36f, 0.12f, 1f));
            CreateDoorMarker("诊所卷闸门", new Vector3(6.15f, -4.02f, 0.13f), new Vector3(0.92f, 0.07f, 0.08f), new Color(0.52f, 0.78f, 0.72f, 1f));
        }

        private void CreateRoomFrame(string roomName, Vector3 center, Vector3 size, Color wallColor, Color trimColor, MapEntrance entrance)
        {
            float wallThickness = 0.08f;
            float doorGap = Mathf.Min(1.45f, size.x * 0.42f);
            float verticalDoorGap = Mathf.Min(1.2f, size.y * 0.5f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float horizontalSegment = Mathf.Max(0.1f, (size.x - doorGap) * 0.5f);
            float verticalSegment = Mathf.Max(0.1f, (size.y - verticalDoorGap) * 0.5f);

            if (entrance == MapEntrance.North)
            {
                CreateWallSegment(roomName + " 北墙左", center + new Vector3(-(doorGap + horizontalSegment) * 0.5f, halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
                CreateWallSegment(roomName + " 北墙右", center + new Vector3((doorGap + horizontalSegment) * 0.5f, halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 北墙", center + new Vector3(0f, halfHeight, 0f), new Vector3(size.x, wallThickness, size.z), wallColor);
            }

            if (entrance == MapEntrance.South)
            {
                CreateWallSegment(roomName + " 南墙左", center + new Vector3(-(doorGap + horizontalSegment) * 0.5f, -halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
                CreateWallSegment(roomName + " 南墙右", center + new Vector3((doorGap + horizontalSegment) * 0.5f, -halfHeight, 0f), new Vector3(horizontalSegment, wallThickness, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 南墙", center + new Vector3(0f, -halfHeight, 0f), new Vector3(size.x, wallThickness, size.z), wallColor);
            }

            if (entrance == MapEntrance.East)
            {
                CreateWallSegment(roomName + " 东墙上", center + new Vector3(halfWidth, (verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
                CreateWallSegment(roomName + " 东墙下", center + new Vector3(halfWidth, -(verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 东墙", center + new Vector3(halfWidth, 0f, 0f), new Vector3(wallThickness, size.y, size.z), wallColor);
            }

            if (entrance == MapEntrance.West)
            {
                CreateWallSegment(roomName + " 西墙上", center + new Vector3(-halfWidth, (verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
                CreateWallSegment(roomName + " 西墙下", center + new Vector3(-halfWidth, -(verticalDoorGap + verticalSegment) * 0.5f, 0f), new Vector3(wallThickness, verticalSegment, size.z), wallColor);
            }
            else
            {
                CreateWallSegment(roomName + " 西墙", center + new Vector3(-halfWidth, 0f, 0f), new Vector3(wallThickness, size.y, size.z), wallColor);
            }

            CreateProp(roomName + " 顶部细线", center + new Vector3(0f, halfHeight - 0.16f, 0.03f), new Vector3(size.x - 0.34f, 0.035f, 0.05f), trimColor);
            CreateProp(roomName + " 底部细线", center + new Vector3(0f, -halfHeight + 0.16f, 0.03f), new Vector3(size.x - 0.34f, 0.035f, 0.05f), trimColor);
            CreateProp(roomName + " 左侧细线", center + new Vector3(-halfWidth + 0.16f, 0f, 0.03f), new Vector3(0.035f, size.y - 0.34f, 0.05f), trimColor);
            CreateProp(roomName + " 右侧细线", center + new Vector3(halfWidth - 0.16f, 0f, 0.03f), new Vector3(0.035f, size.y - 0.34f, 0.05f), trimColor);
            CreateRoomRoundedCaps(roomName, center, size, trimColor);
            CreateRoomCornerCutouts(roomName, center, size);
            CreateRoomAirlockBulges(roomName, center, size, trimColor, entrance);
        }

        private void CreateRoomRoundedCaps(string roomName, Vector3 center, Vector3 size, Color trimColor)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float cap = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.18f, 0.2f, 0.42f);

            CreateShapeProp(roomName + " 圆角舱壁 NW", circleSprite, center + new Vector3(-halfWidth + cap * 0.45f, halfHeight - cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 NE", circleSprite, center + new Vector3(halfWidth - cap * 0.45f, halfHeight - cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 SW", circleSprite, center + new Vector3(-halfWidth + cap * 0.45f, -halfHeight + cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
            CreateShapeProp(roomName + " 圆角舱壁 SE", circleSprite, center + new Vector3(halfWidth - cap * 0.45f, -halfHeight + cap * 0.45f, 0.04f), new Vector3(cap, cap, 0.05f), trimColor);
        }

        private void CreateRoomCornerCutouts(string roomName, Vector3 center, Vector3 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float cut = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.34f, 0.38f, 0.72f);
            Color voidColor = new Color(0.045f, 0.055f, 0.055f, 1f);

            CreateShapeProp(roomName + " 舱室剪影 NW", circleSprite, center + new Vector3(-halfWidth - cut * 0.08f, halfHeight + cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 NE", circleSprite, center + new Vector3(halfWidth + cut * 0.08f, halfHeight + cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 SW", circleSprite, center + new Vector3(-halfWidth - cut * 0.08f, -halfHeight - cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);
            CreateShapeProp(roomName + " 舱室剪影 SE", circleSprite, center + new Vector3(halfWidth + cut * 0.08f, -halfHeight - cut * 0.08f, 0.24f), new Vector3(cut, cut, 0.05f), voidColor);

            if (size.x > 3.2f)
            {
                CreateRotatedProp(roomName + " 斜切暗角 A", center + new Vector3(-halfWidth * 0.6f, halfHeight + 0.02f, 0.23f), new Vector3(size.x * 0.28f, 0.1f, 0.05f), voidColor, -14f);
                CreateRotatedProp(roomName + " 斜切暗角 B", center + new Vector3(halfWidth * 0.55f, -halfHeight - 0.02f, 0.23f), new Vector3(size.x * 0.26f, 0.1f, 0.05f), voidColor, 12f);
            }
        }

        private void CreateRoomAirlockBulges(string roomName, Vector3 center, Vector3 size, Color trimColor, MapEntrance entrance)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Color glass = new Color(0.15f, 0.24f, 0.25f, 1f);

            switch (entrance)
            {
                case MapEntrance.North:
                    CreateShapeProp(roomName + " 外凸气闸", circleSprite, center + new Vector3(0f, halfHeight + 0.08f, 0.12f), new Vector3(0.62f, 0.38f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(0f, halfHeight + 0.1f, 0.18f), new Vector3(0.38f, 0.06f, 0.05f), glass);
                    break;
                case MapEntrance.South:
                    CreateShapeProp(roomName + " 外凸气闸", circleSprite, center + new Vector3(0f, -halfHeight - 0.08f, 0.12f), new Vector3(0.62f, 0.38f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(0f, -halfHeight - 0.1f, 0.18f), new Vector3(0.38f, 0.06f, 0.05f), glass);
                    break;
                case MapEntrance.East:
                    CreateShapeProp(roomName + " 外凸气闸", circleSprite, center + new Vector3(halfWidth + 0.08f, 0f, 0.12f), new Vector3(0.38f, 0.62f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(halfWidth + 0.1f, 0f, 0.18f), new Vector3(0.06f, 0.38f, 0.05f), glass);
                    break;
                case MapEntrance.West:
                    CreateShapeProp(roomName + " 外凸气闸", circleSprite, center + new Vector3(-halfWidth - 0.08f, 0f, 0.12f), new Vector3(0.38f, 0.62f, 0.05f), trimColor);
                    CreateProp(roomName + " 气闸玻璃", center + new Vector3(-halfWidth - 0.1f, 0f, 0.18f), new Vector3(0.06f, 0.38f, 0.05f), glass);
                    break;
            }
        }

        private void CreateWallSegment(string wallName, Vector3 position, Vector3 scale, Color color)
        {
            CreateSolidProp(wallName, position, scale, color);
        }

        private void CreateDoorMarker(string markerName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject marker = CreateProp(markerName, position, scale, color);
            marker.name = markerName + " Door Marker";
            CreateDoorModelOverlay(markerName, position, scale);
        }

        private void CreateArchitecturalVolumeLayer()
        {
            CreateBuildingVolume("西码头仓库", new Vector3(-9.3f, 5.35f, 0f), new Vector3(4.25f, 2.05f, 0.16f), 0.9f, new Color(0.12f, 0.18f, 0.17f, 1f), new Color(0.06f, 0.09f, 0.1f, 1f), "WHARF");
            CreateBuildingVolume("海关查验楼", new Vector3(-5.0f, 5.35f, 0f), new Vector3(2.95f, 2.05f, 0.16f), 1.05f, new Color(0.16f, 0.2f, 0.16f, 1f), new Color(0.08f, 0.1f, 0.09f, 1f), "CUSTOMS");
            CreateBuildingVolume("监控中心", new Vector3(-9.35f, 1.85f, 0f), new Vector3(2.85f, 1.85f, 0.16f), 1.15f, new Color(0.1f, 0.16f, 0.22f, 1f), new Color(0.05f, 0.08f, 0.1f, 1f), "CCTV");
            CreateBuildingVolume("茶餐厅骑楼", new Vector3(-4.8f, 1.65f, 0f), new Vector3(2.85f, 1.8f, 0.16f), 0.78f, new Color(0.34f, 0.19f, 0.1f, 1f), new Color(0.16f, 0.08f, 0.05f, 1f), "茶餐厅");
            CreateBuildingVolume("庙街夜市棚群", new Vector3(-1.0f, 2.75f, 0f), new Vector3(4.0f, 2.05f, 0.16f), 0.62f, new Color(0.3f, 0.12f, 0.08f, 1f), new Color(0.12f, 0.05f, 0.04f, 1f), "NIGHT");
            CreateBuildingVolume("黑钱金融楼", new Vector3(4.75f, 2.75f, 0f), new Vector3(3.3f, 2.05f, 0.16f), 1.55f, new Color(0.14f, 0.16f, 0.24f, 1f), new Color(0.05f, 0.06f, 0.1f, 1f), "FINANCE");
            CreateBuildingVolume("港区电房", new Vector3(8.85f, 5.25f, 0f), new Vector3(2.7f, 2.05f, 0.16f), 1.05f, new Color(0.12f, 0.17f, 0.22f, 1f), new Color(0.05f, 0.07f, 0.08f, 1f), "POWER");
            CreateBuildingVolume("天台机房", new Vector3(8.95f, 1.65f, 0f), new Vector3(2.65f, 1.8f, 0.16f), 1.3f, new Color(0.16f, 0.16f, 0.24f, 1f), new Color(0.07f, 0.07f, 0.1f, 1f), "ROOF");
            CreateBuildingVolume("警队指挥车棚", new Vector3(0f, -5.35f, 0f), new Vector3(4.25f, 1.85f, 0.16f), 0.72f, new Color(0.1f, 0.17f, 0.24f, 1f), new Color(0.04f, 0.07f, 0.1f, 1f), "COMMAND");
            CreateBuildingVolume("证物库冷仓", new Vector3(-8.6f, -5.05f, 0f), new Vector3(3.25f, 1.9f, 0.16f), 1.15f, new Color(0.16f, 0.16f, 0.23f, 1f), new Color(0.07f, 0.07f, 0.1f, 1f), "EVIDENCE");
            CreateBuildingVolume("后巷排档楼", new Vector3(5.6f, -1.55f, 0f), new Vector3(3.45f, 2.1f, 0.16f), 0.86f, new Color(0.26f, 0.13f, 0.08f, 1f), new Color(0.1f, 0.05f, 0.04f, 1f), "ALLEY");
            CreateBuildingVolume("地下诊所唐楼", new Vector3(6.15f, -5.05f, 0f), new Vector3(3.35f, 1.9f, 0.16f), 1.22f, new Color(0.12f, 0.22f, 0.18f, 1f), new Color(0.05f, 0.09f, 0.07f, 1f), "CLINIC");
            CreateTopDownFacilityBackdrop();
        }

        private void CreateBuildingVolume(string name, Vector3 center, Vector3 size, float height, Color facadeColor, Color roofColor, string sign)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Color trim = new Color(0.5f, 0.54f, 0.5f, 1f);
            Color darkTrim = new Color(0.035f, 0.045f, 0.05f, 1f);

            CreateProp("2.5D 建筑体 " + name + " 顶视阴影", center + new Vector3(0.1f, -0.1f, -0.13f), new Vector3(size.x + 0.3f, size.y + 0.26f, 0.05f), new Color(0f, 0f, 0f, 0.22f));
            CreateProp("2.5D 建筑体 " + name + " 室内地台", center + new Vector3(0f, 0f, -0.04f), new Vector3(size.x * 0.93f, size.y * 0.86f, 0.08f), Darken(facadeColor, 1.18f));
            CreateProp("屋顶 " + name + " 顶视房间铭牌", center + new Vector3(0f, halfHeight - 0.18f, 0.15f), new Vector3(Mathf.Min(size.x * 0.72f, 2.35f), 0.14f, 0.08f), roofColor);
            CreateProp("2.5D 建筑体 " + name + " 北墙厚边", center + new Vector3(0f, halfHeight - 0.04f, 0.18f), new Vector3(size.x, 0.13f, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 南墙厚边", center + new Vector3(0f, -halfHeight + 0.04f, 0.18f), new Vector3(size.x, 0.13f, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 西墙厚边", center + new Vector3(-halfWidth + 0.04f, 0f, 0.18f), new Vector3(0.13f, size.y, 0.14f), darkTrim);
            CreateProp("2.5D 建筑体 " + name + " 东墙厚边", center + new Vector3(halfWidth - 0.04f, 0f, 0.18f), new Vector3(0.13f, size.y, 0.14f), darkTrim);
            CreateRoomFloorTiles(name, center, size, facadeColor);
            CreateRoomEquipmentBays(name, center, size, height);
            CreateProp("屋顶 " + name + " 门楣灯", center + new Vector3(0f, -halfHeight + 0.18f, 0.22f), new Vector3(Mathf.Min(1.05f, size.x * 0.35f), 0.08f, 0.08f), new Color(0.94f, 0.72f, 0.12f, 1f));
            CreateProp("屋顶 " + name + " 导航箭头", center + new Vector3(-halfWidth + 0.35f, halfHeight - 0.34f, 0.2f), new Vector3(0.22f, 0.18f, 0.08f), trim);
            CreateWorldLabelAt(sign, ScaleMapPosition(new Vector3(center.x, center.y + halfHeight - 0.3f, -0.18f)), 0.055f);
        }

        private void CreateRoomFloorTiles(string name, Vector3 center, Vector3 size, Color baseColor)
        {
            int columns = Mathf.Clamp(Mathf.RoundToInt(size.x * 1.2f), 3, 7);
            int rows = Mathf.Clamp(Mathf.RoundToInt(size.y * 1.5f), 2, 5);
            float tileWidth = size.x * 0.78f / columns;
            float tileHeight = size.y * 0.62f / rows;
            float startX = center.x - size.x * 0.39f + tileWidth * 0.5f;
            float startY = center.y - size.y * 0.3f + tileHeight * 0.5f;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    float x = startX + column * tileWidth;
                    float y = startY + row * tileHeight;
                    float shade = (row + column) % 2 == 0 ? 1.26f : 1.08f;
                    CreateProp("2.5D 建筑体 " + name + " 顶视地砖 " + row + "-" + column, new Vector3(x, y, 0.02f), new Vector3(tileWidth * 0.82f, tileHeight * 0.78f, 0.04f), Darken(baseColor, shade));
                }
            }
        }

        private void CreateRoomEquipmentBays(string name, Vector3 center, Vector3 size, float height)
        {
            Color screen = new Color(0.06f, 0.58f, 0.72f, 1f);
            Color metal = new Color(0.08f, 0.09f, 0.1f, 1f);
            Color warning = new Color(0.86f, 0.68f, 0.12f, 1f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            CreateProp("屋顶 " + name + " 设备台 A", center + new Vector3(-halfWidth * 0.46f, -halfHeight * 0.18f, 0.2f), new Vector3(0.38f, 0.26f, 0.12f), metal);
            CreateProp("屋顶 " + name + " 设备屏 A", center + new Vector3(-halfWidth * 0.46f, -halfHeight * 0.02f, 0.26f), new Vector3(0.3f, 0.06f, 0.08f), screen);
            CreateProp("屋顶 " + name + " 设备台 B", center + new Vector3(halfWidth * 0.42f, halfHeight * 0.05f, 0.2f), new Vector3(0.34f, 0.3f, 0.12f), metal);
            CreateProp("屋顶 " + name + " 状态灯 B", center + new Vector3(halfWidth * 0.42f, halfHeight * 0.23f, 0.26f), new Vector3(0.22f, 0.05f, 0.07f), screen);
            CreateProp("2.5D 建筑体 " + name + " 警戒斜纹 A", center + new Vector3(-halfWidth * 0.18f, -halfHeight * 0.36f, 0.16f), new Vector3(0.56f, 0.05f, 0.06f), warning);
            CreateProp("2.5D 建筑体 " + name + " 警戒斜纹 B", center + new Vector3(halfWidth * 0.1f, -halfHeight * 0.36f, 0.16f), new Vector3(0.56f, 0.05f, 0.06f), warning);

            if (height > 1.05f)
            {
                CreateProp("屋顶 " + name + " 高风险设备箱", center + new Vector3(0f, 0f, 0.28f), new Vector3(0.36f, 0.24f, 0.12f), new Color(0.22f, 0.2f, 0.12f, 1f));
                CreateProp("屋顶 " + name + " 红色警示点", center + new Vector3(0f, 0.18f, 0.34f), new Vector3(0.08f, 0.08f, 0.06f), new Color(0.9f, 0.08f, 0.06f, 1f));
            }
        }

        private void CreateTopDownFacilityBackdrop()
        {
            Color[] colors =
            {
                new Color(0.07f, 0.085f, 0.09f, 1f),
                new Color(0.06f, 0.075f, 0.08f, 1f),
                new Color(0.08f, 0.075f, 0.065f, 1f)
            };

            for (int i = 0; i < 12; i++)
            {
                float x = -11.4f + i * 2.08f;
                CreateProp("2.5D 建筑体 外围封闭舱段 " + i, new Vector3(x, 7.55f, -0.22f), new Vector3(1.44f, 0.42f, 0.08f), colors[i % colors.Length]);
                CreateProp("屋顶 外围封闭舱段 " + i, new Vector3(x, 7.22f, -0.18f), new Vector3(1.14f, 0.08f, 0.06f), new Color(0.025f, 0.035f, 0.04f, 1f));
            }
        }

        private void CreateRoadDetailLayer()
        {
            Color panelLine = new Color(0.52f, 0.58f, 0.56f, 1f);
            Color rail = new Color(0.34f, 0.4f, 0.4f, 1f);
            Color vent = new Color(0.05f, 0.065f, 0.07f, 1f);
            Color yellow = new Color(0.84f, 0.66f, 0.08f, 1f);

            for (int i = 0; i < 11; i++)
            {
                float x = -9.8f + i * 1.95f;
                CreateProp("主舱地板接缝 " + i, new Vector3(x, 0f, -0.08f), new Vector3(0.52f, 0.035f, 0.06f), panelLine);
            }

            for (int i = 0; i < 9; i++)
            {
                float x = -8.2f + i * 2.05f;
                CreateProp("南舱地板接缝 " + i, new Vector3(x, -3.65f, -0.08f), new Vector3(0.48f, 0.035f, 0.06f), panelLine);
            }

            for (int i = 0; i < 8; i++)
            {
                float y = -5.7f + i * 1.55f;
                CreateProp("西舱导轨 " + i, new Vector3(-7f, y, -0.08f), new Vector3(0.035f, 0.5f, 0.06f), rail);
                CreateProp("东舱导轨 " + i, new Vector3(7.25f, y, -0.08f), new Vector3(0.035f, 0.5f, 0.06f), rail);
            }

            for (int i = 0; i < 5; i++)
            {
                CreateRotatedProp("码头气闸黄黑条 " + i, new Vector3(-6.95f + i * 0.18f, 4.15f, -0.07f), new Vector3(0.07f, 0.62f, 0.06f), i % 2 == 0 ? yellow : vent, 0f);
                CreateRotatedProp("指挥舱黄黑条 " + i, new Vector3(-0.36f + i * 0.18f, -4.15f, -0.07f), new Vector3(0.07f, 0.62f, 0.06f), i % 2 == 0 ? yellow : vent, 0f);
            }

            CreateVentGrate("通风口 A", new Vector3(-3.2f, 0.32f, -0.05f));
            CreateVentGrate("通风口 B", new Vector3(4.9f, -0.32f, -0.05f));
            CreateVentGrate("通风口 C", new Vector3(-1.2f, -3.98f, -0.05f));

            CreateProp("北侧舱内导向杆", new Vector3(2.2f, 4.15f, -0.06f), new Vector3(0.64f, 0.05f, 0.06f), panelLine);
            CreateShapeProp("北侧舱内导向头", diamondSprite, new Vector3(2.62f, 4.15f, -0.05f), new Vector3(0.22f, 0.22f, 0.06f), panelLine);
            CreateProp("后舱导向杆", new Vector3(7.25f, -2.8f, -0.06f), new Vector3(0.05f, 0.64f, 0.06f), panelLine);
            CreateShapeProp("后舱导向头", diamondSprite, new Vector3(7.25f, -3.2f, -0.05f), new Vector3(0.22f, 0.22f, 0.06f), panelLine);
        }

        private void CreateVentGrate(string name, Vector3 position)
        {
            Color vent = new Color(0.04f, 0.055f, 0.06f, 1f);
            Color slit = new Color(0.42f, 0.48f, 0.48f, 1f);
            CreateModelProp(name + " CC0 Vent", name.Contains("主") ? "Props/Prop_Vent_Big.fbx" : "Props/Prop_Vent_Small.fbx", position + new Vector3(0f, 0f, 0.08f), new Vector3(0.48f, 0.48f, 0.14f), 0f);
            CreateShapeProp(name, circleSprite, position, new Vector3(0.32f, 0.32f, 0.06f), vent);

            for (int i = 0; i < 3; i++)
            {
                CreateProp(name + " 格栅 " + i, position + new Vector3(0f, -0.08f + i * 0.08f, 0.03f), new Vector3(0.22f, 0.025f, 0.04f), slit);
            }
        }

        private void CreateSharedCityProps()
        {
            Color metal = new Color(0.1f, 0.12f, 0.13f, 1f);
            Color plastic = new Color(0.14f, 0.28f, 0.32f, 1f);
            Color warning = new Color(0.86f, 0.66f, 0.1f, 1f);

            for (int i = 0; i < 5; i++)
            {
                CreatePrimitiveProp("外壳铆钉 " + i, PrimitiveType.Cylinder, new Vector3(-5.2f + i * 2.6f, -6.98f, 0.08f), new Vector3(0.1f, 0.12f, 0.1f), metal);
                CreateProp("外壳加固梁 " + i, new Vector3(-4.0f + i * 2.6f, -6.98f, 0.1f), new Vector3(1.9f, 0.035f, 0.06f), metal);
            }

            CreateSolidProp("墙面监控终端", new Vector3(2.95f, -3.25f, 0.08f), new Vector3(0.16f, 0.88f, 0.28f), new Color(0.08f, 0.16f, 0.18f, 1f));
            CreateProp("终端冷光屏", new Vector3(3.02f, -3.25f, 0.22f), new Vector3(0.05f, 0.72f, 0.16f), new Color(0.32f, 0.86f, 0.95f, 1f));
            CreateSolidProp("警用通讯柱", new Vector3(-2.7f, -3.18f, 0.08f), new Vector3(0.34f, 0.42f, 0.28f), new Color(0.14f, 0.18f, 0.2f, 1f));
            CreateProp("通讯柱灯窗", new Vector3(-2.7f, -3.19f, 0.2f), new Vector3(0.24f, 0.3f, 0.08f), new Color(0.28f, 0.68f, 0.78f, 1f));
            CreateSolidProp("舱内补给柜", new Vector3(1.9f, 0.82f, 0.08f), new Vector3(0.34f, 0.48f, 0.28f), plastic);
            CreateProp("补给柜状态灯", new Vector3(1.9f, 1.06f, 0.22f), new Vector3(0.24f, 0.04f, 0.1f), new Color(0.88f, 0.18f, 0.22f, 1f));
            CreateSolidProp("可疑封控箱 A", new Vector3(-1.05f, -0.68f, 0.06f), new Vector3(0.62f, 0.16f, 0.18f), warning);
            CreateSolidProp("可疑封控箱 B", new Vector3(1.05f, 0.68f, 0.06f), new Vector3(0.62f, 0.16f, 0.18f), warning);
            CreateProp("封控箱黑条 A", new Vector3(-1.05f, -0.68f, 0.16f), new Vector3(0.36f, 0.04f, 0.06f), metal);
            CreateProp("封控箱黑条 B", new Vector3(1.05f, 0.68f, 0.16f), new Vector3(0.36f, 0.04f, 0.06f), metal);

            CreatePrimitiveProp("旋转摄像头底座", PrimitiveType.Cylinder, new Vector3(3.35f, 0.72f, 0.14f), new Vector3(0.12f, 0.1f, 0.12f), metal);
            CreateProp("旋转摄像头机身", new Vector3(3.35f, 0.9f, 0.28f), new Vector3(0.2f, 0.1f, 0.18f), metal);
            CreatePrimitiveProp("摄像头红点", PrimitiveType.Sphere, new Vector3(3.28f, 0.91f, 0.34f), new Vector3(0.04f, 0.04f, 0.04f), new Color(0.9f, 0.08f, 0.05f, 1f));
            CreatePrimitiveProp("摄像头绿点", PrimitiveType.Sphere, new Vector3(3.42f, 0.91f, 0.34f), new Vector3(0.04f, 0.04f, 0.04f), new Color(0.08f, 0.78f, 0.18f, 1f));

            CreateProp("气闸隔离条 A", new Vector3(-5.65f, -3.62f, 0.06f), new Vector3(0.62f, 0.12f, 0.18f), new Color(0.82f, 0.18f, 0.1f, 1f));
            CreateProp("气闸隔离条 B", new Vector3(-4.92f, -3.62f, 0.06f), new Vector3(0.62f, 0.12f, 0.18f), new Color(0.82f, 0.18f, 0.1f, 1f));
            CreateProp("墙边档案柜", new Vector3(-6.45f, 0.78f, 0.06f), new Vector3(0.36f, 0.3f, 0.22f), new Color(0.08f, 0.2f, 0.28f, 1f));
            CreateProp("档案柜屏幕", new Vector3(-6.45f, 0.92f, 0.18f), new Vector3(0.26f, 0.04f, 0.08f), new Color(0.34f, 0.88f, 0.95f, 1f));
            CreateUnderworldPassageNodes();
        }

        private void CreateUnderworldPassageNodes()
        {
            for (int i = 0; i < UnderworldPassageCount; i++)
            {
                Vector3 position = UnderworldPassageDesignPosition(i);
                CreateModelProp("暗线节点 " + i + " CC0 Vent Hatch", "Props/Prop_Vent_Big.fbx", position + new Vector3(0f, 0f, 0.02f), new Vector3(0.62f, 0.62f, 0.16f), i * 23f);
                GameObject node = CreatePrimitiveProp("暗线节点 " + i, PrimitiveType.Cylinder, position + new Vector3(0f, 0f, 0.1f), new Vector3(0.26f, 0.08f, 0.26f), new Color(0.45f, 0.1f, 0.55f, 1f));
                CreatePropChild(node.transform, "暗线井盖纹", new Vector3(0f, 0f, 0.06f), new Vector3(0.64f, 0.12f, 0.08f), new Color(0.9f, 0.42f, 1f, 1f), PrimitiveType.Cube);
                CreatePropChild(node.transform, "暗线箭头", new Vector3(0f, 0.18f, 0.07f), new Vector3(0.16f, 0.22f, 0.08f), new Color(0.78f, 0.2f, 0.86f, 1f), PrimitiveType.Cube);
            }
        }

        private void CreateLargeMapProps()
        {
            CreateDockyardDressing();
            CreateCustomsDressing();
            CreateCctvRoomDressing();
            CreateTeaCafeDressing();
            CreateNightMarketDressing();
            CreateFinanceDressing();
            CreatePowerRoomDressing();
            CreateRooftopDressing();
            CreateCommandPostDressing();
            CreateEvidenceRoomDressing();
            CreateBackLaneDressing();
            CreateClinicDressing();
        }

        private void CreateZone(string zoneName, Vector3 position, Vector3 scale, Color color)
        {
            CreateProp(zoneName, position, scale, color);
            CreateWorldLabelAt(zoneName, ScaleMapPosition(position + new Vector3(0f, scale.y * 0.34f, -0.16f)), 0.07f);
        }

        private void CreateRoad(string roadName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject road = CreateProp(roadName, position, scale, color);
            road.transform.SetAsFirstSibling();
        }

        private void CreateDockyardDressing()
        {
            Color[] colors =
            {
                new Color(0.08f, 0.15f, 0.22f, 0.78f),
                new Color(0.28f, 0.11f, 0.09f, 0.78f),
                new Color(0.36f, 0.28f, 0.08f, 0.78f),
                new Color(0.08f, 0.22f, 0.15f, 0.78f)
            };

            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    float x = -10.55f + column * 1.45f;
                    float y = 6.05f - row * 0.62f;
                    CreateSolidProp("货柜底影 " + row + "-" + column, new Vector3(x, y, 0.02f), new Vector3(1.16f, 0.34f, 0.08f), colors[(row + column) % colors.Length]);
                    CreateModelProp("成熟港区设施 免费货柜替代模块 " + row + "-" + column, (row + column) % 2 == 0 ? "Props/Prop_Crate4.fbx" : "Props/Prop_Crate3.fbx", new Vector3(x, y, 0.08f), new Vector3(0.92f, 0.34f, 0.34f), column % 2 == 0 ? 0f : 180f, true);
                    CreateProp("货柜细门线 " + row + "-" + column, new Vector3(x - 0.36f, y, 0.18f), new Vector3(0.025f, 0.22f, 0.04f), new Color(0.62f, 0.66f, 0.62f, 0.9f));
                    CreateProp("货柜小编号牌 " + row + "-" + column, new Vector3(x + 0.36f, y + 0.12f, 0.18f), new Vector3(0.13f, 0.035f, 0.035f), new Color(0.82f, 0.72f, 0.22f, 0.9f));
                }
            }

            CreateSolidProp("码头吊机立柱", new Vector3(-10.95f, 4.2f, 0.2f), new Vector3(0.2f, 1.65f, 0.42f), new Color(0.72f, 0.46f, 0.06f, 1f));
            CreateProp("码头吊机横臂", new Vector3(-9.95f, 4.92f, 0.28f), new Vector3(1.95f, 0.12f, 0.22f), new Color(0.72f, 0.46f, 0.06f, 1f));
            CreateProp("吊机挂钩", new Vector3(-9.18f, 4.58f, 0.16f), new Vector3(0.16f, 0.44f, 0.16f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreatePrimitiveProp("系船柱 A", PrimitiveType.Cylinder, new Vector3(-11.1f, 3.65f, 0.08f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.08f, 0.09f, 0.1f, 1f));
            CreatePrimitiveProp("系船柱 B", PrimitiveType.Cylinder, new Vector3(-8.75f, 3.65f, 0.08f), new Vector3(0.18f, 0.08f, 0.18f), new Color(0.08f, 0.09f, 0.1f, 1f));
            CreateProp("港口缆绳", new Vector3(-9.9f, 3.68f, 0.04f), new Vector3(2.1f, 0.05f, 0.06f), new Color(0.48f, 0.36f, 0.18f, 1f));
            CreateProp("叉车车身", new Vector3(-8.38f, 4.72f, 0.07f), new Vector3(0.52f, 0.28f, 0.18f), new Color(0.9f, 0.68f, 0.08f, 1f));
            CreateProp("叉车货叉", new Vector3(-7.92f, 4.72f, 0.08f), new Vector3(0.42f, 0.05f, 0.08f), new Color(0.08f, 0.08f, 0.07f, 1f));
            CreatePrimitiveProp("叉车轮 A", PrimitiveType.Cylinder, new Vector3(-8.56f, 4.55f, 0.08f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.04f, 0.04f, 0.04f, 1f));
            CreatePrimitiveProp("叉车轮 B", PrimitiveType.Cylinder, new Vector3(-8.22f, 4.55f, 0.08f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.04f, 0.04f, 0.04f, 1f));
            CreateProp("地面绑带 A", new Vector3(-10.6f, 5.1f, 0.02f), new Vector3(0.05f, 0.82f, 0.04f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreateProp("地面绑带 B", new Vector3(-8.25f, 5.1f, 0.02f), new Vector3(0.05f, 0.82f, 0.04f), new Color(0.08f, 0.08f, 0.08f, 1f));
            CreateModelDominantDockyardForeground();
        }

        private void CreateModelDominantDockyardForeground()
        {
            Vector3 anchor = new Vector3(-9.42f, 4.92f, 0f);
            CreateModelProp("成熟港区设施 开局主视觉金属平台", "Platforms/Platform_Rails_4WideTall.fbx", anchor + new Vector3(0.1f, -0.42f, 0.04f), new Vector3(2.2f, 0.62f, 0.32f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉门框左", "Platforms/Door_Frame_SquareTall.fbx", anchor + new Vector3(-1.56f, 0.06f, 0.18f), new Vector3(0.42f, 1.22f, 0.64f), 90f, true);
            CreateModelProp("成熟港区设施 开局主视觉门框右", "Platforms/Door_Frame_SquareTall.fbx", anchor + new Vector3(1.56f, 0.02f, 0.18f), new Vector3(0.42f, 1.22f, 0.64f), -90f, true);
            CreateModelProp("成熟港区设施 开局主视觉窗墙", "Walls/WallAstra_Straight_Window.fbx", anchor + new Vector3(0f, 0.78f, 0.28f), new Vector3(2.2f, 0.26f, 0.62f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉电缆墙", "Walls/TopCables_Straight_Hanging.fbx", anchor + new Vector3(-0.1f, 1.08f, 0.46f), new Vector3(1.9f, 0.26f, 0.5f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉地灯左", "Props/Prop_Light_Floor.fbx", anchor + new Vector3(-1.04f, -0.82f, 0.12f), new Vector3(0.36f, 0.36f, 0.44f), 0f);
            CreateModelProp("成熟港区设施 开局主视觉地灯右", "Props/Prop_Light_Floor.fbx", anchor + new Vector3(1.1f, -0.78f, 0.12f), new Vector3(0.36f, 0.36f, 0.44f), 180f);
            CreateModelProp("成熟港区设施 开局主视觉接入终端", "Props/Prop_AccessPoint.fbx", anchor + new Vector3(1.15f, 0.48f, 0.14f), new Vector3(0.5f, 0.36f, 0.38f), 180f);
            CreateModelProp("成熟港区设施 开局主视觉电脑台", "Props/Prop_Computer.fbx", anchor + new Vector3(-1.12f, 0.42f, 0.14f), new Vector3(0.46f, 0.34f, 0.34f), 0f);
            CreateModelProp("成熟港区设施 开局主视觉通风机", "Props/Prop_Vent_Big.fbx", anchor + new Vector3(0.02f, 0.26f, 0.18f), new Vector3(0.72f, 0.36f, 0.24f), 0f, true);
            CreateModelProp("成熟港区设施 开局主视觉弧形护栏左", "Props/Prop_Rail_Round_Big.fbx", anchor + new Vector3(-1.42f, -0.5f, 0.14f), new Vector3(0.64f, 0.46f, 0.28f), 90f, true);
            CreateModelProp("成熟港区设施 开局主视觉弧形护栏右", "Props/Prop_Rail_Round_Big.fbx", anchor + new Vector3(1.42f, -0.5f, 0.14f), new Vector3(0.64f, 0.46f, 0.28f), -90f, true);
            CreateMeshBoxProp("成熟港区设施 开局主视觉冷色导线", anchor + new Vector3(0f, -1.02f, 0.08f), new Vector3(2.15f, 0.04f, 0.05f), new Color(0.08f, 0.72f, 0.86f, 1f));
            CreateMeshBoxProp("成熟港区设施 开局主视觉警戒黄线", anchor + new Vector3(0f, -1.18f, 0.08f), new Vector3(2.3f, 0.04f, 0.05f), new Color(0.92f, 0.7f, 0.08f, 1f));
        }

        private void CreateCustomsDressing()
        {
            CreateSolidProp("查验闸机", new Vector3(-5.0f, 4.42f, 0.06f), new Vector3(1.55f, 0.18f, 0.22f), new Color(0.18f, 0.28f, 0.22f, 1f));
            CreateSolidProp("海关桌", new Vector3(-5.55f, 5.75f, 0.05f), new Vector3(0.82f, 0.42f, 0.18f), new Color(0.24f, 0.24f, 0.2f, 1f));
            CreateSolidProp("扫描门", new Vector3(-4.35f, 5.55f, 0.12f), new Vector3(0.14f, 0.75f, 0.36f), new Color(0.12f, 0.18f, 0.22f, 1f));
            CreateProp("封条箱 A", new Vector3(-5.85f, 4.92f, 0.05f), new Vector3(0.35f, 0.28f, 0.2f), new Color(0.74f, 0.68f, 0.42f, 1f));
            CreateProp("封条箱 B", new Vector3(-5.38f, 4.92f, 0.05f), new Vector3(0.35f, 0.28f, 0.2f), new Color(0.74f, 0.68f, 0.42f, 1f));
            CreateProp("查验告示牌", new Vector3(-4.2f, 4.58f, 0.07f), new Vector3(0.78f, 0.08f, 0.24f), new Color(0.88f, 0.72f, 0.1f, 1f));
            CreateProp("护照托盘", new Vector3(-5.58f, 5.48f, 0.16f), new Vector3(0.32f, 0.16f, 0.05f), new Color(0.08f, 0.18f, 0.36f, 1f));
            CreateProp("查验印章", new Vector3(-5.18f, 5.72f, 0.15f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.46f, 0.1f, 0.08f, 1f));
            CreateProp("行李 X 光带", new Vector3(-4.38f, 4.92f, 0.08f), new Vector3(0.9f, 0.18f, 0.12f), new Color(0.08f, 0.08f, 0.09f, 1f));
            CreatePrimitiveProp("X 光滚轮 A", PrimitiveType.Cylinder, new Vector3(-4.72f, 4.92f, 0.11f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.5f, 0.5f, 0.46f, 1f));
            CreatePrimitiveProp("X 光滚轮 B", PrimitiveType.Cylinder, new Vector3(-4.08f, 4.92f, 0.11f), new Vector3(0.05f, 0.05f, 0.05f), new Color(0.5f, 0.5f, 0.46f, 1f));
        }

        private void CreateCctvRoomDressing()
        {
            CreateSolidProp("监控控制台", new Vector3(-9.35f, 1.2f, 0.06f), new Vector3(1.28f, 0.28f, 0.18f), new Color(0.06f, 0.12f, 0.16f, 1f));

            for (int i = 0; i < 4; i++)
            {
                CreateProp("监控屏 " + i, new Vector3(-10.0f + i * 0.42f, 2.22f, 0.08f), new Vector3(0.32f, 0.06f, 0.22f), new Color(0.05f, 0.45f, 0.58f, 1f));
                CreateProp("监控屏边框 " + i, new Vector3(-10.0f + i * 0.42f, 2.18f, 0.1f), new Vector3(0.36f, 0.035f, 0.24f), new Color(0.02f, 0.03f, 0.04f, 1f));
            }

            CreateSolidProp("录像机柜", new Vector3(-8.28f, 1.25f, 0.07f), new Vector3(0.36f, 0.42f, 0.28f), new Color(0.12f, 0.14f, 0.18f, 1f));
            CreateProp("折叠椅", new Vector3(-9.95f, 1.48f, 0.05f), new Vector3(0.24f, 0.2f, 0.16f), new Color(0.16f, 0.18f, 0.2f, 1f));
            CreateProp("录像带箱", new Vector3(-8.72f, 2.35f, 0.05f), new Vector3(0.46f, 0.24f, 0.18f), new Color(0.2f, 0.2f, 0.18f, 1f));
            CreateProp("键盘灯条", new Vector3(-9.35f, 1.38f, 0.18f), new Vector3(0.98f, 0.04f, 0.06f), new Color(0.08f, 0.72f, 0.82f, 1f));
            CreateProp("咖啡杯", new Vector3(-9.9f, 1.22f, 0.19f), new Vector3(0.1f, 0.1f, 0.1f), new Color(0.74f, 0.68f, 0.54f, 1f));
            CreateProp("硬盘阵列 A", new Vector3(-8.28f, 1.44f, 0.24f), new Vector3(0.28f, 0.05f, 0.05f), new Color(0.08f, 0.62f, 0.18f, 1f));
            CreateProp("硬盘阵列 B", new Vector3(-8.28f, 1.22f, 0.24f), new Vector3(0.28f, 0.05f, 0.05f), new Color(0.08f, 0.62f, 0.18f, 1f));
        }

        private void CreateTeaCafeDressing()
        {
            CreateSolidProp("茶餐厅吧台", new Vector3(-5.6f, 1.95f, 0.06f), new Vector3(0.28f, 1.0f, 0.18f), new Color(0.5f, 0.28f, 0.12f, 1f));

            for (int i = 0; i < 3; i++)
            {
                float y = 1.05f + i * 0.42f;
                CreateSolidProp("卡座桌 " + i, new Vector3(-4.75f, y, 0.05f), new Vector3(0.44f, 0.2f, 0.14f), new Color(0.6f, 0.38f, 0.18f, 1f));
                CreateProp("卡座椅 " + i + "A", new Vector3(-5.1f, y, 0.05f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.42f, 0.12f, 0.08f, 1f));
                CreateProp("卡座椅 " + i + "B", new Vector3(-4.4f, y, 0.05f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.42f, 0.12f, 0.08f, 1f));
                CreateProp("奶茶杯 " + i, new Vector3(-4.75f, y + 0.06f, 0.16f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.78f, 0.56f, 0.32f, 1f));
            }

            CreateProp("收银机", new Vector3(-5.58f, 2.45f, 0.12f), new Vector3(0.18f, 0.18f, 0.14f), new Color(0.12f, 0.16f, 0.18f, 1f));
            CreateProp("厨房隔断", new Vector3(-3.95f, 2.25f, 0.06f), new Vector3(0.7f, 0.12f, 0.18f), new Color(0.72f, 0.62f, 0.42f, 1f));
            CreateSolidProp("冰柜", new Vector3(-3.78f, 1.2f, 0.08f), new Vector3(0.3f, 0.42f, 0.24f), new Color(0.18f, 0.34f, 0.38f, 1f));
            CreateProp("餐牌灯箱", new Vector3(-4.68f, 2.45f, 0.14f), new Vector3(0.72f, 0.07f, 0.18f), new Color(0.92f, 0.72f, 0.26f, 1f));
            CreateProp("厨房炉火", new Vector3(-3.98f, 2.02f, 0.16f), new Vector3(0.16f, 0.07f, 0.08f), new Color(1f, 0.32f, 0.08f, 1f));
            CreateProp("餐具架", new Vector3(-5.58f, 1.42f, 0.18f), new Vector3(0.08f, 0.42f, 0.12f), new Color(0.82f, 0.82f, 0.74f, 1f));
        }

        private void CreateNightMarketDressing()
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -2.35f + i * 1.05f;
                Color stallColor = i % 2 == 0 ? new Color(0.55f, 0.18f, 0.08f, 1f) : new Color(0.18f, 0.38f, 0.18f, 1f);
                CreateSolidProp("夜市摊台 " + i, new Vector3(x, 3.1f, 0.04f), new Vector3(0.72f, 0.36f, 0.2f), stallColor);
                CreateProp("夜市棚顶 " + i, new Vector3(x, 3.35f, 0.18f), new Vector3(0.82f, 0.12f, 0.12f), new Color(0.86f, 0.28f, 0.18f, 1f));
                CreatePrimitiveProp("灯笼 " + i, PrimitiveType.Sphere, new Vector3(x + 0.32f, 3.55f, 0.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.95f, 0.22f, 0.12f, 1f));
                CreateProp("食材盘 " + i, new Vector3(x - 0.16f, 3.08f, 0.18f), new Vector3(0.18f, 0.14f, 0.05f), new Color(0.82f, 0.48f, 0.22f, 1f));
                CreateProp("收钱盒 " + i, new Vector3(x + 0.18f, 3.08f, 0.18f), new Vector3(0.16f, 0.1f, 0.06f), new Color(0.08f, 0.1f, 0.12f, 1f));
            }

            CreateProp("霓虹招牌", new Vector3(-0.65f, 3.78f, 0.06f), new Vector3(1.8f, 0.12f, 0.24f), new Color(0.9f, 0.12f, 0.42f, 1f));
            CreateProp("啤酒箱堆", new Vector3(0.92f, 2.05f, 0.05f), new Vector3(0.42f, 0.32f, 0.18f), new Color(0.36f, 0.2f, 0.08f, 1f));
            CreateSolidProp("排队栏杆 A", new Vector3(-2.85f, 2.25f, 0.06f), new Vector3(0.08f, 0.78f, 0.12f), new Color(0.1f, 0.1f, 0.1f, 1f));
            CreateSolidProp("排队栏杆 B", new Vector3(1.15f, 2.25f, 0.06f), new Vector3(0.08f, 0.78f, 0.12f), new Color(0.1f, 0.1f, 0.1f, 1f));
            CreateProp("地摊胶凳 A", new Vector3(-2.38f, 2.08f, 0.05f), new Vector3(0.18f, 0.18f, 0.12f), new Color(0.82f, 0.18f, 0.12f, 1f));
            CreateProp("地摊胶凳 B", new Vector3(0.38f, 2.05f, 0.05f), new Vector3(0.18f, 0.18f, 0.12f), new Color(0.12f, 0.42f, 0.78f, 1f));
            CreateProp("纸皮箱堆", new Vector3(-1.72f, 2.02f, 0.05f), new Vector3(0.36f, 0.24f, 0.16f), new Color(0.64f, 0.42f, 0.22f, 1f));
            CreateProp("鱼档冰床", new Vector3(-2.06f, 2.34f, 0.06f), new Vector3(0.52f, 0.18f, 0.14f), new Color(0.72f, 0.86f, 0.9f, 1f));
            CreateProp("暗号价牌", new Vector3(-1.98f, 2.62f, 0.16f), new Vector3(0.28f, 0.06f, 0.12f), new Color(0.92f, 0.78f, 0.16f, 1f));
            CreateProp("摊档油桶 A", new Vector3(1.32f, 3.04f, 0.08f), new Vector3(0.16f, 0.16f, 0.18f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("摊档油桶 B", new Vector3(1.52f, 3.04f, 0.08f), new Vector3(0.16f, 0.16f, 0.18f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("夜市布帘", new Vector3(-0.12f, 3.56f, 0.18f), new Vector3(0.52f, 0.08f, 0.1f), new Color(0.2f, 0.34f, 0.68f, 1f));
            CreateProp("霓虹小箭头", new Vector3(0.98f, 3.72f, 0.14f), new Vector3(0.22f, 0.14f, 0.08f), new Color(0.08f, 0.86f, 0.9f, 1f));
        }

        private void CreateFinanceDressing()
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 3.8f + i * 0.7f;
                CreateSolidProp("金融办公桌 " + i, new Vector3(x, 2.55f, 0.05f), new Vector3(0.45f, 0.28f, 0.16f), new Color(0.28f, 0.24f, 0.2f, 1f));
                CreateProp("电脑屏 " + i, new Vector3(x, 2.78f, 0.12f), new Vector3(0.28f, 0.05f, 0.18f), new Color(0.05f, 0.4f, 0.6f, 1f));
                CreateProp("账本 " + i, new Vector3(x - 0.14f, 2.48f, 0.16f), new Vector3(0.16f, 0.1f, 0.04f), new Color(0.78f, 0.72f, 0.54f, 1f));
            }

            CreateSolidProp("保险柜", new Vector3(5.95f, 2.08f, 0.08f), new Vector3(0.42f, 0.42f, 0.3f), new Color(0.18f, 0.18f, 0.22f, 1f));
            CreateSolidProp("档案柜 A", new Vector3(5.95f, 3.05f, 0.08f), new Vector3(0.36f, 0.32f, 0.28f), new Color(0.22f, 0.22f, 0.28f, 1f));
            CreateSolidProp("档案柜 B", new Vector3(6.45f, 3.05f, 0.08f), new Vector3(0.36f, 0.32f, 0.28f), new Color(0.22f, 0.22f, 0.28f, 1f));
            CreateProp("金融楼入口", new Vector3(4.75f, 3.55f, 0.06f), new Vector3(1.05f, 0.16f, 0.24f), new Color(0.32f, 0.32f, 0.42f, 1f));
            CreateProp("保险柜转盘", new Vector3(5.95f, 2.28f, 0.26f), new Vector3(0.08f, 0.04f, 0.08f), new Color(0.78f, 0.72f, 0.54f, 1f));
            CreateProp("碎纸机", new Vector3(3.35f, 3.15f, 0.06f), new Vector3(0.28f, 0.26f, 0.18f), new Color(0.12f, 0.12f, 0.14f, 1f));
            CreateProp("碎纸袋", new Vector3(3.18f, 3.35f, 0.04f), new Vector3(0.18f, 0.16f, 0.1f), new Color(0.68f, 0.68f, 0.62f, 1f));
        }

        private void CreatePowerRoomDressing()
        {
            for (int i = 0; i < 3; i++)
            {
                CreateSolidProp("电房变压器 " + i, new Vector3(8.15f + i * 0.55f, 5.65f, 0.04f), new Vector3(0.34f, 0.52f, 0.32f), new Color(0.18f, 0.24f, 0.34f, 1f));
                CreateProp("电缆桥架 " + i, new Vector3(8.15f + i * 0.55f, 4.52f, 0.08f), new Vector3(0.42f, 0.08f, 0.12f), new Color(0.04f, 0.04f, 0.05f, 1f));
                CreateProp("变压器指示灯 " + i, new Vector3(8.15f + i * 0.55f, 5.92f, 0.22f), new Vector3(0.06f, 0.04f, 0.05f), new Color(0.08f, 0.82f, 0.18f, 1f));
            }

            CreateSolidProp("电闸面板", new Vector3(9.72f, 5.12f, 0.09f), new Vector3(0.28f, 0.62f, 0.28f), new Color(0.08f, 0.12f, 0.18f, 1f));
            CreateProp("黄色警戒线", new Vector3(8.78f, 4.42f, 0.06f), new Vector3(1.45f, 0.08f, 0.1f), new Color(0.9f, 0.7f, 0.08f, 1f));
            CreatePrimitiveProp("压力表", PrimitiveType.Cylinder, new Vector3(9.4f, 5.55f, 0.14f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.72f, 0.78f, 0.75f, 1f));
            CreateProp("红色急停钮", new Vector3(9.72f, 5.36f, 0.25f), new Vector3(0.08f, 0.04f, 0.06f), new Color(0.9f, 0.06f, 0.04f, 1f));
            CreateProp("地面电缆 A", new Vector3(8.68f, 5.05f, 0.01f), new Vector3(1.15f, 0.04f, 0.04f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreateProp("地面电缆 B", new Vector3(9.15f, 5.35f, 0.01f), new Vector3(0.04f, 0.72f, 0.04f), new Color(0.02f, 0.02f, 0.025f, 1f));
        }

        private void CreateRooftopDressing()
        {
            CreateSolidPrimitiveProp("天台水塔", PrimitiveType.Cylinder, new Vector3(9.58f, 1.95f, 0.14f), new Vector3(0.36f, 0.24f, 0.36f), new Color(0.42f, 0.42f, 0.46f, 1f));
            CreateSolidProp("空调外机 A", new Vector3(8.35f, 1.1f, 0.07f), new Vector3(0.38f, 0.3f, 0.22f), new Color(0.54f, 0.54f, 0.52f, 1f));
            CreateSolidProp("空调外机 B", new Vector3(8.85f, 1.1f, 0.07f), new Vector3(0.38f, 0.3f, 0.22f), new Color(0.54f, 0.54f, 0.52f, 1f));
            CreateSolidProp("天台梯门", new Vector3(9.8f, 1.18f, 0.08f), new Vector3(0.32f, 0.46f, 0.28f), new Color(0.18f, 0.18f, 0.22f, 1f));
            CreateSolidProp("围栏北", new Vector3(8.95f, 2.42f, 0.08f), new Vector3(1.7f, 0.07f, 0.14f), new Color(0.1f, 0.1f, 0.12f, 1f));
            CreateSolidProp("围栏东", new Vector3(10.05f, 1.65f, 0.08f), new Vector3(0.07f, 1.35f, 0.14f), new Color(0.1f, 0.1f, 0.12f, 1f));
            CreateProp("天台排水沟", new Vector3(8.12f, 2.16f, 0.02f), new Vector3(0.48f, 0.04f, 0.05f), new Color(0.04f, 0.06f, 0.06f, 1f));
            CreateProp("晾衣绳", new Vector3(9.05f, 1.36f, 0.2f), new Vector3(0.72f, 0.03f, 0.04f), new Color(0.82f, 0.82f, 0.72f, 1f));
            CreateProp("晾晒布 A", new Vector3(8.85f, 1.28f, 0.18f), new Vector3(0.18f, 0.1f, 0.06f), new Color(0.62f, 0.18f, 0.32f, 1f));
            CreateProp("晾晒布 B", new Vector3(9.18f, 1.28f, 0.18f), new Vector3(0.18f, 0.1f, 0.06f), new Color(0.22f, 0.42f, 0.7f, 1f));
        }

        private void CreateCommandPostDressing()
        {
            CreateSolidProp("警用指挥车", new Vector3(0.2f, -5.3f, 0.05f), new Vector3(1.25f, 0.72f, 0.3f), new Color(0.08f, 0.12f, 0.14f, 1f));
            CreateProp("车顶天线", new Vector3(0.2f, -4.82f, 0.28f), new Vector3(0.06f, 0.4f, 0.08f), new Color(0.02f, 0.02f, 0.02f, 1f));
            CreateSolidProp("指挥折叠桌", new Vector3(-1.12f, -5.45f, 0.05f), new Vector3(0.8f, 0.32f, 0.16f), new Color(0.22f, 0.22f, 0.2f, 1f));
            CreateProp("行动白板", new Vector3(-1.12f, -4.92f, 0.12f), new Vector3(0.75f, 0.08f, 0.3f), new Color(0.82f, 0.86f, 0.82f, 1f));
            CreateSolidProp("警灯路障", new Vector3(1.7f, -4.35f, 0.06f), new Vector3(1.2f, 0.12f, 0.18f), new Color(0.1f, 0.28f, 0.9f, 1f));
            CreatePrimitiveProp("路锥 A", PrimitiveType.Cylinder, new Vector3(2.2f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreatePrimitiveProp("路锥 B", PrimitiveType.Cylinder, new Vector3(2.62f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreatePrimitiveProp("路锥 C", PrimitiveType.Cylinder, new Vector3(3.04f, -5.78f, 0.07f), new Vector3(0.14f, 0.1f, 0.14f), new Color(0.9f, 0.34f, 0.08f, 1f));
            CreateProp("车窗玻璃 A", new Vector3(-0.16f, -4.95f, 0.24f), new Vector3(0.28f, 0.05f, 0.1f), new Color(0.18f, 0.48f, 0.58f, 1f));
            CreateProp("车窗玻璃 B", new Vector3(0.42f, -4.95f, 0.24f), new Vector3(0.28f, 0.05f, 0.1f), new Color(0.18f, 0.48f, 0.58f, 1f));
            CreateProp("地图文件 A", new Vector3(-1.2f, -5.38f, 0.16f), new Vector3(0.18f, 0.12f, 0.04f), new Color(0.84f, 0.78f, 0.58f, 1f));
            CreateProp("地图文件 B", new Vector3(-0.92f, -5.48f, 0.16f), new Vector3(0.18f, 0.12f, 0.04f), new Color(0.84f, 0.78f, 0.58f, 1f));
            CreateProp("警灯红", new Vector3(-0.2f, -4.88f, 0.34f), new Vector3(0.16f, 0.06f, 0.06f), new Color(0.9f, 0.06f, 0.06f, 1f));
            CreateProp("警灯蓝", new Vector3(0.6f, -4.88f, 0.34f), new Vector3(0.16f, 0.06f, 0.06f), new Color(0.08f, 0.24f, 0.9f, 1f));
            CreateProp("无人机起降垫", new Vector3(0.96f, -4.62f, 0.08f), new Vector3(0.52f, 0.32f, 0.08f), new Color(0.08f, 0.14f, 0.16f, 1f));
            CreateProp("无人机机臂 A", new Vector3(0.96f, -4.62f, 0.18f), new Vector3(0.5f, 0.05f, 0.06f), new Color(0.08f, 0.62f, 0.8f, 1f));
            CreateProp("无人机机臂 B", new Vector3(0.96f, -4.62f, 0.19f), new Vector3(0.05f, 0.34f, 0.06f), new Color(0.08f, 0.62f, 0.8f, 1f));
            CreateProp("警用电池箱", new Vector3(1.62f, -5.72f, 0.06f), new Vector3(0.32f, 0.22f, 0.16f), new Color(0.18f, 0.26f, 0.28f, 1f));
        }

        private void CreateEvidenceRoomDressing()
        {
            CreateSolidProp("证物冷柜", new Vector3(-8.95f, -5.28f, 0.04f), new Vector3(0.72f, 0.42f, 0.26f), new Color(0.16f, 0.34f, 0.38f, 1f));

            for (int i = 0; i < 3; i++)
            {
                CreateSolidProp("证物货架 " + i, new Vector3(-9.92f + i * 0.62f, -4.45f, 0.07f), new Vector3(0.42f, 0.18f, 0.26f), new Color(0.24f, 0.22f, 0.18f, 1f));
                CreateProp("封存箱 " + i, new Vector3(-9.92f + i * 0.62f, -5.82f, 0.05f), new Vector3(0.36f, 0.28f, 0.18f), new Color(0.7f, 0.62f, 0.38f, 1f));
                CreateProp("证物标签 " + i, new Vector3(-9.92f + i * 0.62f, -4.32f, 0.22f), new Vector3(0.18f, 0.04f, 0.05f), new Color(0.88f, 0.78f, 0.18f, 1f));
            }

            CreateProp("证物封条", new Vector3(-8.18f, -4.42f, 0.08f), new Vector3(0.62f, 0.08f, 0.1f), new Color(0.92f, 0.78f, 0.08f, 1f));
            CreateProp("鉴证灯箱", new Vector3(-7.52f, -5.18f, 0.08f), new Vector3(0.42f, 0.32f, 0.22f), new Color(0.18f, 0.52f, 0.58f, 1f));
            CreateProp("血样冷藏盒", new Vector3(-8.82f, -5.1f, 0.2f), new Vector3(0.2f, 0.12f, 0.06f), new Color(0.68f, 0.08f, 0.08f, 1f));
            CreateProp("证物相片板", new Vector3(-7.72f, -4.58f, 0.16f), new Vector3(0.36f, 0.08f, 0.18f), new Color(0.82f, 0.82f, 0.74f, 1f));
            CreateProp("紫外灯条", new Vector3(-7.52f, -5.02f, 0.22f), new Vector3(0.34f, 0.04f, 0.06f), new Color(0.42f, 0.24f, 0.86f, 1f));
        }

        private void CreateBackLaneDressing()
        {
            CreateSolidProp("后巷垃圾箱", new Vector3(6.2f, -0.95f, 0.04f), new Vector3(0.62f, 0.38f, 0.22f), new Color(0.05f, 0.26f, 0.14f, 1f));
            CreateSolidProp("排档炉头", new Vector3(5.08f, -1.28f, 0.06f), new Vector3(0.42f, 0.28f, 0.2f), new Color(0.18f, 0.18f, 0.16f, 1f));
            CreatePrimitiveProp("煤气瓶 A", PrimitiveType.Cylinder, new Vector3(5.62f, -0.82f, 0.08f), new Vector3(0.12f, 0.18f, 0.12f), new Color(0.18f, 0.42f, 0.42f, 1f));
            CreatePrimitiveProp("煤气瓶 B", PrimitiveType.Cylinder, new Vector3(5.88f, -0.82f, 0.08f), new Vector3(0.12f, 0.18f, 0.12f), new Color(0.18f, 0.42f, 0.42f, 1f));
            CreateProp("雨棚", new Vector3(5.52f, -1.95f, 0.16f), new Vector3(1.28f, 0.12f, 0.12f), new Color(0.42f, 0.1f, 0.08f, 1f));
            CreateSolidProp("黑帮摩托", new Vector3(6.85f, -2.08f, 0.06f), new Vector3(0.66f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.1f, 1f));
            CreateProp("排档火苗", new Vector3(5.08f, -1.12f, 0.18f), new Vector3(0.16f, 0.08f, 0.08f), new Color(1f, 0.28f, 0.06f, 1f));
            CreateProp("墙面涂鸦", new Vector3(4.3f, -0.72f, 0.12f), new Vector3(0.58f, 0.05f, 0.14f), new Color(0.78f, 0.12f, 0.48f, 1f));
            CreateProp("摩托车把", new Vector3(7.18f, -2.08f, 0.14f), new Vector3(0.16f, 0.04f, 0.05f), new Color(0.7f, 0.7f, 0.64f, 1f));
            CreatePrimitiveProp("摩托前轮", PrimitiveType.Cylinder, new Vector3(7.16f, -2.08f, 0.08f), new Vector3(0.09f, 0.04f, 0.09f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreatePrimitiveProp("摩托后轮", PrimitiveType.Cylinder, new Vector3(6.54f, -2.08f, 0.08f), new Vector3(0.09f, 0.04f, 0.09f), new Color(0.02f, 0.02f, 0.025f, 1f));
            CreateProp("摩托车牌架", new Vector3(6.86f, -1.84f, 0.16f), new Vector3(0.22f, 0.05f, 0.06f), new Color(0.84f, 0.84f, 0.76f, 1f));
            CreateProp("后巷油污", new Vector3(6.22f, -2.42f, 0.01f), new Vector3(0.54f, 0.14f, 0.04f), new Color(0.02f, 0.025f, 0.02f, 1f));
            CreateProp("外卖箱", new Vector3(4.62f, -2.42f, 0.06f), new Vector3(0.34f, 0.24f, 0.18f), new Color(0.86f, 0.36f, 0.1f, 1f));
            CreateProp("后门铁闩", new Vector3(4.0f, -1.18f, 0.12f), new Vector3(0.08f, 0.52f, 0.08f), new Color(0.5f, 0.5f, 0.46f, 1f));
        }

        private void CreateClinicDressing()
        {
            CreateSolidProp("诊所病床 A", new Vector3(5.55f, -5.45f, 0.04f), new Vector3(0.78f, 0.38f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
            CreateSolidProp("诊所病床 B", new Vector3(6.65f, -5.45f, 0.04f), new Vector3(0.78f, 0.38f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
            CreateSolidProp("药柜", new Vector3(5.2f, -4.45f, 0.08f), new Vector3(0.42f, 0.32f, 0.3f), new Color(0.2f, 0.34f, 0.28f, 1f));
            CreateProp("手术灯臂", new Vector3(6.1f, -4.78f, 0.18f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.82f, 0.82f, 0.72f, 1f));
            CreatePrimitiveProp("手术灯", PrimitiveType.Sphere, new Vector3(6.1f, -5.04f, 0.22f), new Vector3(0.16f, 0.16f, 0.08f), new Color(0.9f, 0.86f, 0.68f, 1f));
            CreatePrimitiveProp("输液架", PrimitiveType.Cylinder, new Vector3(7.18f, -5.22f, 0.14f), new Vector3(0.06f, 0.26f, 0.06f), new Color(0.74f, 0.74f, 0.68f, 1f));
            CreateProp("病床血压仪 A", new Vector3(5.18f, -5.22f, 0.16f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreateProp("病床血压仪 B", new Vector3(6.28f, -5.22f, 0.16f), new Vector3(0.12f, 0.1f, 0.08f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreateProp("药瓶 A", new Vector3(5.08f, -4.3f, 0.25f), new Vector3(0.06f, 0.06f, 0.12f), new Color(0.72f, 0.82f, 0.86f, 1f));
            CreateProp("药瓶 B", new Vector3(5.28f, -4.3f, 0.25f), new Vector3(0.06f, 0.06f, 0.12f), new Color(0.82f, 0.36f, 0.36f, 1f));
            CreateProp("隐蔽病历箱", new Vector3(7.22f, -4.48f, 0.06f), new Vector3(0.34f, 0.24f, 0.18f), new Color(0.42f, 0.28f, 0.18f, 1f));
        }

        private GameObject CreatePrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, circleSprite, color)
                : CreateSpriteObject(propName, roundedRectSprite, color);
            prop.transform.SetParent(worldRoot.transform, false);
            prop.transform.position = ScaleMapPosition(position);
            prop.transform.localScale = ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateSolidPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreatePrimitiveProp(propName, primitiveType, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere);
            return prop;
        }

        private GameObject CreateProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, roundedRectSprite, color);
            prop.transform.SetParent(worldRoot.transform, false);
            prop.transform.position = ScaleMapPosition(position);
            prop.transform.localScale = ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        private static Color Darken(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        private GameObject CreateSolidProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        private GameObject CreateShapeProp(string propName, Sprite sprite, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, sprite, color);
            prop.transform.SetParent(worldRoot.transform, false);
            prop.transform.position = ScaleMapPosition(position);
            prop.transform.localScale = ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateRotatedProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(worldRoot.transform, false);
            prop.transform.position = ScaleMapPosition(position);
            prop.transform.localScale = ScaleMapSize(scale);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateSolidMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            GameObject prop = CreateMeshBoxProp(propName, position, scale, color, rotationDegrees);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        private GameObject CreateMeshBoxChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            prop.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateMeshPrimitiveChild(Transform parent, string propName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 scale, Color color, Quaternion localRotation)
        {
            GameObject prop = GameObject.CreatePrimitive(primitiveType);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            prop.transform.localRotation = localRotation;
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateMeshPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color, Quaternion rotation)
        {
            GameObject prop = GameObject.CreatePrimitive(primitiveType);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(worldRoot.transform, false);
            prop.transform.position = ScaleMapPosition(position);
            prop.transform.localScale = ScaleMapSize(scale);
            prop.transform.rotation = rotation;
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        private static void Remove3DCollider(GameObject prop)
        {
            UnityEngine.Collider collider = prop.GetComponent<UnityEngine.Collider>();

            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                DestroyImmediate(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        private void ConfigureRuntimeMesh(GameObject prop, Color color)
        {
            Renderer renderer = prop.GetComponent<Renderer>();

            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = RuntimeMeshMaterial(color);
        }

        private Material RuntimeMeshMaterial(Color color)
        {
            string key = Mathf.RoundToInt(color.r * 255f) + "-"
                + Mathf.RoundToInt(color.g * 255f) + "-"
                + Mathf.RoundToInt(color.b * 255f) + "-"
                + Mathf.RoundToInt(color.a * 255f);

            if (runtimeMeshMaterials.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (color.a < 0.99f)
            {
                ConfigureTransparentMaterial(material);
            }

            runtimeMeshMaterials[key] = material;
            return material;
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

        private void RegisterSolidObstacle(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPosition = ScaleMapPosition(position);
            Vector3 scaledScale = ScaleMapSize(scale);
            float width = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float height = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            solidObstacleRects.Add(new Rect(scaledPosition.x - width * 0.5f, scaledPosition.y - height * 0.5f, width, height));
        }

        private void RegisterWalkableArea(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPosition = ScaleMapPosition(position);
            Vector3 scaledScale = ScaleMapSize(scale);
            float width = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float height = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            walkableRects.Add(new Rect(scaledPosition.x - width * 0.5f, scaledPosition.y - height * 0.5f, width, height));
        }

        private static void AttachPhysicsCollider(GameObject prop, Vector3 designScale, bool round)
        {
            if (prop == null)
            {
                return;
            }

            Remove3DCollider(prop);
            Rigidbody2D body = prop.GetComponent<Rigidbody2D>();

            if (body == null)
            {
                body = prop.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            if (round)
            {
                CircleCollider2D circle = prop.GetComponent<CircleCollider2D>();

                if (circle == null)
                {
                    circle = prop.AddComponent<CircleCollider2D>();
                }

                circle.radius = 0.5f;
                circle.isTrigger = false;
                return;
            }

            BoxCollider2D box = prop.GetComponent<BoxCollider2D>();

            if (box == null)
            {
                box = prop.AddComponent<BoxCollider2D>();
            }

            float width = Mathf.Abs(designScale.x) <= 0.18f ? 0.82f : 1f;
            float height = Mathf.Abs(designScale.y) <= 0.18f ? 0.82f : 1f;
            box.size = new Vector2(width, height);
            box.isTrigger = false;
        }

        private GameObject CreatePropChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale, Color color, PrimitiveType primitiveType)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, circleSprite, color)
                : CreateSpriteObject(propName, roundedRectSprite, color);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            SetSortingFromZ(prop);
            return prop;
        }

        private GameObject CreateSpriteChild(Transform parent, string objectName, Sprite sprite, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject child = CreateSpriteObject(objectName, sprite, color);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = scale;
            SetSortingFromZ(child);
            return child;
        }

        private GameObject CreateAssetStoreProp(string propName, string resourcePath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false, bool preserveMaterials = true)
        {
            GameObject prefab = LoadResourcePrefab(resourcePath);

            if (prefab == null || worldRoot == null)
            {
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees, FallbackColorForModel(resourcePath));
            }

            GameObject model = InstantiateModelPrefab(prefab);

            if (model == null)
            {
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees, FallbackColorForModel(resourcePath));
            }

            model.name = propName;
            model.transform.SetParent(worldRoot.transform, false);
            model.transform.position = ScaleMapPosition(position);
            model.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, preserveMaterials);
            SetSortingFromZ(model);
            return model;
        }

        private GameObject CreateSolidAssetStoreProp(string propName, string resourcePath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false, bool preserveMaterials = true)
        {
            GameObject model = CreateAssetStoreProp(propName, resourcePath, position, footprint, rotationDegrees, stretchToFootprint, preserveMaterials);

            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        private GameObject CreateModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            GameObject prefab = LoadQuaterniusModel(relativeFbxPath);

            if (prefab == null || worldRoot == null)
            {
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees, FallbackColorForModel(relativeFbxPath));
            }

            GameObject model = InstantiateModelPrefab(prefab);

            if (model == null)
            {
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees, FallbackColorForModel(relativeFbxPath));
            }

            model.name = propName;
            model.transform.SetParent(worldRoot.transform, false);
            model.transform.position = ScaleMapPosition(position);
            model.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, false);
            SetSortingFromZ(model);
            return model;
        }

        private GameObject CreateModelFallbackProp(string propName, Vector3 position, Vector3 footprint, float rotationDegrees, Color color)
        {
            GameObject fallback = new GameObject(propName);
            fallback.transform.SetParent(worldRoot.transform, false);
            fallback.transform.position = ScaleMapPosition(position);
            fallback.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Vector3 size = ScaleMapSize(footprint);
            float width = Mathf.Max(0.08f, Mathf.Abs(size.x));
            float depth = Mathf.Max(0.08f, Mathf.Abs(size.y));
            float height = Mathf.Max(0.08f, Mathf.Abs(footprint.z));

            CreateMeshBoxChild(fallback.transform, "Fallback Base", new Vector3(0f, 0f, height * 0.35f), new Vector3(width, depth, height * 0.7f), Darken(color, 0.8f));
            CreateMeshBoxChild(fallback.transform, "Fallback Face", new Vector3(0f, depth * 0.38f, height * 0.76f), new Vector3(width * 0.72f, Mathf.Max(0.025f, depth * 0.08f), height * 0.26f), color);

            if (footprint.z > 0.18f)
            {
                CreateMeshBoxChild(fallback.transform, "Fallback Light Strip", new Vector3(0f, -depth * 0.38f, height * 1.02f), new Vector3(width * 0.52f, Mathf.Max(0.02f, depth * 0.08f), height * 0.12f), new Color(0.08f, 0.78f, 0.92f, 1f));
            }

            SetSortingFromZ(fallback);
            return fallback;
        }

        private static Color FallbackColorForModel(string relativeFbxPath)
        {
            if (relativeFbxPath.IndexOf("Light", StringComparison.Ordinal) >= 0)
            {
                return new Color(0.08f, 0.78f, 0.92f, 1f);
            }

            if (relativeFbxPath.IndexOf("Crate", StringComparison.Ordinal) >= 0 || relativeFbxPath.IndexOf("Chest", StringComparison.Ordinal) >= 0)
            {
                return new Color(0.72f, 0.52f, 0.14f, 1f);
            }

            if (relativeFbxPath.IndexOf("Door", StringComparison.Ordinal) >= 0)
            {
                return new Color(0.42f, 0.48f, 0.5f, 1f);
            }

            if (relativeFbxPath.IndexOf("Computer", StringComparison.Ordinal) >= 0 || relativeFbxPath.IndexOf("AccessPoint", StringComparison.Ordinal) >= 0)
            {
                return new Color(0.08f, 0.32f, 0.42f, 1f);
            }

            return new Color(0.24f, 0.28f, 0.3f, 1f);
        }

        private GameObject CreateSolidModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            GameObject model = CreateModelProp(propName, relativeFbxPath, position, footprint, rotationDegrees, stretchToFootprint);

            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        private void CreateWallModelOverlay(string wallName, Vector3 position, Vector3 scale)
        {
            if (Mathf.Abs(scale.x) < 0.2f && Mathf.Abs(scale.y) < 0.2f)
            {
                return;
            }

            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string modelPath = horizontal ? "Walls/ShortWall_Metal2_Straight.fbx" : "Walls/WallAstra_Straight.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = new Vector3(Mathf.Max(Mathf.Abs(scale.x), 0.18f), Mathf.Max(Mathf.Abs(scale.y), 0.18f), Mathf.Max(Mathf.Abs(scale.z), 0.16f));
            CreateModelProp(wallName + " CC0 Wall Module", modelPath, position + new Vector3(0f, 0f, 0.08f), footprint, rotation, true);
        }

        private void CreateDoorModelOverlay(string markerName, Vector3 position, Vector3 scale)
        {
            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string doorPath = markerName.Contains("黑市") || markerName.Contains("维修") || markerName.Contains("暗")
                ? "Platforms/Door_DarkMetal.fbx"
                : "Platforms/Door_Frame_A.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = horizontal
                ? new Vector3(Mathf.Max(0.72f, Mathf.Abs(scale.x)), 0.34f, 0.32f)
                : new Vector3(0.34f, Mathf.Max(0.72f, Mathf.Abs(scale.y)), 0.32f);
            CreateModelProp(markerName + " CC0 Door Frame", doorPath, position + new Vector3(0f, 0f, 0.1f), footprint, rotation, true);
        }

        private GameObject LoadQuaterniusModel(string relativeFbxPath)
        {
            string cacheKey = "Quaternius/" + relativeFbxPath;

            if (modelPrefabCache.TryGetValue(cacheKey, out GameObject cached))
            {
                return cached;
            }

            GameObject prefab = null;

#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusFbxRoot + relativeFbxPath);
#endif
            if (prefab == null)
            {
                string resourcePath = "Quaternius/ModularSciFiMegaKit/FBX/" + relativeFbxPath.Replace(".fbx", string.Empty);
                prefab = Resources.Load<GameObject>(resourcePath);
            }

            modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        private GameObject LoadResourcePrefab(string resourcePath)
        {
            string normalized = NormalizeResourcePath(resourcePath);
            string cacheKey = "Resource/" + normalized;

            if (modelPrefabCache.TryGetValue(cacheKey, out GameObject cached))
            {
                return cached;
            }

            GameObject prefab = null;

#if UNITY_EDITOR
            string assetPath = RuntimeResourcesRoot + normalized + ".prefab";
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab == null)
            {
                assetPath = RuntimeResourcesRoot + normalized + ".fbx";
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }
#endif
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(normalized);
            }

            modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            string normalized = resourcePath.Replace('\\', '/').Trim();

            if (normalized.StartsWith("Assets/_Project/Resources/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring("Assets/_Project/Resources/".Length);
            }

            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            string extension = System.IO.Path.GetExtension(normalized);

            if (!string.IsNullOrEmpty(extension))
            {
                normalized = normalized.Substring(0, normalized.Length - extension.Length);
            }

            return normalized;
        }

        private static GameObject InstantiateModelPrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            }
#endif
            return Instantiate(prefab);
        }

        private static void FitModelToFootprint(GameObject model, Vector3 targetPosition, Vector3 footprint, bool stretchToFootprint)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds))
            {
                return;
            }

            Vector3 desired = ScaleMapSize(footprint);
            float desiredX = Mathf.Max(0.04f, Mathf.Abs(desired.x));
            float desiredY = Mathf.Max(0.04f, Mathf.Abs(desired.y));
            float desiredZ = Mathf.Max(0.05f, Mathf.Abs(footprint.z));

            if (stretchToFootprint)
            {
                Vector3 localScale = model.transform.localScale;
                float xFactor = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yFactor = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float zFactor = bounds.size.z > 0.001f ? desiredZ / bounds.size.z : Mathf.Min(xFactor, yFactor);
                model.transform.localScale = new Vector3(localScale.x * xFactor, localScale.y * zFactor, localScale.z * yFactor);
            }
            else
            {
                float xFactor = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yFactor = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float factor = Mathf.Clamp(Mathf.Min(xFactor, yFactor), 0.02f, 3.0f);
                model.transform.localScale *= factor;
            }

            AlignModelBounds(model, targetPosition);
        }

        private static void AlignModelBounds(GameObject model, Vector3 targetPosition)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds))
            {
                return;
            }

            Vector3 offset = new Vector3(targetPosition.x - bounds.center.x, targetPosition.y - bounds.center.y, targetPosition.z - bounds.min.z);
            model.transform.position += offset;
        }

        private static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(model.transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void ConfigureModelRenderers(GameObject model, bool preserveMaterials)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (renderer.sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    renderer.sharedMaterial = new Material(shader);
                }

                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;

                if (material != null)
                {
                    if (preserveMaterials)
                    {
                        Color sourceColor = ReadMaterialColor(material, Color.white);

                        if (material.HasProperty("_BaseColor"))
                        {
                            Color baseColor = material.GetColor("_BaseColor");
                            material.SetColor("_BaseColor", new Color(Mathf.Clamp01(baseColor.r * 1.04f), Mathf.Clamp01(baseColor.g * 1.04f), Mathf.Clamp01(baseColor.b * 1.04f), baseColor.a));
                        }

                        SetMaterialColor(material, new Color(Mathf.Clamp01(sourceColor.r * 1.04f), Mathf.Clamp01(sourceColor.g * 1.04f), Mathf.Clamp01(sourceColor.b * 1.04f), sourceColor.a));
                        continue;
                    }

                    if (material.HasProperty("_BaseColor"))
                    {
                        Color color = ReadMaterialColor(material, Color.white);
                        material.SetColor("_BaseColor", new Color(Mathf.Clamp01(color.r * 1.1f), Mathf.Clamp01(color.g * 1.1f), Mathf.Clamp01(color.b * 1.1f), color.a));
                    }

                    SetMaterialColor(material, new Color(0.92f, 0.94f, 0.98f, 1f));
                }
            }
        }

        private static Color ReadMaterialColor(Material material, Color fallback)
        {
            if (material == null)
            {
                return fallback;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return fallback;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Vector3 ScaleMapPosition(Vector3 position)
        {
            return new Vector3(position.x * DesignScaleX, position.y * DesignScaleY, position.z);
        }

        private static Vector3 ScaleMapSize(Vector3 scale)
        {
            return new Vector3(scale.x * DesignScaleX, scale.y * DesignScaleY, scale.z);
        }

        private void CreateNeonLight(string lightName, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(worldRoot.transform, false);
            lightObject.transform.position = ScaleMapPosition(position);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        private void ConfigureSceneLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.68f, 1f);

            GameObject lightObject = new GameObject("CC0 Model Fill Light");
            lightObject.transform.SetParent(worldRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.8f, 0.88f, 1f, 1f);
        }

        private void CreateEmergencyBell()
        {
            GameObject bell = CreateSpriteObject("紧急铃", circleSprite, new Color(0.72f, 0.08f, 0.06f, 1f));
            bell.transform.SetParent(worldRoot.transform, false);
            bell.transform.position = ScaleMapPosition(new Vector3(0f, 0f, 0.12f));
            bell.transform.localScale = new Vector3(0.58f, 0.58f, 0.34f);
            SetSortingFromZ(bell);
            CreatePropChild(bell.transform, "Bell Highlight", new Vector3(0f, 0f, 0.08f), new Vector3(0.52f, 0.52f, 0.08f), new Color(1f, 0.34f, 0.22f, 0.9f), PrimitiveType.Cylinder);
            CreateWorldLabelAt("紧急铃", ScaleMapPosition(new Vector3(0f, 0.48f, -0.16f)), 0.075f);
        }

        private void CreateSocialDeductionShipMap()
        {
            CreateHongKongPortDistrictMap();
        }

        private void CreateHongKongPortDistrictMap()
        {
            CreateFloor();
            CreateRoadNetwork();
            CreateMapStructureLayer();
            CreateArchitecturalVolumeLayer();
            CreateShipRooms();
            CreateShipRoomFrames();
            CreateShipTaskDressing();
            CreateLargeMapProps();
            CreateShipAmbientDressing();
            CreateDenseMapMicroDressing();
            CreatePlayableScaleSetDressing();
            CreateQuaterniusModelDressing();
            CreateLargeScalePortSetPieces();
            CreateLargeRoomReadabilityLayer();
            CreateOfficialFreeAssetStoreLayer();
            CreateCommercialArtAdapterLayer();
            CreateVerticalSliceProductionLayer();
        }

        private void CreateLegacyShipMap()
        {
            CreateShipFloor();
            CreateShipCorridors();
            CreateCorridorVolumeLayer();
            CreateShipRooms();
            CreateShipRoomFrames();
            CreateShipTaskDressing();
            CreateUnderworldPassageNodes();
            CreateShipAmbientDressing();
            CreateDenseMapMicroDressing();
            CreatePlayableScaleSetDressing();
            CreateQuaterniusModelDressing();
            CreateLargeScalePortSetPieces();
            CreateOfficialFreeAssetStoreLayer();
            CreateCommercialArtAdapterLayer();
        }

        private void CreateShipFloor()
        {
            Color voidColor = new Color(0.018f, 0.024f, 0.028f, 1f);
            Color hull = new Color(0.07f, 0.086f, 0.094f, 1f);
            Color innerHull = new Color(0.095f, 0.112f, 0.12f, 1f);
            Color sidePod = new Color(0.082f, 0.1f, 0.108f, 1f);

            CreateProp("行动舰外暗区", new Vector3(0f, 0f, -0.39f), new Vector3(26.6f, 16.8f, 0.08f), voidColor);
            CreateShapeProp("行动舰圆角主外壳", roundedRectSprite, new Vector3(0f, 0f, -0.36f), new Vector3(23.8f, 13.7f, 0.08f), hull);
            CreateShapeProp("行动舰圆角内甲板", roundedRectSprite, new Vector3(0f, 0f, -0.35f), new Vector3(22.2f, 12.4f, 0.08f), innerHull);
            CreateShapeProp("行动舰左推进舱外壳", roundedRectSprite, new Vector3(-10.55f, 0.2f, -0.355f), new Vector3(3.0f, 8.8f, 0.08f), sidePod);
            CreateShapeProp("行动舰右推进舱外壳", roundedRectSprite, new Vector3(10.55f, 0.15f, -0.355f), new Vector3(3.0f, 8.65f, 0.08f), sidePod);
            CreateSolidProp("北侧厚舱壁", new Vector3(0f, 6.62f, -0.12f), new Vector3(21.2f, 0.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("南侧厚舱壁", new Vector3(0f, -6.62f, -0.12f), new Vector3(21.2f, 0.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("西侧外舱壁", new Vector3(-11.55f, 0f, -0.12f), new Vector3(0.2f, 10.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));
            CreateSolidProp("东侧外舱壁", new Vector3(11.55f, 0f, -0.12f), new Vector3(0.2f, 10.2f, 0.18f), new Color(0.035f, 0.044f, 0.05f, 1f));

            for (int i = 0; i < 15; i++)
            {
                float x = -10.5f + i * 1.5f;
                CreateProp("行动舰甲板横向拼缝 " + i, new Vector3(x, 6.2f, -0.22f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.28f, 0.34f, 0.35f, 1f));
                CreateProp("行动舰底舱横向拼缝 " + i, new Vector3(x, -6.18f, -0.22f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.28f, 0.34f, 0.35f, 1f));
            }
        }

        private void CreateShipCorridors()
        {
            Color main = new Color(0.205f, 0.232f, 0.242f, 1f);
            Color branch = new Color(0.172f, 0.198f, 0.21f, 1f);
            Color trim = new Color(0.48f, 0.56f, 0.56f, 1f);
            Color guide = new Color(0.88f, 0.68f, 0.09f, 1f);

            CreateShipCorridor("中心会议圆舱", new Vector3(0f, -0.35f, -0.21f), new Vector3(3.0f, 2.35f, 0.08f), main, true);
            CreateShipCorridor("主横连廊", new Vector3(0f, -0.18f, -0.24f), new Vector3(15.5f, 1.2f, 0.08f), main, false);
            CreateShipCorridor("上层主连廊", new Vector3(0f, 3.65f, -0.24f), new Vector3(16.4f, 1.04f, 0.08f), branch, false);
            CreateShipCorridor("下层主连廊", new Vector3(0.12f, -3.9f, -0.24f), new Vector3(15.4f, 1.04f, 0.08f), branch, false);
            CreateShipCorridor("左竖连廊", new Vector3(-6.85f, 0.15f, -0.24f), new Vector3(1.08f, 8.35f, 0.08f), branch, false);
            CreateShipCorridor("右竖连廊", new Vector3(7.05f, 0.08f, -0.24f), new Vector3(1.08f, 8.18f, 0.08f), branch, false);
            CreateShipCorridor("中心上连廊", new Vector3(0f, 1.85f, -0.23f), new Vector3(1.08f, 3.15f, 0.08f), main, false);
            CreateShipCorridor("中心下连廊", new Vector3(0f, -2.35f, -0.23f), new Vector3(1.08f, 3.05f, 0.08f), main, false);
            CreateShipCorridor("左上斜接舱", new Vector3(-3.2f, 2.1f, -0.23f), new Vector3(4.35f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("右上斜接舱", new Vector3(3.35f, 2.08f, -0.23f), new Vector3(4.45f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("左下斜接舱", new Vector3(-3.25f, -2.15f, -0.23f), new Vector3(4.25f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("右下斜接舱", new Vector3(3.42f, -2.12f, -0.23f), new Vector3(4.25f, 0.72f, 0.08f), branch, false);
            CreateShipCorridor("左侧气闸短廊", new Vector3(-9.3f, -0.18f, -0.24f), new Vector3(3.95f, 0.92f, 0.08f), branch, false);
            CreateShipCorridor("右侧气闸短廊", new Vector3(9.2f, -0.18f, -0.24f), new Vector3(3.7f, 0.92f, 0.08f), branch, false);

            CreateShipNode("西北舱路口", new Vector3(-6.85f, 3.65f, -0.18f), 0.44f, trim);
            CreateShipNode("东北舱路口", new Vector3(7.05f, 3.65f, -0.18f), 0.44f, trim);
            CreateShipNode("西南舱路口", new Vector3(-6.85f, -3.9f, -0.18f), 0.44f, trim);
            CreateShipNode("东南舱路口", new Vector3(7.05f, -3.9f, -0.18f), 0.44f, trim);
            CreateShipNode("会议桌圆环", new Vector3(0f, -0.35f, -0.16f), 0.62f, new Color(0.52f, 0.62f, 0.62f, 1f));

            for (int i = 0; i < 6; i++)
            {
                CreateProp("主走廊导向线 " + i, new Vector3(-5.2f + i * 2.05f, -0.18f, -0.1f), new Vector3(0.78f, 0.055f, 0.05f), guide);
                CreateProp("上层导向线 " + i, new Vector3(-5.3f + i * 2.12f, 3.65f, -0.1f), new Vector3(0.72f, 0.045f, 0.05f), new Color(0.54f, 0.62f, 0.62f, 1f));
                CreateProp("下层导向线 " + i, new Vector3(-5.1f + i * 2.08f, -3.9f, -0.1f), new Vector3(0.72f, 0.045f, 0.05f), new Color(0.54f, 0.62f, 0.62f, 1f));
            }
        }

        private void CreateShipCorridor(string name, Vector3 center, Vector3 size, Color color, bool round)
        {
            GameObject corridor = round
                ? CreateShapeProp(name, roundedRectSprite, center, size, color)
                : CreateShapeProp(name, roundedRectSprite, center, size, color);
            corridor.transform.SetAsFirstSibling();
            RegisterWalkableArea(center, size);
        }

        private void CreateShipNode(string name, Vector3 center, float radius, Color color)
        {
            CreateShapeProp(name, circleSprite, center, new Vector3(radius, radius, 0.08f), color);
            CreateShapeProp(name + " 内圈", circleSprite, center + new Vector3(0f, 0f, 0.02f), new Vector3(radius * 0.58f, radius * 0.58f, 0.08f), new Color(0.16f, 0.19f, 0.2f, 1f));
            RegisterWalkableArea(center, new Vector3(radius * 1.9f, radius * 1.9f, 0.08f));
        }

        private void CreateCorridorVolumeLayer()
        {
            Color rail = new Color(0.055f, 0.07f, 0.078f, 1f);
            Color trim = new Color(0.48f, 0.56f, 0.56f, 1f);
            Color light = new Color(0.08f, 0.78f, 0.92f, 1f);
            CreateCorridorRails("主横连廊", new Vector3(0f, -0.18f, 0f), 15.3f, true, rail, trim);
            CreateCorridorRails("上层主连廊", new Vector3(0f, 3.65f, 0f), 16.0f, true, rail, trim);
            CreateCorridorRails("下层主连廊", new Vector3(0.12f, -3.9f, 0f), 15.0f, true, rail, trim);
            CreateCorridorRails("左竖连廊", new Vector3(-6.85f, 0.15f, 0f), 8.0f, false, rail, trim);
            CreateCorridorRails("右竖连廊", new Vector3(7.05f, 0.08f, 0f), 7.85f, false, rail, trim);
            CreateCorridorRails("中心上连廊", new Vector3(0f, 1.85f, 0f), 2.8f, false, rail, trim);
            CreateCorridorRails("中心下连廊", new Vector3(0f, -2.35f, 0f), 2.72f, false, rail, trim);

            for (int i = 0; i < 9; i++)
            {
                float x = -7.2f + i * 1.8f;
                CreateMeshBoxProp("屋顶 主走廊顶灯 " + i, new Vector3(x, 0.52f, 0.42f), new Vector3(0.46f, 0.055f, 0.08f), light);
                CreateMeshBoxProp("屋顶 下走廊地灯 " + i, new Vector3(x + 0.28f, -4.42f, 0.28f), new Vector3(0.34f, 0.045f, 0.06f), Darken(light, 0.85f));
                CreateMeshBoxProp("屋顶 上走廊地灯 " + i, new Vector3(x + 0.16f, 4.18f, 0.28f), new Vector3(0.34f, 0.045f, 0.06f), Darken(light, 0.85f));
            }

            CreateMeshPrimitiveProp("屋顶 会议舱圆形投影台", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.02f), new Vector3(0.92f, 0.03f, 0.92f), new Color(0.42f, 0.48f, 0.48f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("屋顶 会议舱证据屏 A", new Vector3(-0.64f, 0.22f, 0.38f), new Vector3(0.38f, 0.045f, 0.22f), light);
            CreateMeshBoxProp("屋顶 会议舱证据屏 B", new Vector3(0.64f, 0.22f, 0.38f), new Vector3(0.38f, 0.045f, 0.22f), new Color(0.95f, 0.22f, 0.18f, 1f));
        }

        private void CreateCorridorRails(string name, Vector3 center, float length, bool horizontal, Color rail, Color trim)
        {
            if (horizontal)
            {
                CreateMeshBoxProp("2.5D 建筑体 " + name + " 上沿立体护栏", center + new Vector3(0f, 0.58f, 0.22f), new Vector3(length, 0.08f, 0.22f), rail);
                CreateMeshBoxProp("2.5D 建筑体 " + name + " 下沿立体护栏", center + new Vector3(0f, -0.58f, 0.22f), new Vector3(length, 0.08f, 0.22f), rail);

                for (int i = 0; i < Mathf.CeilToInt(length / 2.1f); i++)
                {
                    float x = -length * 0.5f + 0.8f + i * 2.1f;
                    CreateMeshBoxProp("屋顶 " + name + " 立柱 U" + i, center + new Vector3(x, 0.58f, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                    CreateMeshBoxProp("屋顶 " + name + " 立柱 D" + i, center + new Vector3(x, -0.58f, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                }

                return;
            }

            CreateMeshBoxProp("2.5D 建筑体 " + name + " 左沿立体护栏", center + new Vector3(-0.52f, 0f, 0.22f), new Vector3(0.08f, length, 0.22f), rail);
            CreateMeshBoxProp("2.5D 建筑体 " + name + " 右沿立体护栏", center + new Vector3(0.52f, 0f, 0.22f), new Vector3(0.08f, length, 0.22f), rail);

            for (int i = 0; i < Mathf.CeilToInt(length / 1.8f); i++)
            {
                float y = -length * 0.5f + 0.7f + i * 1.8f;
                CreateMeshBoxProp("屋顶 " + name + " 立柱 L" + i, center + new Vector3(-0.52f, y, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
                CreateMeshBoxProp("屋顶 " + name + " 立柱 R" + i, center + new Vector3(0.52f, y, 0.38f), new Vector3(0.08f, 0.08f, 0.34f), trim);
            }
        }

        private void CreateShipRooms()
        {
            foreach (ShipRoomSpec room in ShipRooms())
            {
                CreateShipRoom(room);
            }
        }

        private static ShipRoomSpec[] ShipRooms()
        {
            return new[]
            {
                new ShipRoomSpec("西码头货柜场", "货柜舱", new Vector3(-9.3f, 5.35f, 0f), new Vector3(4.35f, 2.12f, 0.16f), new Color(0.13f, 0.24f, 0.21f, 1f), MapEntrance.South),
                new ShipRoomSpec("海关查验区", "海关查验", new Vector3(-5.0f, 5.35f, 0f), new Vector3(3.05f, 2.08f, 0.16f), new Color(0.18f, 0.25f, 0.19f, 1f), MapEntrance.South),
                new ShipRoomSpec("监控室", "监控中心", new Vector3(-9.35f, 1.85f, 0f), new Vector3(2.95f, 1.9f, 0.16f), new Color(0.1f, 0.19f, 0.27f, 1f), MapEntrance.East),
                new ShipRoomSpec("茶餐厅", "茶餐厅", new Vector3(-4.8f, 1.65f, 0f), new Vector3(2.95f, 1.86f, 0.16f), new Color(0.32f, 0.18f, 0.1f, 1f), MapEntrance.West),
                new ShipRoomSpec("夜市主街", "情报夜市", new Vector3(-1.0f, 2.75f, 0f), new Vector3(4.08f, 2.12f, 0.16f), new Color(0.27f, 0.14f, 0.09f, 1f), MapEntrance.South),
                new ShipRoomSpec("金融楼", "洗钱账房", new Vector3(4.75f, 2.75f, 0f), new Vector3(3.42f, 2.12f, 0.16f), new Color(0.16f, 0.18f, 0.28f, 1f), MapEntrance.West),
                new ShipRoomSpec("电房", "电力机房", new Vector3(8.85f, 5.25f, 0f), new Vector3(2.8f, 2.12f, 0.16f), new Color(0.12f, 0.19f, 0.27f, 1f), MapEntrance.South),
                new ShipRoomSpec("天台通道", "天台观测", new Vector3(8.95f, 1.65f, 0f), new Vector3(2.76f, 1.86f, 0.16f), new Color(0.18f, 0.19f, 0.3f, 1f), MapEntrance.West),
                new ShipRoomSpec("指挥车广场", "指挥广场", new Vector3(0f, -5.35f, 0f), new Vector3(4.35f, 1.92f, 0.16f), new Color(0.11f, 0.2f, 0.29f, 1f), MapEntrance.North),
                new ShipRoomSpec("证物库", "证物冷藏", new Vector3(-8.6f, -5.05f, 0f), new Vector3(3.35f, 1.98f, 0.16f), new Color(0.2f, 0.16f, 0.27f, 1f), MapEntrance.East),
                new ShipRoomSpec("后巷排档", "黑市排档", new Vector3(5.6f, -1.55f, 0f), new Vector3(3.55f, 2.14f, 0.16f), new Color(0.25f, 0.14f, 0.09f, 1f), MapEntrance.West),
                new ShipRoomSpec("地下诊所", "地下诊疗", new Vector3(6.15f, -5.05f, 0f), new Vector3(3.45f, 1.98f, 0.16f), new Color(0.13f, 0.25f, 0.2f, 1f), MapEntrance.North)
            };
        }

        private void CreateShipRoom(ShipRoomSpec room)
        {
            Color wall = new Color(0.052f, 0.064f, 0.07f, 1f);
            Color trim = new Color(0.62f, 0.62f, 0.54f, 1f);
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            CreateShapeProp("2.5D 建筑体 " + room.Name + " 外舱轮廓", roundedRectSprite, room.Center + new Vector3(0f, 0f, -0.1f), new Vector3(room.Size.x + 0.22f, room.Size.y + 0.22f, 0.08f), wall);
            CreateShapeProp("2.5D 建筑体 " + room.Name + " 圆角房间底", roundedRectSprite, room.Center + new Vector3(0f, 0f, -0.07f), room.Size, Darken(room.Floor, 0.86f));
            CreateShapeProp("2.5D 建筑体 " + room.Name + " 中央地板", roundedRectSprite, room.Center + new Vector3(0f, 0f, -0.04f), new Vector3(room.Size.x * 0.9f, room.Size.y * 0.76f, 0.08f), room.Floor);
            CreateRoomVolumeShell(room, wall, trim);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 北厚墙", room.Center + new Vector3(0f, halfHeight - 0.06f, 0.16f), new Vector3(room.Size.x * 0.86f, 0.14f, 0.14f), wall, room.Entrance == MapEntrance.North);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 南厚墙", room.Center + new Vector3(0f, -halfHeight + 0.06f, 0.16f), new Vector3(room.Size.x * 0.86f, 0.14f, 0.14f), wall, room.Entrance == MapEntrance.South);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 西厚墙", room.Center + new Vector3(-halfWidth + 0.06f, 0f, 0.16f), new Vector3(0.14f, room.Size.y * 0.76f, 0.14f), wall, room.Entrance == MapEntrance.West);
            CreateWallSegmentWithDoor("2.5D 建筑体 " + room.Name + " 东厚墙", room.Center + new Vector3(halfWidth - 0.06f, 0f, 0.16f), new Vector3(0.14f, room.Size.y * 0.76f, 0.14f), wall, room.Entrance == MapEntrance.East);
            CreateProp("屋顶 " + room.Name + " 北舱金属边", room.Center + new Vector3(0f, halfHeight - 0.22f, 0.19f), new Vector3(room.Size.x * 0.58f, 0.055f, 0.08f), trim);
            CreateProp("屋顶 " + room.Name + " 舱门灯带", DoorLightPosition(room), DoorLightScale(room), DoorColor(room));
            CreateWorldLabelAt(room.Label, ScaleMapPosition(room.Center + new Vector3(0f, halfHeight - 0.34f, -0.17f)), 0.052f);
            CreateRoomFloorTiles(room.Name, room.Center, room.Size, room.Floor);
            CreateRoomFurniture(room);
            RegisterWalkableArea(room.Center, new Vector3(room.Size.x * 0.86f, room.Size.y * 0.7f, 0.08f));
        }

        private void CreateRoomVolumeShell(ShipRoomSpec room, Color wall, Color trim)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;
            float height = RoomVisualHeight(room);
            Color side = Darken(room.Floor, 0.52f);
            Color roof = Darken(room.Floor, 0.74f);
            Color glass = new Color(0.08f, 0.34f, 0.44f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 后立面体", room.Center + new Vector3(0f, halfHeight + 0.12f, height * 0.5f), new Vector3(room.Size.x * 0.92f, 0.16f, height), side);
            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 左侧立面体", room.Center + new Vector3(-halfWidth - 0.06f, 0f, height * 0.43f), new Vector3(0.14f, room.Size.y * 0.72f, height * 0.86f), Darken(side, 0.86f));
            CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 右侧立面体", room.Center + new Vector3(halfWidth + 0.06f, 0f, height * 0.43f), new Vector3(0.14f, room.Size.y * 0.72f, height * 0.86f), Darken(side, 0.88f));
            CreateMeshBoxProp("屋顶 " + room.Name + " 主板体", room.Center + new Vector3(0f, halfHeight * 0.18f, height + 0.03f), new Vector3(room.Size.x * 0.68f, room.Size.y * 0.26f, 0.08f), roof);
            CreateMeshBoxProp("屋顶 " + room.Name + " 前缘体", room.Center + new Vector3(0f, -halfHeight + 0.1f, height * 0.62f), new Vector3(room.Size.x * 0.48f, 0.12f, height * 0.18f), trim);

            for (int i = 0; i < 3; i++)
            {
                float x = -room.Size.x * 0.25f + i * room.Size.x * 0.25f;
                CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 窗格 " + i, room.Center + new Vector3(x, halfHeight + 0.215f, height * 0.58f), new Vector3(0.34f, 0.035f, 0.18f), glass);
            }

            CreateRooftopKit(room, height);
        }

        private void CreateRooftopKit(ShipRoomSpec room, float height)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;
            Color metal = new Color(0.22f, 0.24f, 0.24f, 1f);
            Color vent = new Color(0.055f, 0.07f, 0.075f, 1f);
            Color light = DoorColor(room);

            CreateMeshBoxProp("屋顶 " + room.Name + " 空调箱", room.Center + new Vector3(-halfWidth * 0.35f, halfHeight * 0.12f, height + 0.14f), new Vector3(0.34f, 0.22f, 0.18f), metal);
            CreateMeshBoxProp("屋顶 " + room.Name + " 风管 A", room.Center + new Vector3(halfWidth * 0.24f, halfHeight * 0.06f, height + 0.12f), new Vector3(0.5f, 0.08f, 0.12f), vent);
            CreateMeshBoxProp("屋顶 " + room.Name + " 风管 B", room.Center + new Vector3(halfWidth * 0.32f, -halfHeight * 0.12f, height + 0.12f), new Vector3(0.08f, 0.36f, 0.12f), vent);
            CreateMeshPrimitiveProp("屋顶 " + room.Name + " 信号灯", PrimitiveType.Cylinder, room.Center + new Vector3(halfWidth * 0.42f, halfHeight * 0.28f, height + 0.22f), new Vector3(0.08f, 0.08f, 0.12f), light, Quaternion.Euler(90f, 0f, 0f));

            if (room.Label.Contains("电力") || room.Label.Contains("监控") || room.Label.Contains("情报"))
            {
                CreateMeshBoxProp("屋顶 " + room.Name + " 天线杆", room.Center + new Vector3(0f, halfHeight * 0.24f, height + 0.32f), new Vector3(0.04f, 0.04f, 0.48f), new Color(0.72f, 0.76f, 0.72f, 1f));
                CreateMeshBoxProp("屋顶 " + room.Name + " 天线横臂", room.Center + new Vector3(0f, halfHeight * 0.24f, height + 0.54f), new Vector3(0.42f, 0.035f, 0.04f), new Color(0.72f, 0.76f, 0.72f, 1f));
            }
        }

        private static float RoomVisualHeight(ShipRoomSpec room)
        {
            if (room.Label.Contains("账房") || room.Label.Contains("监控") || room.Label.Contains("电力"))
            {
                return 0.82f;
            }

            if (room.Label.Contains("冷藏") || room.Label.Contains("诊疗") || room.Label.Contains("观测"))
            {
                return 0.72f;
            }

            if (room.Label.Contains("情报") || room.Label.Contains("黑市"))
            {
                return 0.58f;
            }

            return 0.66f;
        }

        private void CreateWallSegmentWithDoor(string wallName, Vector3 position, Vector3 scale, Color color, bool hasDoor)
        {
            if (!hasDoor)
            {
                CreateWallSegment(wallName, position, scale, color);
                return;
            }

            bool horizontal = scale.x >= scale.y;
            float length = horizontal ? scale.x : scale.y;
            float gap = Mathf.Clamp(length * 0.36f, 0.64f, 0.95f);
            float segmentLength = Mathf.Max(0.12f, (length - gap) * 0.5f);

            if (horizontal)
            {
                float offset = gap * 0.5f + segmentLength * 0.5f;
                CreateWallSegment(wallName + " L", position + new Vector3(-offset, 0f, 0f), new Vector3(segmentLength, scale.y, scale.z), color);
                CreateWallSegment(wallName + " R", position + new Vector3(offset, 0f, 0f), new Vector3(segmentLength, scale.y, scale.z), color);
                return;
            }

            float verticalOffset = gap * 0.5f + segmentLength * 0.5f;
            CreateWallSegment(wallName + " B", position + new Vector3(0f, -verticalOffset, 0f), new Vector3(scale.x, segmentLength, scale.z), color);
            CreateWallSegment(wallName + " T", position + new Vector3(0f, verticalOffset, 0f), new Vector3(scale.x, segmentLength, scale.z), color);
        }

        private static Vector3 DoorLightPosition(ShipRoomSpec room)
        {
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            switch (room.Entrance)
            {
                case MapEntrance.North:
                    return room.Center + new Vector3(0f, halfHeight - 0.12f, 0.22f);
                case MapEntrance.South:
                    return room.Center + new Vector3(0f, -halfHeight + 0.12f, 0.22f);
                case MapEntrance.East:
                    return room.Center + new Vector3(halfWidth - 0.12f, 0f, 0.22f);
                default:
                    return room.Center + new Vector3(-halfWidth + 0.12f, 0f, 0.22f);
            }
        }

        private static Vector3 DoorLightScale(ShipRoomSpec room)
        {
            if (room.Entrance == MapEntrance.North || room.Entrance == MapEntrance.South)
            {
                return new Vector3(Mathf.Min(room.Size.x * 0.42f, 1.25f), 0.07f, 0.08f);
            }

            return new Vector3(0.07f, Mathf.Min(room.Size.y * 0.42f, 0.86f), 0.08f);
        }

        private static Color DoorColor(ShipRoomSpec room)
        {
            if (room.Label.Contains("情报") || room.Label.Contains("黑市"))
            {
                return new Color(0.95f, 0.18f, 0.32f, 1f);
            }

            if (room.Label.Contains("账房") || room.Label.Contains("指挥"))
            {
                return new Color(0.32f, 0.68f, 1f, 1f);
            }

            if (room.Label.Contains("诊疗") || room.Label.Contains("冷藏"))
            {
                return new Color(0.55f, 0.82f, 0.76f, 1f);
            }

            return new Color(0.95f, 0.72f, 0.1f, 1f);
        }

        private void CreateRoomFurniture(ShipRoomSpec room)
        {
            Color metal = new Color(0.08f, 0.1f, 0.11f, 1f);
            Color screen = new Color(0.06f, 0.62f, 0.78f, 1f);
            Color warning = new Color(0.9f, 0.68f, 0.08f, 1f);

            switch (room.Name)
            {
                case "西码头货柜场":
                    CreateWallConsoleSet(room, 0);
                    CreateContainerRack(room.Center + new Vector3(-0.72f, 0.3f, 0.06f), 0);
                    CreateContainerRack(room.Center + new Vector3(0.75f, -0.32f, 0.06f), 2);
                    CreateSolidProp("货柜舱封锁箱", room.Center + new Vector3(0.4f, 0.56f, 0.06f), new Vector3(0.62f, 0.28f, 0.2f), new Color(0.78f, 0.55f, 0.08f, 1f));
                    CreateSolidProp("货柜舱吊臂基座", room.Center + new Vector3(1.55f, 0.42f, 0.08f), new Vector3(0.28f, 0.74f, 0.22f), warning);
                    CreateProp("货柜舱吊臂横梁", room.Center + new Vector3(1.2f, 0.72f, 0.2f), new Vector3(0.92f, 0.08f, 0.08f), warning);
                    break;
                case "海关查验区":
                    CreateWallConsoleSet(room, 1);
                    CreateSolidProp("查验舱扫描门", room.Center + new Vector3(0.82f, 0.15f, 0.1f), new Vector3(0.14f, 0.82f, 0.28f), metal);
                    CreateSolidProp("查验舱检查桌", room.Center + new Vector3(-0.46f, 0.12f, 0.07f), new Vector3(0.8f, 0.34f, 0.18f), new Color(0.24f, 0.26f, 0.2f, 1f));
                    CreateProp("查验舱屏幕", room.Center + new Vector3(-0.46f, 0.38f, 0.2f), new Vector3(0.46f, 0.06f, 0.08f), screen);
                    break;
                case "监控室":
                    CreateWallConsoleSet(room, 2);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateProp("监控墙屏 " + i, room.Center + new Vector3(-0.62f + i * 0.48f, 0.45f, 0.18f), new Vector3(0.36f, 0.08f, 0.16f), screen);
                    }

                    CreateSolidProp("监控操控台", room.Center + new Vector3(-0.15f, -0.18f, 0.07f), new Vector3(0.92f, 0.28f, 0.18f), metal);
                    break;
                case "茶餐厅":
                    CreateWallConsoleSet(room, 3);
                    CreateSolidProp("休息舱吧台", room.Center + new Vector3(-0.78f, 0.08f, 0.07f), new Vector3(0.28f, 1.0f, 0.18f), new Color(0.46f, 0.25f, 0.12f, 1f));
                    CreateBoothSet(room.Center + new Vector3(0.34f, 0.38f, 0.06f), "上");
                    CreateBoothSet(room.Center + new Vector3(0.34f, -0.34f, 0.06f), "下");
                    break;
                case "夜市主街":
                    CreateWallConsoleSet(room, 4);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("情报摊台 " + i, room.Center + new Vector3(-1.15f + i * 1.05f, 0.34f, 0.07f), new Vector3(0.62f, 0.26f, 0.18f), i % 2 == 0 ? new Color(0.62f, 0.12f, 0.1f, 1f) : new Color(0.12f, 0.36f, 0.4f, 1f));
                        CreateProp("情报霓虹牌 " + i, room.Center + new Vector3(-1.15f + i * 1.05f, 0.56f, 0.2f), new Vector3(0.5f, 0.05f, 0.08f), i % 2 == 0 ? new Color(0.96f, 0.22f, 0.52f, 1f) : screen);
                    }
                    break;
                case "金融楼":
                    CreateWallConsoleSet(room, 5);
                    CreateSolidProp("账房保险柜", room.Center + new Vector3(0.92f, -0.24f, 0.09f), new Vector3(0.48f, 0.46f, 0.28f), new Color(0.18f, 0.18f, 0.22f, 1f));
                    CreateSolidProp("账房桌", room.Center + new Vector3(-0.34f, 0.12f, 0.07f), new Vector3(0.86f, 0.3f, 0.18f), new Color(0.26f, 0.22f, 0.18f, 1f));
                    CreateProp("账房现金条", room.Center + new Vector3(-0.08f, 0.34f, 0.2f), new Vector3(0.52f, 0.05f, 0.06f), new Color(0.18f, 0.58f, 0.25f, 1f));
                    break;
                case "电房":
                    CreateWallConsoleSet(room, 6);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("电力舱变压器 " + i, room.Center + new Vector3(-0.64f + i * 0.5f, 0.28f, 0.08f), new Vector3(0.32f, 0.46f, 0.28f), new Color(0.18f, 0.24f, 0.34f, 1f));
                        CreateProp("电力舱指示灯 " + i, room.Center + new Vector3(-0.64f + i * 0.5f, 0.54f, 0.22f), new Vector3(0.06f, 0.04f, 0.05f), i == 1 ? Color.red : Color.green);
                    }

                    CreateProp("电力舱黄黑警戒线", room.Center + new Vector3(0f, -0.46f, 0.1f), new Vector3(1.45f, 0.08f, 0.08f), warning);
                    break;
                case "天台通道":
                    CreateWallConsoleSet(room, 7);
                    CreateSolidProp("观测舱望远镜座", room.Center + new Vector3(-0.28f, 0f, 0.08f), new Vector3(0.5f, 0.22f, 0.18f), metal);
                    CreateProp("观测舱镜筒", room.Center + new Vector3(0.08f, 0.02f, 0.2f), new Vector3(0.42f, 0.08f, 0.08f), screen);
                    CreateProp("观测舱气象屏", room.Center + new Vector3(0.82f, 0.38f, 0.18f), new Vector3(0.44f, 0.06f, 0.14f), screen);
                    break;
                case "指挥车广场":
                    CreateWallConsoleSet(room, 8);
                    CreateShapeProp("指挥舱圆桌", circleSprite, room.Center + new Vector3(0f, -0.02f, 0.08f), new Vector3(1.0f, 0.62f, 0.12f), new Color(0.5f, 0.52f, 0.48f, 1f));
                    CreateSolidProp("行动白板", room.Center + new Vector3(-1.42f, 0.18f, 0.08f), new Vector3(0.54f, 0.16f, 0.22f), new Color(0.82f, 0.86f, 0.82f, 1f));
                    CreateProp("指挥警灯条", room.Center + new Vector3(1.35f, 0.18f, 0.14f), new Vector3(0.82f, 0.08f, 0.08f), new Color(0.12f, 0.32f, 0.96f, 1f));
                    break;
                case "证物库":
                    CreateWallConsoleSet(room, 9);
                    for (int i = 0; i < 3; i++)
                    {
                        CreateSolidProp("证物舱货架 " + i, room.Center + new Vector3(-0.88f + i * 0.56f, 0.34f, 0.08f), new Vector3(0.34f, 0.18f, 0.24f), new Color(0.24f, 0.22f, 0.18f, 1f));
                    }

                    CreateSolidProp("证物舱冷柜", room.Center + new Vector3(0.62f, -0.3f, 0.07f), new Vector3(0.62f, 0.34f, 0.24f), new Color(0.16f, 0.34f, 0.38f, 1f));
                    break;
                case "后巷排档":
                    CreateWallConsoleSet(room, 10);
                    CreateSolidProp("维修舱炉台", room.Center + new Vector3(-0.48f, 0.24f, 0.07f), new Vector3(0.52f, 0.28f, 0.2f), metal);
                    CreatePrimitiveProp("维修舱煤气瓶 A", PrimitiveType.Cylinder, room.Center + new Vector3(0.28f, 0.34f, 0.08f), new Vector3(0.11f, 0.16f, 0.11f), new Color(0.18f, 0.42f, 0.42f, 1f));
                    CreateSolidProp("维修舱摩托", room.Center + new Vector3(0.82f, -0.34f, 0.06f), new Vector3(0.66f, 0.18f, 0.16f), new Color(0.08f, 0.08f, 0.1f, 1f));
                    CreateProp("维修舱火苗", room.Center + new Vector3(-0.48f, 0.42f, 0.2f), new Vector3(0.16f, 0.08f, 0.08f), new Color(1f, 0.28f, 0.06f, 1f));
                    break;
                case "地下诊所":
                    CreateWallConsoleSet(room, 11);
                    CreateSolidProp("诊疗舱病床 A", room.Center + new Vector3(-0.58f, -0.22f, 0.07f), new Vector3(0.7f, 0.34f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
                    CreateSolidProp("诊疗舱病床 B", room.Center + new Vector3(0.52f, -0.22f, 0.07f), new Vector3(0.7f, 0.34f, 0.18f), new Color(0.72f, 0.72f, 0.66f, 1f));
                    CreateSolidProp("诊疗舱药柜", room.Center + new Vector3(-1.18f, 0.32f, 0.09f), new Vector3(0.34f, 0.3f, 0.24f), new Color(0.2f, 0.34f, 0.28f, 1f));
                    CreateProp("诊疗舱手术灯", room.Center + new Vector3(0.05f, 0.46f, 0.22f), new Vector3(0.34f, 0.06f, 0.08f), new Color(0.9f, 0.86f, 0.68f, 1f));
                    break;
            }
        }

        private void CreateWallConsoleSet(ShipRoomSpec room, int seed)
        {
            Color body = seed % 2 == 0 ? new Color(0.08f, 0.15f, 0.18f, 1f) : new Color(0.16f, 0.12f, 0.16f, 1f);
            Color screen = seed % 3 == 0 ? new Color(0.05f, 0.68f, 0.82f, 1f) : new Color(0.2f, 0.78f, 0.56f, 1f);
            float halfWidth = room.Size.x * 0.5f;
            float halfHeight = room.Size.y * 0.5f;

            CreateSolidProp("舱室边柜 " + room.Name + " A", room.Center + new Vector3(-halfWidth * 0.58f, halfHeight * 0.22f, 0.08f), new Vector3(0.28f, 0.34f, 0.22f), body);
            CreateProp("舱室边柜屏 " + room.Name + " A", room.Center + new Vector3(-halfWidth * 0.58f, halfHeight * 0.42f, 0.22f), new Vector3(0.2f, 0.045f, 0.06f), screen);
            CreateSolidProp("舱室边柜 " + room.Name + " B", room.Center + new Vector3(halfWidth * 0.58f, -halfHeight * 0.2f, 0.08f), new Vector3(0.28f, 0.34f, 0.22f), body);
            CreateProp("舱室边柜屏 " + room.Name + " B", room.Center + new Vector3(halfWidth * 0.58f, 0f, 0.22f), new Vector3(0.2f, 0.045f, 0.06f), screen);
            CreateProp("屋顶 " + room.Name + " 线缆槽 A", room.Center + new Vector3(0f, halfHeight * 0.34f, 0.12f), new Vector3(room.Size.x * 0.32f, 0.035f, 0.06f), new Color(0.04f, 0.055f, 0.06f, 1f));
            CreateProp("屋顶 " + room.Name + " 线缆槽 B", room.Center + new Vector3(0f, -halfHeight * 0.34f, 0.12f), new Vector3(room.Size.x * 0.3f, 0.035f, 0.06f), new Color(0.04f, 0.055f, 0.06f, 1f));
        }

        private void CreateContainerRack(Vector3 center, int seed)
        {
            Color[] colors =
            {
                new Color(0.08f, 0.26f, 0.52f, 1f),
                new Color(0.58f, 0.14f, 0.1f, 1f),
                new Color(0.78f, 0.55f, 0.08f, 1f),
                new Color(0.12f, 0.4f, 0.2f, 1f)
            };

            for (int i = 0; i < 3; i++)
            {
                CreateSolidProp("货柜舱迷你货柜 " + seed + "-" + i, center + new Vector3(-0.42f + i * 0.42f, 0f, 0f), new Vector3(0.34f, 0.24f, 0.18f), colors[(seed + i) % colors.Length]);
            }
        }

        private void CreateBoothSet(Vector3 center, string suffix)
        {
            CreateSolidProp("休息舱餐桌 " + suffix, center, new Vector3(0.44f, 0.2f, 0.14f), new Color(0.58f, 0.36f, 0.18f, 1f));
            CreateProp("休息舱座椅 L " + suffix, center + new Vector3(-0.34f, 0f, 0.05f), new Vector3(0.18f, 0.18f, 0.08f), new Color(0.22f, 0.16f, 0.28f, 1f));
            CreateProp("休息舱座椅 R " + suffix, center + new Vector3(0.34f, 0f, 0.05f), new Vector3(0.18f, 0.18f, 0.08f), new Color(0.22f, 0.16f, 0.28f, 1f));
        }

        private void CreateShipRoomFrames()
        {
            foreach (ShipRoomSpec room in ShipRooms())
            {
                CreateDoorMarker(room.Label + " 气闸门", DoorLightPosition(room), DoorLightScale(room), DoorColor(room));
            }
        }

        private void CreateShipTaskDressing()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                CreateTaskConsole("任务控制台 " + task.Name, new Vector3(task.Position.x / DesignScaleX, task.Position.y / DesignScaleY, 0.05f), i);
            }
        }

        private void CreateTaskConsole(string name, Vector3 position, int index)
        {
            Color baseColor = index % 3 == 0 ? new Color(0.08f, 0.16f, 0.18f, 1f) : index % 3 == 1 ? new Color(0.16f, 0.13f, 0.18f, 1f) : new Color(0.16f, 0.12f, 0.08f, 1f);
            string modelPath = index % 4 == 0 ? "Props/Prop_AccessPoint.fbx" : "Props/Prop_Computer.fbx";
            CreateSolidModelProp(name + " CC0 控制台", modelPath, position + new Vector3(0f, 0f, 0.04f), new Vector3(0.55f, 0.38f, 0.3f), index % 2 == 0 ? 0f : 180f);
            CreateSolidProp(name + " 底座碰撞体", position, new Vector3(0.44f, 0.28f, 0.14f), new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f));
            CreateMeshBoxProp(name + " 立体台座", position + new Vector3(0f, 0f, 0.14f), new Vector3(0.5f, 0.34f, 0.22f), baseColor);
            CreateMeshBoxProp(name + " 立体斜屏", position + new Vector3(0f, 0.18f, 0.34f), new Vector3(0.38f, 0.05f, 0.18f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateMeshPrimitiveProp(name + " 实体状态灯", PrimitiveType.Cylinder, position + new Vector3(0.22f, -0.12f, 0.36f), new Vector3(0.06f, 0.06f, 0.08f), new Color(0.95f, 0.72f, 0.1f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateProp(name + " 屏幕发光层", position + new Vector3(0f, 0.16f, 0.12f), new Vector3(0.32f, 0.06f, 0.08f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateShapeProp(name + " 状态灯", circleSprite, position + new Vector3(0.2f, -0.1f, 0.16f), new Vector3(0.08f, 0.08f, 0.05f), new Color(0.95f, 0.72f, 0.1f, 1f));
        }

        private void CreateShipAmbientDressing()
        {
            CreateVentGrate("中心暗线通风口", new Vector3(-3.2f, 0.32f, -0.05f));
            CreateVentGrate("东侧暗线通风口", new Vector3(4.9f, -0.32f, -0.05f));
            CreateVentGrate("南舱暗线通风口", new Vector3(-1.2f, -3.98f, -0.05f));
            CreateVentGrate("西侧主通风口", new Vector3(-6.85f, -1.2f, -0.05f));
            CreateVentGrate("右上主通风口", new Vector3(7.05f, 2.45f, -0.05f));
            CreateShapeProp("会议圆桌", circleSprite, new Vector3(0f, -0.35f, 0.08f), new Vector3(1.15f, 0.72f, 0.12f), new Color(0.42f, 0.45f, 0.4f, 1f));
            CreateSolidProp("会议桌证物箱", new Vector3(0.55f, -0.36f, 0.12f), new Vector3(0.34f, 0.2f, 0.16f), new Color(0.16f, 0.12f, 0.08f, 1f));
            CreateSolidProp("会议桌档案箱", new Vector3(-0.55f, -0.39f, 0.12f), new Vector3(0.34f, 0.2f, 0.16f), new Color(0.08f, 0.14f, 0.18f, 1f));
            CreatePrimitiveProp("会议桌红灯", PrimitiveType.Sphere, new Vector3(-0.15f, -0.02f, 0.12f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.9f, 0.08f, 0.06f, 1f));
            CreatePrimitiveProp("会议桌蓝灯", PrimitiveType.Sphere, new Vector3(0.15f, -0.02f, 0.12f), new Vector3(0.08f, 0.08f, 0.08f), new Color(0.08f, 0.35f, 0.95f, 1f));
            CreateProp("会议座位弧 L", new Vector3(-0.92f, -0.35f, 0.1f), new Vector3(0.34f, 0.18f, 0.08f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("会议座位弧 R", new Vector3(0.92f, -0.35f, 0.1f), new Vector3(0.34f, 0.18f, 0.08f), new Color(0.1f, 0.18f, 0.22f, 1f));
            CreateProp("屋顶 舰桥指挥铭牌", new Vector3(0f, 0.92f, 0.18f), new Vector3(1.2f, 0.08f, 0.08f), new Color(0.42f, 0.72f, 0.84f, 1f));

            for (int i = 0; i < 10; i++)
            {
                float x = -10f + i * 2.2f;
                CreateProp("舱壁铆钉列 " + i, new Vector3(x, 6.75f, 0.04f), new Vector3(0.12f, 0.05f, 0.05f), new Color(0.48f, 0.54f, 0.54f, 1f));
                CreateProp("南舱铆钉列 " + i, new Vector3(x, -6.85f, 0.04f), new Vector3(0.12f, 0.05f, 0.05f), new Color(0.48f, 0.54f, 0.54f, 1f));
            }

            CreateCorridorServiceProps();
        }

        private void CreateDenseMapMicroDressing()
        {
            CreateCorridorFloorPanels();
            CreateCorridorCameraNetwork();
            CreateCorridorCableRuns();
            CreateRoomMicroProps();
            CreateExteriorHullProps();
        }

        private void CreatePlayableScaleSetDressing()
        {
            CreateMainCorridorSetPieces();
            CreateRoomForegroundSilhouettes();
            CreateActionCameraForegroundOccluders();
            CreateDistrictHeroSetPieces();
            CreateLowerDeckActivitySets();
            CreateOrganicRouteLanguage();
            CreatePremiumTaskSetPieces();
            CreateMatureDockyardSetPieces();
            CreateTaskInteractionHalos();
            CreateEmergencyMeetingTableSet();
            CreatePhysicsCollisionMarkers();
            CreateActionViewShowcaseLayer();
        }

        private void CreateActionViewShowcaseLayer()
        {
            Color floor = new Color(0.082f, 0.096f, 0.1f, 1f);
            Color wall = new Color(0.018f, 0.026f, 0.03f, 1f);
            Color glass = new Color(0.08f, 0.44f, 0.52f, 0.92f);
            Color police = new Color(0.08f, 0.32f, 0.92f, 1f);
            Color gang = new Color(0.84f, 0.08f, 0.06f, 1f);
            Color amber = new Color(0.94f, 0.7f, 0.12f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.72f, 1f);
            Color shadow = new Color(0f, 0f, 0f, 0.58f);

            CreateShapeProp("行动视角样板层 中央会议圆形地毯", circleSprite, new Vector3(0f, -0.35f, -0.025f), new Vector3(1.62f, 1.02f, 0.05f), new Color(0.14f, 0.17f, 0.18f, 1f));
            CreateMeshPrimitiveProp("行动视角样板层 中央会议投票圆桌", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.16f), new Vector3(0.72f, 0.05f, 0.72f), new Color(0.32f, 0.36f, 0.34f, 1f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("行动视角样板层 圆桌证据蓝线", new Vector3(-0.14f, -0.11f, 0.34f), new Vector3(0.86f, 0.035f, 0.05f), glass, 6f);
            CreateMeshBoxProp("行动视角样板层 圆桌嫌疑红线", new Vector3(0.18f, -0.56f, 0.34f), new Vector3(0.58f, 0.035f, 0.05f), gang, -12f);

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 seat = new Vector3(Mathf.Cos(angle) * 1.24f, -0.35f + Mathf.Sin(angle) * 0.72f, 0.13f);
                CreateMeshPrimitiveProp("行动视角样板层 会议座位尺度点 " + i, PrimitiveType.Cylinder, seat, new Vector3(0.16f, 0.035f, 0.16f), i % 2 == 0 ? police : gang, Quaternion.Euler(90f, 0f, 0f));
            }

            (string name, Vector3 center, Vector3 size, Color color)[] roomSlices =
            {
                ("监控室近景切片", new Vector3(-2.85f, 0.92f, 0f), new Vector3(2.45f, 1.18f, 0.08f), new Color(0.1f, 0.18f, 0.24f, 1f)),
                ("茶餐厅近景切片", new Vector3(-3.92f, -1.58f, 0f), new Vector3(2.15f, 1.04f, 0.08f), new Color(0.3f, 0.17f, 0.1f, 1f)),
                ("情报夜市近景切片", new Vector3(1.72f, 1.18f, 0f), new Vector3(2.65f, 1.1f, 0.08f), new Color(0.24f, 0.12f, 0.08f, 1f)),
                ("主干道封控近景切片", new Vector3(2.55f, -1.58f, 0f), new Vector3(2.32f, 1.05f, 0.08f), new Color(0.12f, 0.16f, 0.17f, 1f))
            };

            for (int i = 0; i < roomSlices.Length; i++)
            {
                Vector3 center = roomSlices[i].center;
                Vector3 size = roomSlices[i].size;
                float halfWidth = size.x * 0.5f;
                float halfHeight = size.y * 0.5f;
                CreateShapeProp("行动视角样板层 " + roomSlices[i].name + " 圆角地面", roundedRectSprite, center + new Vector3(0f, 0f, -0.035f), size, roomSlices[i].color);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 后墙体", center + new Vector3(0f, halfHeight + 0.08f, 0.46f), new Vector3(size.x, 0.11f, 0.78f), wall);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 前景檐影", center + new Vector3(0f, -halfHeight - 0.08f, 0.58f), new Vector3(size.x * 0.78f, 0.13f, 0.34f), shadow);
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 左侧厚墙", center + new Vector3(-halfWidth - 0.06f, 0f, 0.34f), new Vector3(0.1f, size.y * 0.72f, 0.52f), Darken(wall, 1.25f));
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 右侧厚墙", center + new Vector3(halfWidth + 0.06f, 0f, 0.34f), new Vector3(0.1f, size.y * 0.72f, 0.52f), Darken(wall, 1.18f));
                CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 门楣灯", center + new Vector3(0f, -halfHeight + 0.08f, 0.66f), new Vector3(size.x * 0.42f, 0.035f, 0.08f), i % 2 == 0 ? glass : amber);

                for (int window = 0; window < 3; window++)
                {
                    float x = -halfWidth * 0.46f + window * halfWidth * 0.46f;
                    CreateMeshBoxProp("行动视角样板层 " + roomSlices[i].name + " 后窗 " + window, center + new Vector3(x, halfHeight + 0.145f, 0.62f), new Vector3(0.28f, 0.035f, 0.14f), glass);
                }
            }

            Vector3[] routePanels =
            {
                new Vector3(-3.05f, -0.22f, 0.02f),
                new Vector3(-1.65f, 0.22f, 0.02f),
                new Vector3(0.15f, 0.48f, 0.02f),
                new Vector3(1.72f, 0.12f, 0.02f),
                new Vector3(3.0f, -0.58f, 0.02f),
                new Vector3(1.18f, -1.72f, 0.02f),
                new Vector3(-0.72f, -1.52f, 0.02f),
                new Vector3(-2.55f, -1.1f, 0.02f)
            };

            for (int i = 0; i < routePanels.Length; i++)
            {
                float rotation = i % 2 == 0 ? -14f : 16f;
                CreateMeshBoxProp("行动视角样板层 非直角走廊地砖 " + i, routePanels[i], new Vector3(1.04f, 0.22f, 0.05f), floor, rotation);
                CreateMeshBoxProp("行动视角样板层 非直角导向灯 " + i, routePanels[i] + new Vector3(0f, 0.16f, 0.08f), new Vector3(0.72f, 0.035f, 0.05f), i % 2 == 0 ? amber : glass, rotation);
            }

            (Vector3 position, Vector3 size, float rotation, Color color)[] floorBreakup =
            {
                (new Vector3(-0.72f, -0.82f, 0.035f), new Vector3(0.86f, 0.16f, 0.05f), -10f, new Color(0.14f, 0.17f, 0.18f, 1f)),
                (new Vector3(0.62f, -0.92f, 0.035f), new Vector3(0.94f, 0.14f, 0.05f), 12f, new Color(0.12f, 0.15f, 0.16f, 1f)),
                (new Vector3(-1.08f, 0.38f, 0.035f), new Vector3(0.78f, 0.14f, 0.05f), 8f, new Color(0.11f, 0.145f, 0.15f, 1f)),
                (new Vector3(0.92f, 0.42f, 0.035f), new Vector3(0.72f, 0.14f, 0.05f), -8f, new Color(0.14f, 0.16f, 0.16f, 1f)),
                (new Vector3(-2.2f, -0.36f, 0.035f), new Vector3(0.62f, 0.12f, 0.05f), -16f, new Color(0.08f, 0.42f, 0.5f, 1f)),
                (new Vector3(2.08f, -0.28f, 0.035f), new Vector3(0.62f, 0.12f, 0.05f), 16f, new Color(0.86f, 0.62f, 0.12f, 1f)),
                (new Vector3(-0.12f, -1.32f, 0.04f), new Vector3(1.36f, 0.035f, 0.05f), 0f, new Color(0.08f, 0.72f, 0.86f, 1f)),
                (new Vector3(0.06f, 0.92f, 0.04f), new Vector3(1.24f, 0.035f, 0.05f), 0f, new Color(0.94f, 0.72f, 0.12f, 1f))
            };

            for (int i = 0; i < floorBreakup.Length; i++)
            {
                CreateMeshBoxProp("行动视角样板层 中心地面细节 " + i, floorBreakup[i].position, floorBreakup[i].size, floorBreakup[i].color, floorBreakup[i].rotation);
            }

            CreateActionViewTaskShowcase(13, new Vector3(-1.42f, -0.18f, 0f), "通讯干扰终端");
            CreateActionViewTaskShowcase(5, new Vector3(-3.88f, 0.08f, 0f), "茶餐厅线人录音");
            CreateActionViewTaskShowcase(20, new Vector3(-1.95f, 1.55f, 0f), "夜市暗号");
            CreateActionViewTaskShowcase(18, new Vector3(1.74f, -2.18f, 0f), "车牌追踪");

            (Vector3 position, Color primary, Color accent, string label)[] npcRefs =
            {
                (new Vector3(-2.42f, -0.82f, 0.16f), police, glass, "警方"),
                (new Vector3(0.92f, -0.85f, 0.16f), gang, amber, "嫌疑"),
                (new Vector3(-0.78f, 0.86f, 0.16f), new Color(0.22f, 0.48f, 0.32f, 1f), amber, "路人"),
                (new Vector3(2.48f, 0.38f, 0.16f), new Color(0.36f, 0.28f, 0.42f, 1f), glass, "卧底")
            };

            for (int i = 0; i < npcRefs.Length; i++)
            {
                CreateActionViewScaleCharacter("行动视角样板层 尺度NPC " + i, npcRefs[i].position, npcRefs[i].primary, npcRefs[i].accent, npcRefs[i].label);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -4.8f + i * 0.86f;
                float y = i % 2 == 0 ? -2.42f : 1.92f;
                CreateMeshBoxProp("行动视角样板层 街区杂物箱 " + i, new Vector3(x, y, 0.13f), new Vector3(0.34f, 0.22f, 0.24f), i % 3 == 0 ? amber : i % 3 == 1 ? new Color(0.12f, 0.36f, 0.42f, 1f) : new Color(0.42f, 0.18f, 0.12f, 1f), i % 2 == 0 ? -8f : 10f);
            }

            CreateMeshBoxProp("行动视角样板层 近景警戒线 A", new Vector3(-1.88f, -2.38f, 0.22f), new Vector3(1.35f, 0.04f, 0.08f), amber, -10f);
            CreateMeshBoxProp("行动视角样板层 近景警戒线 B", new Vector3(1.35f, -2.12f, 0.22f), new Vector3(1.18f, 0.04f, 0.08f), amber, 12f);
            CreateMeshBoxProp("行动视角样板层 近景电缆桥", new Vector3(0.05f, 1.58f, 0.74f), new Vector3(2.85f, 0.08f, 0.14f), wall);
            CreateMeshBoxProp("行动视角样板层 电缆桥冷光条", new Vector3(0.05f, 1.48f, 0.88f), new Vector3(2.18f, 0.035f, 0.05f), glass);
            CreateMeshBoxProp("行动视角样板层 证据白板主面", new Vector3(-0.78f, -1.88f, 0.52f), new Vector3(0.82f, 0.06f, 0.42f), paper, -8f);
            CreateMeshBoxProp("行动视角样板层 证据白板红线", new Vector3(-0.9f, -1.84f, 0.78f), new Vector3(0.5f, 0.025f, 0.04f), gang, 8f);
            CreateMeshBoxProp("行动视角样板层 证据白板蓝线", new Vector3(-0.62f, -1.84f, 0.66f), new Vector3(0.42f, 0.025f, 0.04f), police, -14f);
        }

        private void CreateActionViewTaskShowcase(int taskId, Vector3 position, string label)
        {
            Color accent = TaskPanelAccent(taskId);
            Color dark = new Color(0.035f, 0.045f, 0.05f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.12f, 1f);

            CreateShapeProp("行动视角样板层 " + label + " 任务地面光圈", softCircleSprite, position + new Vector3(0f, 0f, 0.035f), new Vector3(1.16f, 0.72f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.26f));
            CreateMeshBoxProp("行动视角样板层 " + label + " 大型任务台", position + new Vector3(0f, 0f, 0.28f), new Vector3(0.72f, 0.38f, 0.38f), dark);
            CreateMeshBoxProp("行动视角样板层 " + label + " 高亮交互屏", position + new Vector3(0f, 0.24f, 0.62f), new Vector3(0.52f, 0.04f, 0.22f), accent);
            CreateMeshBoxProp("行动视角样板层 " + label + " E键提示牌", position + new Vector3(-0.48f, 0.18f, 0.72f), new Vector3(0.22f, 0.035f, 0.14f), amber);
            CreateMeshPrimitiveProp("行动视角样板层 " + label + " 顶部信标", PrimitiveType.Cylinder, position + new Vector3(0.46f, -0.16f, 0.68f), new Vector3(0.06f, 0.06f, 0.46f), accent, Quaternion.identity);
            CreateMeshBoxProp("行动视角样板层 " + label + " 信标灯帽", position + new Vector3(0.46f, -0.16f, 0.96f), new Vector3(0.18f, 0.035f, 0.08f), amber);
            CreateWorldLabelAt(label, ScaleMapPosition(position + new Vector3(0f, 0.6f, 0.1f)), 0.06f);
        }

        private void CreateActionViewScaleCharacter(string name, Vector3 position, Color primary, Color accent, string label)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(worldRoot.transform, false);
            root.transform.position = ScaleMapPosition(position);
            root.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            CreateMeshPrimitiveChild(root.transform, "Shadow", PrimitiveType.Cylinder, new Vector3(0f, -0.32f, -0.12f), new Vector3(0.52f, 0.08f, 0.28f), new Color(0f, 0f, 0f, 0.32f), Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, -0.04f, 0.2f), new Vector3(0.26f, 0.26f, 0.56f), primary, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshPrimitiveChild(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0.04f, 0.25f, 0.52f), new Vector3(0.3f, 0.26f, 0.26f), primary, Quaternion.identity);
            CreateMeshBoxChild(root.transform, "Visor", new Vector3(0.13f, 0.42f, 0.56f), new Vector3(0.22f, 0.035f, 0.1f), new Color(0.58f, 0.9f, 1f, 1f));
            CreateMeshPrimitiveChild(root.transform, "Arm L", PrimitiveType.Capsule, new Vector3(-0.22f, -0.04f, 0.28f), new Vector3(0.07f, 0.07f, 0.28f), accent, Quaternion.Euler(90f, 0f, 12f));
            CreateMeshPrimitiveChild(root.transform, "Arm R", PrimitiveType.Capsule, new Vector3(0.22f, -0.04f, 0.28f), new Vector3(0.07f, 0.07f, 0.28f), accent, Quaternion.Euler(90f, 0f, -12f));
            CreateMeshBoxChild(root.transform, "Role Strip " + label, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.2f, 0.035f, 0.06f), accent);
            SetSortingFromZ(root);
        }

        private void CreateMainCorridorSetPieces()
        {
            Color dark = new Color(0.035f, 0.045f, 0.048f, 1f);
            Color metal = new Color(0.12f, 0.145f, 0.15f, 1f);
            Color screen = new Color(0.04f, 0.7f, 0.84f, 1f);
            Color warning = new Color(0.92f, 0.72f, 0.08f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float x = -7.1f + i * 2.05f;
                CreateModelProp("CC0 主廊强化舱板 " + i, i % 2 == 0 ? "Walls/TopCables_Straight.fbx" : "Walls/TopAstra_Straight.fbx", new Vector3(x, -0.78f, 0.18f), new Vector3(0.92f, 0.18f, 0.34f), 0f, true);
                CreateModelProp("CC0 主廊上墙窗 " + i, "Walls/WallAstra_Straight_Window.fbx", new Vector3(x + 0.16f, 0.92f, 0.2f), new Vector3(0.96f, 0.22f, 0.36f), 180f, true);
                CreateMeshBoxProp("主廊检修盖发光边 " + i, new Vector3(x, -0.18f, 0.06f), new Vector3(0.54f, 0.035f, 0.04f), i % 2 == 0 ? screen : warning);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -5.8f + i * 2.3f;
                CreateModelProp("CC0 上层连廊窗墙 " + i, "Walls/TopWindow_Straight.fbx", new Vector3(x, 4.33f, 0.2f), new Vector3(1.05f, 0.2f, 0.36f), 0f, true);
                CreateModelProp("CC0 下层连廊电缆墙 " + i, "Walls/TopCables_Straight_Hanging.fbx", new Vector3(x + 0.26f, -4.58f, 0.2f), new Vector3(1.05f, 0.2f, 0.36f), 180f, true);
            }

            Vector3[] kioskPositions =
            {
                new Vector3(-3.8f, -0.84f, 0.1f),
                new Vector3(3.65f, 0.66f, 0.1f),
                new Vector3(-6.1f, -3.42f, 0.1f),
                new Vector3(6.36f, 3.18f, 0.1f)
            };

            for (int i = 0; i < kioskPositions.Length; i++)
            {
                Vector3 position = kioskPositions[i];
                CreateSolidModelProp("CC0 巡逻服务柜 " + i, "Props/Prop_AccessPoint.fbx", position + new Vector3(0f, 0f, 0.03f), new Vector3(0.42f, 0.32f, 0.32f), i % 2 == 0 ? 0f : 180f);
                CreateMeshBoxProp("巡逻服务柜屏 " + i, position + new Vector3(0f, 0.18f, 0.32f), new Vector3(0.28f, 0.04f, 0.1f), screen);
                CreateMeshBoxProp("巡逻服务柜黄黑边 " + i, position + new Vector3(0f, -0.18f, 0.2f), new Vector3(0.38f, 0.04f, 0.06f), warning);
            }

            CreateSolidProp("主廊移动拒马 A", new Vector3(-2.3f, 0.45f, 0.07f), new Vector3(0.68f, 0.14f, 0.2f), dark);
            CreateSolidProp("主廊移动拒马 B", new Vector3(2.48f, -0.78f, 0.07f), new Vector3(0.68f, 0.14f, 0.2f), dark);
            CreateMeshBoxProp("拒马反光条 A", new Vector3(-2.3f, 0.5f, 0.22f), new Vector3(0.52f, 0.035f, 0.05f), warning);
            CreateMeshBoxProp("拒马反光条 B", new Vector3(2.48f, -0.73f, 0.22f), new Vector3(0.52f, 0.035f, 0.05f), warning);
            CreateMeshBoxProp("主廊地面油污暗斑", new Vector3(4.9f, -0.12f, -0.02f), new Vector3(0.64f, 0.18f, 0.04f), new Color(0.015f, 0.018f, 0.018f, 1f));
            CreateMeshBoxProp("下廊刹车痕 A", new Vector3(-2.2f, -3.68f, -0.02f), new Vector3(0.72f, 0.045f, 0.04f), dark, -8f);
            CreateMeshBoxProp("下廊刹车痕 B", new Vector3(-1.48f, -3.8f, -0.02f), new Vector3(0.62f, 0.045f, 0.04f), dark, -8f);
            CreateMeshBoxProp("中心交叉口巡逻箭头 A", new Vector3(-0.62f, -1.15f, 0.02f), new Vector3(0.28f, 0.18f, 0.04f), warning, -22f);
            CreateMeshBoxProp("中心交叉口巡逻箭头 B", new Vector3(0.72f, 0.52f, 0.02f), new Vector3(0.28f, 0.18f, 0.04f), screen, 22f);
            CreateModelProp("CC0 中心环形护栏 L", "Props/Prop_Rail_Round_Big.fbx", new Vector3(-0.72f, -0.35f, 0.18f), new Vector3(0.62f, 0.42f, 0.24f), 90f, true);
            CreateModelProp("CC0 中心环形护栏 R", "Props/Prop_Rail_Round_Big.fbx", new Vector3(0.72f, -0.35f, 0.18f), new Vector3(0.62f, 0.42f, 0.24f), -90f, true);
            CreateMeshBoxProp("主廊管线桥", new Vector3(0f, 0.96f, 0.32f), new Vector3(2.4f, 0.07f, 0.12f), metal);
            CreateModelProp("CC0 管线桥夹具 A", "Props/Prop_PipeHolder.fbx", new Vector3(-1.12f, 0.98f, 0.38f), new Vector3(0.2f, 0.16f, 0.18f), 0f);
            CreateModelProp("CC0 管线桥夹具 B", "Props/Prop_PipeHolder.fbx", new Vector3(1.12f, 0.98f, 0.38f), new Vector3(0.2f, 0.16f, 0.18f), 180f);
        }

        private void CreateRoomForegroundSilhouettes()
        {
            foreach (ShipRoomSpec room in ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                Color shadow = new Color(0.015f, 0.018f, 0.02f, 0.42f);
                Color glass = new Color(0.16f, 0.38f, 0.45f, 0.92f);

                CreateMeshBoxProp("前景墙体阴影 " + room.Name + " N", room.Center + new Vector3(0f, halfHeight + 0.08f, 0.22f), new Vector3(room.Size.x * 0.9f, 0.08f, 0.18f), shadow);
                CreateMeshBoxProp("前景墙体阴影 " + room.Name + " S", room.Center + new Vector3(0f, -halfHeight - 0.08f, 0.2f), new Vector3(room.Size.x * 0.82f, 0.08f, 0.16f), shadow);
                CreateModelProp("CC0 " + room.Name + " 顶部窗墙", "Walls/WallWindow_Straight.fbx", room.Center + new Vector3(0f, halfHeight - 0.08f, 0.28f), new Vector3(Mathf.Min(room.Size.x * 0.64f, 1.9f), 0.2f, 0.36f), 0f, true);
                CreateMeshBoxProp("房间玻璃反光 " + room.Name, room.Center + new Vector3(halfWidth * 0.38f, halfHeight - 0.2f, 0.38f), new Vector3(Mathf.Min(0.72f, room.Size.x * 0.24f), 0.035f, 0.08f), glass);
                CreateMeshBoxProp("房间地面编号条 " + room.Name, room.Center + new Vector3(-halfWidth * 0.32f, -halfHeight * 0.34f, 0.04f), new Vector3(Mathf.Min(0.86f, room.Size.x * 0.24f), 0.04f, 0.04f), DoorColor(room));

                if (room.Size.x > 3.3f)
                {
                    CreateModelProp("CC0 " + room.Name + " 角落圆柱 A", "Columns/Column_Round.fbx", room.Center + new Vector3(-halfWidth + 0.38f, halfHeight - 0.32f, 0.18f), new Vector3(0.22f, 0.22f, 0.4f), 0f);
                    CreateModelProp("CC0 " + room.Name + " 角落圆柱 B", "Columns/Column_Pipes.fbx", room.Center + new Vector3(halfWidth - 0.4f, -halfHeight + 0.32f, 0.18f), new Vector3(0.22f, 0.22f, 0.4f), 0f);
                }
            }
        }

        private void CreateActionCameraForegroundOccluders()
        {
            Color deepShadow = new Color(0.006f, 0.009f, 0.011f, 0.74f);
            Color bulkhead = new Color(0.018f, 0.025f, 0.03f, 0.92f);
            Color glass = new Color(0.12f, 0.32f, 0.38f, 0.68f);
            Color trim = new Color(0.44f, 0.52f, 0.52f, 0.86f);

            foreach (ShipRoomSpec room in ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                float topY = halfHeight + 0.2f;
                float bottomY = -halfHeight - 0.16f;

                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 上檐阴影", room.Center + new Vector3(0f, topY, 0.78f), new Vector3(room.Size.x * 0.82f, 0.18f, 0.42f), deepShadow);
                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 下檐黑边", room.Center + new Vector3(0f, bottomY, 0.58f), new Vector3(room.Size.x * 0.58f, 0.12f, 0.32f), bulkhead);

                if (room.Entrance == MapEntrance.East || room.Entrance == MapEntrance.West)
                {
                    float side = room.Entrance == MapEntrance.East ? halfWidth + 0.18f : -halfWidth - 0.18f;
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 侧门厚框", room.Center + new Vector3(side, 0f, 0.62f), new Vector3(0.18f, room.Size.y * 0.56f, 0.36f), bulkhead);
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 侧门玻璃", room.Center + new Vector3(side, 0f, 0.88f), new Vector3(0.05f, room.Size.y * 0.32f, 0.18f), glass);
                }
                else
                {
                    float side = room.Entrance == MapEntrance.North ? topY : bottomY;
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 横门厚框", room.Center + new Vector3(0f, side, 0.62f), new Vector3(room.Size.x * 0.36f, 0.14f, 0.36f), bulkhead);
                    CreateMeshBoxProp("前景遮挡层 " + room.Name + " 横门灯缝", room.Center + new Vector3(0f, side, 0.88f), new Vector3(room.Size.x * 0.28f, 0.035f, 0.08f), DoorColor(room));
                }

                CreateMeshBoxProp("前景遮挡层 " + room.Name + " 识别灯带", room.Center + new Vector3(-halfWidth * 0.35f, topY - 0.1f, 0.98f), new Vector3(Mathf.Min(room.Size.x * 0.26f, 0.96f), 0.035f, 0.08f), trim);
            }

            Vector3[] corridorOccluders =
            {
                new Vector3(-5.3f, 0.7f, 0.74f),
                new Vector3(-0.8f, 0.82f, 0.74f),
                new Vector3(3.9f, 0.68f, 0.74f),
                new Vector3(-5.2f, -4.5f, 0.72f),
                new Vector3(0.2f, -4.55f, 0.72f),
                new Vector3(5.35f, -4.5f, 0.72f)
            };

            for (int i = 0; i < corridorOccluders.Length; i++)
            {
                Vector3 position = corridorOccluders[i];
                CreateMeshBoxProp("前景遮挡层 主廊低顶梁 " + i, position, new Vector3(1.36f, 0.12f, 0.36f), bulkhead);
                CreateMeshBoxProp("前景遮挡层 主廊顶梁冷光 " + i, position + new Vector3(0f, -0.08f, 0.22f), new Vector3(1.0f, 0.035f, 0.06f), new Color(0.08f, 0.72f, 0.86f, 0.92f));
            }
        }

        private void CreateDistrictHeroSetPieces()
        {
            CreateDockyardHeroSet();
            CreateMarketHeroSet();
            CreateCommandAndEvidenceHeroSet();
            CreateClinicAndBackLaneHeroSet();
            CreateFinancePowerHeroSet();
        }

        private void CreateDockyardHeroSet()
        {
            Color crane = new Color(0.84f, 0.56f, 0.06f, 1f);
            Color steel = new Color(0.06f, 0.075f, 0.08f, 1f);
            Color blue = new Color(0.08f, 0.18f, 0.28f, 0.78f);
            Color red = new Color(0.28f, 0.1f, 0.08f, 0.78f);

            CreateSolidProp("2.5D 建筑体 巨型货柜龙门架左脚", new Vector3(-10.7f, 5.18f, 0.22f), new Vector3(0.16f, 1.78f, 0.64f), crane);
            CreateSolidProp("2.5D 建筑体 巨型货柜龙门架右脚", new Vector3(-8.2f, 5.18f, 0.22f), new Vector3(0.16f, 1.78f, 0.64f), crane);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架横梁", new Vector3(-9.45f, 6.05f, 0.98f), new Vector3(2.85f, 0.12f, 0.14f), crane);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架吊轨", new Vector3(-9.45f, 5.62f, 0.74f), new Vector3(2.32f, 0.06f, 0.08f), steel);
            CreateMeshBoxProp("屋顶 巨型货柜龙门架吊钩线", new Vector3(-9.05f, 5.35f, 0.52f), new Vector3(0.04f, 0.52f, 0.06f), steel);
            CreateSolidProp("2.5D 建筑体 高层货柜底影一层", new Vector3(-9.55f, 5.46f, 0.06f), new Vector3(1.18f, 0.38f, 0.08f), blue);
            CreateSolidProp("2.5D 建筑体 高层货柜底影二层", new Vector3(-9.08f, 5.86f, 0.26f), new Vector3(1.08f, 0.34f, 0.08f), red);
            CreateModelProp("成熟港区设施 龙门架下免费货柜一层", "Props/Prop_Crate4.fbx", new Vector3(-9.55f, 5.46f, 0.16f), new Vector3(1.02f, 0.36f, 0.34f), 0f, true);
            CreateModelProp("成熟港区设施 龙门架下免费货柜二层", "Props/Prop_Crate3.fbx", new Vector3(-9.08f, 5.86f, 0.38f), new Vector3(0.92f, 0.32f, 0.32f), 180f, true);
            CreateMeshBoxProp("前景遮挡层 货柜区吊机暗影", new Vector3(-9.62f, 4.38f, 0.86f), new Vector3(2.1f, 0.16f, 0.28f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateMarketHeroSet()
        {
            Color redCanvas = new Color(0.72f, 0.12f, 0.08f, 1f);
            Color greenCanvas = new Color(0.12f, 0.38f, 0.22f, 1f);
            Color neon = new Color(0.95f, 0.16f, 0.46f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.18f, 1f);

            for (int i = 0; i < 4; i++)
            {
                float x = -2.45f + i * 0.98f;
                CreateMeshBoxProp("2.5D 建筑体 夜市折叠棚立柱 " + i, new Vector3(x, 3.42f, 0.3f), new Vector3(0.06f, 0.5f, 0.44f), new Color(0.06f, 0.045f, 0.04f, 1f));
                CreateMeshBoxProp("屋顶 夜市彩棚 " + i, new Vector3(x, 3.64f, 0.68f), new Vector3(0.92f, 0.18f, 0.18f), i % 2 == 0 ? redCanvas : greenCanvas);
                CreateMeshBoxProp("屋顶 夜市招牌灯字 " + i, new Vector3(x, 3.76f, 0.86f), new Vector3(0.54f, 0.035f, 0.06f), i % 2 == 0 ? neon : amber);
            }

            CreateMeshBoxProp("前景遮挡层 夜市人潮顶棚阴影", new Vector3(-0.84f, 2.62f, 0.82f), new Vector3(3.2f, 0.14f, 0.28f), new Color(0.03f, 0.012f, 0.01f, 0.68f));
            CreateMeshBoxProp("2.5D 建筑体 茶餐厅骑楼雨棚", new Vector3(-4.82f, 2.6f, 0.62f), new Vector3(1.82f, 0.2f, 0.18f), new Color(0.72f, 0.42f, 0.14f, 1f));
            CreateMeshBoxProp("屋顶 茶餐厅霓虹长牌", new Vector3(-4.82f, 2.82f, 0.86f), new Vector3(1.4f, 0.04f, 0.08f), neon);
        }

        private void CreateCommandAndEvidenceHeroSet()
        {
            Color policeBlue = new Color(0.08f, 0.28f, 0.9f, 1f);
            Color policeRed = new Color(0.9f, 0.08f, 0.06f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.74f, 1f);
            Color uv = new Color(0.45f, 0.24f, 0.92f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 指挥车车身高体", new Vector3(0.12f, -5.34f, 0.38f), new Vector3(1.82f, 0.82f, 0.62f), new Color(0.06f, 0.11f, 0.15f, 1f));
            CreateMeshBoxProp("屋顶 指挥车顶灯红", new Vector3(-0.34f, -4.78f, 0.9f), new Vector3(0.34f, 0.06f, 0.08f), policeRed);
            CreateMeshBoxProp("屋顶 指挥车顶灯蓝", new Vector3(0.48f, -4.78f, 0.9f), new Vector3(0.34f, 0.06f, 0.08f), policeBlue);
            CreateMeshBoxProp("前景遮挡层 指挥车车头阴影", new Vector3(0.12f, -4.72f, 0.72f), new Vector3(1.9f, 0.16f, 0.28f), new Color(0f, 0f, 0f, 0.64f));
            CreateMeshBoxProp("2.5D 建筑体 行动白板高架", new Vector3(-1.35f, -5.72f, 0.48f), new Vector3(0.96f, 0.08f, 0.56f), paper);
            CreateMeshBoxProp("屋顶 行动白板红线", new Vector3(-1.46f, -5.66f, 0.78f), new Vector3(0.66f, 0.025f, 0.06f), policeRed, 12f);

            CreateMeshBoxProp("2.5D 建筑体 证物冷柜高体", new Vector3(-8.3f, -5.16f, 0.38f), new Vector3(1.42f, 0.5f, 0.58f), new Color(0.12f, 0.28f, 0.34f, 1f));
            CreateMeshBoxProp("屋顶 证物紫外扫描架", new Vector3(-8.3f, -4.78f, 0.78f), new Vector3(1.2f, 0.055f, 0.08f), uv);
            CreateMeshBoxProp("前景遮挡层 证物库冷柜门影", new Vector3(-8.3f, -4.58f, 0.72f), new Vector3(1.28f, 0.12f, 0.24f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateClinicAndBackLaneHeroSet()
        {
            Color clinic = new Color(0.36f, 0.72f, 0.62f, 1f);
            Color metal = new Color(0.08f, 0.09f, 0.09f, 1f);
            Color canvas = new Color(0.48f, 0.1f, 0.08f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 诊所招牌高体", new Vector3(7.55f, -5.05f, 0.76f), new Vector3(0.16f, 1.08f, 0.56f), new Color(0.06f, 0.14f, 0.1f, 1f));
            CreateMeshBoxProp("屋顶 诊所绿十字竖", new Vector3(7.62f, -5.05f, 1.1f), new Vector3(0.04f, 0.52f, 0.08f), clinic);
            CreateMeshBoxProp("屋顶 诊所绿十字横", new Vector3(7.62f, -5.05f, 1.1f), new Vector3(0.04f, 0.08f, 0.34f), clinic);
            CreateMeshBoxProp("前景遮挡层 诊所帘影", new Vector3(6.18f, -4.28f, 0.76f), new Vector3(1.6f, 0.12f, 0.34f), new Color(0.02f, 0.04f, 0.035f, 0.66f));

            CreateMeshBoxProp("2.5D 建筑体 后巷排档雨棚高体", new Vector3(5.62f, -1.92f, 0.62f), new Vector3(1.7f, 0.24f, 0.22f), canvas);
            CreateMeshBoxProp("屋顶 后巷油烟管", new Vector3(4.92f, -1.58f, 0.84f), new Vector3(0.14f, 0.14f, 0.52f), metal);
            CreateMeshBoxProp("前景遮挡层 后巷暗门阴影", new Vector3(6.28f, -0.72f, 0.68f), new Vector3(1.12f, 0.12f, 0.28f), new Color(0f, 0f, 0f, 0.64f));
        }

        private void CreateFinancePowerHeroSet()
        {
            Color glass = new Color(0.08f, 0.36f, 0.48f, 1f);
            Color gold = new Color(0.92f, 0.7f, 0.16f, 1f);
            Color warning = new Color(0.92f, 0.18f, 0.08f, 1f);
            Color blue = new Color(0.16f, 0.52f, 0.92f, 1f);

            CreateMeshBoxProp("2.5D 建筑体 金融楼玻璃幕墙", new Vector3(4.78f, 3.75f, 0.78f), new Vector3(1.8f, 0.12f, 0.72f), glass);
            for (int i = 0; i < 4; i++)
            {
                CreateMeshBoxProp("屋顶 金融楼窗格 " + i, new Vector3(4.18f + i * 0.38f, 3.82f, 0.98f), new Vector3(0.22f, 0.03f, 0.08f), blue);
            }
            CreateMeshBoxProp("屋顶 金融楼金色招牌", new Vector3(4.78f, 3.94f, 1.18f), new Vector3(1.34f, 0.035f, 0.07f), gold);

            CreateMeshBoxProp("2.5D 建筑体 电房高压母线架", new Vector3(8.78f, 6.08f, 0.72f), new Vector3(1.74f, 0.12f, 0.56f), new Color(0.09f, 0.12f, 0.18f, 1f));
            CreateMeshBoxProp("屋顶 电房红色警报条", new Vector3(8.78f, 6.18f, 1.08f), new Vector3(1.42f, 0.035f, 0.07f), warning);
            CreateMeshBoxProp("前景遮挡层 电房电缆阴影", new Vector3(8.78f, 4.42f, 0.74f), new Vector3(1.6f, 0.14f, 0.3f), new Color(0f, 0f, 0f, 0.62f));
        }

        private void CreateLowerDeckActivitySets()
        {
            Color commandBlue = new Color(0.08f, 0.36f, 0.72f, 1f);
            Color evidencePurple = new Color(0.42f, 0.24f, 0.84f, 1f);
            Color clinicGreen = new Color(0.24f, 0.58f, 0.46f, 1f);
            Color metal = new Color(0.07f, 0.085f, 0.09f, 1f);
            Color paper = new Color(0.82f, 0.82f, 0.72f, 1f);

            CreateSolidModelProp("CC0 指挥车车头", "Props/Prop_Crate4.fbx", new Vector3(-1.28f, -5.22f, 0.12f), new Vector3(0.72f, 0.4f, 0.32f), 0f);
            CreateSolidModelProp("CC0 指挥车设备箱", "Props/Prop_AccessPoint.fbx", new Vector3(1.18f, -5.25f, 0.12f), new Vector3(0.58f, 0.38f, 0.32f), 180f);
            CreateMeshBoxProp("指挥车蓝白灯 A", new Vector3(-0.72f, -4.78f, 0.28f), new Vector3(0.42f, 0.055f, 0.08f), commandBlue);
            CreateMeshBoxProp("指挥车红白灯 B", new Vector3(0.62f, -4.78f, 0.28f), new Vector3(0.42f, 0.055f, 0.08f), new Color(0.82f, 0.1f, 0.08f, 1f));
            CreateMeshBoxProp("行动路线白板主面", new Vector3(-0.18f, -5.74f, 0.22f), new Vector3(1.36f, 0.06f, 0.28f), paper);
            CreateMeshBoxProp("行动路线红线", new Vector3(-0.28f, -5.72f, 0.38f), new Vector3(0.72f, 0.025f, 0.04f), new Color(0.86f, 0.08f, 0.06f, 1f), 8f);
            CreateMeshBoxProp("行动路线蓝线", new Vector3(0.18f, -5.7f, 0.39f), new Vector3(0.62f, 0.025f, 0.04f), commandBlue, -12f);

            for (int i = 0; i < 4; i++)
            {
                float x = -9.62f + i * 0.58f;
                CreateSolidModelProp("CC0 证物库矮架 " + i, i % 2 == 0 ? "Props/Prop_Chest.fbx" : "Props/Prop_Crate3.fbx", new Vector3(x, -5.32f, 0.1f), new Vector3(0.42f, 0.28f, 0.28f), i * 12f);
                CreateMeshBoxProp("证物库紫外编号 " + i, new Vector3(x, -5.05f, 0.34f), new Vector3(0.22f, 0.035f, 0.05f), evidencePurple);
            }

            CreateSolidProp("证物库移动冷柜", new Vector3(-7.68f, -4.58f, 0.08f), new Vector3(0.72f, 0.34f, 0.22f), new Color(0.16f, 0.32f, 0.36f, 1f));
            CreateMeshBoxProp("证物库冷柜温度屏", new Vector3(-7.68f, -4.34f, 0.26f), new Vector3(0.38f, 0.04f, 0.08f), new Color(0.06f, 0.74f, 0.86f, 1f));
            CreateMeshBoxProp("证物库脚印胶片", new Vector3(-8.58f, -5.72f, 0.06f), new Vector3(0.48f, 0.12f, 0.04f), new Color(0.02f, 0.025f, 0.028f, 1f), -14f);

            CreateSolidModelProp("CC0 诊所推车", "Props/Prop_ItemHolder.fbx", new Vector3(5.28f, -4.62f, 0.12f), new Vector3(0.42f, 0.32f, 0.28f), 0f);
            CreateSolidModelProp("CC0 诊所仪器柜", "Props/Prop_AccessPoint.fbx", new Vector3(7.12f, -5.42f, 0.12f), new Vector3(0.42f, 0.34f, 0.3f), 180f);
            CreateMeshBoxProp("诊所生命监护绿线", new Vector3(7.12f, -5.16f, 0.34f), new Vector3(0.32f, 0.035f, 0.05f), clinicGreen);
            CreateMeshBoxProp("诊所隔帘轨", new Vector3(6.16f, -4.34f, 0.35f), new Vector3(1.45f, 0.04f, 0.07f), metal);
            CreateMeshBoxProp("诊所半透明隔帘 A", new Vector3(5.72f, -4.48f, 0.24f), new Vector3(0.08f, 0.38f, 0.16f), new Color(0.5f, 0.78f, 0.72f, 0.78f));
            CreateMeshBoxProp("诊所半透明隔帘 B", new Vector3(6.58f, -4.48f, 0.24f), new Vector3(0.08f, 0.38f, 0.16f), new Color(0.5f, 0.78f, 0.72f, 0.78f));

            CreateSolidModelProp("CC0 后巷油桶堆", "Props/Prop_Barrel_Large.fbx", new Vector3(5.1f, -2.22f, 0.1f), new Vector3(0.36f, 0.32f, 0.28f), 0f);
            CreateSolidModelProp("CC0 后巷工具箱", "Props/Prop_Chest.fbx", new Vector3(6.48f, -1.2f, 0.1f), new Vector3(0.48f, 0.32f, 0.28f), 90f);
            CreateMeshBoxProp("后巷雨棚阴影", new Vector3(5.68f, -1.92f, 0.28f), new Vector3(1.55f, 0.08f, 0.1f), new Color(0.1f, 0.035f, 0.03f, 1f));
        }

        private void CreateOrganicRouteLanguage()
        {
            Color routeShadow = new Color(0.045f, 0.052f, 0.052f, 1f);
            Color routeEdge = new Color(0.32f, 0.39f, 0.39f, 1f);
            Color yellow = new Color(0.9f, 0.68f, 0.1f, 1f);
            Color blue = new Color(0.08f, 0.58f, 0.8f, 1f);

            Vector3[] nodes =
            {
                new Vector3(-7.18f, 3.92f, -0.06f),
                new Vector3(-4.22f, 2.42f, -0.06f),
                new Vector3(-0.72f, 0.78f, -0.06f),
                new Vector3(3.22f, 1.2f, -0.06f),
                new Vector3(6.98f, 3.78f, -0.06f),
                new Vector3(-6.92f, -3.78f, -0.06f),
                new Vector3(-2.48f, -2.18f, -0.06f),
                new Vector3(2.28f, -2.42f, -0.06f),
                new Vector3(6.88f, -3.58f, -0.06f)
            };

            for (int i = 0; i < nodes.Length; i++)
            {
                Vector3 node = nodes[i];
                CreateShapeProp("非直角动线 弯角缓冲区 " + i, softCircleSprite, node, new Vector3(i % 2 == 0 ? 1.18f : 0.92f, i % 2 == 0 ? 0.68f : 0.56f, 0.06f), routeShadow);
                CreateMeshBoxProp("非直角动线 弯角导向灯 " + i, node + new Vector3(0f, 0.28f, 0.08f), new Vector3(0.54f, 0.035f, 0.05f), i % 2 == 0 ? yellow : blue, i % 3 == 0 ? -12f : 10f);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -7.2f + i * 2.05f;
                CreateRotatedProp("非直角动线 主廊错位地砖 " + i, new Vector3(x, i % 2 == 0 ? -0.52f : 0.18f, -0.05f), new Vector3(0.62f, 0.18f, 0.05f), routeEdge, i % 2 == 0 ? -9f : 8f);
            }

            CreateMeshBoxProp("非直角动线 夜市蛇形标线 A", new Vector3(-2.1f, 2.28f, 0.04f), new Vector3(1.18f, 0.04f, 0.05f), yellow, -18f);
            CreateMeshBoxProp("非直角动线 夜市蛇形标线 B", new Vector3(-0.72f, 2.9f, 0.04f), new Vector3(1.08f, 0.04f, 0.05f), blue, 16f);
            CreateMeshBoxProp("非直角动线 后巷急弯灯带", new Vector3(5.7f, -2.72f, 0.04f), new Vector3(1.32f, 0.04f, 0.05f), yellow, -14f);
            CreateMeshBoxProp("非直角动线 证物库转角冷光", new Vector3(-7.25f, -4.28f, 0.04f), new Vector3(1.02f, 0.04f, 0.05f), blue, 18f);
        }

        private void CreatePremiumTaskSetPieces()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                Vector3 position = new Vector3(task.Position.x / DesignScaleX, task.Position.y / DesignScaleY, 0f);
                CreatePremiumTaskSetPiece(task.Id, task.Name, position);
            }
        }

        private void CreatePremiumTaskSetPiece(int taskId, string taskName, Vector3 position)
        {
            Color accent = TaskPanelAccent(taskId);
            Color dark = new Color(0.035f, 0.045f, 0.05f, 1f);
            Color metal = new Color(0.14f, 0.16f, 0.16f, 1f);
            Color warning = new Color(0.92f, 0.7f, 0.08f, 1f);
            int mode = TaskTemplateMode(taskId);

            CreateShapeProp("成熟任务站 " + taskName + " 地面工作区", roundedRectSprite, position + new Vector3(0f, 0f, -0.045f), new Vector3(0.98f, 0.58f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.16f));
            CreateMeshBoxProp("成熟任务站 " + taskName + " 立体背板", position + new Vector3(0f, 0.34f, 0.44f), new Vector3(0.82f, 0.08f, 0.48f), Darken(accent, 0.36f));
            CreateMeshBoxProp("成熟任务站 " + taskName + " 主操作台", position + new Vector3(0f, -0.02f, 0.26f), new Vector3(0.72f, 0.36f, 0.28f), dark);
            CreateMeshBoxProp("成熟任务站 " + taskName + " 状态屏", position + new Vector3(0f, 0.24f, 0.62f), new Vector3(0.46f, 0.04f, 0.18f), accent);
            CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 警示灯", PrimitiveType.Cylinder, position + new Vector3(0.42f, -0.18f, 0.54f), new Vector3(0.08f, 0.08f, 0.1f), warning, Quaternion.Euler(90f, 0f, 0f));

            if (mode == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    CreateMeshBoxProp("成熟任务站 " + taskName + " 多屏矩阵 " + i, position + new Vector3(-0.28f + i * 0.28f, 0.36f, 0.76f), new Vector3(0.2f, 0.035f, 0.12f), new Color(0.04f, 0.74f, 0.86f, 1f));
                }
            }
            else if (mode == 1)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 封条闸门左", position + new Vector3(-0.36f, 0.02f, 0.46f), new Vector3(0.08f, 0.52f, 0.34f), metal);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 封条闸门右", position + new Vector3(0.36f, 0.02f, 0.46f), new Vector3(0.08f, 0.52f, 0.34f), metal);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 黄色封条", position + new Vector3(0f, -0.26f, 0.58f), new Vector3(0.76f, 0.04f, 0.06f), warning);
            }
            else if (mode == 2)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 高压闸刀", position + new Vector3(0.2f, 0.04f, 0.7f), new Vector3(0.08f, 0.5f, 0.08f), new Color(0.86f, 0.12f, 0.08f, 1f), -18f);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 电缆线束 A", position + new Vector3(-0.22f, -0.18f, 0.48f), new Vector3(0.42f, 0.04f, 0.05f), metal, 12f);
                CreateMeshBoxProp("成熟任务站 " + taskName + " 电缆线束 B", position + new Vector3(-0.18f, 0.1f, 0.5f), new Vector3(0.36f, 0.04f, 0.05f), metal, -10f);
            }
            else if (mode == 3)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 证物托盘", position + new Vector3(0f, -0.1f, 0.48f), new Vector3(0.52f, 0.24f, 0.08f), new Color(0.82f, 0.84f, 0.76f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 扫描光带", position + new Vector3(0f, 0.08f, 0.68f), new Vector3(0.52f, 0.035f, 0.08f), new Color(0.4f, 0.24f, 0.86f, 1f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 样本管", PrimitiveType.Cylinder, position + new Vector3(0.26f, -0.18f, 0.62f), new Vector3(0.05f, 0.05f, 0.16f), accent, Quaternion.identity);
            }
            else if (mode == 4)
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 账本抽屉", position + new Vector3(-0.22f, -0.18f, 0.5f), new Vector3(0.28f, 0.16f, 0.08f), new Color(0.46f, 0.34f, 0.16f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 现金捆", position + new Vector3(0.2f, -0.18f, 0.5f), new Vector3(0.22f, 0.14f, 0.08f), new Color(0.16f, 0.5f, 0.22f, 1f));
                CreateMeshBoxProp("成熟任务站 " + taskName + " 冻结蓝屏", position + new Vector3(0f, 0.36f, 0.82f), new Vector3(0.58f, 0.035f, 0.08f), new Color(0.08f, 0.46f, 0.88f, 1f));
            }
            else
            {
                CreateMeshBoxProp("成熟任务站 " + taskName + " 路线板", position + new Vector3(0f, 0.28f, 0.74f), new Vector3(0.56f, 0.04f, 0.24f), new Color(0.78f, 0.72f, 0.54f, 1f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 路线红点", PrimitiveType.Cylinder, position + new Vector3(-0.16f, 0.32f, 0.86f), new Vector3(0.05f, 0.05f, 0.04f), new Color(0.88f, 0.1f, 0.06f, 1f), Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPrimitiveProp("成熟任务站 " + taskName + " 路线蓝点", PrimitiveType.Cylinder, position + new Vector3(0.18f, 0.22f, 0.86f), new Vector3(0.05f, 0.05f, 0.04f), new Color(0.08f, 0.32f, 0.9f, 1f), Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private void CreateMatureDockyardSetPieces()
        {
            CreateMatureAssetCluster("北货柜泊位", new Vector3(-9.42f, 5.16f, 0f), 0f, 0);
            CreateMatureAssetCluster("西侧水警泊位", new Vector3(-9.78f, 1.6f, 0f), -90f, 1);
            CreateMatureAssetCluster("夜市后勤口", new Vector3(-3.68f, 2.94f, 0f), 8f, 2);
            CreateMatureAssetCluster("金融楼卸货口", new Vector3(4.98f, 3.12f, 0f), -6f, 3);
            CreateMatureAssetCluster("电房维修坪", new Vector3(8.42f, 5.58f, 0f), 2f, 4);
            CreateMatureAssetCluster("后巷诊所口", new Vector3(6.08f, -3.82f, 0f), -12f, 5);
            CreateMatureAssetCluster("证物库外场", new Vector3(-8.34f, -4.86f, 0f), 5f, 6);
            CreateMatureAssetCluster("指挥车警戒线", new Vector3(0.36f, -5.16f, 0f), 0f, 7);

            string[] railModels =
            {
                "Props/Prop_Rail_2.fbx",
                "Props/Prop_Rail_3.fbx",
                "Props/Prop_Rail_4.fbx",
                "Props/Prop_Rail_Round_Small.fbx"
            };

            Vector3[] railLine =
            {
                new Vector3(-7.4f, 4.22f, 0f),
                new Vector3(-5.54f, 3.18f, 0f),
                new Vector3(-2.22f, 1.86f, 0f),
                new Vector3(1.08f, 1.38f, 0f),
                new Vector3(4.78f, 2.32f, 0f),
                new Vector3(7.4f, 3.96f, 0f),
                new Vector3(6.38f, -2.92f, 0f),
                new Vector3(2.42f, -3.12f, 0f),
                new Vector3(-2.26f, -3.0f, 0f),
                new Vector3(-6.72f, -3.72f, 0f)
            };

            for (int i = 0; i < railLine.Length; i++)
            {
                float rotation = i % 2 == 0 ? 18f : -14f;
                CreateModelProp("成熟港区设施 免费护栏动线 " + i, railModels[i % railModels.Length], railLine[i], new Vector3(0.58f, 0.22f, 0.2f), rotation, true);
                CreateModelProp("成熟港区设施 免费地面箭头标识 " + i, i % 3 == 0 ? "Decals/Decal_Line_Bend1_R.fbx" : "Decals/Decal_Line_Straight.fbx", railLine[i] + new Vector3(0.0f, -0.28f, -0.02f), new Vector3(0.48f, 0.22f, 0.04f), rotation, true);
            }

            ShipRoomSpec[] rooms = ShipRooms();

            for (int i = 0; i < rooms.Length; i++)
            {
                ShipRoomSpec room = rooms[i];
                float halfWidth = room.Size.x * 0.5f;
                Vector3 left = room.Center + new Vector3(-halfWidth + 0.48f, 0.12f, 0.16f);
                Vector3 right = room.Center + new Vector3(halfWidth - 0.48f, -0.18f, 0.16f);
                CreateModelProp("成熟港区设施 房间免费通风机 " + room.Name, "Props/Prop_Vent_Wide.fbx", left, new Vector3(0.52f, 0.18f, 0.18f), i % 2 == 0 ? 0f : 180f, true);
                CreateModelProp("成熟港区设施 房间免费照明灯 " + room.Name, i % 2 == 0 ? "Props/Prop_Light_Wide.fbx" : "Props/Prop_Light_Small.fbx", right, new Vector3(0.46f, 0.18f, 0.16f), i % 2 == 0 ? 180f : 0f, true);
            }

            CreateMatureDockyardVehicleAndStreetLayer();
            CreateMatureDockyardCrowdScaleProps();
        }

        private void CreateMatureDockyardVehicleAndStreetLayer()
        {
            Color policeBlue = new Color(0.08f, 0.24f, 0.78f, 1f);
            Color policeRed = new Color(0.86f, 0.08f, 0.06f, 1f);
            Color taxiRed = new Color(0.7f, 0.08f, 0.06f, 1f);
            Color taxiWhite = new Color(0.86f, 0.86f, 0.78f, 1f);
            Color van = new Color(0.1f, 0.16f, 0.18f, 1f);

            CreateVehicleSetPiece("成熟港区设施 警用冲锋车", new Vector3(-0.15f, -5.38f, 0.1f), new Vector3(1.55f, 0.72f, 0.42f), van, policeBlue, 0f);
            CreateVehicleSetPiece("成熟港区设施 茶餐厅红的士", new Vector3(-4.15f, 0.78f, 0.1f), new Vector3(1.25f, 0.54f, 0.34f), taxiRed, taxiWhite, 8f);
            CreateVehicleSetPiece("成熟港区设施 后巷黑色面包车", new Vector3(6.62f, -2.32f, 0.1f), new Vector3(1.38f, 0.58f, 0.38f), new Color(0.035f, 0.04f, 0.045f, 1f), new Color(0.38f, 0.46f, 0.48f, 1f), -10f);

            CreateMeshBoxProp("成熟港区设施 警车顶灯红", new Vector3(-0.55f, -4.86f, 0.52f), new Vector3(0.24f, 0.05f, 0.07f), policeRed);
            CreateMeshBoxProp("成熟港区设施 警车顶灯蓝", new Vector3(0.45f, -4.86f, 0.52f), new Vector3(0.24f, 0.05f, 0.07f), policeBlue);

            Vector3[] roadblockPositions =
            {
                new Vector3(-3.25f, -3.64f, 0.1f),
                new Vector3(-2.62f, -3.78f, 0.1f),
                new Vector3(1.82f, -4.18f, 0.1f),
                new Vector3(2.48f, -4.02f, 0.1f),
                new Vector3(4.18f, 1.34f, 0.1f),
                new Vector3(4.8f, 1.12f, 0.1f),
                new Vector3(-7.38f, 4.28f, 0.1f),
                new Vector3(-6.78f, 4.08f, 0.1f)
            };

            for (int i = 0; i < roadblockPositions.Length; i++)
            {
                Vector3 position = roadblockPositions[i];
                CreateSolidMeshBoxProp("成熟港区设施 可碰撞水马路障 " + i, position, new Vector3(0.46f, 0.12f, 0.22f), i % 2 == 0 ? policeBlue : policeRed, i % 2 == 0 ? -12f : 14f);
                CreateMeshBoxProp("成熟港区设施 水马反光白条 " + i, position + new Vector3(0f, 0.02f, 0.18f), new Vector3(0.34f, 0.035f, 0.04f), new Color(0.86f, 0.86f, 0.78f, 1f), i % 2 == 0 ? -12f : 14f);
            }
        }

        private void CreateVehicleSetPiece(string name, Vector3 position, Vector3 size, Color body, Color stripe, float rotationDegrees)
        {
            CreateSolidMeshBoxProp(name + " 车身", position + new Vector3(0f, 0f, 0.18f), size, body, rotationDegrees);
            CreateMeshBoxProp(name + " 前挡风玻璃", position + new Vector3(size.x * 0.18f, size.y * 0.28f, 0.48f), new Vector3(size.x * 0.24f, 0.04f, 0.1f), new Color(0.12f, 0.42f, 0.52f, 1f), rotationDegrees);
            CreateMeshBoxProp(name + " 侧面识别条", position + new Vector3(0f, -size.y * 0.28f, 0.42f), new Vector3(size.x * 0.72f, 0.035f, 0.06f), stripe, rotationDegrees);

            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -size.x * 0.32f : size.x * 0.32f;
                float y = i % 2 == 0 ? -size.y * 0.36f : size.y * 0.36f;
                CreateMeshPrimitiveProp(name + " 轮胎 " + i, PrimitiveType.Cylinder, position + new Vector3(x, y, 0.14f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.015f, 0.015f, 0.018f, 1f), Quaternion.Euler(90f, 0f, rotationDegrees));
            }
        }

        private void CreateMatureDockyardCrowdScaleProps()
        {
            Color cone = new Color(0.9f, 0.34f, 0.08f, 1f);
            Color white = new Color(0.86f, 0.86f, 0.78f, 1f);
            Color sign = new Color(0.92f, 0.72f, 0.08f, 1f);

            for (int i = 0; i < 24; i++)
            {
                float band = i % 6;
                float row = i / 6;
                Vector3 position = new Vector3(-5.9f + band * 2.25f + (row % 2) * 0.38f, -6.25f + row * 0.62f, 0.08f);
                CreateMeshPrimitiveProp("成熟港区设施 路锥阵列 " + i, PrimitiveType.Cylinder, position, new Vector3(0.1f, 0.08f, 0.12f), cone, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshBoxProp("成熟港区设施 路锥白条 " + i, position + new Vector3(0f, 0f, 0.11f), new Vector3(0.11f, 0.035f, 0.025f), white);
            }

            Vector3[] signPositions =
            {
                new Vector3(-8.88f, 3.86f, 0.32f),
                new Vector3(-3.28f, 2.26f, 0.32f),
                new Vector3(2.68f, -3.36f, 0.32f),
                new Vector3(7.72f, 3.78f, 0.32f),
                new Vector3(6.34f, -4.12f, 0.32f)
            };

            for (int i = 0; i < signPositions.Length; i++)
            {
                CreateMeshBoxProp("成熟港区设施 港区警示立牌 " + i, signPositions[i], new Vector3(0.46f, 0.055f, 0.34f), sign, i % 2 == 0 ? 8f : -8f);
                CreateMeshBoxProp("成熟港区设施 警示立牌黑条 " + i, signPositions[i] + new Vector3(0f, 0.04f, 0.1f), new Vector3(0.32f, 0.025f, 0.04f), new Color(0.04f, 0.04f, 0.04f, 1f), i % 2 == 0 ? 8f : -8f);
            }
        }

        private void CreateMatureAssetCluster(string clusterName, Vector3 center, float rotation, int variant)
        {
            string[] bulkyProps =
            {
                "Props/Prop_Crate3.fbx",
                "Props/Prop_Crate4.fbx",
                "Props/Prop_Chest.fbx",
                "Props/Prop_Barrel_Large.fbx"
            };

            string[] utilityProps =
            {
                "Props/Prop_AccessPoint.fbx",
                "Props/Prop_Computer.fbx",
                "Props/Prop_ItemHolder.fbx",
                "Props/Prop_Cable_1.fbx",
                "Props/Prop_Cable_3.fbx",
                "Props/Prop_Vent_Big.fbx",
                "Props/Prop_Light_Floor.fbx",
                "Props/Prop_PipeHolder.fbx"
            };

            string[] platformProps =
            {
                "Platforms/Platform_Metal2.fbx",
                "Platforms/Platform_DarkPlates.fbx",
                "Platforms/Platform_3Plates.fbx",
                "Platforms/Platform_Rails_4Wide.fbx",
                "Platforms/Platform_Stairs_2.fbx",
                "Platforms/Door_Frame_A.fbx"
            };

            Vector3[] offsets =
            {
                new Vector3(-0.72f, 0.22f, 0.08f),
                new Vector3(-0.28f, -0.22f, 0.08f),
                new Vector3(0.34f, 0.22f, 0.08f),
                new Vector3(0.76f, -0.16f, 0.08f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 offset = RotateOffset(offsets[i], rotation);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费货物组 " + i, bulkyProps[(variant + i) % bulkyProps.Length], center + offset, new Vector3(0.48f, 0.34f, 0.32f), rotation + i * 11f, false);
            }

            for (int i = 0; i < utilityProps.Length; i++)
            {
                float angle = rotation + i * 31f;
                Vector3 ring = RotateOffset(new Vector3(Mathf.Cos(i * 0.72f) * 1.02f, Mathf.Sin(i * 0.72f) * 0.58f, 0.1f), rotation);
                Vector3 footprint = i % 3 == 0 ? new Vector3(0.38f, 0.24f, 0.28f) : new Vector3(0.32f, 0.2f, 0.24f);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费设备件 " + i, utilityProps[i], center + ring, footprint, angle, false);
            }

            for (int i = 0; i < platformProps.Length; i++)
            {
                Vector3 strip = RotateOffset(new Vector3(-1.12f + i * 0.45f, 0.78f, -0.02f), rotation);
                CreateModelProp("成熟港区设施 " + clusterName + " 免费平台件 " + i, platformProps[i], center + strip, new Vector3(0.52f, 0.26f, 0.14f), rotation + (i % 2 == 0 ? 0f : 180f), true);
            }

            CreateMeshBoxProp("成熟港区设施 " + clusterName + " 警戒反光地线 A", center + RotateOffset(new Vector3(0f, -0.72f, 0.04f), rotation), new Vector3(1.68f, 0.035f, 0.05f), new Color(0.92f, 0.7f, 0.08f, 1f), rotation);
            CreateMeshBoxProp("成熟港区设施 " + clusterName + " 冷光编号条 B", center + RotateOffset(new Vector3(0.42f, 0.62f, 0.06f), rotation), new Vector3(0.92f, 0.035f, 0.05f), new Color(0.08f, 0.72f, 0.86f, 1f), rotation + 8f);
            CreateShapeProp("成熟港区设施 " + clusterName + " 作业区底影", roundedRectSprite, center + new Vector3(0f, 0f, -0.055f), new Vector3(2.38f, 1.28f, 0.04f), new Color(0.02f, 0.026f, 0.028f, 0.68f));
        }

        private static Vector3 RotateOffset(Vector3 offset, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos, offset.z);
        }

        private void CreateTaskInteractionHalos()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / DesignScaleX, task.Position.y / DesignScaleY, 0f);
                Color accent = TaskPanelAccent(task.Id);
                CreateShapeProp("任务交互范围环 " + task.Name, circleSprite, designPosition + new Vector3(0f, 0f, -0.03f), new Vector3(0.72f, 0.46f, 0.04f), new Color(accent.r, accent.g, accent.b, 0.2f));
                CreateShapeProp("任务可读性 外发光底环 " + task.Name, softCircleSprite, designPosition + new Vector3(0f, 0f, 0.03f), new Vector3(0.96f, 0.62f, 0.05f), new Color(accent.r, accent.g, accent.b, 0.24f));
                CreateMeshBoxProp("任务可读性 交互键 E " + task.Name, designPosition + new Vector3(-0.36f, 0.34f, 0.52f), new Vector3(0.18f, 0.035f, 0.12f), new Color(0.94f, 0.76f, 0.12f, 1f));
                CreateMeshBoxProp("任务可读性 状态灯条 " + task.Name, designPosition + new Vector3(0.02f, 0.38f, 0.58f), new Vector3(0.52f, 0.04f, 0.08f), accent);
                CreateMeshPrimitiveProp("任务可读性 竖向信标 " + task.Name, PrimitiveType.Cylinder, designPosition + new Vector3(0.42f, 0.18f, 0.42f), new Vector3(0.04f, 0.04f, 0.58f), accent, Quaternion.identity);
                CreateMeshBoxProp("任务可读性 信标顶灯 " + task.Name, designPosition + new Vector3(0.42f, 0.18f, 0.74f), new Vector3(0.16f, 0.035f, 0.08f), new Color(0.96f, 0.92f, 0.42f, 1f));

                if (task.Id % 3 == 0)
                {
                    CreateModelProp("CC0 任务备用小终端 " + task.Name, "Props/Prop_Computer.fbx", designPosition + new Vector3(0.36f, -0.22f, 0.08f), new Vector3(0.32f, 0.24f, 0.24f), 180f);
                }
                else if (task.Id % 3 == 1)
                {
                    CreateModelProp("CC0 任务工具架 " + task.Name, "Props/Prop_ItemHolder.fbx", designPosition + new Vector3(-0.36f, 0.2f, 0.08f), new Vector3(0.28f, 0.22f, 0.24f), 0f);
                }
                else
                {
                    CreateModelProp("CC0 任务线缆夹 " + task.Name, "Props/Prop_Clamp.fbx", designPosition + new Vector3(0.32f, 0.18f, 0.08f), new Vector3(0.24f, 0.2f, 0.22f), 90f);
                }
            }
        }

        private void CreateEmergencyMeetingTableSet()
        {
            Color table = new Color(0.24f, 0.28f, 0.28f, 1f);
            Color seat = new Color(0.08f, 0.12f, 0.14f, 1f);
            CreateMeshPrimitiveProp("会议桌低矮圆台", PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0.08f), new Vector3(0.74f, 0.035f, 0.74f), table, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("会议桌证据投影线 A", new Vector3(0f, -0.12f, 0.28f), new Vector3(0.82f, 0.035f, 0.04f), new Color(0.05f, 0.72f, 0.86f, 1f));
            CreateMeshBoxProp("会议桌证据投影线 B", new Vector3(0.18f, -0.56f, 0.28f), new Vector3(0.46f, 0.035f, 0.04f), new Color(0.95f, 0.22f, 0.18f, 1f));

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * 1.18f, -0.35f + Mathf.Sin(angle) * 0.78f, 0.09f);
                CreateMeshPrimitiveProp("会议玩家座位 " + i, PrimitiveType.Cylinder, position, new Vector3(0.16f, 0.025f, 0.16f), seat, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        private void CreatePhysicsCollisionMarkers()
        {
            Color bumper = new Color(0.04f, 0.05f, 0.052f, 1f);
            Color stripe = new Color(0.92f, 0.72f, 0.08f, 1f);

            Vector3[] positions =
            {
                new Vector3(-7.65f, 0.92f, 0.08f),
                new Vector3(7.86f, -0.72f, 0.08f),
                new Vector3(-4.1f, -4.46f, 0.08f),
                new Vector3(4.35f, 4.14f, 0.08f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 position = positions[i];
                CreateSolidProp("实体碰撞防撞墩 " + i, position, new Vector3(0.22f, 0.42f, 0.2f), bumper);
                CreateMeshBoxProp("防撞墩反光贴 " + i, position + new Vector3(0f, 0.18f, 0.2f), new Vector3(0.18f, 0.035f, 0.05f), stripe);
            }
        }

        private void CreateLargeScalePortSetPieces()
        {
            CreateExteriorDockVista();
            CreateDistrictIdentityLandmarks();
            CreateShipLikeSightlineWalls();
            CreateLargeHongKongPortBackdrop();
            CreateLargeDistrictDepthSilhouettes();
            CreateLargePlayableSightlineSetPieces();
            CreateRoundEndShowcaseSet();
        }

        private void CreateLargeRoomReadabilityLayer()
        {
            Color outerWall = new Color(0.018f, 0.026f, 0.03f, 1f);
            Color innerWall = new Color(0.05f, 0.064f, 0.07f, 1f);
            Color shadow = new Color(0.004f, 0.006f, 0.008f, 0.62f);
            Color glass = new Color(0.08f, 0.38f, 0.46f, 1f);

            foreach (ShipRoomSpec room in ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                float height = RoomVisualHeight(room) + 0.24f;
                Color light = DoorColor(room);

                CreateMeshBoxProp("大场景港区层 房间高外壳北 " + room.Name, room.Center + new Vector3(0f, halfHeight + 0.2f, height * 0.56f), new Vector3(room.Size.x + 0.52f, 0.18f, height), outerWall);
                CreateMeshBoxProp("大场景港区层 房间高外壳西 " + room.Name, room.Center + new Vector3(-halfWidth - 0.18f, 0f, height * 0.46f), new Vector3(0.18f, room.Size.y + 0.26f, height * 0.82f), innerWall);
                CreateMeshBoxProp("大场景港区层 房间高外壳东 " + room.Name, room.Center + new Vector3(halfWidth + 0.18f, 0f, height * 0.46f), new Vector3(0.18f, room.Size.y + 0.26f, height * 0.82f), innerWall);
                CreateMeshBoxProp("前景遮挡层 房间前檐阴影 " + room.Name, room.Center + new Vector3(0f, -halfHeight - 0.12f, 0.86f), new Vector3(room.Size.x * 0.82f, 0.18f, 0.44f), shadow);
                CreateMeshBoxProp("屋顶 房间厚檐发光边 " + room.Name, room.Center + new Vector3(0f, halfHeight + 0.32f, height + 0.1f), new Vector3(room.Size.x * 0.62f, 0.05f, 0.08f), light);

                for (int i = 0; i < 3; i++)
                {
                    float x = -room.Size.x * 0.28f + i * room.Size.x * 0.28f;
                    CreateMeshBoxProp("大场景港区层 房间远窗 " + room.Name + " " + i, room.Center + new Vector3(x, halfHeight + 0.31f, height * 0.62f), new Vector3(0.26f, 0.035f, 0.16f), glass);
                }

                CreateRoomPortalKit(room);
            }

            CreateCurvedCorridorReadability();
            CreatePlayableSightlineBlockers();
        }

        private void CreateRoomPortalKit(ShipRoomSpec room)
        {
            Vector3 door = DoorLightPosition(room);
            Vector3 doorScale = DoorLightScale(room);
            Color light = DoorColor(room);
            bool horizontal = room.Entrance == MapEntrance.North || room.Entrance == MapEntrance.South;
            float rotation = horizontal ? 0f : 90f;
            Vector3 frameSize = horizontal
                ? new Vector3(Mathf.Max(0.7f, doorScale.x + 0.3f), 0.34f, 0.6f)
                : new Vector3(0.34f, Mathf.Max(0.7f, doorScale.y + 0.3f), 0.6f);
            Vector3 offset = Vector3.zero;

            switch (room.Entrance)
            {
                case MapEntrance.North:
                    offset = new Vector3(0f, 0.28f, 0.1f);
                    break;
                case MapEntrance.South:
                    offset = new Vector3(0f, -0.28f, 0.1f);
                    break;
                case MapEntrance.East:
                    offset = new Vector3(0.28f, 0f, 0.1f);
                    break;
                case MapEntrance.West:
                    offset = new Vector3(-0.28f, 0f, 0.1f);
                    break;
            }

            Vector3 portalCenter = door + offset;
            CreateModelProp("大场景港区层 房间门框模型 " + room.Name, "Platforms/Door_Frame_SquareTall.fbx", portalCenter, frameSize, rotation, true);
            CreateMeshBoxProp("大场景港区层 房间门楣灯 " + room.Name, portalCenter + new Vector3(0f, 0f, 0.34f), horizontal ? new Vector3(frameSize.x * 0.58f, 0.04f, 0.08f) : new Vector3(0.04f, frameSize.y * 0.58f, 0.08f), light, rotation);
            CreateMeshBoxProp("前景遮挡层 门口短阴影 " + room.Name, portalCenter + new Vector3(0f, -0.08f, 0.46f), horizontal ? new Vector3(frameSize.x * 0.76f, 0.1f, 0.22f) : new Vector3(0.1f, frameSize.y * 0.76f, 0.22f), new Color(0f, 0f, 0f, 0.46f), rotation);
        }

        private void CreateCurvedCorridorReadability()
        {
            Color routeBlue = new Color(0.08f, 0.55f, 0.72f, 1f);
            Color routeAmber = new Color(0.95f, 0.68f, 0.1f, 1f);
            Color floorDark = new Color(0.045f, 0.056f, 0.06f, 1f);

            Vector3[] routeCenters =
            {
                new Vector3(-6.4f, 3.72f, 0.04f),
                new Vector3(-3.9f, 3.08f, 0.04f),
                new Vector3(-1.25f, 1.55f, 0.04f),
                new Vector3(1.82f, 1.42f, 0.04f),
                new Vector3(4.4f, 0.26f, 0.04f),
                new Vector3(6.62f, -1.92f, 0.04f),
                new Vector3(3.28f, -3.42f, 0.04f),
                new Vector3(-0.22f, -3.28f, 0.04f),
                new Vector3(-4.55f, -3.55f, 0.04f),
                new Vector3(-7.2f, -1.72f, 0.04f)
            };

            for (int i = 0; i < routeCenters.Length; i++)
            {
                float rotation = i % 2 == 0 ? -16f : 18f;
                CreateMeshBoxProp("非直角动线 主路弧形地板块 " + i, routeCenters[i], new Vector3(1.28f, 0.18f, 0.05f), floorDark, rotation);
                CreateModelProp("非直角动线 免费弯线地贴 " + i, i % 2 == 0 ? "Decals/Decal_Line_Bend1_R.fbx" : "Decals/Decal_Line_Bend2_L.fbx", routeCenters[i] + new Vector3(0f, 0f, 0.03f), new Vector3(0.72f, 0.36f, 0.04f), rotation, true);
                CreateMeshBoxProp("非直角动线 巡逻导光条 " + i, routeCenters[i] + new Vector3(0f, 0.16f, 0.08f), new Vector3(0.82f, 0.035f, 0.04f), i % 2 == 0 ? routeBlue : routeAmber, rotation);
            }
        }

        private void CreatePlayableSightlineBlockers()
        {
            Color darkMetal = new Color(0.025f, 0.032f, 0.035f, 1f);
            Color cable = new Color(0.08f, 0.09f, 0.09f, 1f);
            Color policeLight = new Color(0.08f, 0.38f, 0.95f, 1f);
            Color gangLight = new Color(0.9f, 0.12f, 0.08f, 1f);

            (Vector3 center, Vector3 size, float rotation)[] blockers =
            {
                (new Vector3(-5.92f, 0.55f, 0.2f), new Vector3(1.05f, 0.18f, 0.5f), -12f),
                (new Vector3(-3.05f, -1.18f, 0.2f), new Vector3(0.18f, 1.05f, 0.5f), 10f),
                (new Vector3(2.68f, -1.02f, 0.2f), new Vector3(1.1f, 0.18f, 0.5f), 12f),
                (new Vector3(5.02f, 1.02f, 0.2f), new Vector3(0.18f, 1.02f, 0.5f), -10f),
                (new Vector3(-7.82f, 3.18f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), 18f),
                (new Vector3(7.58f, 3.32f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), -18f),
                (new Vector3(-5.98f, -4.72f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), -8f),
                (new Vector3(3.68f, -4.72f, 0.2f), new Vector3(0.92f, 0.18f, 0.46f), 8f)
            };

            for (int i = 0; i < blockers.Length; i++)
            {
                CreateSolidMeshBoxProp("大场景港区层 可玩视线阻挡墙 " + i, blockers[i].center, blockers[i].size, darkMetal, blockers[i].rotation);
                CreateMeshBoxProp("大场景港区层 阻挡墙电缆 " + i, blockers[i].center + new Vector3(0f, 0f, 0.32f), new Vector3(blockers[i].size.x * 0.64f, 0.035f, 0.06f), cable, blockers[i].rotation);
                CreateMeshBoxProp("大场景港区层 阻挡墙警匪状态灯 " + i, blockers[i].center + new Vector3(0f, 0.11f, 0.42f), new Vector3(Mathf.Max(0.16f, blockers[i].size.x * 0.38f), 0.035f, 0.05f), i % 2 == 0 ? policeLight : gangLight, blockers[i].rotation);
            }
        }

        private void CreateOfficialFreeAssetStoreLayer()
        {
            CreateOfficialFreeRoadTiles();
            CreateOfficialFreeBuildingShells();
            CreateOfficialFreeStreetFurniture();
            CreateOfficialFreeVehicleSetPieces();
            CreateOfficialFreeCrowdAndTaskDressing();
            CreateDenseOfficialFreeStreetLayer();
        }

        private void CreateOfficialFreeRoadTiles()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation)[] roadTiles =
            {
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Crossroads_1", new Vector3(0f, -0.08f, -0.22f), new Vector3(1.55f, 1.15f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-3.85f, 0.08f, -0.22f), new Vector3(3.1f, 0.48f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(4.15f, -0.15f, -0.22f), new Vector3(3.25f, 0.48f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-6.95f, 3.78f, -0.22f), new Vector3(3.6f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(5.15f, 3.98f, -0.22f), new Vector3(3.95f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-6.25f, -3.72f, -0.22f), new Vector3(3.7f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(4.9f, -3.58f, -0.22f), new Vector3(3.6f, 0.46f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(-7.18f, 1.45f, -0.22f), new Vector3(2.65f, 0.46f, 0.06f), 90f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_10m", new Vector3(7.12f, 1.18f, -0.22f), new Vector3(2.85f, 0.46f, 0.06f), 90f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_turn", new Vector3(8.82f, 4.18f, -0.21f), new Vector3(0.92f, 0.82f, 0.06f), 0f),
                (AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_turn", new Vector3(-7.0f, -4.42f, -0.21f), new Vector3(0.82f, 0.92f, 0.06f), 180f)
            };

            for (int i = 0; i < roadTiles.Length; i++)
            {
                GameObject tile = CreateAssetStoreProp("官方免费素材层 模块化道路 " + i, roadTiles[i].path, roadTiles[i].position, roadTiles[i].footprint, roadTiles[i].rotation, true);

                if (tile != null)
                {
                    tile.transform.SetAsFirstSibling();
                }
            }

            for (int i = 0; i < 10; i++)
            {
                float x = -8.5f + i * 1.9f;
                CreateAssetStoreProp("官方免费素材层 道路标记 " + i, AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Road_1_line", new Vector3(x, i % 2 == 0 ? 0.44f : -0.58f, -0.18f), new Vector3(0.42f, 0.16f, 0.04f), i % 2 == 0 ? 0f : 12f, false);
            }
        }

        private void CreateOfficialFreeBuildingShells()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation, bool solid)[] buildings =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Factory", new Vector3(-10.75f, 6.72f, 0.04f), new Vector3(1.35f, 0.78f, 0.9f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_big_color01", new Vector3(4.75f, 4.38f, 0.04f), new Vector3(1.22f, 0.78f, 1.28f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Coffee Shop", new Vector3(-4.82f, 2.68f, 0.04f), new Vector3(1.0f, 0.64f, 0.66f), 0f, false),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Restaurant", new Vector3(-0.9f, 3.95f, 0.04f), new Vector3(1.25f, 0.7f, 0.72f), 0f, false),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Drug Store", new Vector3(6.85f, -4.18f, 0.04f), new Vector3(1.0f, 0.64f, 0.74f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Auto Service", new Vector3(6.15f, -2.62f, 0.04f), new Vector3(1.1f, 0.7f, 0.72f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_small_color02", new Vector3(8.82f, 2.72f, 0.04f), new Vector3(0.92f, 0.62f, 1.0f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Super Market", new Vector3(-8.92f, -4.0f, 0.04f), new Vector3(1.18f, 0.72f, 0.76f), 0f, true)
            };

            for (int i = 0; i < buildings.Length; i++)
            {
                string name = "官方免费素材层 城市建筑壳 " + i;

                if (buildings[i].solid)
                {
                    CreateSolidAssetStoreProp(name, buildings[i].path, buildings[i].position, buildings[i].footprint, buildings[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp(name, buildings[i].path, buildings[i].position, buildings[i].footprint, buildings[i].rotation, false);
                }
            }

            string[] syntyBuildings =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_04",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_07",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Building/SM_Gen_Bld_Background_10"
            };

            for (int i = 0; i < syntyBuildings.Length; i++)
            {
                CreateAssetStoreProp("官方免费素材层 远景楼宇补强 " + i, syntyBuildings[i], new Vector3(-10.2f + i * 6.6f, 7.02f, 0.22f), new Vector3(1.45f, 0.34f, 1.05f + i * 0.12f), 0f, false);
            }
        }

        private void CreateOfficialFreeStreetFurniture()
        {
            string[] furniture =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Bench_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Hydrant",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_cone",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Trash_can_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole_traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Control Barrier Fence",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_medium",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light"
            };

            Vector3[] positions =
            {
                new Vector3(-5.1f, 0.78f, 0.1f),
                new Vector3(-8.4f, 3.42f, 0.1f),
                new Vector3(-3.4f, -3.12f, 0.1f),
                new Vector3(2.4f, -3.18f, 0.1f),
                new Vector3(4.25f, 0.92f, 0.1f),
                new Vector3(7.88f, 3.42f, 0.1f),
                new Vector3(-1.12f, -0.82f, 0.1f),
                new Vector3(0.68f, -4.74f, 0.1f),
                new Vector3(-1.05f, 4.02f, 0.1f),
                new Vector3(8.92f, -0.86f, 0.1f),
                new Vector3(-7.7f, -4.92f, 0.1f),
                new Vector3(6.8f, -4.84f, 0.1f),
                new Vector3(-10.2f, 4.24f, 0.1f),
                new Vector3(10.15f, -3.52f, 0.1f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                string path = furniture[i % furniture.Length];
                Vector3 footprint = i % 4 == 0 ? new Vector3(0.38f, 0.24f, 0.42f) : new Vector3(0.28f, 0.2f, 0.32f);

                if (i % 3 != 1)
                {
                    CreateSolidAssetStoreProp("官方免费素材层 街道小物 " + i, path, positions[i], footprint, i * 17f, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费素材层 街道小物 " + i, path, positions[i], footprint, i * 17f, false);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                CreateAssetStoreProp("官方免费素材层 行道树 " + i, AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Tree1", new Vector3(-10.8f + i * 3.05f, i % 2 == 0 ? 6.7f : -6.62f, 0.08f), new Vector3(0.42f, 0.42f, 0.82f), 0f, false);
            }
        }

        private void CreateOfficialFreeVehicleSetPieces()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation)[] vehicles =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(0.88f, -6.0f, 0.1f), new Vector3(0.88f, 0.42f, 0.34f), 0f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(5.45f, -2.58f, 0.1f), new Vector3(0.82f, 0.4f, 0.32f), -12f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color01", new Vector3(-10.42f, 5.62f, 0.12f), new Vector3(1.05f, 0.45f, 0.42f), 4f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color02", new Vector3(-8.9f, 6.18f, 0.12f), new Vector3(1.05f, 0.45f, 0.42f), -4f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Ambulance", new Vector3(7.1f, -5.62f, 0.1f), new Vector3(0.92f, 0.42f, 0.34f), 0f),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color01", new Vector3(-2.1f, 6.82f, 0.1f), new Vector3(1.25f, 0.48f, 0.38f), 0f)
            };

            for (int i = 0; i < vehicles.Length; i++)
            {
                CreateSolidAssetStoreProp("官方免费素材层 车辆道具 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
            }
        }

        private void CreateOfficialFreeCrowdAndTaskDressing()
        {
            string[] crowd =
            {
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Female_01",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Town_Female_01"
            };

            Vector3[] crowdPositions =
            {
                new Vector3(-5.55f, 4.54f, 0.12f),
                new Vector3(-4.22f, 1.26f, 0.12f),
                new Vector3(-1.88f, 3.55f, 0.12f),
                new Vector3(4.1f, 2.02f, 0.12f),
                new Vector3(5.82f, -0.82f, 0.12f),
                new Vector3(-8.25f, -4.52f, 0.12f)
            };

            for (int i = 0; i < crowdPositions.Length; i++)
            {
                GameObject character = CreateAssetStoreProp("官方免费素材层 场景人群 " + i, crowd[i % crowd.Length], crowdPositions[i], new Vector3(0.24f, 0.24f, 0.58f), i % 2 == 0 ? 0f : 180f, false);

                if (character != null)
                {
                    character.transform.localScale *= 0.78f;
                }
            }

            string[] taskProps =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Cardboard_Box_02",
                AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/SM_PolygonPrototype_Prop_Ladder_1x2_01P",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bus Stop"
            };

            for (int i = 0; i < tasks.Count; i += 3)
            {
                OnlineTaskState task = tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / DesignScaleX, task.Position.y / DesignScaleY, 0.18f);
                CreateAssetStoreProp("官方免费素材层 任务旁实物 " + task.Id, taskProps[i % taskProps.Length], designPosition + new Vector3(0.42f, -0.28f, 0f), new Vector3(0.34f, 0.26f, 0.32f), i * 11f, false);
            }
        }

        private void CreateDenseOfficialFreeStreetLayer()
        {
            string[] shopBuildings =
            {
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Bar",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Bakery",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Chicken Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Clothing",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Fast Food",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Fruits  Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Gas Station",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Gift Shop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Music Store",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Pizza",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Residential_color01",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building Sky_big_color02"
            };

            (Vector3 position, Vector3 footprint, float rotation, bool solid)[] buildingPlacements =
            {
                (new Vector3(-11.2f, 5.48f, 0.18f), new Vector3(1.08f, 0.62f, 0.96f), 2f, true),
                (new Vector3(-9.92f, 6.86f, 0.18f), new Vector3(1.02f, 0.58f, 0.82f), -5f, true),
                (new Vector3(-5.92f, 6.72f, 0.18f), new Vector3(0.96f, 0.56f, 0.78f), 3f, true),
                (new Vector3(-4.1f, 2.94f, 0.18f), new Vector3(0.92f, 0.52f, 0.72f), -8f, false),
                (new Vector3(-2.05f, 4.2f, 0.18f), new Vector3(0.98f, 0.56f, 0.76f), 7f, false),
                (new Vector3(0.85f, 4.26f, 0.18f), new Vector3(1.05f, 0.58f, 0.8f), -6f, false),
                (new Vector3(3.52f, 3.95f, 0.18f), new Vector3(1.08f, 0.58f, 0.92f), 4f, true),
                (new Vector3(6.1f, 3.72f, 0.18f), new Vector3(1.0f, 0.56f, 0.76f), -3f, true),
                (new Vector3(8.4f, 4.02f, 0.18f), new Vector3(1.08f, 0.58f, 0.96f), 6f, true),
                (new Vector3(9.62f, 1.72f, 0.18f), new Vector3(0.96f, 0.52f, 0.84f), -7f, true),
                (new Vector3(7.55f, -2.2f, 0.18f), new Vector3(1.02f, 0.58f, 0.82f), 8f, true),
                (new Vector3(5.82f, -4.78f, 0.18f), new Vector3(1.1f, 0.62f, 0.88f), -4f, true),
                (new Vector3(1.72f, -6.28f, 0.18f), new Vector3(1.18f, 0.62f, 0.84f), 3f, true),
                (new Vector3(-3.68f, -5.98f, 0.18f), new Vector3(1.08f, 0.58f, 0.82f), -5f, true),
                (new Vector3(-7.82f, -5.92f, 0.18f), new Vector3(1.0f, 0.56f, 0.8f), 6f, true),
                (new Vector3(-10.35f, -3.64f, 0.18f), new Vector3(1.08f, 0.58f, 0.84f), -7f, true)
            };

            for (int i = 0; i < buildingPlacements.Length; i++)
            {
                string name = "官方免费街区密度层 临街铺面 " + i;

                if (buildingPlacements[i].solid)
                {
                    CreateSolidAssetStoreProp(name, shopBuildings[i % shopBuildings.Length], buildingPlacements[i].position, buildingPlacements[i].footprint, buildingPlacements[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp(name, shopBuildings[i % shopBuildings.Length], buildingPlacements[i].position, buildingPlacements[i].footprint, buildingPlacements[i].rotation, false);
                }
            }

            CreateDenseOfficialFreeRoadFurniture();
            CreateDenseOfficialFreeTransitAndVehicleProps();
            CreateDenseOfficialFreeTaskAnchors();
        }

        private void CreateDenseOfficialFreeRoadFurniture()
        {
            string[] furniture =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_cone",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole_traffic_light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Bench_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Trash_can_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Hydrant",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Pole1",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic cone",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Sign_stop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Signal_small",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_small",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Keypad_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_05",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Control Barrier Fence",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_large",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Solar Panel",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof Antenna"
            };

            Vector3[] positions =
            {
                new Vector3(-8.78f, 4.28f, 0.14f),
                new Vector3(-7.42f, 4.12f, 0.14f),
                new Vector3(-5.85f, 3.54f, 0.14f),
                new Vector3(-4.62f, 2.42f, 0.14f),
                new Vector3(-3.16f, 0.72f, 0.14f),
                new Vector3(-1.62f, 1.46f, 0.14f),
                new Vector3(0.68f, 0.86f, 0.14f),
                new Vector3(2.2f, 1.28f, 0.14f),
                new Vector3(3.92f, 0.72f, 0.14f),
                new Vector3(5.18f, 1.55f, 0.14f),
                new Vector3(6.82f, 3.42f, 0.14f),
                new Vector3(8.18f, 4.55f, 0.14f),
                new Vector3(8.42f, 2.52f, 0.14f),
                new Vector3(7.62f, 0.12f, 0.14f),
                new Vector3(6.4f, -1.62f, 0.14f),
                new Vector3(4.52f, -2.62f, 0.14f),
                new Vector3(2.6f, -3.72f, 0.14f),
                new Vector3(0.42f, -4.62f, 0.14f),
                new Vector3(-1.62f, -4.48f, 0.14f),
                new Vector3(-3.85f, -3.68f, 0.14f),
                new Vector3(-5.92f, -3.85f, 0.14f),
                new Vector3(-7.52f, -4.55f, 0.14f),
                new Vector3(-8.72f, -2.18f, 0.14f),
                new Vector3(-7.82f, -0.42f, 0.14f),
                new Vector3(-6.92f, 1.28f, 0.14f),
                new Vector3(-4.02f, 4.12f, 0.14f),
                new Vector3(-0.85f, 3.84f, 0.14f),
                new Vector3(2.88f, 3.32f, 0.14f),
                new Vector3(5.65f, 4.32f, 0.14f),
                new Vector3(9.35f, -3.95f, 0.14f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                bool solid = i % 5 == 0 || i % 7 == 0;
                Vector3 footprint = i % 4 == 0
                    ? new Vector3(0.34f, 0.24f, 0.48f)
                    : i % 4 == 1
                        ? new Vector3(0.24f, 0.2f, 0.36f)
                        : new Vector3(0.2f, 0.18f, 0.3f);

                if (solid)
                {
                    CreateSolidAssetStoreProp("官方免费街区密度层 路边物件 " + i, furniture[i % furniture.Length], positions[i], footprint, i * 13f, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费街区密度层 路边物件 " + i, furniture[i % furniture.Length], positions[i], footprint, i * 13f, false);
                }
            }
        }

        private void CreateDenseOfficialFreeTransitAndVehicleProps()
        {
            (string path, Vector3 position, Vector3 footprint, float rotation, bool solid)[] vehicles =
            {
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(-1.1f, -5.92f, 0.14f), new Vector3(0.9f, 0.42f, 0.36f), 4f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", new Vector3(1.42f, -5.78f, 0.14f), new Vector3(0.9f, 0.42f, 0.36f), -6f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(-3.05f, 3.98f, 0.14f), new Vector3(0.84f, 0.38f, 0.32f), -11f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", new Vector3(3.25f, -3.38f, 0.14f), new Vector3(0.84f, 0.38f, 0.32f), 14f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Bus_color02", new Vector3(-3.8f, 6.68f, 0.14f), new Vector3(1.2f, 0.48f, 0.4f), 0f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Truck_color01", new Vector3(-10.3f, 6.02f, 0.14f), new Vector3(1.12f, 0.46f, 0.4f), 3f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Container_color03", new Vector3(-9.45f, 4.82f, 0.14f), new Vector3(1.12f, 0.46f, 0.42f), -2f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Ambulance", new Vector3(7.78f, -5.15f, 0.14f), new Vector3(0.92f, 0.42f, 0.34f), -8f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_SUV_color02", new Vector3(8.45f, -1.88f, 0.14f), new Vector3(0.86f, 0.4f, 0.34f), 8f, true),
                (AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Pick up Truck_color02", new Vector3(5.52f, -0.62f, 0.14f), new Vector3(0.88f, 0.4f, 0.34f), -12f, true)
            };

            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i].solid)
                {
                    CreateSolidAssetStoreProp("官方免费街区密度层 交通车辆 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
                }
                else
                {
                    CreateAssetStoreProp("官方免费街区密度层 交通车辆 " + i, vehicles[i].path, vehicles[i].position, vehicles[i].footprint, vehicles[i].rotation, false);
                }
            }

            string[] pavement =
            {
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Pavement",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Complex/Road_1_line_5m",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Road_1_line",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Roads/Crossroads_1_lines_walk"
            };

            for (int i = 0; i < 14; i++)
            {
                float x = -10.2f + i * 1.58f;
                float y = i % 2 == 0 ? 6.36f : -6.12f;
                CreateAssetStoreProp("官方免费街区密度层 人行道铺面 " + i, pavement[i % pavement.Length], new Vector3(x, y, -0.2f), new Vector3(0.78f, 0.32f, 0.04f), i % 2 == 0 ? 0f : 180f, true);
            }
        }

        private void CreateDenseOfficialFreeTaskAnchors()
        {
            string[] anchors =
            {
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof prop air",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Roof_prop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bus Stop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Dustbin",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Manhole_01",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Sewer_hatch",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_medium"
            };

            for (int i = 0; i < tasks.Count; i++)
            {
                OnlineTaskState task = tasks[i];
                Vector3 designPosition = new Vector3(task.Position.x / DesignScaleX, task.Position.y / DesignScaleY, 0.22f);
                Vector3 offset = new Vector3(i % 2 == 0 ? -0.38f : 0.38f, i % 3 == 0 ? 0.3f : -0.26f, 0f);
                CreateAssetStoreProp("官方免费街区密度层 任务实体锚点 " + task.Id, anchors[i % anchors.Length], designPosition + offset, new Vector3(0.34f, 0.26f, 0.34f), i * 19f, false);
            }
        }

        private void CreateLargeHongKongPortBackdrop()
        {
            Color skylineDark = new Color(0.024f, 0.032f, 0.04f, 1f);
            Color skylineMid = new Color(0.038f, 0.052f, 0.064f, 1f);
            Color windowBlue = new Color(0.08f, 0.44f, 0.58f, 1f);
            Color windowAmber = new Color(0.86f, 0.62f, 0.18f, 1f);

            for (int i = 0; i < 14; i++)
            {
                float x = -11.4f + i * 1.75f;
                float height = 0.46f + i % 5 * 0.16f;
                CreateMeshBoxProp("大场景港区层 远景香港楼宇体 " + i, new Vector3(x, 7.36f, height * 0.5f), new Vector3(1.0f + i % 3 * 0.22f, 0.16f, height), i % 2 == 0 ? skylineDark : skylineMid);

                for (int w = 0; w < 3; w++)
                {
                    CreateMeshBoxProp("大场景港区层 远景楼宇窗格 " + i + "-" + w, new Vector3(x - 0.28f + w * 0.28f, 7.47f, 0.22f + w * 0.12f), new Vector3(0.12f, 0.026f, 0.045f), (i + w) % 2 == 0 ? windowBlue : windowAmber);
                }
            }

            for (int i = 0; i < 10; i++)
            {
                float y = -5.8f + i * 1.18f;
                CreateMeshBoxProp("大场景港区层 西侧海面码头反光 " + i, new Vector3(-12.25f, y, -0.2f), new Vector3(0.74f, 0.035f, 0.04f), new Color(0.16f, 0.36f, 0.44f, 1f));
                CreateMeshBoxProp("大场景港区层 东侧海面码头反光 " + i, new Vector3(12.25f, y + 0.5f, -0.2f), new Vector3(0.66f, 0.035f, 0.04f), new Color(0.12f, 0.32f, 0.42f, 1f));
            }

            CreateMeshBoxProp("大场景港区层 远景青马桥剪影", new Vector3(0f, 7.05f, 0.42f), new Vector3(8.6f, 0.06f, 0.08f), new Color(0.08f, 0.1f, 0.1f, 1f));
            CreateMeshBoxProp("大场景港区层 远景桥塔左", new Vector3(-3.2f, 7.12f, 0.72f), new Vector3(0.08f, 0.08f, 0.78f), new Color(0.08f, 0.1f, 0.1f, 1f));
            CreateMeshBoxProp("大场景港区层 远景桥塔右", new Vector3(3.25f, 7.12f, 0.74f), new Vector3(0.08f, 0.08f, 0.82f), new Color(0.08f, 0.1f, 0.1f, 1f));
        }

        private void CreateLargeDistrictDepthSilhouettes()
        {
            Color nearShadow = new Color(0.006f, 0.008f, 0.01f, 0.82f);
            Color metalDark = new Color(0.035f, 0.044f, 0.048f, 1f);
            Color trim = new Color(0.42f, 0.48f, 0.48f, 1f);

            Vector3[] gantries =
            {
                new Vector3(-9.5f, 6.42f, 0.86f),
                new Vector3(-5.18f, 6.24f, 0.74f),
                new Vector3(8.7f, 6.12f, 0.8f),
                new Vector3(5.85f, -4.12f, 0.72f),
                new Vector3(-8.55f, -4.12f, 0.76f)
            };

            for (int i = 0; i < gantries.Length; i++)
            {
                Vector3 center = gantries[i];
                CreateMeshBoxProp("大场景港区层 区域门架横梁 " + i, center, new Vector3(2.25f, 0.12f, 0.14f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架左柱 " + i, center + new Vector3(-1.05f, -0.18f, -0.32f), new Vector3(0.1f, 0.1f, 0.7f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架右柱 " + i, center + new Vector3(1.05f, -0.18f, -0.32f), new Vector3(0.1f, 0.1f, 0.7f), metalDark);
                CreateMeshBoxProp("大场景港区层 区域门架冷光 " + i, center + new Vector3(0f, -0.08f, 0.08f), new Vector3(1.7f, 0.035f, 0.05f), trim);
            }

            Vector3[] foregroundShadows =
            {
                new Vector3(-9.4f, 3.58f, 0.82f),
                new Vector3(-4.8f, 0.72f, 0.78f),
                new Vector3(4.75f, 1.22f, 0.8f),
                new Vector3(8.85f, 3.72f, 0.82f),
                new Vector3(0.0f, -4.1f, 0.78f),
                new Vector3(6.15f, -3.68f, 0.8f)
            };

            for (int i = 0; i < foregroundShadows.Length; i++)
            {
                CreateMeshBoxProp("大场景港区层 近景房檐投影 " + i, foregroundShadows[i], new Vector3(2.4f, 0.18f, 0.42f), nearShadow, i % 2 == 0 ? 0f : 4f);
            }
        }

        private void CreateLargePlayableSightlineSetPieces()
        {
            Color wall = new Color(0.025f, 0.034f, 0.038f, 1f);
            Color accent = new Color(0.08f, 0.68f, 0.84f, 1f);
            Color warning = new Color(0.9f, 0.68f, 0.08f, 1f);

            Vector3[] blockers =
            {
                new Vector3(-5.68f, 4.18f, 0.22f),
                new Vector3(-3.22f, 3.18f, 0.22f),
                new Vector3(2.85f, 3.12f, 0.22f),
                new Vector3(5.88f, 1.08f, 0.22f),
                new Vector3(3.48f, -2.78f, 0.22f),
                new Vector3(-4.72f, -2.82f, 0.22f),
                new Vector3(-8.18f, -1.12f, 0.22f),
                new Vector3(7.88f, -1.12f, 0.22f)
            };

            for (int i = 0; i < blockers.Length; i++)
            {
                bool horizontal = i % 2 == 0;
                Vector3 scale = horizontal ? new Vector3(1.35f, 0.16f, 0.42f) : new Vector3(0.18f, 1.08f, 0.42f);
                CreateSolidMeshBoxProp("大场景港区层 真实视线阻挡设备 " + i, blockers[i], scale, wall, i % 3 == 0 ? -8f : 8f);
                CreateMeshBoxProp("大场景港区层 阻挡设备编号灯 " + i, blockers[i] + new Vector3(0f, 0.08f, 0.28f), horizontal ? new Vector3(0.78f, 0.035f, 0.05f) : new Vector3(0.035f, 0.62f, 0.05f), i % 2 == 0 ? accent : warning, i % 3 == 0 ? -8f : 8f);
            }

            for (int i = 0; i < 8; i++)
            {
                float x = -7.4f + i * 2.1f;
                CreateMeshBoxProp("大场景港区层 可读性道路弧线 " + i, new Vector3(x, i % 2 == 0 ? -0.92f : 0.68f, 0.04f), new Vector3(0.92f, 0.035f, 0.04f), i % 2 == 0 ? warning : accent, i % 2 == 0 ? -14f : 14f);
            }
        }

        private void CreateCommercialArtAdapterLayer()
        {
            Color policeBlue = new Color(0.08f, 0.28f, 0.88f, 1f);
            Color gangRed = new Color(0.82f, 0.1f, 0.08f, 1f);
            Color neonCyan = new Color(0.08f, 0.72f, 0.9f, 1f);
            Color neonAmber = new Color(0.9f, 0.68f, 0.12f, 1f);
            Color neonPink = new Color(0.94f, 0.18f, 0.46f, 1f);
            Color steel = new Color(0.08f, 0.1f, 0.12f, 1f);
            Color glass = new Color(0.12f, 0.38f, 0.46f, 1f);

            CreateMeshBoxProp("资源适配层 港区主门头", new Vector3(0f, 6.95f, 0.96f), new Vector3(4.2f, 0.16f, 0.26f), policeBlue);
            CreateMeshBoxProp("资源适配层 港区副门头", new Vector3(-8.95f, 4.72f, 0.9f), new Vector3(2.2f, 0.12f, 0.22f), gangRed);
            CreateMeshBoxProp("资源适配层 港区夜市大灯牌", new Vector3(-1.1f, 4.02f, 0.82f), new Vector3(2.42f, 0.1f, 0.24f), neonPink);
            CreateMeshBoxProp("资源适配层 港区证物区门头", new Vector3(-8.7f, -3.98f, 0.86f), new Vector3(2.0f, 0.1f, 0.22f), neonCyan);
            CreateMeshBoxProp("资源适配层 港区诊所灯箱", new Vector3(7.62f, -4.9f, 0.88f), new Vector3(1.72f, 0.1f, 0.22f), neonAmber);
            CreateMeshBoxProp("资源适配层 港区金融楼顶牌", new Vector3(4.8f, 4.1f, 0.9f), new Vector3(1.98f, 0.1f, 0.22f), neonAmber);
            CreateMeshBoxProp("资源适配层 港区电房告示墙", new Vector3(8.9f, 6.0f, 0.86f), new Vector3(1.7f, 0.08f, 0.2f), policeBlue);
            CreateMeshBoxProp("资源适配层 港区指挥车指示板", new Vector3(0f, -6.02f, 0.84f), new Vector3(2.1f, 0.08f, 0.2f), neonCyan);

            CreateModelProp("资源适配层 港区门卫岗亭", "Props/Prop_AccessPoint.fbx", new Vector3(-7.52f, 4.92f, 0.12f), new Vector3(0.72f, 0.48f, 0.38f), 90f);
            CreateModelProp("资源适配层 港区广播终端", "Props/Prop_Computer.fbx", new Vector3(0.88f, 6.28f, 0.12f), new Vector3(0.72f, 0.46f, 0.36f), 0f);
            CreateModelProp("资源适配层 港区门禁闸机", "Platforms/Door_Frame_A.fbx", new Vector3(-4.95f, 5.06f, 0.14f), new Vector3(0.56f, 0.98f, 0.42f), 90f, true);
            CreateModelProp("资源适配层 港区大箱体", "Props/Prop_Chest.fbx", new Vector3(6.8f, -1.92f, 0.12f), new Vector3(0.74f, 0.52f, 0.42f), -8f);
            CreateModelProp("资源适配层 港区灯柱", "Props/Prop_Light_Wide.fbx", new Vector3(9.62f, 4.92f, 0.18f), new Vector3(0.68f, 0.22f, 0.22f), 0f, true);
            CreateModelProp("资源适配层 港区通风架", "Props/Prop_Vent_Big.fbx", new Vector3(-2.1f, -0.94f, 0.12f), new Vector3(0.74f, 0.42f, 0.24f), 0f, true);
            CreateModelProp("资源适配层 港区钢架", "Platforms/Platform_Rails_4Wide.fbx", new Vector3(2.2f, 0.84f, 0.16f), new Vector3(1.28f, 0.26f, 0.38f), 0f, true);

            CreateSolidMeshBoxProp("资源适配层 港区入口钢箱", new Vector3(-10.16f, 5.48f, 0.1f), new Vector3(1.28f, 0.42f, 0.36f), steel, 4f);
            CreateSolidMeshBoxProp("资源适配层 港区侧边玻璃棚", new Vector3(6.16f, 2.42f, 0.12f), new Vector3(1.14f, 0.28f, 0.26f), glass, -6f);
            CreateSolidMeshBoxProp("资源适配层 港区检修高柜", new Vector3(-6.48f, -4.12f, 0.12f), new Vector3(0.72f, 0.42f, 0.46f), steel, 10f);
        }

        private void CreateExteriorDockVista()
        {
            Color water = new Color(0.03f, 0.08f, 0.1f, 1f);
            Color dock = new Color(0.12f, 0.14f, 0.13f, 1f);
            Color crane = new Color(0.84f, 0.58f, 0.08f, 1f);
            CreateShapeProp("维港远景水面西", roundedRectSprite, new Vector3(-12.2f, 3.0f, -0.32f), new Vector3(1.6f, 8.6f, 0.06f), water);
            CreateShapeProp("维港远景水面东", roundedRectSprite, new Vector3(12.2f, -2.8f, -0.32f), new Vector3(1.6f, 8.4f, 0.06f), water);
            CreateMeshBoxProp("码头外缘泊位线西", new Vector3(-10.72f, 4.32f, 0.08f), new Vector3(0.08f, 2.65f, 0.12f), dock);
            CreateMeshBoxProp("码头外缘泊位线东", new Vector3(10.72f, -2.72f, 0.08f), new Vector3(0.08f, 2.5f, 0.12f), dock);
            CreateSolidProp("外景集装箱堆 A", new Vector3(-10.92f, 5.88f, 0.05f), new Vector3(0.74f, 0.34f, 0.18f), new Color(0.08f, 0.24f, 0.52f, 1f));
            CreateSolidProp("外景集装箱堆 B", new Vector3(-10.98f, 5.42f, 0.05f), new Vector3(0.72f, 0.34f, 0.18f), new Color(0.58f, 0.12f, 0.08f, 1f));
            CreateSolidProp("外景集装箱堆 C", new Vector3(10.86f, -4.72f, 0.05f), new Vector3(0.74f, 0.34f, 0.18f), new Color(0.12f, 0.38f, 0.2f, 1f));
            CreateMeshBoxProp("外景龙门吊立柱 A", new Vector3(-10.72f, 5.2f, 0.44f), new Vector3(0.08f, 1.42f, 0.64f), crane);
            CreateMeshBoxProp("外景龙门吊横梁 A", new Vector3(-10.72f, 5.86f, 0.84f), new Vector3(0.92f, 0.06f, 0.08f), crane);
            CreateMeshBoxProp("外景龙门吊吊钩 A", new Vector3(-10.34f, 5.62f, 0.48f), new Vector3(0.08f, 0.42f, 0.08f), new Color(0.05f, 0.05f, 0.05f, 1f));
            CreateMeshBoxProp("东侧巡逻船体", new Vector3(10.88f, -1.42f, 0.06f), new Vector3(0.92f, 0.36f, 0.16f), new Color(0.08f, 0.16f, 0.22f, 1f));
            CreateMeshBoxProp("东侧巡逻船警灯", new Vector3(10.88f, -1.12f, 0.22f), new Vector3(0.52f, 0.05f, 0.06f), new Color(0.08f, 0.36f, 0.92f, 1f));

            for (int i = 0; i < 8; i++)
            {
                float y = -5.8f + i * 1.42f;
                CreateProp("水面反光西 " + i, new Vector3(-12.18f, y, -0.26f), new Vector3(0.68f, 0.035f, 0.04f), new Color(0.18f, 0.38f, 0.42f, 1f));
                CreateProp("水面反光东 " + i, new Vector3(12.18f, y + 0.62f, -0.26f), new Vector3(0.62f, 0.035f, 0.04f), new Color(0.16f, 0.34f, 0.42f, 1f));
            }
        }

        private void CreateDistrictIdentityLandmarks()
        {
            Color neonPink = new Color(0.96f, 0.16f, 0.46f, 1f);
            Color neonBlue = new Color(0.06f, 0.72f, 0.9f, 1f);
            Color amber = new Color(0.9f, 0.66f, 0.12f, 1f);
            CreateMeshBoxProp("茶餐厅大型霓虹牌底", new Vector3(-4.8f, 2.42f, 0.52f), new Vector3(1.2f, 0.08f, 0.26f), new Color(0.08f, 0.035f, 0.03f, 1f));
            CreateMeshBoxProp("茶餐厅大型霓虹字 A", new Vector3(-5.08f, 2.46f, 0.66f), new Vector3(0.42f, 0.035f, 0.05f), neonPink);
            CreateMeshBoxProp("茶餐厅大型霓虹字 B", new Vector3(-4.52f, 2.46f, 0.66f), new Vector3(0.42f, 0.035f, 0.05f), amber);
            CreateMeshBoxProp("金融楼洗钱账房招牌", new Vector3(4.78f, 3.72f, 0.62f), new Vector3(1.35f, 0.07f, 0.28f), new Color(0.04f, 0.05f, 0.08f, 1f));
            CreateMeshBoxProp("金融楼招牌蓝线", new Vector3(4.78f, 3.76f, 0.78f), new Vector3(1.08f, 0.03f, 0.04f), neonBlue);
            CreateMeshBoxProp("夜市棚顶排档灯箱", new Vector3(-1.02f, 3.74f, 0.48f), new Vector3(1.8f, 0.08f, 0.22f), new Color(0.2f, 0.04f, 0.04f, 1f));
            CreateMeshBoxProp("夜市灯箱霓虹线", new Vector3(-1.02f, 3.78f, 0.62f), new Vector3(1.48f, 0.035f, 0.05f), neonPink);
            CreateMeshBoxProp("证物库冷链大门", new Vector3(-7.08f, -5.05f, 0.42f), new Vector3(0.08f, 1.08f, 0.42f), new Color(0.08f, 0.24f, 0.28f, 1f));
            CreateMeshBoxProp("证物库冷链状态灯", new Vector3(-7.02f, -4.72f, 0.68f), new Vector3(0.035f, 0.38f, 0.05f), neonBlue);
            CreateMeshBoxProp("地下诊所唐楼外墙牌", new Vector3(7.62f, -5.02f, 0.54f), new Vector3(0.08f, 0.88f, 0.3f), new Color(0.08f, 0.16f, 0.12f, 1f));
            CreateMeshBoxProp("地下诊所十字灯", new Vector3(7.66f, -5.02f, 0.72f), new Vector3(0.04f, 0.46f, 0.04f), new Color(0.52f, 0.92f, 0.78f, 1f));
            CreateMeshBoxProp("地下诊所十字灯横", new Vector3(7.66f, -5.02f, 0.72f), new Vector3(0.04f, 0.08f, 0.24f), new Color(0.52f, 0.92f, 0.78f, 1f));

            for (int i = 0; i < 6; i++)
            {
                CreateMeshBoxProp("货柜区编号灯 " + i, new Vector3(-10.78f + i * 0.58f, 6.08f, 0.32f), new Vector3(0.26f, 0.035f, 0.06f), i % 2 == 0 ? amber : neonBlue);
                CreateMeshBoxProp("电房高压警示灯 " + i, new Vector3(8.0f + i * 0.34f, 6.1f, 0.36f), new Vector3(0.16f, 0.035f, 0.06f), i % 2 == 0 ? Color.red : amber);
            }
        }

        private void CreateShipLikeSightlineWalls()
        {
            Color bulkhead = new Color(0.025f, 0.035f, 0.04f, 1f);
            Color highlight = new Color(0.42f, 0.5f, 0.5f, 1f);
            Vector3[] wallCenters =
            {
                new Vector3(-2.95f, 1.18f, 0.38f),
                new Vector3(3.05f, 1.18f, 0.38f),
                new Vector3(-3.05f, -1.58f, 0.36f),
                new Vector3(3.05f, -1.58f, 0.36f),
                new Vector3(-8.18f, -2.42f, 0.36f),
                new Vector3(8.24f, 2.62f, 0.36f)
            };

            for (int i = 0; i < wallCenters.Length; i++)
            {
                Vector3 center = wallCenters[i];
                bool horizontal = i < 4;
                Vector3 scale = horizontal ? new Vector3(1.4f, 0.12f, 0.42f) : new Vector3(0.12f, 1.35f, 0.42f);
                CreateSolidProp("视线遮挡厚舱壁 " + i, center, scale, bulkhead);
                CreateMeshBoxProp("视线遮挡舱壁高光 " + i, center + new Vector3(0f, horizontal ? 0.08f : 0f, 0.26f), horizontal ? new Vector3(1.1f, 0.035f, 0.06f) : new Vector3(0.035f, 1.05f, 0.06f), highlight);
            }
        }

        private void CreateRoundEndShowcaseSet()
        {
            Color police = new Color(0.08f, 0.32f, 0.82f, 1f);
            Color gang = new Color(0.78f, 0.08f, 0.06f, 1f);
            CreateMeshBoxProp("结算舞台警方投影", new Vector3(-0.62f, 0.18f, 0.58f), new Vector3(0.52f, 0.05f, 0.3f), police);
            CreateMeshBoxProp("结算舞台黑帮投影", new Vector3(0.62f, 0.18f, 0.58f), new Vector3(0.52f, 0.05f, 0.3f), gang);
            CreateMeshBoxProp("结算舞台证据时间线", new Vector3(0f, -0.92f, 0.18f), new Vector3(1.9f, 0.055f, 0.08f), new Color(0.84f, 0.72f, 0.22f, 1f));
            CreateMeshBoxProp("结算舞台投票箱", new Vector3(0f, -0.35f, 0.42f), new Vector3(0.42f, 0.28f, 0.34f), new Color(0.12f, 0.16f, 0.17f, 1f));
        }

        private void CreateCorridorFloorPanels()
        {
            Color seam = new Color(0.34f, 0.4f, 0.4f, 1f);
            Color plateA = new Color(0.14f, 0.165f, 0.17f, 1f);
            Color plateB = new Color(0.12f, 0.145f, 0.15f, 1f);

            for (int i = 0; i < 13; i++)
            {
                float x = -6.9f + i * 1.15f;
                CreateProp("主横连廊可拆地板 " + i, new Vector3(x, -0.18f, -0.075f), new Vector3(0.82f, 0.34f, 0.04f), i % 2 == 0 ? plateA : plateB);
                CreateProp("主横连廊地板编号条 " + i, new Vector3(x, 0.18f, -0.04f), new Vector3(0.34f, 0.035f, 0.04f), seam);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -6.5f + i * 1.18f;
                CreateProp("上层连廊可拆地板 " + i, new Vector3(x, 3.65f, -0.075f), new Vector3(0.78f, 0.3f, 0.04f), i % 2 == 0 ? plateB : plateA);
                CreateProp("下层连廊可拆地板 " + i, new Vector3(x + 0.18f, -3.9f, -0.075f), new Vector3(0.78f, 0.3f, 0.04f), i % 2 == 0 ? plateA : plateB);
            }

            for (int i = 0; i < 7; i++)
            {
                float y = -2.9f + i * 0.95f;
                CreateProp("左竖连廊竖向舱板 " + i, new Vector3(-6.85f, y, -0.075f), new Vector3(0.3f, 0.62f, 0.04f), i % 2 == 0 ? plateA : plateB);
                CreateProp("右竖连廊竖向舱板 " + i, new Vector3(7.05f, y, -0.075f), new Vector3(0.3f, 0.62f, 0.04f), i % 2 == 0 ? plateB : plateA);
            }
        }

        private void CreateCorridorCameraNetwork()
        {
            Vector3[] positions =
            {
                new Vector3(-5.6f, 0.54f, 0.34f),
                new Vector3(-0.8f, 0.54f, 0.34f),
                new Vector3(4.25f, 0.38f, 0.34f),
                new Vector3(-6.55f, 3.12f, 0.34f),
                new Vector3(6.78f, 3.12f, 0.34f),
                new Vector3(-6.55f, -3.34f, 0.34f),
                new Vector3(6.95f, -3.34f, 0.34f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                CreateWallCamera("走廊监控 " + i, positions[i], i % 2 == 0 ? 0f : 180f);
            }
        }

        private void CreateWallCamera(string name, Vector3 position, float rotation)
        {
            CreateModelProp(name + " CC0 支架", "Props/Prop_Clamp.fbx", position + new Vector3(0f, 0f, 0.02f), new Vector3(0.22f, 0.18f, 0.2f), rotation);
            CreateMeshBoxProp(name + " 机身", position + new Vector3(0f, 0.08f, 0.08f), new Vector3(0.18f, 0.12f, 0.1f), new Color(0.04f, 0.055f, 0.06f, 1f), rotation);
            CreateMeshPrimitiveProp(name + " 镜头", PrimitiveType.Sphere, position + new Vector3(0f, 0.17f, 0.08f), new Vector3(0.08f, 0.08f, 0.06f), new Color(0.08f, 0.78f, 0.92f, 1f), Quaternion.identity);
        }

        private void CreateCorridorCableRuns()
        {
            Color cable = new Color(0.025f, 0.035f, 0.038f, 1f);
            Color signal = new Color(0.08f, 0.64f, 0.78f, 1f);

            for (int i = 0; i < 8; i++)
            {
                float x = -7.2f + i * 2.05f;
                CreateModelProp("CC0 主廊线缆 " + i, "Props/Prop_Cable_1.fbx", new Vector3(x, 0.5f, 0.18f), new Vector3(0.78f, 0.08f, 0.12f), i % 2 == 0 ? 0f : 180f, true);
                CreateProp("主廊线缆阴影 " + i, new Vector3(x, 0.44f, 0.06f), new Vector3(0.78f, 0.035f, 0.04f), cable);
            }

            for (int i = 0; i < 6; i++)
            {
                float y = -2.45f + i * 0.98f;
                CreateModelProp("CC0 左竖管线 " + i, "Props/Prop_Cable_3.fbx", new Vector3(-7.42f, y, 0.16f), new Vector3(0.08f, 0.62f, 0.12f), 90f, true);
                CreateModelProp("CC0 右竖管线 " + i, "Props/Prop_Cable_3.fbx", new Vector3(7.58f, y, 0.16f), new Vector3(0.08f, 0.62f, 0.12f), 90f, true);
                CreateProp("右竖状态光点 " + i, new Vector3(7.34f, y + 0.18f, 0.11f), new Vector3(0.06f, 0.045f, 0.04f), signal);
            }
        }

        private void CreateRoomMicroProps()
        {
            foreach (ShipRoomSpec room in ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;
                Color label = DoorColor(room);

                CreateModelProp("CC0 " + room.Name + " 墙面窄灯", "Props/Prop_Light_Small.fbx", room.Center + new Vector3(-halfWidth + 0.4f, halfHeight - 0.28f, 0.26f), new Vector3(0.18f, 0.18f, 0.16f), 0f);
                CreateMeshBoxProp("屋顶 " + room.Name + " 门牌背光", room.Center + new Vector3(halfWidth * 0.18f, halfHeight - 0.3f, 0.31f), new Vector3(Mathf.Min(0.72f, room.Size.x * 0.22f), 0.04f, 0.08f), label);
                CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 小型通风百叶", room.Center + new Vector3(halfWidth - 0.32f, halfHeight - 0.12f, 0.36f), new Vector3(0.28f, 0.035f, 0.13f), new Color(0.04f, 0.055f, 0.06f, 1f));

                for (int i = 0; i < 3; i++)
                {
                    CreateMeshBoxProp("2.5D 建筑体 " + room.Name + " 百叶缝 " + i, room.Center + new Vector3(halfWidth - 0.32f, halfHeight - 0.095f, 0.32f + i * 0.045f), new Vector3(0.23f, 0.025f, 0.015f), new Color(0.46f, 0.52f, 0.52f, 1f));
                }
            }
        }

        private void CreateExteriorHullProps()
        {
            Color red = new Color(0.86f, 0.08f, 0.08f, 1f);
            Color blue = new Color(0.08f, 0.28f, 0.82f, 1f);
            Color amber = new Color(0.92f, 0.68f, 0.08f, 1f);

            for (int i = 0; i < 9; i++)
            {
                float x = -8.6f + i * 2.15f;
                CreateMeshPrimitiveProp("屋顶 外壳应急警灯红 " + i, PrimitiveType.Cylinder, new Vector3(x, 6.78f, 0.16f), new Vector3(0.12f, 0.12f, 0.08f), i % 2 == 0 ? red : blue, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshPrimitiveProp("屋顶 南外壳定位灯 " + i, PrimitiveType.Cylinder, new Vector3(x + 0.72f, -6.78f, 0.16f), new Vector3(0.1f, 0.1f, 0.08f), amber, Quaternion.Euler(90f, 0f, 0f));
            }

            CreateModelProp("CC0 左侧维修梯", "Platforms/Platform_Stairs_4Wide.fbx", new Vector3(-10.9f, -2.8f, 0.18f), new Vector3(0.64f, 1.2f, 0.36f), 90f, true);
            CreateModelProp("CC0 右侧维修梯", "Platforms/Platform_Stairs_4Wide.fbx", new Vector3(10.75f, 2.7f, 0.18f), new Vector3(0.64f, 1.2f, 0.36f), -90f, true);
        }

        private void CreateCorridorServiceProps()
        {
            Color metal = new Color(0.08f, 0.1f, 0.11f, 1f);
            Color screen = new Color(0.06f, 0.62f, 0.78f, 1f);
            Color warning = new Color(0.86f, 0.66f, 0.08f, 1f);

            for (int i = 0; i < 5; i++)
            {
                float x = -5.4f + i * 2.7f;
                CreateSolidProp("主走廊壁柜 " + i, new Vector3(x, 0.58f, 0.07f), new Vector3(0.32f, 0.22f, 0.2f), metal);
                CreateProp("主走廊壁柜屏 " + i, new Vector3(x, 0.72f, 0.2f), new Vector3(0.22f, 0.04f, 0.06f), screen);
                CreateSolidProp("下层走廊补给箱 " + i, new Vector3(-5.2f + i * 2.55f, -4.42f, 0.07f), new Vector3(0.42f, 0.2f, 0.18f), i % 2 == 0 ? warning : metal);
            }

            for (int i = 0; i < 4; i++)
            {
                float y = -2.5f + i * 1.45f;
                CreateSolidProp("左竖连廊封控箱 " + i, new Vector3(-7.45f, y, 0.07f), new Vector3(0.22f, 0.34f, 0.18f), metal);
                CreateSolidProp("右竖连廊封控箱 " + i, new Vector3(7.65f, y, 0.07f), new Vector3(0.22f, 0.34f, 0.18f), metal);
            }

            CreateProp("主走廊红色警戒条", new Vector3(2.2f, 0.52f, 0.08f), new Vector3(1.2f, 0.08f, 0.08f), new Color(0.86f, 0.08f, 0.06f, 1f));
            CreateProp("上层走廊证物导线", new Vector3(-1.8f, 4.14f, 0.08f), new Vector3(1.6f, 0.055f, 0.08f), new Color(0.48f, 0.84f, 0.82f, 1f));
            CreateProp("下层走廊警戒导线", new Vector3(3.2f, -3.36f, 0.08f), new Vector3(1.4f, 0.055f, 0.08f), warning);
        }

        private void CreateQuaterniusModelDressing()
        {
            CreateModelRoomKits();
            CreateModelCorridorKits();
            CreateModelFloorPlates();
        }

        private void CreateModelRoomKits()
        {
            foreach (ShipRoomSpec room in ShipRooms())
            {
                float halfWidth = room.Size.x * 0.5f;
                float halfHeight = room.Size.y * 0.5f;

                CreateModelProp("CC0 舱内顶灯 " + room.Name, "Props/Prop_Light_Wide.fbx", room.Center + new Vector3(0f, halfHeight * 0.48f, 0.28f), new Vector3(0.72f, 0.18f, 0.18f), 0f);

                switch (room.Name)
                {
                    case "西码头货柜场":
                        CreateSolidModelProp("CC0 蓝色货柜 " + room.Name, "Props/Prop_Crate4.fbx", room.Center + new Vector3(-1.25f, 0.22f, 0.1f), new Vector3(0.78f, 0.46f, 0.42f), 0f);
                        CreateSolidModelProp("CC0 封存货箱 " + room.Name, "Props/Prop_Chest.fbx", room.Center + new Vector3(0.45f, -0.35f, 0.1f), new Vector3(0.62f, 0.42f, 0.36f), 12f);
                        CreateSolidModelProp("CC0 堆货箱 " + room.Name, "Props/Prop_Crate3.fbx", room.Center + new Vector3(1.28f, 0.42f, 0.1f), new Vector3(0.54f, 0.36f, 0.34f), -8f);
                        break;
                    case "海关查验区":
                        CreateSolidModelProp("CC0 查验终端 " + room.Name, "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.55f, 0.22f, 0.12f), new Vector3(0.62f, 0.42f, 0.38f), 180f);
                        CreateModelProp("CC0 查验门框 " + room.Name, "Platforms/Door_Frame_SquareTall.fbx", room.Center + new Vector3(0.82f, 0.15f, 0.18f), new Vector3(0.42f, 0.96f, 0.46f), 90f, true);
                        break;
                    case "监控室":
                        CreateSolidModelProp("CC0 监控电脑 A", "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.52f, -0.18f, 0.12f), new Vector3(0.58f, 0.38f, 0.36f), 0f);
                        CreateSolidModelProp("CC0 监控电脑 B", "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(0.38f, -0.18f, 0.12f), new Vector3(0.54f, 0.36f, 0.34f), 0f);
                        break;
                    case "茶餐厅":
                        CreateSolidModelProp("CC0 休息舱箱柜 A", "Props/Prop_Chest.fbx", room.Center + new Vector3(-0.86f, 0.35f, 0.12f), new Vector3(0.55f, 0.36f, 0.32f), 90f);
                        CreateSolidModelProp("CC0 休息舱箱柜 B", "Props/Prop_Crate3.fbx", room.Center + new Vector3(0.82f, -0.28f, 0.1f), new Vector3(0.42f, 0.34f, 0.3f), -8f);
                        break;
                    case "夜市主街":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 情报摊设备 " + i, i == 1 ? "Props/Prop_ItemHolder.fbx" : "Props/Prop_Crate4.fbx", room.Center + new Vector3(-1.1f + i * 1.05f, 0.18f, 0.1f), new Vector3(0.56f, 0.38f, 0.34f), i * 9f);
                        }
                        break;
                    case "金融楼":
                        CreateSolidModelProp("CC0 账房保险柜", "Props/Prop_Chest.fbx", room.Center + new Vector3(0.92f, -0.24f, 0.12f), new Vector3(0.62f, 0.48f, 0.42f), -90f);
                        CreateSolidModelProp("CC0 账房电脑", "Props/Prop_Computer.fbx", room.Center + new Vector3(-0.34f, 0.12f, 0.12f), new Vector3(0.62f, 0.38f, 0.36f), 0f);
                        break;
                    case "电房":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 电力设备 " + i, "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(-0.64f + i * 0.5f, 0.22f, 0.12f), new Vector3(0.4f, 0.38f, 0.38f), i % 2 == 0 ? 0f : 180f);
                        }
                        break;
                    case "天台通道":
                        CreateSolidModelProp("CC0 观测平台", "Platforms/Platform_Round1.fbx", room.Center + new Vector3(-0.18f, 0f, 0.08f), new Vector3(0.72f, 0.62f, 0.18f), 0f, true);
                        CreateSolidModelProp("CC0 观测灯", "Props/Prop_Light_Floor.fbx", room.Center + new Vector3(0.78f, 0.28f, 0.1f), new Vector3(0.35f, 0.35f, 0.38f), 0f);
                        break;
                    case "指挥车广场":
                        CreateSolidModelProp("CC0 指挥圆桌箱", "Props/Prop_Chest.fbx", room.Center + new Vector3(0.0f, -0.04f, 0.14f), new Vector3(0.78f, 0.48f, 0.4f), 0f);
                        CreateSolidModelProp("CC0 指挥终端", "Props/Prop_AccessPoint.fbx", room.Center + new Vector3(-1.25f, 0.22f, 0.12f), new Vector3(0.48f, 0.38f, 0.36f), 90f);
                        break;
                    case "证物库":
                        for (int i = 0; i < 3; i++)
                        {
                            CreateSolidModelProp("CC0 证物箱 " + i, i == 2 ? "Props/Prop_Chest.fbx" : "Props/Prop_Crate3.fbx", room.Center + new Vector3(-0.9f + i * 0.6f, 0.25f, 0.1f), new Vector3(0.45f, 0.34f, 0.32f), i * 7f);
                        }
                        break;
                    case "后巷排档":
                        CreateSolidModelProp("CC0 维修箱", "Props/Prop_Crate4.fbx", room.Center + new Vector3(-0.5f, 0.24f, 0.1f), new Vector3(0.56f, 0.38f, 0.32f), 12f);
                        CreateSolidModelProp("CC0 管线夹具", "Props/Prop_PipeHolder.fbx", room.Center + new Vector3(0.82f, -0.28f, 0.1f), new Vector3(0.62f, 0.3f, 0.32f), 90f);
                        break;
                    case "地下诊所":
                        CreateSolidModelProp("CC0 诊疗柜", "Props/Prop_Chest.fbx", room.Center + new Vector3(-1.18f, 0.32f, 0.1f), new Vector3(0.48f, 0.38f, 0.34f), 90f);
                        CreateModelProp("CC0 诊疗灯", "Props/Prop_Light_Floor.fbx", room.Center + new Vector3(0.05f, 0.46f, 0.14f), new Vector3(0.34f, 0.34f, 0.4f), 0f);
                        break;
                }

                CreateModelProp("CC0 舱室立柱 L " + room.Name, "Columns/Column_MetalSupport.fbx", room.Center + new Vector3(-halfWidth + 0.26f, -halfHeight + 0.22f, 0.18f), new Vector3(0.22f, 0.22f, 0.42f), 0f);
                CreateModelProp("CC0 舱室立柱 R " + room.Name, "Columns/Column_MetalSupport.fbx", room.Center + new Vector3(halfWidth - 0.26f, halfHeight - 0.22f, 0.18f), new Vector3(0.22f, 0.22f, 0.42f), 0f);
            }
        }

        private static string RoomPlatformModel(ShipRoomSpec room)
        {
            if (room.Label.Contains("账房") || room.Label.Contains("监控") || room.Label.Contains("电力"))
            {
                return "Platforms/Platform_DarkPlates.fbx";
            }

            if (room.Label.Contains("指挥") || room.Label.Contains("观测"))
            {
                return "Platforms/Platform_CenterPlate.fbx";
            }

            return "Platforms/Platform_Simple.fbx";
        }

        private void CreateModelCorridorKits()
        {
            for (int i = 0; i < 7; i++)
            {
                float x = -6.2f + i * 2.05f;
                CreateModelProp("CC0 主廊顶灯 " + i, "Props/Prop_Light_Wide.fbx", new Vector3(x, 0.5f, 0.18f), new Vector3(0.72f, 0.16f, 0.16f), 0f, true);
                CreateModelProp("CC0 下廊地灯 " + i, "Props/Prop_Light_Small.fbx", new Vector3(x + 0.35f, -4.48f, 0.08f), new Vector3(0.25f, 0.25f, 0.18f), 0f);
            }

            for (int i = 0; i < 5; i++)
            {
                float y = -3.05f + i * 1.45f;
                CreateModelProp("CC0 左廊栏杆 " + i, "Props/Prop_Rail_3.fbx", new Vector3(-7.48f, y, 0.12f), new Vector3(0.22f, 0.74f, 0.2f), 90f, true);
                CreateModelProp("CC0 右廊栏杆 " + i, "Props/Prop_Rail_3.fbx", new Vector3(7.68f, y, 0.12f), new Vector3(0.22f, 0.74f, 0.2f), 90f, true);
            }

            CreateSolidModelProp("CC0 主廊封锁箱 A", "Props/Prop_Crate4.fbx", new Vector3(-1.05f, -0.68f, 0.1f), new Vector3(0.7f, 0.26f, 0.32f), 0f);
            CreateSolidModelProp("CC0 主廊封锁箱 B", "Props/Prop_Crate3.fbx", new Vector3(1.05f, 0.68f, 0.1f), new Vector3(0.7f, 0.26f, 0.32f), 180f);
            CreateModelProp("CC0 会议舱圆环平台", "Platforms/Platform_Round1.fbx", new Vector3(0f, -0.35f, -0.02f), new Vector3(1.55f, 1.25f, 0.1f), 0f, true);
            CreateModelProp("CC0 中央门禁电脑", "Props/Prop_Computer.fbx", new Vector3(1.95f, 0.82f, 0.12f), new Vector3(0.42f, 0.36f, 0.34f), -90f);
            CreateModelProp("CC0 墙面监控终端", "Props/Prop_AccessPoint.fbx", new Vector3(2.95f, -3.25f, 0.12f), new Vector3(0.3f, 0.82f, 0.36f), 90f, true);
        }

        private void CreateModelFloorPlates()
        {
            for (int i = 0; i < 8; i++)
            {
                float x = -7f + i * 2f;
                CreateModelProp("CC0 上层地板模块 " + i, i % 2 == 0 ? "Platforms/Platform_3Plates.fbx" : "Platforms/Platform_Squares.fbx", new Vector3(x, 3.65f, 0.02f), new Vector3(0.46f, 0.32f, 0.05f), 0f);
                CreateModelProp("CC0 下层地板模块 " + i, i % 2 == 0 ? "Platforms/Platform_Metal2.fbx" : "Platforms/Platform_Simple2.fbx", new Vector3(x, -3.9f, 0.02f), new Vector3(0.46f, 0.32f, 0.05f), 0f);
            }
        }

        private void EnsureRuntimeSprites()
        {
            if (roundedRectSprite != null && circleSprite != null && softCircleSprite != null && diamondSprite != null && capsuleSprite != null)
            {
                return;
            }

            roundedRectSprite = CreateRoundedRectSprite("Runtime Rounded Rect", 32, 6);
            circleSprite = CreateCircleSprite("Runtime Circle", 32, false);
            softCircleSprite = CreateCircleSprite("Runtime Soft Circle", 32, true);
            diamondSprite = CreateDiamondSprite("Runtime Diamond", 32);
            capsuleSprite = CreateRoundedRectSprite("Runtime Capsule", 32, 14);
        }

        private GameObject CreateSpriteObject(string objectName, Sprite sprite, Color color)
        {
            EnsureRuntimeSprites();
            GameObject spriteObject = new GameObject(objectName);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : roundedRectSprite;
            renderer.color = color;
            renderer.sortingOrder = SortingOrderForZ(spriteObject.transform.position.z);
            return spriteObject;
        }

        private static Sprite CreateRoundedRectSprite(string spriteName, int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0, x - (size - radius - 1));
                    float dy = Mathf.Max(radius - y, 0, y - (size - radius - 1));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    texture.SetPixel(x, y, distance <= radius ? fill : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateCircleSprite(string spriteName, int size, bool softEdge)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = softEdge ? Mathf.Clamp01((radius - distance) / (radius * 0.18f)) : distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateDiamondSprite(string spriteName, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            Color clear = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float manhattan = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    texture.SetPixel(x, y, manhattan <= center ? Color.white : clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize)
        {
            GameObject labelObject = new GameObject("Label " + text);
            labelObject.transform.SetParent(worldRoot.transform, false);
            labelObject.transform.position = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = new Color(0.72f, 0.78f, 0.72f, 0.96f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sortingOrder = SortingOrderForZ(position.z) + 20;
            }

            if (Camera.main != null)
            {
                BillboardLabel(labelObject.transform);
            }

            worldLabels.Add(label);
            return label;
        }

        private TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = new Color(0.88f, 0.92f, 0.88f, 1f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.sortingOrder = 900;
            }

            if (Camera.main != null)
            {
                BillboardLabel(labelObject.transform);
            }

            return label;
        }

        private static void BillboardLabel(Transform labelTransform)
        {
            if (labelTransform == null || Camera.main == null)
            {
                return;
            }

            Vector3 direction = Camera.main.transform.position - labelTransform.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Camera.main.transform.up);
        }

        private string BuildPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            if (!state.Alive)
            {
                return "出局";
            }

            if (isLocal)
            {
                return "你\n" + ProfessionName(state.Profession);
            }

            return state.DisplayName.Length > 4 ? state.DisplayName.Substring(0, 4) : state.DisplayName;
        }

        private bool ShouldShowPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            if (phase == OnlineMatchPhase.Action)
            {
                if (tacticalMapOpen)
                {
                    return true;
                }

                return isLocal || !state.Alive;
            }

            return true;
        }

        private bool IsNearCameraSubject(Vector3 position)
        {
            if (tacticalMapOpen || phase != OnlineMatchPhase.Action)
            {
                return true;
            }

            return Vector3.Distance(position, LocalCameraTarget()) <= 2.4f;
        }

        private static void SetTextMeshVisible(TextMesh label, bool visible)
        {
            MeshRenderer renderer = label == null ? null : label.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private static void RemoveStaleVisuals<T>(Dictionary<T, GameObject> visuals, HashSet<T> seen)
        {
            List<T> stale = new List<T>();

            foreach (KeyValuePair<T, GameObject> pair in visuals)
            {
                if (!seen.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (T key in stale)
            {
                if (visuals[key] != null)
                {
                    Destroy(visuals[key]);
                }

                visuals.Remove(key);
            }
        }

        private static void SetColor(GameObject target, Color color)
        {
            SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
                return;
            }

            Renderer renderer = target.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;

                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                    if (Application.isPlaying)
                    {
                        renderer.material = material;
                    }
                    else
                    {
                        renderer.sharedMaterial = material;
                    }
                }

                material.color = color;
            }
        }

        private static void SetPlayerVisualColors(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            SpriteRenderer rootRenderer = visual.GetComponent<SpriteRenderer>();

            if (rootRenderer != null)
            {
                rootRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            Transform coat = visual.transform.Find("Coat");

            if (coat != null)
            {
                SetColor(coat.gameObject, PlayerColor(state, isLocal));
            }

            Transform bodyVolume = visual.transform.Find("Body Volume");

            if (bodyVolume != null)
            {
                SetColor(bodyVolume.gameObject, PlayerColor(state, isLocal));
            }

            Transform helmetVolume = visual.transform.Find("Helmet Volume");

            if (helmetVolume != null)
            {
                SetColor(helmetVolume.gameObject, PlayerColor(state, isLocal));
            }

            Transform packVolume = visual.transform.Find("Pack Volume");
            Transform localRing = visual.transform.Find("Local Ring");
            Transform localArrow = visual.transform.Find("Local Arrow");

            Color accent = PlayerAccentColor(state);
            Transform torso = visual.transform.Find("Torso");
            Transform armL = visual.transform.Find("Arm L");
            Transform armR = visual.transform.Find("Arm R");

            if (torso != null)
            {
                SetColor(torso.gameObject, accent);
            }

            if (armL != null)
            {
                SetColor(armL.gameObject, accent);
            }

            if (armR != null)
            {
                SetColor(armR.gameObject, accent);
            }

            if (packVolume != null)
            {
                SetColor(packVolume.gameObject, Darken(accent, 0.7f));
            }

            if (localRing != null)
            {
                localRing.gameObject.SetActive(isLocal && state.Alive);
                SetColor(localRing.gameObject, new Color(0.08f, 0.72f, 0.95f, 0.62f));
            }

            if (localArrow != null)
            {
                localArrow.gameObject.SetActive(isLocal && state.Alive);
                SetColor(localArrow.gameObject, new Color(0.95f, 0.82f, 0.12f, 1f));
            }
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
                interactionRadius.localScale = new Vector3(InteractionRange * 2f * pulse, InteractionRange * 1.18f * pulse, 0.05f);
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
            for (int i = 0; i < bodies.Count; i++)
            {
                OnlineBodyState body = bodies[i];

                if (!body.Reported && Vector3.Distance(position, body.Position) <= ReportRange * 1.4f)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindChildTransform(Transform root, params string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform found = root.Find(names[i]);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SetChildActive(GameObject root, string childName, bool active)
        {
            Transform child = root == null ? null : root.transform.Find(childName);

            if (child != null && child.gameObject.activeSelf != active)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void SetSortingFromZ(GameObject target)
        {
            int sortingOrder = SortingOrderForZ(target.transform.position.z);

            foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sortingOrder = sortingOrder + SortingOrderForLocalZ(renderer.transform.localPosition.z);
            }

            foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sortingOrder = sortingOrder + SortingOrderForLocalZ(renderer.transform.localPosition.z) + 20;
            }
        }

        private static int SortingOrderForZ(float z)
        {
            return Mathf.RoundToInt((z + 2f) * 100f);
        }

        private static int SortingOrderForLocalZ(float z)
        {
            return Mathf.RoundToInt(z * 40f);
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
            bodies.Clear();
            killCooldowns.Clear();
            abilityCooldowns.Clear();
            botThinkTimers.Clear();
            botVoteTimers.Clear();
            botTargets.Clear();
            BuildDefaultTasks();
            evidenceScore = 0;
            evidenceMilestoneIndex = 0;
            lastMeetingReason = "尚未召开会议。";
            lastVoteOutcome = "尚未投票。";
            lastEvidenceEvent = "尚未取得关键证据。";
            lastSabotageEvent = "尚未发生破坏。";
            blackoutTimer = 0f;
            lockdownTimer = 0f;
            communicationJamTimer = 0f;
            evidenceLeakTimer = 0f;
            evidenceLeakAccumulator = 0f;
            patrolAlertTimer = 0f;
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
            switch (taskId)
            {
                case 0:
                    return new Vector3(0.42f, 0.24f, 0.22f);
                case 1:
                case 23:
                case 25:
                    return new Vector3(0.5f, 0.32f, 0.22f);
                case 2:
                    return new Vector3(0.32f, 0.1f, 0.32f);
                case 3:
                case 21:
                case 24:
                    return new Vector3(0.34f, 0.34f, 0.2f);
                case 4:
                    return new Vector3(0.36f, 0.22f, 0.28f);
                case 5:
                case 20:
                case 22:
                case 26:
                case 27:
                    return new Vector3(0.34f, 0.34f, 0.2f);
                default:
                    return new Vector3(0.34f, 0.34f, 0.18f);
            }
        }

        private static Color PlayerAccentColor(OnlinePlayerState state)
        {
            switch (state.Profession)
            {
                case OnlineProfession.Inspector:
                    return new Color(0.08f, 0.18f, 0.45f, 1f);
                case OnlineProfession.Forensics:
                    return new Color(0.14f, 0.52f, 0.5f, 1f);
                case OnlineProfession.Tech:
                    return new Color(0.18f, 0.36f, 0.72f, 1f);
                case OnlineProfession.UndercoverAgent:
                    return new Color(0.42f, 0.28f, 0.62f, 1f);
                case OnlineProfession.Enforcer:
                    return new Color(0.48f, 0.08f, 0.08f, 1f);
                case OnlineProfession.Fixer:
                    return new Color(0.36f, 0.26f, 0.08f, 1f);
                case OnlineProfession.Driver:
                    return new Color(0.1f, 0.38f, 0.18f, 1f);
                default:
                    return new Color(0.32f, 0.32f, 0.36f, 1f);
            }
        }

        private static Color PlayerColor(OnlinePlayerState state, bool isLocal)
        {
            if (!state.Alive)
            {
                return new Color(0.32f, 0.32f, 0.32f, 1f);
            }

            if (isLocal)
            {
                return new Color(0.96f, 0.82f, 0.18f, 1f);
            }

            if (state.PublicRole == OnlineRole.Gang)
            {
                return new Color(0.75f, 0.08f, 0.08f, 1f);
            }

            if (state.PublicRole == OnlineRole.Undercover)
            {
                return new Color(0.16f, 0.6f, 0.78f, 1f);
            }

            return new Color(0.78f, 0.78f, 0.86f, 1f);
        }

        private static Vector3 SpawnPosition(int index)
        {
            Vector3[] spawns =
            {
                ScaleMapPosition(new Vector3(-9.45f, 4.95f, 0f)),
                ScaleMapPosition(new Vector3(-5.05f, 1.82f, 0f)),
                ScaleMapPosition(new Vector3(-1.08f, 2.62f, 0f)),
                ScaleMapPosition(new Vector3(4.72f, 2.68f, 0f)),
                ScaleMapPosition(new Vector3(8.82f, 4.45f, 0f)),
                ScaleMapPosition(new Vector3(0f, -4.55f, 0f)),
                ScaleMapPosition(new Vector3(-8.55f, -4.42f, 0f)),
                ScaleMapPosition(new Vector3(5.52f, -1.32f, 0f)),
                ScaleMapPosition(new Vector3(6.05f, -4.42f, 0f)),
                ScaleMapPosition(new Vector3(-9.32f, 1.18f, 0f))
            };

            return spawns[index % spawns.Length];
        }

        private static Vector3 UnderworldPassageDesignPosition(int index)
        {
            switch (index % UnderworldPassageCount)
            {
                case 0:
                    return new Vector3(-6.85f, 0.72f, 0f);
                case 1:
                    return new Vector3(1.65f, 3.65f, 0f);
                case 2:
                    return new Vector3(6.95f, -2.25f, 0f);
                default:
                    return new Vector3(-7.25f, -4.15f, 0f);
            }
        }

        private static Vector3 UnderworldPassagePosition(int index)
        {
            return ScaleMapPosition(UnderworldPassageDesignPosition(index));
        }

        private static string BotName(int index)
        {
            string[] names =
            {
                "巡警陈",
                "技侦周",
                "线人林",
                "便衣何",
                "阿泰",
                "疤脸",
                "码头辉",
                "诊所梁"
            };

            return names[(index - 1) % names.Length];
        }

        private static string TaskNameFor(int id)
        {
            switch (id)
            {
                case 0:
                    return "调取监控";
                case 1:
                    return "查封货柜";
                case 2:
                    return "修复电闸";
                case 3:
                    return "扫描证物";
                case 4:
                    return "上传档案";
                case 5:
                    return "盘问线人";
                case 6:
                    return "无线电监听";
                case 7:
                    return "门禁取证";
                case 8:
                    return "天台目击";
                case 9:
                    return "诊所搜查";
                case 10:
                    return "码头巡线";
                case 11:
                    return "财务追踪";
                case 12:
                    return "解除卷闸";
                case 13:
                    return "恢复通讯";
                case 14:
                    return "备用发电";
                case 15:
                    return "整理弹道";
                case 16:
                    return "清点赃款";
                case 17:
                    return "巡逻打卡";
                case 18:
                    return "追踪车牌";
                case 19:
                    return "核对病历";
                case 20:
                    return "追查鱼档暗号";
                case 21:
                    return "比对电话录音";
                case 22:
                    return "封存黑钱袋";
                case 23:
                    return "检查码头冷柜";
                case 24:
                    return "恢复警用无人机";
                case 25:
                    return "排查后巷摩托";
                case 26:
                    return "核验巡逻路线";
                case 27:
                    return "加固证人安全屋";
                default:
                    return "未知任务";
            }
        }

        private static string TaskDistrictName(int id)
        {
            switch (id)
            {
                case 1:
                case 10:
                case 23:
                    return "西码头货柜场";
                case 0:
                case 21:
                    return "监控中心";
                case 2:
                case 14:
                    return "港区电房";
                case 3:
                case 15:
                case 26:
                    return "证物库";
                case 4:
                case 24:
                    return "警队指挥车棚";
                case 5:
                    return "茶餐厅骑楼";
                case 6:
                case 13:
                case 20:
                    return "庙街夜市棚群";
                case 7:
                case 11:
                case 16:
                case 22:
                    return "黑钱金融楼";
                case 8:
                case 27:
                    return "天台机房";
                case 9:
                case 19:
                    return "地下诊所唐楼";
                case 12:
                case 25:
                    return "后巷排档楼";
                case 17:
                case 18:
                    return "中环主干道";
                default:
                    return "九龙港城";
            }
        }

        private static Vector3 TaskPositionFor(int id)
        {
            switch (id)
            {
                case 0:
                    return ScaleMapPosition(new Vector3(-9.45f, 2.2f, 0f));
                case 1:
                    return ScaleMapPosition(new Vector3(-8.75f, 5.72f, 0f));
                case 2:
                    return ScaleMapPosition(new Vector3(8.7f, 5.42f, 0f));
                case 3:
                    return ScaleMapPosition(new Vector3(-8.65f, -4.95f, 0f));
                case 4:
                    return ScaleMapPosition(new Vector3(0.2f, -5.32f, 0f));
                case 5:
                    return ScaleMapPosition(new Vector3(-4.82f, 1.38f, 0f));
                case 6:
                    return ScaleMapPosition(new Vector3(-0.25f, 3.25f, 0f));
                case 7:
                    return ScaleMapPosition(new Vector3(3.98f, 2.9f, 0f));
                case 8:
                    return ScaleMapPosition(new Vector3(8.82f, 1.72f, 0f));
                case 9:
                    return ScaleMapPosition(new Vector3(6.12f, -5.12f, 0f));
                case 10:
                    return ScaleMapPosition(new Vector3(-10.55f, 4.45f, 0f));
                case 11:
                    return ScaleMapPosition(new Vector3(5.65f, 3.22f, 0f));
                case 12:
                    return ScaleMapPosition(new Vector3(5.38f, -1.02f, 0f));
                case 13:
                    return ScaleMapPosition(new Vector3(-1.42f, -0.18f, 0f));
                case 14:
                    return ScaleMapPosition(new Vector3(9.62f, 4.72f, 0f));
                case 15:
                    return ScaleMapPosition(new Vector3(-7.62f, -5.68f, 0f));
                case 16:
                    return ScaleMapPosition(new Vector3(6.35f, 2.18f, 0f));
                case 17:
                    return ScaleMapPosition(new Vector3(-6.86f, 0.16f, 0f));
                case 18:
                    return ScaleMapPosition(new Vector3(1.74f, -3.62f, 0f));
                case 19:
                    return ScaleMapPosition(new Vector3(7.25f, -4.45f, 0f));
                case 20:
                    return ScaleMapPosition(new Vector3(-1.95f, 2.22f, 0f));
                case 21:
                    return ScaleMapPosition(new Vector3(-9.88f, 1.25f, 0f));
                case 22:
                    return ScaleMapPosition(new Vector3(5.65f, 2.52f, 0f));
                case 23:
                    return ScaleMapPosition(new Vector3(-10.35f, 5.05f, 0f));
                case 24:
                    return ScaleMapPosition(new Vector3(0.95f, -4.62f, 0f));
                case 25:
                    return ScaleMapPosition(new Vector3(6.74f, -1.95f, 0f));
                case 26:
                    return ScaleMapPosition(new Vector3(-6.85f, -2.82f, 0f));
                case 27:
                    return ScaleMapPosition(new Vector3(8.95f, 1.12f, 0f));
                default:
                    return Vector3.zero;
            }
        }

        private static int TaskRequiredProgress(int id)
        {
            switch (id)
            {
                case 1:
                case 11:
                case 16:
                case 22:
                case 23:
                case 25:
                case 26:
                    return 3;
                case 4:
                case 12:
                case 14:
                case 18:
                case 21:
                case 24:
                case 27:
                    return 3;
                default:
                    return 3;
            }
        }

        private static OnlineProfession ProfessionFor(OnlineRole role, int seed)
        {
            if (role == OnlineRole.Gang)
            {
                OnlineProfession[] gangProfessions =
                {
                    OnlineProfession.Enforcer,
                    OnlineProfession.Fixer,
                    OnlineProfession.Driver
                };

                return gangProfessions[seed % gangProfessions.Length];
            }

            if (role == OnlineRole.Undercover)
            {
                return OnlineProfession.UndercoverAgent;
            }

            OnlineProfession[] policeProfessions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Forensics,
                OnlineProfession.Tech
            };

            return policeProfessions[seed % policeProfessions.Length];
        }

        private static OnlineProfession BotProfession(int index)
        {
            OnlineProfession[] professions =
            {
                OnlineProfession.Inspector,
                OnlineProfession.Tech,
                OnlineProfession.UndercoverAgent,
                OnlineProfession.Forensics,
                OnlineProfession.Enforcer,
                OnlineProfession.Fixer,
                OnlineProfession.Driver
            };

            return professions[(index - 1) % professions.Length];
        }

        private static string ProfessionName(OnlineProfession profession)
        {
            switch (profession)
            {
                case OnlineProfession.Inspector:
                    return "反黑督察";
                case OnlineProfession.Forensics:
                    return "鉴证员";
                case OnlineProfession.Tech:
                    return "技侦警员";
                case OnlineProfession.UndercoverAgent:
                    return "潜伏探员";
                case OnlineProfession.Enforcer:
                    return "黑帮打手";
                case OnlineProfession.Fixer:
                    return "白纸扇";
                case OnlineProfession.Driver:
                    return "车手";
                default:
                    return "待分配";
            }
        }

        private static string RoleName(OnlineRole role)
        {
            switch (role)
            {
                case OnlineRole.Gang:
                    return "黑帮";
                case OnlineRole.Undercover:
                    return "卧底";
                case OnlineRole.Police:
                    return "警方";
                default:
                    return "未公开";
            }
        }

        private static string PhaseName(OnlineMatchPhase matchPhase)
        {
            switch (matchPhase)
            {
                case OnlineMatchPhase.Opening:
                    return "开局简报";
                case OnlineMatchPhase.Action:
                    return "行动";
                case OnlineMatchPhase.Meeting:
                    return "会议";
                case OnlineMatchPhase.Voting:
                    return "投票";
                case OnlineMatchPhase.Result:
                    return "结算";
                default:
                    return "房间";
            }
        }

        private static Vector3 ClampToOnlineMap(Vector3 position)
        {
            return new Vector3(Mathf.Clamp(position.x, -MapHalfWidth, MapHalfWidth), Mathf.Clamp(position.y, -MapHalfHeight, MapHalfHeight), 0f);
        }

        private static Vector3 ClampToDesignMap(Vector3 position)
        {
            return new Vector3(Mathf.Clamp(position.x, -DesignMapHalfWidth, DesignMapHalfWidth), Mathf.Clamp(position.y, -DesignMapHalfHeight, DesignMapHalfHeight), 0f);
        }

        private Vector3 ResolveMapCollision(Vector3 from, Vector3 requested)
        {
            Vector3 clampedFrom = ClampToOnlineMap(from);
            Vector3 clampedRequested = ClampToOnlineMap(requested);
            Vector3 delta = clampedRequested - clampedFrom;
            float distance = delta.magnitude;

            if (distance <= 0.0001f)
            {
                return IsWalkable(clampedRequested) ? clampedRequested : FindNearestOpenPosition(clampedRequested, clampedFrom);
            }

            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / CollisionTraceStep));
            Vector3 lastValid = IsWalkable(clampedFrom) ? clampedFrom : FindNearestOpenPosition(clampedFrom, Vector3.zero);

            for (int i = 1; i <= steps; i++)
            {
                Vector3 candidate = Vector3.Lerp(clampedFrom, clampedRequested, i / (float)steps);

                if (IsWalkable(candidate))
                {
                    lastValid = candidate;
                    continue;
                }

                Vector3 slideX = new Vector3(candidate.x, lastValid.y, 0f);

                if (IsWalkable(slideX))
                {
                    lastValid = slideX;
                    continue;
                }

                Vector3 slideY = new Vector3(lastValid.x, candidate.y, 0f);

                if (IsWalkable(slideY))
                {
                    lastValid = slideY;
                }
            }

            return ClampToOnlineMap(lastValid);
        }

        private Vector3 FindNearestOpenPosition(Vector3 requested, Vector3 fallback)
        {
            Vector3 clamped = ClampToOnlineMap(requested);

            if (IsWalkable(clamped))
            {
                return clamped;
            }

            Vector3 fallbackClamped = ClampToOnlineMap(fallback);

            if (IsWalkable(fallbackClamped))
            {
                return fallbackClamped;
            }

            Vector3[] anchors =
            {
                ScaleMapPosition(new Vector3(0f, -4.35f, 0f)),
                new Vector3(0f, 0f, 0f),
                ScaleMapPosition(new Vector3(-7f, 0f, 0f)),
                ScaleMapPosition(new Vector3(7.25f, 0f, 0f))
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                if (IsWalkable(anchors[i]))
                {
                    return anchors[i];
                }
            }

            for (int ring = 1; ring <= 8; ring++)
            {
                float radius = ring * 0.22f;

                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    Vector3 candidate = ClampToOnlineMap(clamped + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));

                    if (IsWalkable(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return Vector3.zero;
        }

        private bool IsWalkable(Vector3 position)
        {
            if (position.x < -MapHalfWidth + PlayerCollisionRadius
                || position.x > MapHalfWidth - PlayerCollisionRadius
                || position.y < -MapHalfHeight + PlayerCollisionRadius
                || position.y > MapHalfHeight - PlayerCollisionRadius)
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

        private int CountActiveNamedWorldObjects(string prefix)
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;

            foreach (Transform child in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.activeInHierarchy && child.name.StartsWith(prefix, StringComparison.Ordinal))
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
            Suspicion = suspicion;
            IsBot = isBot;
        }

        public ulong ClientId;
        public string DisplayName;
        public Vector3 Position;
        public Vector2 Input;
        public bool Ready;
        public bool Alive;
        public bool IsBot;
        public OnlineRole PublicRole;
        public OnlineProfession Profession;
        public float KillCooldown;
        public float AbilityCooldown;
        public int Suspicion;
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
        Ability
    }

    public enum OnlineProfession
    {
        Inspector,
        Forensics,
        Tech,
        UndercoverAgent,
        Enforcer,
        Fixer,
        Driver
    }
}
