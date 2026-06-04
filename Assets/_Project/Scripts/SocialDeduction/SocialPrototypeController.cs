using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using GanglandUndercover.UI;
using GanglandUndercover.Audio;
using GanglandUndercover.World;
using GanglandUndercover.SocialDeduction.MiniGames;
using GanglandUndercover.Online;
using UnityEngine;

namespace GanglandUndercover.SocialDeduction
{
    public sealed class SocialPrototypeController : MonoBehaviour
    {
        private const float MoveSpeed = 3.6f;
        private const float BotSpeed = 1.45f;
        private const float InteractRange = 1.15f;
        private const float KillRange = 0.9f;
        private const float PlayerKillCooldownSeconds = 8f;
        private const float AiKillCooldownSeconds = 10f;
        private const float BlackoutDurationSeconds = 18f;
        private const float RoundDurationSeconds = 420f;
        private const float BotInteractRange = 0.85f;
        private const float BotTaskCooldownSeconds = 4.5f;
        private const float FootprintIntervalSeconds = 1.05f;
        private const float AudioFootstepIntervalSeconds = 0.4f;
        private const float FootprintLifetimeSeconds = 13f;
        private const float RouteMemorySeconds = 45f;
        private const int MaxRouteEntriesPerCharacter = 5;
        private const int EmergencyMeetingLimit = 2;
        private const int EvidenceTarget = 10;
        private const int MaxUndercoverExposure = 100;
        private const int MaxGangHeat = 100;
        private const float SurveillancePulseSeconds = 5f;
        private const float GroundZ = 0f;
        private const float FloorZ = 0.08f;
        private const float CharacterZ = -0.28f;
        private const float LabelZ = -0.55f;
        private const float CameraFollowDistance = 7.5f;
        private const float CameraFollowHeight = 7.5f;
        private const float CameraTargetZ = -0.15f;

        private readonly List<SocialCharacter> characters = new List<SocialCharacter>();
        private readonly List<TaskStation> taskStations = new List<TaskStation>();
        private readonly List<BodyMarker> bodies = new List<BodyMarker>();
        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private readonly List<Material> generatedMaterials = new List<Material>();
        private readonly List<FootprintTrail> footprintTrails = new List<FootprintTrail>();
        private readonly List<RouteEntry> routeEntries = new List<RouteEntry>();
        private readonly List<NamedZone> zones = new List<NamedZone>();
        private readonly List<SurveillanceNode> surveillanceNodes = new List<SurveillanceNode>();
        private readonly Dictionary<SocialCharacter, int> suspicionScores = new Dictionary<SocialCharacter, int>();
        private readonly Dictionary<SocialCharacter, float> footprintTimers = new Dictionary<SocialCharacter, float>();
        private readonly Dictionary<SocialCharacter, Vector3> lastTracePositions = new Dictionary<SocialCharacter, Vector3>();
        private readonly Dictionary<SocialCharacter, string> lastKnownAreas = new Dictionary<SocialCharacter, string>();
        private readonly List<string> taskChallengeOptions = new List<string>();

        private SocialCharacter player;
        private EmergencyButton emergencyButton;
        private VentSystem ventSystem;
        private EnvironmentManager environmentManager;
        private CriticalTaskSystem criticalTaskSystem;
        private SecurityCamera securityCamera;
        private TaskStation activeTaskChallenge;
        private float playerKillCooldown;
        private float aiKillCooldown;
        private float blackoutTimer;
        private float audioFootstepTimer;
        private float roundTimer;
        private float surveillancePulseTimer;
        private int meetingsCalled;
        private int emergencyMeetingsCalled;
        private int evidenceScore;
        private int undercoverExposure;
        private int gangHeat;
        private int falseLeadCount;
        private int witnessStatementCount;
        private int chaseCount;
        private int activeTaskCorrectOption;
        private GameObject hudObject;
        private SocialCharacter currentPrimarySuspect;
        private bool activeTaskIsSabotage;
        private string taskChallengeTitle = string.Empty;
        private string taskChallengeBody = string.Empty;
        private string latestSurveillanceIntel = string.Empty;
        private MiniGameBase activeMiniGame;

        // --- 离线聊天系统 ---
        private readonly List<ChatMessage> offlineChatMessages = new List<ChatMessage>();
        private string offlineChatInput = string.Empty;
        private Vector2 offlineChatScroll;
        private float offlineChatMessageTimer;
        private int offlineChatMessageIndex;
        private bool offlineChatRoundComplete;
        private ChatSystem offlineChatSystem;

        // --- 回合制策略层 ---
        private GameController turnController;
        private GameObject turnHudObject;
        private GameObject turnMapObject;

        public event Action Changed;

        public GameLanguage Language { get; private set; } = GameLanguage.Chinese;
        public MapType CurrentMapType { get; private set; } = MapType.GanglandDistrict;
        public bool HasStarted { get; private set; }
        public bool IsRoleRevealVisible { get; private set; }
        public bool IsMeeting { get; private set; }
        public bool IsGameOver { get; private set; }
        public SocialRole PlayerRole { get; private set; } = SocialRole.Police;
        public string LastEvent { get; private set; } = "选择身份开始游戏。";
        public string MeetingReason { get; private set; } = string.Empty;
        public string ResultText { get; private set; } = string.Empty;
        public string CurrentClue { get; private set; } = string.Empty;
        public string CaseLog { get; private set; } = string.Empty;
        public IReadOnlyList<SocialCharacter> Characters => characters;
        public int CompletedTasks => taskStations.Count(task => task.IsCompleted);
        public int TotalTasks => taskStations.Count;
        public float PlayerKillCooldown => playerKillCooldown;
        public bool IsBlackout => blackoutTimer > 0f;
        public float BlackoutTimer => blackoutTimer;
        public float RoundTimer => roundTimer;
        public int MeetingsCalled => meetingsCalled;
        public int EmergencyMeetingsCalled => emergencyMeetingsCalled;
        public int EmergencyMeetingLimitValue => EmergencyMeetingLimit;
        public int EvidenceScore => evidenceScore;
        public int EvidenceTargetValue => EvidenceTarget;
        public int UndercoverExposure => undercoverExposure;
        public int MaxUndercoverExposureValue => MaxUndercoverExposure;
        public int GangHeat => gangHeat;
        public int MaxGangHeatValue => MaxGangHeat;
        public int FalseLeadCount => falseLeadCount;
        public int WitnessStatementCount => witnessStatementCount;
        public int ChaseCount => chaseCount;
        public bool IsTaskChallengeVisible => activeTaskChallenge != null || activeMiniGame != null;
        public string TaskChallengeTitle => taskChallengeTitle;
        public string TaskChallengeBody => taskChallengeBody;
        public IReadOnlyList<string> TaskChallengeOptions => taskChallengeOptions;
        public string InteractionPrompt => BuildInteractionPrompt();
        public string RouteIntel => BuildRouteIntel();
        public string TaskChecklist => BuildTaskChecklist();
        public string RosterSummary => BuildRosterSummary();
        public string CaseBoard => BuildCaseBoard();
        public string SuspectBoard => BuildSuspectBoard();
        public string SurveillanceIntel => latestSurveillanceIntel;
        public string SpecialActionPrompt => BuildSpecialActionPrompt();
        public string RoleBrief => BuildRoleBrief();
        public string GoalBrief => BuildGoalBrief();
        public int ActiveFootprintCount => footprintTrails.Count;

        /// <summary>
        /// 由 Bootstrap 控制：false 时 Awake 不自动启动游戏。
        /// </summary>
        public bool AutoStartOnAwake { get; set; } = true;

        private void Awake()
        {
            BuildHud();
            InitTurnController();
            InitTurnHud();

            if (AutoStartOnAwake)
            {
                StartGame(SocialRole.Undercover);
            }
        }

        /// <summary>
        /// Bootstrap 入口：由 PrototypeBootstrap 在 Offline 模式下调用。
        /// </summary>
        public void StartOfflineMode(SocialRole role)
        {
            if (HasStarted)
            {
                return;
            }

            StartGame(role);
        }

        /// <summary>
        /// 设置当前地图类型。必须在 StartGame 前调用。
        /// </summary>
        public void SetMapType(MapType mapType)
        {
            if (HasStarted)
            {
                Debug.LogWarning("[SocialPrototypeController] 游戏已开始，地图类型不可更改。");
                return;
            }

            CurrentMapType = mapType;
        }

        private void OnDestroy()
        {
            ClearWorld();

            if (turnMapObject != null)
            {
                DestroyGenerated(turnMapObject);
                turnMapObject = null;
            }

            if (turnHudObject != null)
            {
                DestroyGenerated(turnHudObject);
                turnHudObject = null;
            }

            if (hudObject != null)
            {
                DestroyGenerated(hudObject);
                hudObject = null;
            }
        }

        private void Update()
        {
            if (activeTaskChallenge != null || activeMiniGame != null)
            {
                if (activeMiniGame == null)
                {
                    HandleTaskChallengeInput();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    // Esc 取消小游戏
                    CancelMiniGame();
                }
                FollowCamera();
                return;
            }

            if (!HasStarted || IsRoleRevealVisible || IsMeeting || IsGameOver)
            {
                FollowCamera();
                return;
            }

            TickCooldowns();
            TickRoundTimer();
            MovePlayer();
            MoveBots();
            TickEvidenceTrails();
            TickSurveillance();

            if (TryBotReportBodies())
            {
                return;
            }

            TryBotTaskActions();
            TryAiGangKill();
            TickVentSystem();
            TickSecurityCamera();
            TickCriticalTaskSystem();
            HandleInput();
            UpdateOfflineChat();
            FollowCamera();
        }

        public void ToggleLanguage()
        {
            Language = Language == GameLanguage.Chinese ? GameLanguage.English : GameLanguage.Chinese;
            LastEvent = Language == GameLanguage.Chinese ? "语言已切换为中文。" : "Language switched to English.";
            Changed?.Invoke();
        }

        public void StartGame(SocialRole role)
        {
            EnsureRuntimeScaffolding();
            ClearWorld();
            PlayerRole = role;
            HasStarted = true;
            IsRoleRevealVisible = true;
            IsMeeting = false;
            IsGameOver = false;
            ResultText = string.Empty;
            MeetingReason = string.Empty;
            activeTaskChallenge = null;
            activeTaskIsSabotage = false;
            taskChallengeTitle = string.Empty;
            taskChallengeBody = string.Empty;
            taskChallengeOptions.Clear();
            playerKillCooldown = 0f;
            aiKillCooldown = AiKillCooldownSeconds;
            blackoutTimer = 0f;
            roundTimer = RoundDurationSeconds;
            surveillancePulseTimer = SurveillancePulseSeconds;
            meetingsCalled = 0;
            emergencyMeetingsCalled = 0;
            evidenceScore = 0;
            undercoverExposure = role == SocialRole.Undercover ? 18 : 0;
            gangHeat = role == SocialRole.Gang ? 12 : 18;
            falseLeadCount = 0;
            witnessStatementCount = 0;
            chaseCount = 0;
            currentPrimarySuspect = null;
            CurrentClue = string.Empty;
            latestSurveillanceIntel = "技侦频道待命。摄像头会周期性记录可疑路线。";
            CaseLog = string.Empty;

            BuildWorld();
            LastEvent = role == SocialRole.Gang
                ? "你是黑帮成员。伪装巡逻、制造断电、阻止专案组收网。"
                : role == SocialRole.Undercover
                    ? "你是潜伏探员。完成取证任务，报告倒下的人，别暴露路线。"
                    : role == SocialRole.Mole
                        ? "你是黑帮线人。伪装为警方技侦，暗中收集卧底情报。"
                        : "你是专案警员。完成取证任务，报告尸体，找出黑帮线人。";
            Changed?.Invoke();
        }

        public void BeginRound()
        {
            if (!HasStarted || IsGameOver)
            {
                return;
            }

            IsRoleRevealVisible = false;
            LastEvent = PlayerRole == SocialRole.Gang
                ? "行动开始。靠近目标按 Q，E 破坏，F 伪造证词，C 反侦察。"
                : PlayerRole == SocialRole.Undercover
                    ? "行动开始。E 取证，F 接头传证，C 调监控；小心暴露值。"
                    : PlayerRole == SocialRole.Mole
                        ? "行动开始。E 跟踪调查，F 潜入档案，C 秘密接头；保持伪装。"
                        : "行动开始。E 取证，F 封锁追捕，C 调监控，发现尸体按 R。";
            AddCaseLog("开局", RoleName(PlayerRole) + " 进入港区。");
            Changed?.Invoke();
        }

        public void CastVote(SocialCharacter target)
        {
            if (!IsMeeting || target == null || !target.IsAlive)
            {
                return;
            }

            string voterResult = target.CharacterName + " 被投出局。身份是：" + RoleName(target.Role) + "。";
            target.Kill();
            IsMeeting = false;
            RemoveBodiesFor(target);
            CurrentClue = string.Empty;
            currentPrimarySuspect = null;
            LastEvent = voterResult;
            AddCaseLog("会议投票", voterResult);

            if (target.IsPlayer)
            {
                FinishGame(PlayerRole == SocialRole.Gang ? "警方胜利：你的黑帮身份被投出局。" : "行动失败：你被投出局，港区收网失去关键执行人。");
                Changed?.Invoke();
                return;
            }

            CheckVictory();
            Changed?.Invoke();
        }

        public void SkipVote()
        {
            if (!IsMeeting)
            {
                return;
            }

            IsMeeting = false;
            CurrentClue = string.Empty;
            currentPrimarySuspect = null;
            LastEvent = "会议无结果，所有人继续行动。";
            AddCaseLog("会议投票", "跳过投票。");
            Changed?.Invoke();
        }

        public void ResolveAutoVote()
        {
            if (!IsMeeting)
            {
                return;
            }

            SocialCharacter suspect = PickAutoSuspect();

            if (suspect == null)
            {
                SkipVote();
                return;
            }

            CastVote(suspect);
        }

        public void ResolveTaskChallenge(int optionIndex)
        {
            if (activeTaskChallenge == null)
            {
                return;
            }

            bool success = optionIndex == activeTaskCorrectOption;
            TaskStation task = activeTaskChallenge;
            bool sabotage = activeTaskIsSabotage;
            activeTaskChallenge = null;
            activeTaskIsSabotage = false;
            taskChallengeTitle = string.Empty;
            taskChallengeBody = string.Empty;
            taskChallengeOptions.Clear();

            if (sabotage)
            {
                ResolveSabotageChallenge(task, success);
            }
            else
            {
                ResolveEvidenceChallenge(task, success);
            }

            Changed?.Invoke();
        }

        private void TickCooldowns()
        {
            bool shouldNotify = false;

            if (playerKillCooldown > 0f)
            {
                playerKillCooldown -= Time.deltaTime;
                shouldNotify = true;
            }

            if (aiKillCooldown > 0f)
            {
                aiKillCooldown -= Time.deltaTime;
            }

            if (blackoutTimer > 0f)
            {
                blackoutTimer -= Time.deltaTime;

                if (blackoutTimer <= 0f)
                {
                    blackoutTimer = 0f;
                    LastEvent = "断电结束，视野恢复。";
                    shouldNotify = true;

                    // 通知 EnvironmentManager 恢复灯光
                    if (environmentManager != null)
                    {
                        environmentManager.SetBlackout(false);
                    }
                }
            }

            if (shouldNotify)
            {
                Changed?.Invoke();
            }
        }

        private void TickRoundTimer()
        {
            if (roundTimer <= 0f)
            {
                return;
            }

            roundTimer -= Time.deltaTime;

            if (roundTimer <= 0f)
            {
                roundTimer = 0f;
                FinishGame("黑帮胜利：专案组错过收网窗口，港区证据链断裂。");
                Changed?.Invoke();
            }
        }

        private void MovePlayer()
        {
            if (player == null || !player.IsAlive)
            {
                return;
            }

            Vector3 direction = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f);

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            player.transform.position = ClampToMap(player.transform.position + direction * MoveSpeed * Time.deltaTime);

            // Audio footstep (0.4s interval, spatialized)
            if (direction.sqrMagnitude > 0.01f)
            {
                audioFootstepTimer -= Time.deltaTime;
                if (audioFootstepTimer <= 0f)
                {
                    audioFootstepTimer = AudioFootstepIntervalSeconds;
                    AudioManager.Instance?.PlaySFXAtPoint(SoundEffect.Footstep, player.transform.position);
                }
            }
            else
            {
                audioFootstepTimer = 0f;
            }
        }

        private void MoveBots()
        {
            foreach (SocialCharacter character in characters)
            {
                if (character.IsPlayer || !character.IsAlive)
                {
                    continue;
                }

                character.BotDecisionTimer -= Time.deltaTime;
                character.BotActionCooldown -= Time.deltaTime;

                if (character.BotDecisionTimer <= 0f)
                {
                    PickBotTarget(character);
                }

                if (!character.HasBotTarget || Vector3.Distance(character.transform.position, character.BotTarget) < 0.2f)
                {
                    PickBotTarget(character);
                }

                Vector3 direction = character.BotTarget - character.transform.position;

                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();
                }

                float speed = IsBlackout ? BotSpeed * 0.75f : BotSpeed;
                character.transform.position = ClampToMap(character.transform.position + direction * speed * Time.deltaTime);
            }
        }

        private void TickEvidenceTrails()
        {
            for (int i = footprintTrails.Count - 1; i >= 0; i--)
            {
                FootprintTrail trail = footprintTrails[i];

                if (trail == null)
                {
                    footprintTrails.RemoveAt(i);
                    continue;
                }

                trail.RemainingSeconds -= Time.deltaTime;
                float normalizedLifetime = Mathf.Clamp01(trail.RemainingSeconds / FootprintLifetimeSeconds);
                trail.Refresh(normalizedLifetime);

                if (trail.RemainingSeconds <= 0f)
                {
                    footprintTrails.RemoveAt(i);
                    DestroyGenerated(trail.gameObject);
                }
            }

            routeEntries.RemoveAll(entry => roundTimer > 0f && entry.RoundTime - roundTimer > RouteMemorySeconds);

            foreach (SocialCharacter character in characters)
            {
                if (!character.IsAlive)
                {
                    continue;
                }

                TrackCharacterRoute(character);
            }
        }

        private void TickSurveillance()
        {
            surveillancePulseTimer -= Time.deltaTime;

            if (surveillancePulseTimer > 0f)
            {
                return;
            }

            surveillancePulseTimer = SurveillancePulseSeconds;
            PulseSurveillance(false);
        }

        private void TrackCharacterRoute(SocialCharacter character)
        {
            Vector3 currentPosition = character.transform.position;

            if (!lastTracePositions.TryGetValue(character, out Vector3 lastPosition))
            {
                lastTracePositions[character] = currentPosition;
                lastKnownAreas[character] = GetAreaName(currentPosition);
                RecordRoute(character, lastKnownAreas[character], currentPosition);
                return;
            }

            float movedDistance = Vector3.Distance(currentPosition, lastPosition);
            string areaName = GetAreaName(currentPosition);
            lastKnownAreas.TryGetValue(character, out string previousArea);

            if (movedDistance >= 0.28f)
            {
                footprintTimers.TryGetValue(character, out float footprintTimer);
                footprintTimer -= Time.deltaTime;

                if (footprintTimer <= 0f)
                {
                    CreateFootprint(character, currentPosition);
                    footprintTimer = FootprintIntervalSeconds;
                }

                footprintTimers[character] = footprintTimer;
                lastTracePositions[character] = currentPosition;
            }

            if (previousArea != areaName)
            {
                lastKnownAreas[character] = areaName;
                RecordRoute(character, areaName, currentPosition);
            }
        }

        private void RecordRoute(SocialCharacter character, string areaName, Vector3 position)
        {
            routeEntries.RemoveAll(entry => entry.Character == character && entry.AreaName == areaName);
            routeEntries.Add(new RouteEntry(character, areaName, position, roundTimer));

            List<RouteEntry> entriesForCharacter = routeEntries
                .Where(entry => entry.Character == character)
                .OrderByDescending(entry => entry.RoundTime)
                .ToList();

            for (int i = MaxRouteEntriesPerCharacter; i < entriesForCharacter.Count; i++)
            {
                routeEntries.Remove(entriesForCharacter[i]);
            }
        }

        private void PulseSurveillance(bool forced)
        {
            if (surveillanceNodes.Count == 0 || IsBlackout && !forced)
            {
                return;
            }

            SurveillanceNode node = surveillanceNodes
                .OrderBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();

            if (node == null)
            {
                return;
            }

            SocialCharacter observed = characters
                .Where(character => character.IsAlive && Vector3.Distance(character.transform.position, node.Position) <= node.Radius)
                .OrderBy(character => character.Role == SocialRole.Gang ? 0 : 1)
                .ThenBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();

            if (observed == null)
            {
                latestSurveillanceIntel = node.NodeName + " 没拍到可确认目标，只记录到雨夜人流。";
                return;
            }

            int suspicionGain = observed.Role == SocialRole.Gang ? 10 : 4;
            AddSuspicion(observed, suspicionGain, "监控");
            latestSurveillanceIntel = node.NodeName + " 拍到 " + observed.CharacterName + " 出现在 " + GetAreaName(observed.transform.position) + "；近期路线：" + BuildCharacterRoute(observed) + "。";

            if (forced)
            {
                CurrentClue = "监控线索：" + latestSurveillanceIntel;
            }
        }

        private void CreateFootprint(SocialCharacter character, Vector3 position)
        {
            GameObject footprintObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            footprintObject.name = character.CharacterName + " Footprint";
            generatedObjects.Add(footprintObject);
            footprintObject.transform.position = new Vector3(position.x, position.y, FloorZ - 0.28f);
            footprintObject.transform.localScale = new Vector3(0.14f, 0.012f, 0.24f);
            footprintObject.transform.rotation = Quaternion.Euler(90f, 0f, UnityEngine.Random.Range(-24f, 24f));

            Color color = character.Role == SocialRole.Gang
                ? new Color(0.72f, 0.22f, 0.16f, 0.52f)
                : new Color(0.46f, 0.58f, 0.72f, 0.42f);
            SetColor(footprintObject, color);

            FootprintTrail trail = footprintObject.AddComponent<FootprintTrail>();
            trail.Bind(character, FootprintLifetimeSeconds);
            footprintTrails.Add(trail);
        }

        private void PickBotTarget(SocialCharacter character)
        {
            Vector3 target;

            if (character.Role == SocialRole.Gang)
            {
                target = PickGangTarget(character);
            }
            else
            {
                target = PickInvestigatorTarget(character);
            }

            Vector3 clamped = ClampToMap(target);
            character.BotTarget = new Vector3(clamped.x, clamped.y, character.transform.position.z);
            character.HasBotTarget = true;
            character.BotDecisionTimer = UnityEngine.Random.Range(2.0f, 4.2f);
        }

        private Vector3 PickGangTarget(SocialCharacter character)
        {
            SocialCharacter victim = characters
                .Where(other => other.IsAlive && other.Role != SocialRole.Gang && other != character)
                .OrderBy(other => Vector3.Distance(character.transform.position, other.transform.position))
                .FirstOrDefault();

            if (victim != null && aiKillCooldown <= 2f && UnityEngine.Random.value > 0.35f)
            {
                return victim.transform.position;
            }

            TaskStation task = taskStations
                .Where(station => !station.IsCompleted || !station.IsSabotaged)
                .OrderBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();

            return task != null ? task.transform.position : RandomMapPoint();
        }

        private Vector3 PickInvestigatorTarget(SocialCharacter character)
        {
            BodyMarker body = bodies
                .Where(marker => marker != null)
                .OrderBy(marker => Vector3.Distance(character.transform.position, marker.transform.position))
                .FirstOrDefault();

            if (body != null && UnityEngine.Random.value > 0.35f)
            {
                return body.transform.position;
            }

            TaskStation task = taskStations
                .Where(station => !station.IsCompleted)
                .OrderBy(station => Vector3.Distance(character.transform.position, station.transform.position) + UnityEngine.Random.Range(0f, 2f))
                .FirstOrDefault();

            return task != null ? task.transform.position : RandomMapPoint();
        }

        private bool TryBotReportBodies()
        {
            foreach (SocialCharacter character in characters)
            {
                if (character.IsPlayer || !character.IsAlive || character.Role == SocialRole.Gang)
                {
                    continue;
                }

                BodyMarker body = bodies
                    .Where(marker => marker != null && Vector3.Distance(character.transform.position, marker.transform.position) <= BotInteractRange)
                    .OrderBy(marker => Vector3.Distance(character.transform.position, marker.transform.position))
                    .FirstOrDefault();

                if (body == null)
                {
                    continue;
                }

                bodies.Remove(body);
                string victimName = body.Victim.CharacterName;
                DestroyGenerated(body.gameObject);
                StartMeeting(character.CharacterName + " 发现 " + victimName + " 的尸体。");
                return true;
            }

            return false;
        }

        private void TryBotTaskActions()
        {
            foreach (SocialCharacter character in characters)
            {
                if (character.IsPlayer || !character.IsAlive || character.BotActionCooldown > 0f)
                {
                    continue;
                }

                TaskStation task = taskStations
                    .Where(station => Vector3.Distance(character.transform.position, station.transform.position) <= BotInteractRange)
                    .OrderBy(station => Vector3.Distance(character.transform.position, station.transform.position))
                    .FirstOrDefault();

                if (task == null)
                {
                    continue;
                }

                character.BotActionCooldown = BotTaskCooldownSeconds + UnityEngine.Random.Range(0.5f, 2.5f);

                if (character.Role == SocialRole.Gang)
                {
                    task.Sabotage();
                    TriggerBlackout();
                    LastEvent = "有人破坏了 " + task.TaskName + "，港区短暂断电。";
                    AddCaseLog("破坏", task.TaskName + " 被破坏。");
                    Changed?.Invoke();
                    return;
                }

                if (!task.IsCompleted)
                {
                    task.Work();
                    LastEvent = character.CharacterName + " 正在处理 " + task.TaskName + "。";
                    AddCaseLog("取证", character.CharacterName + " 推进了 " + task.TaskName + "。");
                    CheckVictory();
                    Changed?.Invoke();
                    return;
                }
            }
        }

        private void TryAiGangKill()
        {
            if (aiKillCooldown > 0f)
            {
                return;
            }

            SocialCharacter gang = characters.FirstOrDefault(character => !character.IsPlayer && character.IsAlive && character.Role == SocialRole.Gang);

            if (gang == null)
            {
                return;
            }

            SocialCharacter target = characters
                .Where(character => character.IsAlive && character.Role != SocialRole.Gang && Vector3.Distance(gang.transform.position, character.transform.position) <= KillRange)
                .OrderBy(character => Vector3.Distance(gang.transform.position, character.transform.position))
                .FirstOrDefault();

            if (target == null)
            {
                if (!IsBlackout)
                {
                    TriggerBlackout();
                    aiKillCooldown = AiKillCooldownSeconds * 0.7f;
                    LastEvent = "灯灭了。黑帮可能在利用断电转移路线。";
                    Changed?.Invoke();
                }

                return;
            }

            KillCharacter(target);
            aiKillCooldown = AiKillCooldownSeconds;
            gangHeat = Mathf.Min(MaxGangHeat, gangHeat + 18);
            LastEvent = "有人倒下了。找到尸体后按 R 报告。";
            CheckVictory();
            Changed?.Invoke();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                // 查看监控中 → E 退出
                if (securityCamera != null && securityCamera.IsViewing)
                {
                    securityCamera.DeactivateViewing();
                    LastEvent = "退出监控查看。";
                    Changed?.Invoke();
                    return;
                }

                TryInteract();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryPlayerKill();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryReportBody();
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                TryRoleAction();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                TrySurveillanceAction();
            }

            if (Input.GetKeyDown(KeyCode.V))
            {
                // 监控站优先：如果玩家在监控站附近 → 查看摄像头
                if (securityCamera != null && securityCamera.IsPlayerNearMonitor(player.transform.position))
                {
                    if (securityCamera.IsViewing)
                    {
                        string result = securityCamera.CycleNextView();
                        LastEvent = result;
                        Changed?.Invoke();
                    }
                    else
                    {
                        string result = securityCamera.ActivateViewing();
                        LastEvent = result;
                        Changed?.Invoke();
                    }
                    return;
                }

                // 否则走通风管
                TryVentAction();
            }
        }

        private void TickSecurityCamera()
        {
            if (securityCamera == null) return;
            if (!HasStarted || IsMeeting || IsGameOver) return;

            // 检测各摄像头的 Impostor 视野
            securityCamera.TickDetection(characters);

            // 如果玩家离开监控站范围 → 自动退出查看
            if (securityCamera.IsViewing && player != null && player.IsAlive)
            {
                if (!securityCamera.IsPlayerNearMonitor(player.transform.position))
                {
                    securityCamera.DeactivateViewing();
                }
            }
        }

        private void TryInteract()
        {
            if (player == null || !player.IsAlive)
            {
                return;
            }

            TaskStation task = FindNearestTask();

            if (task != null)
            {
                if (PlayerRole == SocialRole.Gang)
                {
                    StartTaskChallenge(task, true);
                }
                else if (!task.IsCompleted)
                {
                    StartTaskChallenge(task, false);
                }
                else
                {
                    LastEvent = "这个任务已经完成。";
                }

                Changed?.Invoke();
                return;
            }

            if (emergencyButton != null && Vector3.Distance(player.transform.position, emergencyButton.transform.position) <= InteractRange)
            {
                if (emergencyMeetingsCalled >= EmergencyMeetingLimit)
                {
                    LastEvent = "紧急会议次数已用完。";
                    Changed?.Invoke();
                    return;
                }

                emergencyMeetingsCalled++;
                StartMeeting("紧急会议被按下。");
                return;
            }

            LastEvent = "附近没有可交互目标。靠近任务点、紧急按钮或尸体。";
            Changed?.Invoke();
        }

        private void TryPlayerKill()
        {
            if (PlayerRole != SocialRole.Gang && PlayerRole != SocialRole.Mole)
            {
                LastEvent = "只有黑帮可以击倒目标。";
                Changed?.Invoke();
                return;
            }

            if (playerKillCooldown > 0f)
            {
                LastEvent = "击倒冷却中。";
                Changed?.Invoke();
                return;
            }

            SocialCharacter target = characters
                .Where(character => !character.IsPlayer && character.IsAlive && character.Role != SocialRole.Gang && Vector3.Distance(player.transform.position, character.transform.position) <= KillRange)
                .OrderBy(character => Vector3.Distance(player.transform.position, character.transform.position))
                .FirstOrDefault();

            if (target == null)
            {
                LastEvent = "附近没有可击倒目标。";
                Changed?.Invoke();
                return;
            }

            KillCharacter(target);
            playerKillCooldown = PlayerKillCooldownSeconds;
            gangHeat = Mathf.Min(MaxGangHeat, gangHeat + 16);
            AddSuspicion(player, 14, "击倒");
            LastEvent = "你击倒了 " + target.CharacterName + "。尽快离开现场。";
            AddCaseLog("倒下", target.CharacterName + " 在港区失联。");
            CheckVictory();
            Changed?.Invoke();
        }

        private void TryReportBody()
        {
            BodyMarker body = bodies
                .Where(marker => marker != null && Vector3.Distance(player.transform.position, marker.transform.position) <= InteractRange)
                .OrderBy(marker => Vector3.Distance(player.transform.position, marker.transform.position))
                .FirstOrDefault();

            if (body == null)
            {
                LastEvent = "附近没有尸体可报告。";
                Changed?.Invoke();
                return;
            }

            bodies.Remove(body);
            string victimName = body.Victim.CharacterName;
            DestroyGenerated(body.gameObject);
            AudioManager.Instance?.PlaySFX(SoundEffect.BodyReport);
            StartMeeting("发现 " + victimName + " 的尸体。");
        }

        private void HandleTaskChallengeInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ResolveTaskChallenge(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ResolveTaskChallenge(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ResolveTaskChallenge(2);
            }
        }

        // ──────────────────────────────────────────────
        //  离线聊天系统
        // ──────────────────────────────────────────────

        private void UpdateOfflineChat()
        {
            if (!IsMeeting || offlineChatSystem == null) return;

            // AI 自动发送预设消息（模拟多人讨论）
            offlineChatMessageTimer -= Time.deltaTime;
            if (offlineChatMessageTimer <= 0f && !offlineChatRoundComplete)
            {
                SendNextAiChatMessage();
                offlineChatMessageTimer = 3.5f + UnityEngine.Random.Range(-0.5f, 0.5f);
            }
        }

        /// <summary>
        /// 发送下一条 AI 预设消息。
        /// </summary>
        private void SendNextAiChatMessage()
        {
            if (offlineChatSystem == null) return;

            string[] chinesePool =
            {
                "我觉得我们应该查一下监控录像，看看谁的路线有问题。",
                "有人看到可疑人物在货柜码头附近吗？",
                "我注意到昨晚夜市巷有异常活动。",
                "证物库那边好像被翻动过，有人承认吗？",
                "专案办公室的情报显示黑帮有内应。",
                "我昨天在主街看到有人鬼鬼祟祟的。",
                "地下诊所的登记记录对不上，谁去过那里？",
                "我建议先查一下每个人的任务完成情况。",
                "侦探日志里提到货柜码头有异常交易记录。",
                "证据链断了几条，说明有人在干扰调查方向。",
            };

            string[] englishPool =
            {
                "I think we should check the surveillance footage — someone's route looks off.",
                "Has anyone seen anything unusual near the Dockyard?",
                "I noticed some strange activity around the Night Market last night.",
                "The Evidence Warehouse looks tampered with. Anyone want to admit it?",
                "Intel from the Police Precinct suggests there's a mole in the gang.",
                "I saw someone acting shady on Main Street yesterday.",
                "The underground clinic's records don't add up. Who's been there?",
                "I suggest we check everyone's task completion first.",
                "The detective log mentions unusual transactions at the Dockyard.",
                "Several evidence chains are broken — someone is interfering with the investigation.",
            };

            string[] pool = Language == GameLanguage.Chinese ? chinesePool : englishPool;

            int index = offlineChatMessageIndex;
            string content;

            if (index >= pool.Length)
            {
                content = pool[UnityEngine.Random.Range(0, pool.Length)];
            }
            else
            {
                content = pool[index];
            }

            offlineChatMessageIndex++;

            // 从存活角色中选一个非玩家发言人
            SocialCharacter speaker = null;
            foreach (SocialCharacter character in characters)
            {
                if (!character.IsAlive || character.IsPlayer) continue;
                speaker = character;
                break;
            }

            if (speaker == null) return;

            Faction speakerFaction = GetFaction(speaker.Role);
            offlineChatSystem.ReceiveMessage(
                speaker.CharacterName,
                speaker.CharacterName,
                content,
                false,
                speakerFaction);
        }

        /// <summary>
        /// 离线聊天发送回调（玩家输入发送）。
        /// </summary>
        private void OnOfflineChatSend(string content)
        {
            if (!IsMeeting || offlineChatSystem == null || player == null) return;

            Faction faction = GetFaction(PlayerRole);
            offlineChatSystem.ReceiveMessage(
                "local",
                player.CharacterName,
                content,
                false,
                faction);
        }

        /// <summary>
        /// 离线模式聊天 GUI 渲染 —— 使用 ChatSystem 绘制聊天面板。
        /// </summary>
        private void OnGUI()
        {
            if (!IsMeeting || offlineChatSystem == null) return;

            float chatWidth = Screen.width * 0.27f;
            float chatHeight = Screen.height * 0.34f;
            Rect chatArea = new Rect(
                Screen.width - chatWidth - 18f,
                Screen.height * 0.62f,
                chatWidth,
                chatHeight);

            offlineChatSystem.ProcessInputKeys();
            offlineChatSystem.DrawChatPanel(chatArea, GUI.skin);
        }

        private void StartTaskChallenge(TaskStation task, bool sabotage)
        {
            activeTaskChallenge = task;
            activeTaskIsSabotage = sabotage;

            // 尝试启动 MiniGame（优先于文本多选）
            Type miniGameType = PickMiniGameType(task.TaskName);
            if (miniGameType != null)
            {
                GameObject miniGameObj = new GameObject("MiniGame_" + task.TaskName);
                activeMiniGame = (MiniGameBase)miniGameObj.AddComponent(miniGameType);
                activeMiniGame.OnComplete += OnMiniGameComplete;
                activeMiniGame.OnCancel += OnMiniGameCancel;
                activeMiniGame.Show();

                LastEvent = "正在处理：" + task.TaskName + "（Esc 取消）。";
                Changed?.Invoke();
                return;
            }

            // 回退到文本多选
            activeTaskCorrectOption = UnityEngine.Random.Range(0, 3);
            taskChallengeOptions.Clear();

            string areaName = GetAreaName(task.transform.position);
            string correct;
            string wrongA;
            string wrongB;

            if (sabotage)
            {
                taskChallengeTitle = "黑帮反侦察：" + task.TaskName;
                taskChallengeBody = "选择最不容易留下铁证的做法。按 1 / 2 / 3。";
                correct = "切断 " + areaName + " 的备用电源，再转移现场记录";
                wrongA = "当众砸毁设备后直接离开";
                wrongB = "留下同伴名字制造恐吓标记";
            }
            else
            {
                taskChallengeTitle = "警方案件任务：" + task.TaskName;
                taskChallengeBody = "选择最能推进证据链的动作。按 1 / 2 / 3。";
                correct = BuildCorrectEvidenceOption(task.TaskName, areaName);
                wrongA = "只记录传闻，不核对时间和地点";
                wrongB = "强行收队，跳过证物封存流程";
            }

            for (int i = 0; i < 3; i++)
            {
                if (i == activeTaskCorrectOption)
                {
                    taskChallengeOptions.Add(correct);
                }
                else if (!taskChallengeOptions.Contains(wrongA))
                {
                    taskChallengeOptions.Add(wrongA);
                }
                else
                {
                    taskChallengeOptions.Add(wrongB);
                }
            }

            LastEvent = "正在处理：" + task.TaskName + "。";
            Changed?.Invoke();
        }

        private string BuildCorrectEvidenceOption(string taskName, string areaName)
        {
            if (taskName.Contains("监控"))
            {
                return "比对 " + areaName + " 摄像头时间码和最后出现人员";
            }

            if (taskName.Contains("货柜"))
            {
                return "核对封条编号、货单和码头出入记录";
            }

            if (taskName.Contains("电闸"))
            {
                return "恢复供电后保存断电前后的门禁日志";
            }

            if (taskName.Contains("证物"))
            {
                return "扫描指纹、封存袋编号和血迹方向";
            }

            return "上传原始档案并锁定修改记录";
        }

        private void ResolveEvidenceChallenge(TaskStation task, bool success)
        {
            if (success)
            {
                task.Work();
                int gain = task.IsCompleted ? 3 : 2;
                if (task.IsCompleted)
                {
                    AudioManager.Instance?.PlaySFX(SoundEffect.TaskComplete);
                }
                evidenceScore = Mathf.Min(EvidenceTarget, evidenceScore + gain);
                witnessStatementCount += task.TaskName.Contains("监控") ? 0 : 1;
                gangHeat = Mathf.Min(MaxGangHeat, gangHeat + 4);

                if (IsBlackout && task.TaskName.Contains("电闸"))
                {
                    blackoutTimer = 0f;
                    LastEvent = "你修复电闸并保存门禁日志，断电结束。证据链 +" + gain + "。";
                    AddCaseLog("维修", LastEvent);
                }
                else
                {
                    LastEvent = task.IsCompleted
                        ? task.TaskName + " 完成，证据链 +" + gain + "。"
                        : task.TaskName + " 推进，证据链 +" + gain + "。";
                    AddCaseLog("取证", LastEvent);
                }
            }
            else
            {
                task.Work();
                evidenceScore = Mathf.Min(EvidenceTarget, evidenceScore + 1);
                falseLeadCount++;
                LastEvent = "流程有瑕疵，只得到弱线索。证据链 +1，假线索 +1。";
                AddCaseLog("弱线索", task.TaskName + " 产生弱线索。");
            }

            if (PlayerRole == SocialRole.Undercover)
            {
                undercoverExposure = Mathf.Min(MaxUndercoverExposure, undercoverExposure + (success ? 5 : 9));
            }

            CheckVictory();
        }

        private void ResolveSabotageChallenge(TaskStation task, bool success)
        {
            task.Sabotage();
            TriggerBlackout();
            AudioManager.Instance?.PlaySFX(SoundEffect.Sabotage);

            if (success)
            {
                falseLeadCount++;
                evidenceScore = Mathf.Max(0, evidenceScore - 2);
                gangHeat = Mathf.Max(0, gangHeat - 10);
                LastEvent = "破坏成功且痕迹很轻，证据链 -2，黑帮热度下降。";
                AddCaseLog("精准破坏", task.TaskName + " 被悄悄处理。");
            }
            else
            {
                AddSuspicion(player, 18, "破坏失误");
                gangHeat = Mathf.Min(MaxGangHeat, gangHeat + 12);
                evidenceScore = Mathf.Max(0, evidenceScore - 1);
                LastEvent = "破坏完成但留下明显痕迹，黑帮热度上升。";
                AddCaseLog("粗糙破坏", task.TaskName + " 留下可疑痕迹。");
            }

            CheckVictory();
        }

        // ────────────── MiniGame 集成 ──────────────

        /// <summary>
        /// 根据任务名称分配合适的小游戏类型。
        /// </summary>
        private static System.Type PickMiniGameType(string taskName)
        {
            // ── 警察局地图任务映射 ──
            MiniGameType? policeType = PoliceStationTasks.GetMiniGameType(taskName);
            if (policeType.HasValue)
            {
                switch (policeType.Value)
                {
                    case MiniGameType.SortTask:            return typeof(SortTask);
                    case MiniGameType.ScanTask:            return typeof(ScanTask);
                    case MiniGameType.TapTask:             return typeof(TapTask);
                    case MiniGameType.KeypadTask:          return typeof(KeypadTask);
                    case MiniGameType.EvidenceArchiveTask: return typeof(EvidenceArchiveTask);
                    default: return typeof(SortTask);
                }
            }

            // ── 连线类 ──
            if (taskName.Contains("货柜") || taskName.Contains("电闸"))
            {
                return typeof(WireTask); // 连线：连接货柜封条 / 修复电路
            }

            // ── 记忆类 ──
            if (taskName.Contains("监控"))
            {
                return typeof(MemoryTask); // 记忆：记住摄像头画面特征
            }

            // ── 刷卡/扫描类 ──
            if (taskName.Contains("证物") || taskName.Contains("档案"))
            {
                return typeof(SwipeCardTask); // 刷卡：扫描证物 / 上传档案
            }

            // ── 密码键盘类 ──
            if (taskName.Contains("密码") || taskName.Contains("保险箱") || taskName.Contains("门禁"))
            {
                return typeof(KeypadTask); // 数字键盘：输入4位密码
            }

            // ── 分类排序类 ──
            if (taskName.Contains("分类") || taskName.Contains("垃圾") || taskName.Contains("归档") || taskName.Contains("整理"))
            {
                return typeof(SortTask); // 拖拽分类：将物品拖入正确槽位
            }

            // ── 扫描类 ──
            if (taskName.Contains("扫描") || taskName.Contains("体检") || taskName.Contains("化验") || taskName.Contains("MedBay"))
            {
                return typeof(ScanTask); // 圆形扫描：在绿色区域停止
            }

            // ── 快速点击类 ──
            if (taskName.Contains("点击") || taskName.Contains("反应") || taskName.Contains("射击") || taskName.Contains("校准"))
            {
                return typeof(TapTask); // 快速点击：限时点击全部目标
            }

            // ── 新增3种小游戏映射 ──
            // 航向校准
            if (taskName.Contains("航向") || taskName.Contains("校准") || taskName.Contains("校准仪"))
            {
                return typeof(CalibrateTask); // 十字准星校准
            }

            // 清理陨石
            if (taskName.Contains("陨石") || taskName.Contains("太空") || taskName.Contains("碎片"))
            {
                return typeof(AsteroidTask); // 点击击碎陨石
            }

            // 下载数据
            if (taskName.Contains("下载") || taskName.Contains("上传") || taskName.Contains("数据"))
            {
                return typeof(DownloadTask); // 进度条+信号干扰修复
            }

            // 默认随机一种（含新增类型）
            int hash = Mathf.Abs(taskName.GetHashCode()) % 10;
            switch (hash)
            {
                case 0: return typeof(WireTask);
                case 1: return typeof(SwipeCardTask);
                case 2: return typeof(MemoryTask);
                case 3: return typeof(KeypadTask);
                case 4: return typeof(SortTask);
                case 5: return typeof(ScanTask);
                case 6: return typeof(TapTask);
                case 7: return typeof(CalibrateTask);
                case 8: return typeof(AsteroidTask);
                case 9: return typeof(DownloadTask);
                default: return typeof(WireTask);
            }
        }

        private void OnMiniGameComplete(MiniGameBase miniGame)
        {
            if (activeTaskChallenge == null) return;

            TaskStation task = activeTaskChallenge;
            bool sabotage = activeTaskIsSabotage;
            CleanupMiniGame();

            if (sabotage)
            {
                ResolveSabotageChallenge(task, true);
            }
            else
            {
                ResolveEvidenceChallenge(task, true);
            }

            Changed?.Invoke();
        }

        private void OnMiniGameCancel(MiniGameBase miniGame)
        {
            if (activeTaskChallenge == null) return;

            TaskStation task = activeTaskChallenge;
            bool sabotage = activeTaskIsSabotage;
            CleanupMiniGame();

            if (sabotage)
            {
                ResolveSabotageChallenge(task, false);
            }
            else
            {
                ResolveEvidenceChallenge(task, false);
            }

            Changed?.Invoke();
        }

        private void CancelMiniGame()
        {
            if (activeMiniGame != null)
            {
                activeMiniGame.OnComplete -= OnMiniGameComplete;
                activeMiniGame.OnCancel -= OnMiniGameCancel;
                activeMiniGame.Hide();
                Destroy(activeMiniGame.gameObject);
                activeMiniGame = null;
                activeTaskChallenge = null;
                activeTaskIsSabotage = false;
                taskChallengeTitle = string.Empty;
                taskChallengeBody = string.Empty;
                LastEvent = "任务已取消。";
            }
        }

        private void CleanupMiniGame()
        {
            if (activeMiniGame == null) return;

            activeMiniGame.OnComplete -= OnMiniGameComplete;
            activeMiniGame.OnCancel -= OnMiniGameCancel;
            activeMiniGame.Hide();
            Destroy(activeMiniGame.gameObject);
            activeMiniGame = null;
        }

        // ──────────────── 角色动作 ────────────────

        private void TryRoleAction()
        {
            if (player == null || !player.IsAlive)
            {
                return;
            }

            if (PlayerRole == SocialRole.Gang)
            {
                falseLeadCount++;
                gangHeat = Mathf.Max(0, gangHeat - 8);
                SocialCharacter framed = characters
                    .Where(character => character.IsAlive && !character.IsPlayer && character.Role != SocialRole.Gang)
                    .OrderBy(_ => UnityEngine.Random.value)
                    .FirstOrDefault();

                if (framed != null)
                {
                    AddSuspicion(framed, 18, "伪证");
                    CurrentClue = "黑帮伪证：有人声称 " + framed.CharacterName + " 在案发前离开现场，但证词来源可疑。";
                }

                LastEvent = "你放出假证词，降低黑帮热度，但会议线索被污染。";
                AddCaseLog("伪证", LastEvent);
                Changed?.Invoke();
                return;
            }

            if (PlayerRole == SocialRole.Undercover)
            {
                int gain = UnityEngine.Random.Range(2, 4);
                evidenceScore = Mathf.Min(EvidenceTarget, evidenceScore + gain);
                undercoverExposure = Mathf.Min(MaxUndercoverExposure, undercoverExposure + 14);
                witnessStatementCount++;
                LastEvent = "你完成一次秘密接头，传出 " + gain + " 份证据，但卧底暴露值上升。";
                AddCaseLog("接头", LastEvent);
                CheckVictory();
                Changed?.Invoke();
                return;
            }

            SocialCharacter suspect = FindHighestSuspicionCharacter();

            if (suspect == null)
            {
                LastEvent = "暂无足够嫌疑对象可追捕。先调监控或收集证据。";
                Changed?.Invoke();
                return;
            }

            chaseCount++;
            AddSuspicion(suspect, 12, "追捕");
            gangHeat = Mathf.Min(MaxGangHeat, gangHeat + 10);
            evidenceScore = Mathf.Min(EvidenceTarget, evidenceScore + (suspect.Role == SocialRole.Gang ? 2 : 1));
            LastEvent = "警方短追捕锁定 " + suspect.CharacterName + "，获得行动轨迹和随身物证。";
            AddCaseLog("追捕", LastEvent);
            CheckVictory();
            Changed?.Invoke();
        }

        private void TrySurveillanceAction()
        {
            if (player == null || !player.IsAlive)
            {
                return;
            }

            if (PlayerRole == SocialRole.Gang)
            {
                TriggerBlackout();
                falseLeadCount++;
                gangHeat = Mathf.Max(0, gangHeat - 12);
                latestSurveillanceIntel = "黑帮反侦察：摄像头片段被覆盖，最近路线可信度下降。";
                CurrentClue = latestSurveillanceIntel;
                AddCaseLog("反侦察", latestSurveillanceIntel);
                LastEvent = "你覆盖摄像头并制造断电，争取了一段假不在场证明。";
                Changed?.Invoke();
                return;
            }

            PulseSurveillance(true);
            evidenceScore = Mathf.Min(EvidenceTarget, evidenceScore + 1);
            undercoverExposure = PlayerRole == SocialRole.Undercover
                ? Mathf.Min(MaxUndercoverExposure, undercoverExposure + 8)
                : undercoverExposure;
            LastEvent = "你调取监控，案件板新增一条路线线索。";
            AddCaseLog("监控", latestSurveillanceIntel);
            CheckVictory();
            Changed?.Invoke();
        }

        private void StartMeeting(string reason)
        {
            IsRoleRevealVisible = false;
            IsMeeting = true;
            MeetingReason = reason;
            LastEvent = reason + " 选择一个怀疑对象投票。";
            meetingsCalled++;
            AddCaseLog("会议", reason);

            // 初始化离线聊天
            offlineChatMessages.Clear();
            offlineChatInput = string.Empty;
            offlineChatScroll = Vector2.zero;
            offlineChatMessageTimer = 2.5f;
            offlineChatMessageIndex = 0;
            offlineChatRoundComplete = false;

            // 初始化跨模式ChatSystem
            offlineChatSystem = new ChatSystem(OnOfflineChatSend);
            offlineChatSystem.CurrentPhase = OnlineMatchPhase.Meeting;
            offlineChatSystem.CanSend = player != null && player.IsAlive;
            offlineChatSystem.IsAlive = player != null && player.IsAlive;
            offlineChatSystem.LocalFaction = GetFaction(PlayerRole);

            // AI 在会议开始时发送开场消息
            SendNextAiChatMessage();

            Changed?.Invoke();
        }

        private void KillCharacter(SocialCharacter target)
        {
            target.Kill();
            AudioManager.Instance?.PlaySFX(SoundEffect.Kill);

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = target.CharacterName + " Body";
            generatedObjects.Add(bodyObject);
            bodyObject.transform.position = new Vector3(target.transform.position.x, target.transform.position.y, CharacterZ + 0.08f);
            bodyObject.transform.localScale = new Vector3(0.58f, 0.22f, 0.22f);
            bodyObject.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-16f, 16f));
            SetColor(bodyObject, new Color(0.75f, 0.05f, 0.04f, 1f));

            BodyMarker marker = bodyObject.AddComponent<BodyMarker>();
            marker.Bind(target);
            bodies.Add(marker);
            CurrentClue = BuildClue(target);
            AddCaseLog("线索", CurrentClue);
            evidenceScore = target.Role == SocialRole.Gang ? evidenceScore : Mathf.Min(EvidenceTarget, evidenceScore + 1);

            if (target.IsPlayer)
            {
                // 检查是否仍有友方存活（卧底+警察=友方）
                SocialRole playerRole = PlayerRole;
                bool hasAliveTeammate = characters.Any(c =>
                    c.IsAlive && c != target && IsSameFaction(c.Role, playerRole));
                
                if (hasAliveTeammate)
                {
                    // 进入鬼魂模式 — 可继续做任务帮助队友
                    GhostMode ghost = target.GetComponent<GhostMode>();
                    if (ghost == null) ghost = target.gameObject.AddComponent<GhostMode>();
                    ghost.EnterGhostMode();
                    ghost.CanDoTasks = true;
                    ghost.CanReportBody = false;
                    ghost.GhostCanCallMeeting = false;
                    AddCaseLog("事件", $"{target.CharacterName} 被淘汰，进入鬼魂模式，可继续帮助队友。");
                    AddCaseLog("提示", "按 Tab 查看剩余队友状态。");
                }
                else
                {
                    FinishGame("黑帮胜利：你被击倒，港区证据链中断。");
                }
            }
        }

        private void TriggerBlackout()
        {
            blackoutTimer = BlackoutDurationSeconds;

            // 通知 EnvironmentManager 切换灯光
            if (environmentManager != null)
            {
                environmentManager.SetBlackout(true);
            }
        }

        private string BuildClue(SocialCharacter victim)
        {
            SocialCharacter nearestAlive = characters
                .Where(character => character.IsAlive && character != victim)
                .OrderBy(character => Vector3.Distance(character.transform.position, victim.transform.position))
                .FirstOrDefault();

            if (nearestAlive == null)
            {
                currentPrimarySuspect = null;
                return "线索：尸体附近没有明显目击者。";
            }

            float distance = Vector3.Distance(nearestAlive.transform.position, victim.transform.position);
            string certainty = distance < 1.6f ? "强" : "弱";
            currentPrimarySuspect = nearestAlive;
            AddSuspicion(nearestAlive, distance < 1.6f ? 22 : 12, "尸体附近");

            string areaName = GetAreaName(victim.transform.position);
            string route = BuildCharacterRoute(nearestAlive);
            string footprint = CountRecentFootprints(victim.transform.position, 1.7f) + " 组新脚印";
            return "线索(" + certainty + ")：" + areaName + " 发现尸体；附近最后看到 " + nearestAlive.CharacterName + "；现场有 " + footprint + "；近期路线：" + route + "。这不是铁证。";
        }

        private SocialCharacter PickAutoSuspect()
        {
            SocialCharacter highSuspicion = FindHighestSuspicionCharacter();

            if (highSuspicion != null && highSuspicion.IsAlive && UnityEngine.Random.value > 0.2f)
            {
                return highSuspicion;
            }

            SocialCharacter clueSuspect = FindClueSuspect();

            if (clueSuspect != null && clueSuspect.IsAlive)
            {
                return clueSuspect;
            }

            SocialCharacter gang = characters.FirstOrDefault(character => character.IsAlive && character.Role == SocialRole.Gang);

            if (gang != null && UnityEngine.Random.value > 0.35f)
            {
                return gang;
            }

            return characters
                .Where(character => character.IsAlive && !character.IsPlayer)
                .OrderByDescending(character => character.Role == SocialRole.Gang ? 1 : 0)
                .ThenBy(_ => UnityEngine.Random.value)
                .FirstOrDefault();
        }

        private SocialCharacter FindClueSuspect()
        {
            if (currentPrimarySuspect != null && currentPrimarySuspect.IsAlive)
            {
                return currentPrimarySuspect;
            }

            if (string.IsNullOrEmpty(CurrentClue))
            {
                return null;
            }

            return characters.FirstOrDefault(character => CurrentClue.Contains(character.CharacterName));
        }

        private void CheckVictory()
        {
            if (IsGameOver)
            {
                return;
            }

            int aliveGang = characters.Count(character => character.IsAlive && character.Role == SocialRole.Gang);
            int aliveNonGang = characters.Count(character => character.IsAlive && character.Role != SocialRole.Gang);

            if (aliveGang == 0)
            {
                FinishGame("警方胜利：黑帮被投出局。");
                return;
            }

            if (evidenceScore >= EvidenceTarget && CompletedTasks >= Mathf.Max(3, TotalTasks - 1))
            {
                FinishGame(PlayerRole == SocialRole.Gang ? "专案组胜利：证据链闭合，你没能挡住收网。" : "专案组胜利：证据链闭合，港区开始收网。");
                return;
            }

            if (PlayerRole == SocialRole.Undercover && undercoverExposure >= MaxUndercoverExposure)
            {
                FinishGame("黑帮胜利：你的卧底身份被识破，证据传递中断。");
                return;
            }

            if (PlayerRole == SocialRole.Gang && gangHeat >= MaxGangHeat && evidenceScore >= EvidenceTarget - 2)
            {
                FinishGame("警方胜利：黑帮热度爆表，专案组提前收网。");
                return;
            }

            if (aliveGang >= aliveNonGang)
            {
                FinishGame("黑帮胜利：黑帮人数已经压过警方阵营。");
            }
        }

        private void FinishGame(string result)
        {
            IsGameOver = true;
            IsRoleRevealVisible = false;
            IsMeeting = false;
            ResultText = result;
            LastEvent = result;
            AddCaseLog("结局", result);
        }

        private void AddCaseLog(string label, string message)
        {
            string line = "[" + label + "] " + message;

            if (string.IsNullOrEmpty(CaseLog))
            {
                CaseLog = line;
                return;
            }

            CaseLog = line + "\n" + CaseLog;
        }

        private string BuildRouteIntel()
        {
            if (!HasStarted || characters.Count == 0)
            {
                return string.Empty;
            }

            List<string> snippets = characters
                .Where(character => character.IsAlive)
                .Select(character => character.CharacterName + ": " + BuildCharacterRoute(character))
                .Take(4)
                .ToList();

            return string.Join(" | ", snippets);
        }

        private string BuildTaskChecklist()
        {
            if (taskStations.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = taskStations
                .Select(task => (task.IsCompleted ? "[OK] " : task.IsSabotaged ? "[!] " : "[ ] ")
                    + task.TaskName
                    + " "
                    + task.Progress
                    + "/"
                    + task.RequiredProgress)
                .ToList();

            return string.Join("\n", lines);
        }

        private string BuildRosterSummary()
        {
            if (characters.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = characters
                .Select(character => (character.IsAlive ? "● " : "× ")
                    + character.CharacterName
                    + (character.IsPlayer ? "（你）" : string.Empty))
                .ToList();

            return string.Join("\n", lines);
        }

        private string BuildCaseBoard()
        {
            if (!HasStarted)
            {
                return string.Empty;
            }

            return "证据链 " + evidenceScore + "/" + EvidenceTarget
                + "\n黑帮热度 " + gangHeat + "/" + MaxGangHeat
                + "\n卧底暴露 " + undercoverExposure + "/" + MaxUndercoverExposure
                + "\n证词 " + witnessStatementCount
                + " | 假线索 " + falseLeadCount
                + " | 追捕 " + chaseCount
                + "\n监控：" + latestSurveillanceIntel;
        }

        private string BuildSuspectBoard()
        {
            if (characters.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = characters
                .Where(character => character.IsAlive)
                .OrderByDescending(character => GetSuspicion(character))
                .Select(character => character.CharacterName
                    + (character.IsPlayer ? "（你）" : string.Empty)
                    + " 嫌疑 "
                    + GetSuspicion(character)
                    + " | "
                    + BuildCharacterRoute(character))
                .Take(5)
                .ToList();

            return string.Join("\n", lines);
        }

        private string BuildSpecialActionPrompt()
        {
            if (!HasStarted || IsMeeting || IsGameOver || player == null || !player.IsAlive)
            {
                return string.Empty;
            }

            switch (PlayerRole)
            {
                case SocialRole.Gang:
                    return "F 伪造证词 | C 覆盖监控/断电";
                case SocialRole.Undercover:
                    return "F 秘密接头传证 | C 调取监控";
                default:
                    return "F 封锁追捕高嫌疑人 | C 调取监控";
            }
        }

        private void AddSuspicion(SocialCharacter character, int amount, string reason)
        {
            if (character == null)
            {
                return;
            }

            int current = GetSuspicion(character);
            suspicionScores[character] = Mathf.Clamp(current + amount, 0, 100);

            if (amount > 0)
            {
                AddCaseLog("嫌疑", character.CharacterName + " +" + amount + "：" + reason);
            }
        }

        private int GetSuspicion(SocialCharacter character)
        {
            return character != null && suspicionScores.TryGetValue(character, out int score) ? score : 0;
        }

        private SocialCharacter FindHighestSuspicionCharacter()
        {
            return characters
                .Where(character => character.IsAlive && !character.IsPlayer)
                .OrderByDescending(GetSuspicion)
                .FirstOrDefault(character => GetSuspicion(character) > 0);
        }

        private string BuildRoleBrief()
        {
            switch (PlayerRole)
            {
                case SocialRole.Gang:
                    return "你的身份：黑帮线人";
                case SocialRole.Undercover:
                    return "你的身份：潜伏探员";
                case SocialRole.Mole:
                    return "你的身份：黑帮线人（潜伏警方）";
                default:
                    return "你的身份：专案警员";
            }
        }

        private string BuildGoalBrief()
        {
            switch (PlayerRole)
            {
                case SocialRole.Gang:
                    return "目标：破坏证据、制造断电、击倒专案组成员，并在会议中隐藏身份。";
                case SocialRole.Undercover:
                    return "目标：完成取证任务，利用路线线索投出黑帮，同时保持潜伏。";
                case SocialRole.Mole:
                    return "目标：混入警方内部，暗中收集卧底情报，掩护黑帮成员，误导警方搜查方向。";
                default:
                    return "目标：完成证据链，报告尸体，在会议中投出黑帮线人。";
            }
        }

        private string RoleName(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang:
                    return "黑帮";
                case SocialRole.Undercover:
                    return "卧底";
                case SocialRole.Mole:
                    return "线人";
                default:
                    return "警察";
            }
        }

        private string BuildCharacterRoute(SocialCharacter character)
        {
            List<string> areas = routeEntries
                .Where(entry => entry.Character == character)
                .OrderByDescending(entry => entry.RoundTime)
                .Select(entry => entry.AreaName)
                .Distinct()
                .Take(3)
                .ToList();

            if (areas.Count == 0)
            {
                return GetAreaName(character.transform.position);
            }

            return string.Join(" > ", areas);
        }

        private int CountRecentFootprints(Vector3 center, float radius)
        {
            return footprintTrails.Count(trail => trail != null && Vector3.Distance(trail.transform.position, center) <= radius);
        }

        private string GetAreaName(Vector3 position)
        {
            NamedZone zone = zones
                .Where(candidate => candidate.Contains(position))
                .OrderBy(candidate => Vector3.Distance(candidate.Center, position))
                .FirstOrDefault();

            return zone != null ? zone.Name : "主街";
        }

        private void RemoveBodiesFor(SocialCharacter target)
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                BodyMarker body = bodies[i];

                if (body == null || body.Victim != target)
                {
                    if (body == null)
                    {
                        bodies.RemoveAt(i);
                    }

                    continue;
                }

                bodies.RemoveAt(i);
                DestroyGenerated(body.gameObject);
            }
        }

        private TaskStation FindNearestTask()
        {
            return taskStations
                .Where(task => Vector3.Distance(player.transform.position, task.transform.position) <= InteractRange)
                .OrderBy(task => Vector3.Distance(player.transform.position, task.transform.position))
                .FirstOrDefault();
        }

        private string BuildInteractionPrompt()
        {
            if (!HasStarted || IsMeeting || IsGameOver || player == null || !player.IsAlive)
            {
                return string.Empty;
            }

            BodyMarker body = bodies
                .Where(marker => marker != null && Vector3.Distance(player.transform.position, marker.transform.position) <= InteractRange)
                .OrderBy(marker => Vector3.Distance(player.transform.position, marker.transform.position))
                .FirstOrDefault();

            if (body != null)
            {
                return "R 报告尸体：" + body.Victim.CharacterName;
            }

            SocialCharacter target = characters
                .Where(character => !character.IsPlayer && character.IsAlive && character.Role != SocialRole.Gang && Vector3.Distance(player.transform.position, character.transform.position) <= KillRange)
                .OrderBy(character => Vector3.Distance(player.transform.position, character.transform.position))
                .FirstOrDefault();

            bool isHostileRole = PlayerRole == SocialRole.Gang || PlayerRole == SocialRole.Mole;

            if (isHostileRole && target != null)
            {
                return playerKillCooldown <= 0f
                    ? "Q 击倒：" + target.CharacterName
                    : "击倒冷却：" + Mathf.CeilToInt(playerKillCooldown) + "s";
            }

            TaskStation task = FindNearestTask();

            if (task != null)
            {
                if (isHostileRole)
                {
                    return "E 破坏：" + task.TaskName;
                }

                return task.IsCompleted
                    ? task.TaskName + " 已完成"
                    : "E 取证：" + task.TaskName + " " + task.Progress + "/" + task.RequiredProgress;
            }

            if (emergencyButton != null && Vector3.Distance(player.transform.position, emergencyButton.transform.position) <= InteractRange)
            {
                return emergencyMeetingsCalled >= EmergencyMeetingLimit
                    ? "紧急会议次数已用完"
                    : "E 召开紧急会议";
            }

            // 监控站提示
            if (securityCamera != null && securityCamera.IsPlayerNearMonitor(player.transform.position))
            {
                if (securityCamera.IsViewing)
                {
                    return "V 切换摄像头 | E 退出";
                }
                return "V 查看监控摄像头";
            }

            // 通风管提示
            string ventPrompt = BuildVentPrompt();
            if (!string.IsNullOrEmpty(ventPrompt))
            {
                return ventPrompt;
            }

            return "靠近蓝色任务点取证，红色按钮开会，发现尸体按 R。";
        }

        private void FollowCamera()
        {
            if (Camera.main == null || player == null)
            {
                return;
            }

            // 查看监控时，镜头锁定在监控站位置
            if (securityCamera != null && securityCamera.IsViewing)
            {
                Vector3 monitorPos = securityCamera.MonitorStationPosition;
                Vector3 desiredPos = new Vector3(monitorPos.x, monitorPos.y - CameraFollowDistance, -CameraFollowHeight);
                Camera.main.transform.position = Vector3.Lerp(
                    Camera.main.transform.position,
                    desiredPos,
                    Time.deltaTime * 4f);
                Camera.main.transform.LookAt(new Vector3(monitorPos.x, monitorPos.y, CameraTargetZ));
                Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, 6.85f, Time.deltaTime * 4f);
                Camera.main.nearClipPlane = 0.01f;
                Camera.main.farClipPlane = 100f;
                return;
            }

            Vector3 position = player.transform.position;
            Vector3 target = new Vector3(position.x, position.y, CameraTargetZ);
            Vector3 desiredCam = new Vector3(position.x, position.y - CameraFollowDistance, -CameraFollowHeight);

            Camera.main.transform.position = Vector3.Lerp(
                Camera.main.transform.position,
                desiredCam,
                Time.deltaTime * 4f);
            Camera.main.transform.LookAt(target);
            Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, IsBlackout ? 4.15f : 6.85f, Time.deltaTime * 4f);
            Camera.main.nearClipPlane = 0.01f;
            Camera.main.farClipPlane = 100f;
        }

        private void BuildHud()
        {
            if (hudObject != null)
            {
                DestroyGenerated(hudObject);
            }

            hudObject = new GameObject("Social Prototype HUD");
            Type hudType = Type.GetType("GanglandUndercover.SocialDeduction.SocialPrototypeHud, Assembly-CSharp")
                ?? typeof(SocialPrototypeController).Assembly.GetType("GanglandUndercover.SocialDeduction.SocialPrototypeHud");

            if (hudType == null)
            {
                Debug.LogError("SocialPrototypeHud type could not be resolved.");
                return;
            }

            Component hud = hudObject.AddComponent(hudType);
            MethodInfo bindMethod = hudType.GetMethod("Bind", BindingFlags.Instance | BindingFlags.Public);

            if (bindMethod != null)
            {
                bindMethod.Invoke(hud, new object[] { this });
            }
        }

        private void BuildWorld()
        {
            if (CurrentMapType == MapType.PoliceStation)
            {
                BuildPoliceStationWorld();
                return;
            }

            // 默认：九龙港区地图
            ConfigureSceneLighting();
            SetupEnvironment();

            // 初始化灯光天气与装饰系统
            if (environmentManager != null)
            {
                environmentManager.InitializeAllAtmosphereSystems(transform);
            }

            CreateFloor();
            CreateZone("货柜码头", new Vector3(-3.25f, 1.85f, 0f), new Vector2(2.9f, 2.0f));
            CreateZone("夜市巷", new Vector3(0f, 2.05f, 0f), new Vector2(2.7f, 1.9f));
            CreateZone("专案办公室", new Vector3(3.25f, 1.25f, 0f), new Vector2(2.6f, 2.1f));
            CreateZone("证物库", new Vector3(-2.8f, -1.9f, 0f), new Vector2(2.7f, 1.9f));
            CreateZone("地下诊所", new Vector3(2.65f, -2f, 0f), new Vector2(2.8f, 1.9f));
            CreateZone("主街", new Vector3(0f, 0.05f, 0f), new Vector2(8.8f, 0.95f));
            CreateZone("竖巷", new Vector3(0f, -0.55f, 0f), new Vector2(1.05f, 5.2f));
            CreateLane("主街", new Vector3(0f, 0.05f, FloorZ), new Vector3(8.4f, 0.72f, 0.12f));
            CreateLane("竖巷", new Vector3(0f, -0.55f, FloorZ + 0.01f), new Vector3(0.78f, 4.8f, 0.12f));
            CreateRoom("货柜码头", new Vector3(-3.25f, 1.85f, FloorZ + 0.06f), new Vector3(2.55f, 1.8f, 0.22f), new Color(0.16f, 0.21f, 0.2f, 1f));
            CreateRoom("夜市巷", new Vector3(0f, 2.05f, FloorZ + 0.06f), new Vector3(2.35f, 1.55f, 0.2f), new Color(0.2f, 0.17f, 0.12f, 1f));
            CreateRoom("专案办公室", new Vector3(3.25f, 1.25f, FloorZ + 0.06f), new Vector3(2.2f, 1.8f, 0.24f), new Color(0.13f, 0.18f, 0.24f, 1f));
            CreateRoom("证物库", new Vector3(-2.8f, -1.9f, FloorZ + 0.06f), new Vector3(2.35f, 1.55f, 0.22f), new Color(0.18f, 0.16f, 0.22f, 1f));
            CreateRoom("地下诊所", new Vector3(2.65f, -2f, FloorZ + 0.06f), new Vector3(2.45f, 1.55f, 0.22f), new Color(0.13f, 0.22f, 0.19f, 1f));
            CreateHarborProps();
            CreateWalls();
            CreateCeilings();

            // ─── 程序化建筑生成（v3）───
            if (environmentManager != null)
            {
                environmentManager.BuildDistrict(transform, generatedObjects);
            }
            CreateTask("查封货柜", new Vector3(-4f, 1.8f, 0f));
            CreateTask("调取监控", new Vector3(0.1f, 2.6f, 0f));
            CreateTask("修复电闸", new Vector3(3.9f, 1.2f, 0f));
            CreateTask("扫描证物", new Vector3(-2.7f, -2.6f, 0f));
            CreateTask("上传档案", new Vector3(2.8f, -2.5f, 0f));
            CreateEmergencyButton(new Vector3(0f, 0f, 0f));
            CreateSurveillanceNode("码头天眼", new Vector3(-3.2f, 1.85f, 0f), 2.15f);
            CreateSurveillanceNode("夜市闭路电视", new Vector3(0f, 2.1f, 0f), 1.85f);
            CreateSurveillanceNode("警署路口镜头", new Vector3(3.25f, 1.2f, 0f), 2.0f);
            CreateSurveillanceNode("后巷门禁", new Vector3(0f, -1.8f, 0f), 1.75f);
            CreateVents();
            CreateSecuritySystems();
            CreateCharacters();
            InitTurnMap();
            InitCriticalTaskSystem();
        }

        // ─── 警察局地图构建 ──────────────────────────

        private void BuildPoliceStationWorld()
        {
            ConfigureSceneLighting();
            SetupEnvironment();

            // 初始化灯光天气与装饰系统
            if (environmentManager != null)
            {
                environmentManager.InitializeAllAtmosphereSystems(transform);
            }

            CreateFloor();

            // 6 个区域：大厅 / 审讯室 / 证物室 / 武器库 / 拘留室 / 简报室
            CreateZone("大厅",   PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Lobby),        PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Lobby));
            CreateZone("审讯室", PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Interrogation), PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Interrogation));
            CreateZone("证物室", PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Evidence),      PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Evidence));
            CreateZone("武器库", PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Armory),        PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Armory));
            CreateZone("拘留室", PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Cells),         PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Cells));
            CreateZone("简报室", PoliceStationMap.GetAreaCenter(PoliceStationMap.Area.Briefing),      PoliceStationMap.GetAreaSize(PoliceStationMap.Area.Briefing));

            // 走廊（大厅延伸区域）
            CreateZone("警局走廊", new Vector3(0f, -1.2f, 0f), new Vector2(7.0f, 0.8f));

            // 区域房间渲染
            foreach (PoliceStationMap.Area area in System.Enum.GetValues(typeof(PoliceStationMap.Area)))
            {
                Vector3 center = PoliceStationMap.GetAreaCenter(area);
                Vector2 size = PoliceStationMap.GetAreaSize(area);
                Color color = PoliceStationMap.GetAreaColor(area);
                CreateRoom(PoliceStationMap.GetAreaName(area), new Vector3(center.x, center.y, FloorZ + 0.06f),
                    new Vector3(size.x - 0.3f, size.y - 0.3f, 0.22f), color);
            }

            // 走廊标线
            CreateLane("走廊通道", new Vector3(0f, -1.2f, FloorZ), new Vector3(6.8f, 0.58f, 0.12f));

            // 墙壁
            CreateWalls();

            // 天花板
            CreateCeilings();

            // ── 任务站 ──────────────────────────────────────
            CreateTask("整理档案", PoliceStationMap.GetTaskPosition(PoliceStationMap.Area.Lobby));
            CreateTask("审讯记录", PoliceStationMap.GetTaskPosition(PoliceStationMap.Area.Interrogation));
            CreateTask("证据归档", PoliceStationMap.GetTaskPosition(PoliceStationMap.Area.Evidence));
            CreateTask("武器清点", PoliceStationMap.GetTaskPosition(PoliceStationMap.Area.Armory));
            CreateTask("调取监控", PoliceStationMap.GetTaskPosition(PoliceStationMap.Area.Briefing));

            // ── 紧急按钮 ──────────────────────────────────
            CreateEmergencyButton(PoliceStationMap.EmergencyButtonPosition);

            // ── 监控节点 ──────────────────────────────────
            var surveillanceCfgs = PoliceStationMap.GetSurveillanceConfigs();
            foreach (var cfg in surveillanceCfgs)
            {
                CreateSurveillanceNode(cfg.name, cfg.position, cfg.radius);
            }

            // ── 通风管 ──────────────────────────────────
            CreatePoliceStationVents();

            // ── 安保系统 ──────────────────────────────────
            CreateSecuritySystems();

            // ── 角色 ──────────────────────────────────
            CreateCharacters();

            // ── 回合制地图 ─────────────────────────────
            InitTurnMap();

            InitCriticalTaskSystem();
        }

        private void CreatePoliceStationVents()
        {
            ventSystem = gameObject.AddComponent<VentSystem>();
            ventSystem.Bind(
                onTeleport: pos =>
                {
                    if (player != null)
                    {
                        player.transform.position = new Vector3(pos.x, pos.y, CharacterZ);
                    }
                },
                getPlayerPosition: () => player != null ? player.transform.position : Vector3.zero,
                isPlayerGang: () => PlayerRole == SocialRole.Gang || PlayerRole == SocialRole.Mole,
                isPlayerAlive: () => player != null && player.IsAlive,
                onSetBlackoutAlpha: alpha => { }
            );

            var configs = PoliceStationMap.VentConfigs;
            List<VentNode> ventNodes = new List<VentNode>();

            for (int i = 0; i < configs.Length; i++)
            {
                ventNodes.Add(new VentNode(configs[i].name, configs[i].position, configs[i].connections));
            }

            ventSystem.BuildVisuals(ventNodes, FloorZ);
        }

        // ─── 紧急任务系统 ─────────────────────────────

        private void InitCriticalTaskSystem()
        {
            criticalTaskSystem = gameObject.AddComponent<CriticalTaskSystem>();
            criticalTaskSystem.OnCriticalTaskStarted += HandleCriticalTaskStarted;
            criticalTaskSystem.OnCriticalTaskCompleted += HandleCriticalTaskCompleted;
            criticalTaskSystem.OnCriticalTaskFailed += HandleCriticalTaskFailed;
            Debug.Log("[SocialPrototypeController] 紧急任务系统已初始化。");
        }

        private void HandleCriticalTaskStarted(CriticalTaskType type)
        {
            // 暂停 AI 操作，所有玩家必须参与修复
            if (turnController != null)
            {
                turnController.PauseAI();
            }
            Debug.Log($"[SocialPrototypeController] 紧急任务开始: {type}，AI 已暂停。");
        }

        private void HandleCriticalTaskCompleted(CriticalTaskType type)
        {
            // 恢复 AI 操作
            if (turnController != null)
            {
                turnController.ResumeAI();
            }
            Debug.Log($"[SocialPrototypeController] 紧急任务完成: {type}，AI 已恢复。");
        }

        private void HandleCriticalTaskFailed(CriticalTaskType type)
        {
            // 紧急任务失败 → 对应阵营自动失败
            if (turnController != null)
            {
                turnController.ResumeAI();
            }

            // 根据任务类型触发对应阵营失败
            // O2 失败 → 所有玩家死亡（黑帮胜利）
            // Reactor 失败 → 所有玩家死亡（黑帮胜利）
            // 这里触发游戏结束逻辑
            Debug.Log($"[SocialPrototypeController] 紧急任务失败: {type}，触发游戏结束。");
            TriggerGameOverForCriticalFailure(type);
        }

        private void TriggerGameOverForCriticalFailure(CriticalTaskType type)
        {
            // 紧急任务失败 → 黑帮胜利（所有警察/平民死亡）
            // 这里可以调用 VictoryEvaluator 或直接结束游戏
            Debug.Log($"[SocialPrototypeController] 紧急任务 {type} 失败，黑帮获胜！");
            // TODO: 调用 Game Over 逻辑
        }

        private void TickCriticalTaskSystem()
        {
            if (criticalTaskSystem != null && criticalTaskSystem.State == CriticalTaskState.Active)
            {
                // O2：空格键连点修复
                if (criticalTaskSystem.ActiveType == CriticalTaskType.O2)
                {
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        criticalTaskSystem.ClickO2Repair(0.08f);
                    }
                }

                // Reactor：Q 按钮A、E 按钮B（需要同时按住 0.6s，重复3次）
                if (criticalTaskSystem.ActiveType == CriticalTaskType.Reactor)
                {
                    criticalTaskSystem.HoldReactorButtonA(Input.GetKey(KeyCode.Q));
                    criticalTaskSystem.HoldReactorButtonB(Input.GetKey(KeyCode.E));
                }
            }
        }

        /// <summary>公共方法：由 SabotagePanel / 破坏系统触发紧急任务。</summary>
        public void TriggerCriticalTask(CriticalTaskType type)
        {
            if (criticalTaskSystem == null)
            {
                Debug.LogError("[SocialPrototypeController] CriticalTaskSystem 未初始化！");
                return;
            }

            criticalTaskSystem.Trigger(type);
        }

        /// <summary>获取当前紧急任务系统（供 UI 读取状态）。</summary>
        public CriticalTaskSystem GetCriticalTaskSystem() => criticalTaskSystem;

        // ──────────────────────────────────────────────
        //  通风管系统（Among Us Vent System）
        // ──────────────────────────────────────────────

        private void CreateVents()
        {
            ventSystem = gameObject.AddComponent<VentSystem>();
            ventSystem.Bind(
                onTeleport: pos =>
                {
                    if (player != null)
                    {
                        player.transform.position = new Vector3(pos.x, pos.y, CharacterZ);
                    }
                },
                getPlayerPosition: () => player != null ? player.transform.position : Vector3.zero,
                isPlayerGang: () => PlayerRole == SocialRole.Gang || PlayerRole == SocialRole.Mole,
                isPlayerAlive: () => player != null && player.IsAlive,
                onSetBlackoutAlpha: alpha => { /* 单机版暂不实现黑屏闪烁，预留接口 */ }
            );

            // 6 个通风管节点，按区域分布，拓扑连接参考 Among Us 逻辑
            List<VentNode> ventNodes = new List<VentNode>
            {
                // 0: 货柜码头 ↔ 夜市巷 / 证物库
                new VentNode("码头通风管", new Vector3(-3.55f, 1.48f, 0f), 1, 3),
                // 1: 夜市巷 ↔ 货柜码头 / 专案办公室 / 主街
                new VentNode("夜市通风管", new Vector3(0.25f, 2.4f, 0f), 0, 2, 5),
                // 2: 专案办公室 ↔ 夜市巷 / 地下诊所
                new VentNode("办公室通风管", new Vector3(3.55f, 0.85f, 0f), 1, 4),
                // 3: 证物库 ↔ 货柜码头 / 地下诊所 / 主街
                new VentNode("证物库通风管", new Vector3(-2.5f, -2.35f, 0f), 0, 4, 5),
                // 4: 地下诊所 ↔ 专案办公室 / 证物库
                new VentNode("诊所通风管", new Vector3(2.35f, -2.45f, 0f), 2, 3),
                // 5: 主街 ↔ 夜市巷 / 证物库
                new VentNode("主街通风管", new Vector3(0.5f, -0.25f, 0f), 1, 3)
            };

            ventSystem.BuildVisuals(ventNodes, FloorZ);
        }

        private void TickVentSystem()
        {
            if (ventSystem == null) return;
            if (player == null || !player.IsAlive) return;
            if (!HasStarted || IsMeeting || IsGameOver) return;
            if (activeTaskChallenge != null || activeMiniGame != null) return;

            ventSystem.Tick();
        }

        private void TryVentAction()
        {
            if (ventSystem == null || player == null || !player.IsAlive) return;
            if (PlayerRole != SocialRole.Gang && PlayerRole != SocialRole.Mole)
            {
                LastEvent = "只有黑帮可以使用通风管。";
                Changed?.Invoke();
                return;
            }

            // 玩家已在通风管中 → 弹目的地选择
            if (ventSystem.IsInVent || ventSystem.CurrentVentIndex.HasValue)
            {
                ShowVentDestinationMenu();
                return;
            }

            // 玩家在通风管附近 → 进入
            if (ventSystem.TryEnterVent())
            {
                player.isInsideVent = true;
                LastEvent = "进入通风管...按下 V 选择目的地，或再次按 V 退出。";
                Changed?.Invoke();
                return;
            }

            // 不在任何通风管附近
            LastEvent = "附近没有通风管。";
            Changed?.Invoke();
        }

        private void ShowVentDestinationMenu()
        {
            if (ventSystem == null) return;

            int? currentIdx = ventSystem.CurrentVentIndex;
            if (!currentIdx.HasValue) return;

            IReadOnlyList<int> dests = ventSystem.AvailableDestinations;
            if (dests.Count == 0)
            {
                LastEvent = "该通风管没有连接其他节点。";
                Changed?.Invoke();
                return;
            }

            // 只有一个目标 → 直接瞬移
            if (dests.Count == 1)
            {
                TravelVent(dests[0]);
                return;
            }

            // 多个目标 → 循环选择（简化处理：每次按 V 依次选择）
            // 复杂交互机在 Update 中通过数字键 1-9 选择
            // 简化实现：显示提示后，使用回调下一次 V 来选择
            // 这里采用自动选择最近非当前节点的方式
            TravelVent(dests[0]);
        }

        private void TravelVent(int targetIndex)
        {
            if (ventSystem == null || player == null) return;
            if (ventSystem.CooldownRemaining > 0f)
            {
                LastEvent = "通风管冷却中：" + Mathf.CeilToInt(ventSystem.CooldownRemaining) + "s";
                Changed?.Invoke();
                return;
            }

            string destinationName = ventSystem.GetNodeName(targetIndex);
            ventSystem.TravelTo(targetIndex);
            player.isInsideVent = false;
            LastEvent = "通过通风管抵达：" + destinationName + "。";
            Changed?.Invoke();
        }

        private string BuildVentPrompt()
        {
            if (ventSystem == null || player == null || !player.IsAlive) return string.Empty;
            if (PlayerRole != SocialRole.Gang && PlayerRole != SocialRole.Mole) return string.Empty;

            if (ventSystem.IsInTransition) return "通风管传送中...";

            if (ventSystem.IsInVent || ventSystem.CurrentVentIndex.HasValue)
            {
                string destList = "";
                IReadOnlyList<int> dests = ventSystem.AvailableDestinations;
                for (int i = 0; i < dests.Count; i++)
                {
                    destList += ventSystem.GetNodeName(dests[i]);
                    if (i < dests.Count - 1) destList += " / ";
                }

                return "V 传送到：" + destList + " (再按 V 退出)";
            }

            int? nearestIdx = ventSystem.GetNearestVentIndex();
            if (nearestIdx.HasValue)
            {
                return ventSystem.CooldownRemaining > 0f
                    ? "V 通风管 冷却：" + Mathf.CeilToInt(ventSystem.CooldownRemaining) + "s"
                    : "V 使用通风管 → " + ventSystem.GetNodeName(nearestIdx.Value);
            }

            return string.Empty;
        }

        private void ConfigureSceneLighting()
        {
            if (Camera.main != null)
            {
                Camera.main.orthographic = true;
                Camera.main.orthographicSize = 6.85f;
                Camera.main.nearClipPlane = 0.01f;
                Camera.main.farClipPlane = 100f;
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0.075f, 0.105f, 0.11f, 1f);
                Camera.main.transform.position = new Vector3(0f, -CameraFollowDistance, -CameraFollowHeight);
                Camera.main.transform.LookAt(new Vector3(0f, 0f, CameraTargetZ));
            }

            Light existingLight = FindAnyObjectByType<Light>();

            if (existingLight != null)
            {
                existingLight.type = LightType.Directional;
                existingLight.intensity = 1.85f;
                existingLight.color = new Color(1f, 0.92f, 0.74f, 1f);
                existingLight.transform.rotation = Quaternion.Euler(52f, -35f, 20f);
            }
        }

        private void CreateZone(string zoneName, Vector3 center, Vector2 size)
        {
            zones.Add(new NamedZone(zoneName, center, size));
        }

        private void CreateSurveillanceNode(string nodeName, Vector3 position, float radius)
        {
            surveillanceNodes.Add(new SurveillanceNode(nodeName, position, radius));

            GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nodeObject.name = nodeName;
            generatedObjects.Add(nodeObject);
            nodeObject.transform.position = new Vector3(position.x, position.y, FloorZ - 0.18f);
            nodeObject.transform.localScale = new Vector3(radius * 0.18f, 0.012f, radius * 0.18f);
            nodeObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetColor(nodeObject, new Color(0.12f, 0.52f, 0.72f, 0.28f));

            TextMesh label = CreateWorldLabel(nodeObject.transform, new Vector3(0f, 0f, LabelZ), 0.095f);
            label.text = nodeName;
            label.color = new Color(0.62f, 0.9f, 1f, 1f);
        }

        private void CreateHarborProps()
        {
            CreateContainerStack("Blue Containers", new Vector3(-4.28f, -0.45f, FloorZ - 0.26f), new Color(0.08f, 0.22f, 0.46f, 1f), 3);
            CreateContainerStack("Red Containers", new Vector3(-4.1f, 2.65f, FloorZ - 0.26f), new Color(0.52f, 0.12f, 0.08f, 1f), 2);
            CreateContainerStack("Green Containers", new Vector3(-1.95f, -3.0f, FloorZ - 0.26f), new Color(0.08f, 0.36f, 0.2f, 1f), 2);
            CreateTruck(new Vector3(3.92f, -0.62f, FloorZ - 0.22f));
            CreateMarketStall(new Vector3(0.88f, 2.82f, FloorZ - 0.19f));
            CreateLightPost(new Vector3(-0.95f, 0.52f, FloorZ - 0.2f));
            CreateLightPost(new Vector3(2.25f, 0.42f, FloorZ - 0.2f));
            CreateLightPost(new Vector3(-3.85f, -2.85f, FloorZ - 0.2f));
        }

        private void CreateContainerStack(string stackName, Vector3 basePosition, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);
                container.name = stackName + " " + (i + 1);
                generatedObjects.Add(container);
                container.transform.position = basePosition + new Vector3(0.12f * i, 0.34f * i, -0.42f * i);
                container.transform.localScale = new Vector3(1.05f, 0.42f, 0.38f);
                container.transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 0f : 3f);
                SetColor(container, color * (1f - i * 0.08f));

                GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = container.name + " Stripe";
                generatedObjects.Add(stripe);
                stripe.transform.SetParent(container.transform, false);
                stripe.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                stripe.transform.localScale = new Vector3(0.92f, 0.08f, 0.08f);
                SetColor(stripe, new Color(0.86f, 0.82f, 0.62f, 1f));
            }
        }

        private void CreateTruck(Vector3 position)
        {
            GameObject truck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            truck.name = "Evidence Truck";
            generatedObjects.Add(truck);
            truck.transform.position = position;
            truck.transform.localScale = new Vector3(1.18f, 0.58f, 0.38f);
            SetColor(truck, new Color(0.36f, 0.38f, 0.32f, 1f));

            GameObject cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cab.name = "Evidence Truck Cab";
            generatedObjects.Add(cab);
            cab.transform.SetParent(truck.transform, false);
            cab.transform.localPosition = new Vector3(0.48f, 0f, -0.58f);
            cab.transform.localScale = new Vector3(0.36f, 0.9f, 0.72f);
            SetColor(cab, new Color(0.14f, 0.2f, 0.24f, 1f));

            CreateWheel(truck.transform, new Vector3(-0.34f, -0.56f, 0.14f));
            CreateWheel(truck.transform, new Vector3(0.34f, -0.56f, 0.14f));
            CreateWheel(truck.transform, new Vector3(-0.34f, 0.56f, 0.14f));
            CreateWheel(truck.transform, new Vector3(0.34f, 0.56f, 0.14f));
        }

        private void CreateWheel(Transform parent, Vector3 localPosition)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Truck Wheel";
            generatedObjects.Add(wheel);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wheel.transform.localScale = new Vector3(0.15f, 0.08f, 0.15f);
            SetColor(wheel, new Color(0.02f, 0.02f, 0.02f, 1f));
        }

        private void CreateMarketStall(Vector3 position)
        {
            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObject.name = "Night Market Stall";
            generatedObjects.Add(baseObject);
            baseObject.transform.position = position;
            baseObject.transform.localScale = new Vector3(0.82f, 0.42f, 0.32f);
            SetColor(baseObject, new Color(0.44f, 0.22f, 0.12f, 1f));

            GameObject awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            awning.name = "Night Market Awning";
            generatedObjects.Add(awning);
            awning.transform.SetParent(baseObject.transform, false);
            awning.transform.localPosition = new Vector3(0f, 0f, -0.88f);
            awning.transform.localScale = new Vector3(1.18f, 1.25f, 0.16f);
            SetColor(awning, new Color(0.76f, 0.58f, 0.16f, 1f));
        }

        private void CreateLightPost(Vector3 position)
        {
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Street Light Post";
            generatedObjects.Add(post);
            post.transform.position = position;
            post.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            post.transform.localScale = new Vector3(0.06f, 0.62f, 0.06f);
            SetColor(post, new Color(0.1f, 0.1f, 0.095f, 1f));

            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Street Light Lamp";
            generatedObjects.Add(lamp);
            lamp.transform.SetParent(post.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            lamp.transform.localScale = new Vector3(2.7f, 2.7f, 2.7f);
            SetColor(lamp, new Color(1f, 0.78f, 0.32f, 1f));

            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.7f, 0.36f, 1f);
            light.intensity = 1.2f;
            light.range = 3.3f;
        }

        private void CreateCharacters()
        {
            Vector3 playerSpawn = CurrentMapType == MapType.PoliceStation
                ? new Vector3(0f, 0.2f, 0f) // 警察局大厅
                : new Vector3(-3.2f, -0.8f, 0f); // 港区

            player = CreateCharacter("你", PlayerRole, true, playerSpawn);

            if (CurrentMapType == MapType.PoliceStation)
            {
                BuildPoliceStationCharacters();
            }
            else
            {
                BuildGanglandCharacters();
            }
        }

        /// <summary>
        /// 警察局角色分配：更多 Police + Mole，更少 Gang + Undercover。
        /// 2 Police、1 Undercover、1 Mole，共 4 个 Bot。
        /// </summary>
        private void BuildPoliceStationCharacters()
        {
            List<SocialRole> botRoles = new List<SocialRole>
            {
                SocialRole.Police,     // 警长张
                SocialRole.Police,     // 巡警王
                SocialRole.Undercover, // 卧底李
                SocialRole.Mole        // 线人赵（伪装警察）
            };

            // 若玩家已选了某个角色，调整 Bot 以保持平衡
            if (PlayerRole == SocialRole.Police)
            {
                botRoles[0] = SocialRole.Mole;
            }
            else if (PlayerRole == SocialRole.Mole)
            {
                botRoles[3] = SocialRole.Police;
            }
            else if (PlayerRole == SocialRole.Undercover)
            {
                botRoles[2] = SocialRole.Gang;
            }

            CreateCharacter("警长张", botRoles[0], false, new Vector3(1.2f, 0.6f, 0f));
            CreateCharacter("巡警王", botRoles[1], false, new Vector3(-1.4f, -0.6f, 0f));
            CreateCharacter("卧底李", botRoles[2], false, new Vector3(-2.8f, 1.4f, 0f));
            CreateCharacter("线人赵", botRoles[3], false, new Vector3(2.6f, 1.2f, 0f));
        }

        /// <summary>
        /// 九龙港区角色分配：Police + Undercover + Gang + Mole，各 1 个。
        /// </summary>
        private void BuildGanglandCharacters()
        {
            List<SocialRole> botRoles = new List<SocialRole>
            {
                SocialRole.Police,     // 巡警陈 — 表面警察
                SocialRole.Undercover, // 线人林 — 伪装为黑帮的警察卧底
                SocialRole.Gang,       // 疤脸 — 表面黑帮
                SocialRole.Mole        // 技侦周 — 伪装为警察的黑帮线人
            };

            if (PlayerRole == SocialRole.Gang)
            {
                botRoles[2] = SocialRole.Police;
            }
            else if (PlayerRole == SocialRole.Mole)
            {
                botRoles[3] = SocialRole.Police;
            }

            CreateCharacter("巡警陈", botRoles[0], false, new Vector3(-1.6f, 1.1f, 0f));
            CreateCharacter("线人林", botRoles[1], false, new Vector3(2.3f, -1.3f, 0f));
            CreateCharacter("疤脸",   botRoles[2], false, new Vector3(-2.2f, -1.7f, 0f));
            CreateCharacter("技侦周", botRoles[3], false, new Vector3(1.6f, 1.2f, 0f));
        }

        private static string GetPrefabPathForRole(SocialRole role, bool isPlayer)
        {
            switch (role)
            {
                case SocialRole.Police:
                    return "AssetStore/DenysAlmaral/CityPeople/Prefabs/professions/police_Female_A";
                case SocialRole.Undercover:
                    return "AssetStore/DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G";
                case SocialRole.Gang:
                    return "AssetStore/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K";
                case SocialRole.Mole:
                    return "AssetStore/DenysAlmaral/CityPeople/Prefabs/professions/police_Female_A";
                default:
                    return "AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01";
            }
        }

        /// <summary>
        /// Task-Name → AssetStore/Synty/PolygonGeneric 道具资源路径映射。
        /// </summary>
        private static string GetTaskPropPath(string taskName)
        {
            switch (taskName)
            {
                case "查封货柜":
                    return "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_02";
                case "调取监控":
                    return "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Screen_01";
                case "修复电闸":
                    return "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01";
                case "扫描证物":
                    return "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_03";
                case "上传档案":
                    return "AssetStore/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Keypad_01";
                default:
                    return null;
            }
        }

        private SocialCharacter CreateCharacter(string characterName, SocialRole role, bool isPlayer, Vector3 position)
        {
            string prefabPath = GetPrefabPathForRole(role, isPlayer);
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            GameObject characterObject;

            Color roleColor = role == SocialRole.Gang ? new Color(0.72f, 0.22f, 0.16f, 1f)
                : role == SocialRole.Undercover ? new Color(0.72f, 0.22f, 0.16f, 1f)
                : role == SocialRole.Mole ? new Color(0.22f, 0.36f, 0.72f, 1f)
                : new Color(0.22f, 0.36f, 0.72f, 1f);

            if (prefab != null)
            {
                // --- 预制体路径 ---
                characterObject = Instantiate(prefab);
                characterObject.name = characterName;
                generatedObjects.Add(characterObject);
                characterObject.transform.position = new Vector3(position.x, position.y, CharacterZ);
                characterObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // 根据 Bounds 自适应缩放（参考 FitCharacterAdapterToPlayer 逻辑）
                FitCharacterToMap(characterObject);

                // 角色着色：通过 Tint 方式保留原始材质纹理
                TintCharacterModel(characterObject, roleColor);

                // 配置 Animator：挂载 GanglandCharacter.controller
                ConfigureCharacterAnimator(characterObject);
            }
            else
            {
                // --- 回退胶囊体 ---
                characterObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                characterObject.name = characterName;
                generatedObjects.Add(characterObject);
                characterObject.transform.position = new Vector3(position.x, position.y, CharacterZ);
                characterObject.transform.localScale = new Vector3(0.42f, 0.42f, 0.82f);
                characterObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                SetColor(characterObject, roleColor);
            }

            // --- 阴影 ---
            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = characterName + " Shadow";
            generatedObjects.Add(shadow);
            shadow.transform.SetParent(characterObject.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.08f, 0.52f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(0.74f, 0.035f, 0.48f);
            SetColor(shadow, new Color(0f, 0f, 0f, 0.32f));

            // --- 标签 ---
            TextMesh label = CreateWorldLabel(characterObject.transform, new Vector3(0f, 0.86f, -0.32f), 0.13f);
            label.text = characterName;

            // --- SocialCharacter 组件绑定 ---
            SocialCharacter character = characterObject.AddComponent<SocialCharacter>();
            if (prefab != null)
            {
                character.BindForPrefab(characterName, role, isPlayer);
            }
            else
            {
                character.Bind(characterName, role, isPlayer);
            }

            characters.Add(character);
            return character;
        }

        /// <summary>
        /// 根据角色模型 Bounds 自适应缩放（参照 OnlineMatchController.FitCharacterAdapterToPlayer 逻辑）。
        /// </summary>
        private static void FitCharacterToMap(GameObject model)
        {
            model.transform.localScale = Vector3.one;

            Renderer[] allRenderers = model.GetComponentsInChildren<Renderer>(true);
            if (allRenderers == null || allRenderers.Length == 0)
            {
                model.transform.localScale = new Vector3(0.42f, 0.42f, 0.82f);
                return;
            }

            Bounds combined = allRenderers[0].bounds;
            for (int i = 1; i < allRenderers.Length; i++)
            {
                combined.Encapsulate(allRenderers[i].bounds);
            }

            float largest = Mathf.Max(combined.size.x, combined.size.y, combined.size.z);
            float factor = largest > 0.001f ? 0.82f / largest : 0.18f;
            float clamped = Mathf.Clamp(factor, 0.04f, 0.32f);
            model.transform.localScale = Vector3.one * clamped;
        }

        /// <summary>
        /// Tint 着色：将现有材质颜色向 roleColor 方向混合，保留原始纹理/贴图。
        /// 参照 OnlineMatchController.TintCharacterAdapter 逻辑精简版。
        /// </summary>
        private void TintCharacterModel(GameObject model, Color roleColor)
        {
            Renderer[] allRenderers = model.GetComponentsInChildren<Renderer>(true);
            if (allRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in allRenderers)
            {
                Material material = CreateTintMaterial(renderer);
                if (material == null)
                {
                    continue;
                }

                Color current = material.color;
                Color tinted = Color.Lerp(current, roleColor, 0.28f);
                material.color = new Color(tinted.r, tinted.g, tinted.b, current.a);
            }
        }

        private Material CreateTintMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            if (Application.isPlaying)
            {
                return renderer.material;
            }

            Material source = renderer.sharedMaterial;
            Material material = source != null ? new Material(source) : new Material(FindColorShader());
            renderer.sharedMaterial = material;
            generatedMaterials.Add(material);
            return material;
        }

        /// <summary>
        /// 为角色 GameObject 配置 Animator 并挂载 GanglandCharacter.controller。
        /// </summary>
        private void ConfigureCharacterAnimator(GameObject characterObject)
        {
            Animator animator = characterObject.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = characterObject.AddComponent<Animator>();
            }

            RuntimeAnimatorController controller = LoadGanglandCharacterController();
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            SocialCharacter character = characterObject.GetComponent<SocialCharacter>();
            if (character != null)
            {
                character.BindAnimator(animator);
            }
        }

        /// <summary>
        /// 加载 GanglandCharacter.controller（通过 GUID: 1f860609e221b48e6a101a78e9c6f70e）。
        /// Editor 下走 AssetDatabase.GUIDToAssetPath，运行时通过 Resources 回退。
        /// </summary>
        private static RuntimeAnimatorController LoadGanglandCharacterController()
        {
            const string controllerGuid = "1f860609e221b48e6a101a78e9c6f70e";

#if UNITY_EDITOR
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(controllerGuid);
            if (!string.IsNullOrEmpty(assetPath))
            {
                RuntimeAnimatorController controller =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(assetPath);
                if (controller != null)
                {
                    return controller;
                }
            }
#endif
            // Runtime 回退：尝试 Resources 路径
            return Resources.Load<RuntimeAnimatorController>("Art/Animators/GanglandCharacter");
        }

        private void CreateTask(string taskName, Vector3 position)
        {
            // --- 基础任务站方块（保留作为交互底板）---
            GameObject taskObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            taskObject.name = taskName;
            generatedObjects.Add(taskObject);
            taskObject.transform.position = new Vector3(position.x, position.y, CharacterZ + 0.1f);
            taskObject.transform.localScale = new Vector3(0.82f, 0.62f, 0.42f);

            // --- 3D 道具模型 ---
            string propPath = GetTaskPropPath(taskName);
            if (!string.IsNullOrEmpty(propPath))
            {
                GameObject propPrefab = Resources.Load<GameObject>(propPath);
                if (propPrefab != null)
                {
                    GameObject propModel = Instantiate(propPrefab);
                    propModel.name = taskName + " Prop";
                    generatedObjects.Add(propModel);
                    propModel.transform.SetParent(taskObject.transform, false);
                    propModel.transform.localPosition = new Vector3(0f, 0f, -0.65f);
                    propModel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    propModel.transform.localScale = Vector3.one * 0.52f;
                }
            }

            // --- 屏幕指示器 ---
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = taskName + " Screen";
            generatedObjects.Add(screen);
            screen.transform.SetParent(taskObject.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.22f, -0.58f);
            screen.transform.localScale = new Vector3(0.68f, 0.08f, 0.36f);
            SetColor(screen, new Color(0.08f, 0.28f, 0.42f, 1f));

            CreateWorldLabel(taskObject.transform, new Vector3(0f, 0.72f, LabelZ), 0.105f);

            TaskStation station = taskObject.AddComponent<TaskStation>();
            station.Bind(taskName);
            taskStations.Add(station);
        }

        private void CreateEmergencyButton(Vector3 position)
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buttonObject.name = "Emergency Button";
            generatedObjects.Add(buttonObject);
            buttonObject.transform.position = new Vector3(position.x, position.y, CharacterZ + 0.18f);
            buttonObject.transform.localScale = new Vector3(0.48f, 0.18f, 0.48f);
            buttonObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetColor(buttonObject, new Color(0.78f, 0.08f, 0.05f, 1f));

            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.name = "Emergency Button Base";
            generatedObjects.Add(baseObject);
            baseObject.transform.SetParent(buttonObject.transform, false);
            baseObject.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            baseObject.transform.localScale = new Vector3(1.28f, 0.62f, 1.28f);
            baseObject.transform.localRotation = Quaternion.identity;
            SetColor(baseObject, new Color(0.16f, 0.16f, 0.14f, 1f));

            CreateWorldLabel(buttonObject.transform, new Vector3(0f, 0.62f, LabelZ), 0.11f);
            emergencyButton = buttonObject.AddComponent<EmergencyButton>();
        }

        private static TextMesh CreateWorldLabel(Transform parent, Vector3 localPosition, float characterSize)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = Quaternion.Euler(58f, 0f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = Color.white;
            return label;
        }

        private void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Port Hideout Floor";
            generatedObjects.Add(floor);
            floor.transform.position = new Vector3(0f, 0f, GroundZ + 0.18f);
            floor.transform.localScale = new Vector3(10.4f, 7.3f, 0.22f);
            SetColor(floor, new Color(0.1f, 0.11f, 0.1f, 1f));

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Harbor Water";
            generatedObjects.Add(water);
            water.transform.position = new Vector3(-5.9f, 0f, GroundZ + 0.11f);
            water.transform.localScale = new Vector3(1.6f, 7.3f, 0.12f);
            SetColor(water, new Color(0.02f, 0.15f, 0.19f, 1f));

            GameObject quay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quay.name = "Concrete Quay Edge";
            generatedObjects.Add(quay);
            quay.transform.position = new Vector3(-5.04f, 0f, GroundZ - 0.02f);
            quay.transform.localScale = new Vector3(0.22f, 7.3f, 0.42f);
            SetColor(quay, new Color(0.28f, 0.28f, 0.24f, 1f));
        }

        private void CreateRoom(string roomName, Vector3 position, Vector3 scale, Color color)
        {
            // --- 基础方块（保留作为底板）---
            GameObject room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.name = roomName;
            generatedObjects.Add(room);
            room.transform.position = position;
            room.transform.localScale = scale;
            SetColor(room, color);

            // --- 尝试加载 AssetStore 墙面装饰 ---
            string wallPrefabPath = "AssetStore/Synty/PolygonGeneric/Prefabs/Base/SM_Bld_Base_Wall_Half_02";
            GameObject wallPrefab = Resources.Load<GameObject>(wallPrefabPath);
            if (wallPrefab != null)
            {
                // 北墙装饰
                PlaceRoomDecor(room.transform, wallPrefab, roomName + " North Wall", new Vector3(0f, 0.52f, -0.72f), new Vector3(1f, 0.85f, 0.35f));
                // 南墙装饰
                PlaceRoomDecor(room.transform, wallPrefab, roomName + " South Wall", new Vector3(0f, -0.52f, -0.72f), new Vector3(1f, 0.85f, 0.35f));
            }

            TextMesh label = CreateWorldLabel(room.transform, new Vector3(0f, 0f, LabelZ), 0.12f);
            label.text = roomName;
            label.color = new Color(0.86f, 0.82f, 0.68f, 1f);
        }

        private void PlaceRoomDecor(Transform parent, GameObject prefab, string name, Vector3 localPosition, Vector3 localScale)
        {
            GameObject decor = Instantiate(prefab);
            decor.name = name;
            generatedObjects.Add(decor);
            decor.transform.SetParent(parent, false);
            decor.transform.localPosition = localPosition;
            decor.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            decor.transform.localScale = localScale;
        }

        private void CreateRoomTrim(Transform parent, string trimName, Vector3 localPosition, Vector3 localScale)
        {
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = trimName;
            generatedObjects.Add(trim);
            trim.transform.SetParent(parent, false);
            trim.transform.localPosition = localPosition;
            trim.transform.localScale = localScale;
            SetColor(trim, new Color(0.33f, 0.31f, 0.25f, 1f));
        }

        private void CreateLane(string laneName, Vector3 position, Vector3 scale)
        {
            GameObject lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lane.name = laneName;
            generatedObjects.Add(lane);
            lane.transform.position = position;
            lane.transform.localScale = scale;
            SetColor(lane, new Color(0.19f, 0.18f, 0.15f, 1f));
        }

        private void CreateCover(string coverName, Vector3 position, Vector3 scale)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = coverName;
            generatedObjects.Add(cover);
            cover.transform.position = position;
            cover.transform.localScale = scale;
            SetColor(cover, new Color(0.31f, 0.28f, 0.18f, 1f));
        }

        private void CreateWalls()
        {
            CreateWall("North Wall", new Vector3(0f, 3.55f, 0f), new Vector3(10.4f, 0.18f, 0.35f));
            CreateWall("South Wall", new Vector3(0f, -3.55f, 0f), new Vector3(10.4f, 0.18f, 0.35f));
            CreateWall("West Wall", new Vector3(-5.1f, 0f, 0f), new Vector3(0.18f, 7.2f, 0.35f));
            CreateWall("East Wall", new Vector3(5.1f, 0f, 0f), new Vector3(0.18f, 7.2f, 0.35f));
        }

        // ──────────────────────────────────────────────
        //  环境管理（EnvironmentManager）
        // ──────────────────────────────────────────────

        private void SetupEnvironment()
        {
            if (environmentManager != null) return;

            GameObject envObj = new GameObject("Environment Manager");
            generatedObjects.Add(envObj);
            environmentManager = envObj.AddComponent<EnvironmentManager>();

            // 设置雾效
            environmentManager.SetupFog();

            // 创建区域氛围灯光（暖/冷/红）
            environmentManager.CreateZoneAreaLights(transform, generatedObjects);
        }

        // ──────────────────────────────────────────────
        //  天花板 / 屋顶
        // ──────────────────────────────────────────────

        private void CreateCeilings()
        {
            // 为 5 个房间添加天花板（Synty SM_Bld_Base_Ceiling_01）
            string ceilingPrefabPath = "AssetStore/Synty/PolygonGeneric/Prefabs/Base/SM_Bld_Base_Ceiling_01";
            GameObject ceilingPrefab = Resources.Load<GameObject>(ceilingPrefabPath);

            var roomConfigs = new List<(string name, Vector3 center, Vector2 size)>
            {
                ("货柜码头", new Vector3(-3.25f, 1.85f, 0f), new Vector2(2.55f, 1.8f)),
                ("夜市巷",   new Vector3(0f, 2.05f, 0f),     new Vector2(2.35f, 1.55f)),
                ("专案办公室", new Vector3(3.25f, 1.25f, 0f), new Vector2(2.2f, 1.8f)),
                ("证物库",   new Vector3(-2.8f, -1.9f, 0f),  new Vector2(2.35f, 1.55f)),
                ("地下诊所", new Vector3(2.65f, -2f, 0f),    new Vector2(2.45f, 1.55f)),
            };

            foreach (var cfg in roomConfigs)
            {
                if (ceilingPrefab != null)
                {
                    GameObject ceiling = Instantiate(ceilingPrefab);
                    ceiling.name = cfg.name + " Ceiling";
                    generatedObjects.Add(ceiling);
                    ceiling.transform.position = new Vector3(cfg.center.x, cfg.center.y, FloorZ + 0.38f);
                    ceiling.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                    float scaleX = cfg.size.x / 2.5f; // 标准 prefab 约 2.5 单位宽
                    float scaleY = cfg.size.y / 2.5f;
                    ceiling.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
                else
                {
                    // 回退：简单 Plane
                    GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plane.name = cfg.name + " Ceiling (Fallback)";
                    generatedObjects.Add(plane);
                    plane.transform.position = new Vector3(cfg.center.x, cfg.center.y, FloorZ + 0.38f);
                    plane.transform.localScale = new Vector3(cfg.size.x, cfg.size.y, 0.06f);
                    SetColor(plane, new Color(0.18f, 0.16f, 0.14f, 1f));
                }
            }
        }

        // ──────────────────────────────────────────────
        //  监控摄像头系统（SecurityCamera）
        // ──────────────────────────────────────────────

        private void CreateSecuritySystems()
        {
            if (securityCamera != null) return;

            GameObject secObj = new GameObject("Security Camera System");
            generatedObjects.Add(secObj);
            securityCamera = secObj.AddComponent<SecurityCamera>();
            securityCamera.Initialize(this, generatedObjects);
        }

        private void CreateWall(string wallName, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallName;
            generatedObjects.Add(wall);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            SetColor(wall, new Color(0.22f, 0.2f, 0.16f, 1f));
        }

        private void SetColor(GameObject target, Color color)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                return;
            }

            Material material = new Material(FindColorShader());
            material.color = color;
            renderer.sharedMaterial = material;
            generatedMaterials.Add(material);
        }

        private static Shader FindColorShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void ClearWorld()
        {
            foreach (GameObject generated in generatedObjects)
            {
                if (generated != null)
                {
                    DestroyGenerated(generated);
                }
            }

            foreach (Material material in generatedMaterials)
            {
                if (material != null)
                {
                    DestroyGenerated(material);
                }
            }

            generatedObjects.Clear();
            generatedMaterials.Clear();
            footprintTrails.Clear();
            routeEntries.Clear();
            zones.Clear();
            surveillanceNodes.Clear();
            suspicionScores.Clear();
            footprintTimers.Clear();
            lastTracePositions.Clear();
            lastKnownAreas.Clear();
            taskChallengeOptions.Clear();
            characters.Clear();
            taskStations.Clear();
            bodies.Clear();
            player = null;
            emergencyButton = null;
            activeTaskChallenge = null;
            currentPrimarySuspect = null;
            CleanupMiniGame();

            if (ventSystem != null)
            {
                ventSystem.ClearVisuals();
                DestroyGenerated(ventSystem);
                ventSystem = null;
            }

            if (securityCamera != null)
            {
                securityCamera.Cleanup();
                DestroyGenerated(securityCamera);
                securityCamera = null;
            }

            if (environmentManager != null)
            {
                DestroyGenerated(environmentManager);
                environmentManager = null;
            }
        }

        private static void DestroyGenerated(GameObject generated)
        {
            if (Application.isPlaying)
            {
                Destroy(generated);
            }
            else
            {
                DestroyImmediate(generated);
            }
        }

        private static void DestroyGenerated(UnityEngine.Object generated)
        {
            if (Application.isPlaying)
            {
                Destroy(generated);
            }
            else
            {
                DestroyImmediate(generated);
            }
        }

        private static Vector3 RandomMapPoint()
        {
            return new Vector3(UnityEngine.Random.Range(-4.25f, 4.25f), UnityEngine.Random.Range(-2.85f, 2.85f), 0f);
        }

        private static Vector3 ClampToMap(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, -4.55f, 4.55f),
                Mathf.Clamp(position.y, -3.05f, 3.05f),
                position.z);
        }

        // ──────────────────────────────────────────────
        //  回合制策略层桥接
        // ──────────────────────────────────────────────

        private void InitTurnController()
        {
            if (turnController != null)
            {
                turnController.Changed -= OnTurnStateChanged;
            }

            turnController = new GameController();
            turnController.Changed += OnTurnStateChanged;
        }

        private void EnsureTurnController()
        {
            if (turnController == null)
            {
                InitTurnController();
            }
        }

        private void EnsureRuntimeScaffolding()
        {
            EnsureTurnController();

            if (hudObject == null)
            {
                BuildHud();
            }

            if (turnHudObject == null)
            {
                InitTurnHud();
            }
        }

        private void InitTurnHud()
        {
            EnsureTurnController();

            if (turnHudObject != null)
            {
                DestroyGenerated(turnHudObject);
            }

            turnHudObject = new GameObject("Turn Prototype HUD");
            PrototypeHud hud = turnHudObject.AddComponent<PrototypeHud>();
            hud.Bind(turnController);
        }

        private void InitTurnMap()
        {
            EnsureTurnController();

            if (turnMapObject != null)
            {
                DestroyGenerated(turnMapObject);
            }

            turnMapObject = new GameObject("District Map View");
            DistrictMapView mapView = turnMapObject.AddComponent<DistrictMapView>();
            mapView.Bind(turnController);
        }

        private void OnTurnStateChanged()
        {
            GameState state = turnController.State;

            if (state.Phase == GamePhase.PlayerTurn)
            {
                IsMeeting = false;
                string factionName = state.PlayerFaction == Faction.Gang ? "黑帮" :
                    state.PlayerFaction == Faction.Undercover ? "卧底" : "警察";
                LastEvent = "第" + state.Day + "天 " + factionName + " 回合 —— 选择区域执行行动。";
            }
            else if (state.Phase == GamePhase.AiTurn)
            {
                IsMeeting = false;
                LastEvent = "对手行动中...";
            }
            else if (state.Phase == GamePhase.Meeting)
            {
                // 会议阶段：自动执行 AI 投票并同步淘汰
                if (turnController.ShouldHoldMeeting)
                {
                    turnController.RunMeeting();
                    SyncTurnElimination();

                    // RunMeeting 可能触发 GameOver
                    if (turnController.State.Phase == GamePhase.GameOver)
                    {
                        IsGameOver = true;
                        LastEvent = turnController.State.Result;
                        Changed?.Invoke();
                        return;
                    }
                }
                // RunMeeting 内部已调用 AdvanceToNextDay → Changed，无需再触发
                return;
            }
            else if (state.Phase == GamePhase.GameOver)
            {
                IsGameOver = true;
                LastEvent = state.Result;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// 根据当前选中角色执行回合动作：移动到目标区域并生效。
        /// </summary>
        public void ExecuteTurnAction(SocialCharacter character, DistrictType districtType, PlayerAction action)
        {
            if (turnController == null || character == null || !character.IsAlive) return;

            GameState state = turnController.State;
            if (state.Phase != GamePhase.PlayerTurn) return;

            DistrictState district = state.GetDistrict(districtType);
            turnController.SelectDistrict(districtType);

            // 3D 角色移动到区域
            Vector3 worldPos = GetDistrictWorldPosition(districtType);
            character.transform.position = new Vector3(worldPos.x, worldPos.y, CharacterZ);
            LastEvent = character.CharacterName + " 前往 " + district.DisplayName + "。";

            // 执行策略动作
            turnController.RunPlayerAction(districtType, action);

            Changed?.Invoke();
        }

        /// <summary>
        /// 回合制会议：从存活角色中投票。
        /// 通过 GameController.ForceMeeting() 触发官方会议流程。
        /// </summary>
        public void StartTurnMeeting()
        {
            if (turnController == null) return;

            // 强制触发会议（绕过天数额定检查）
            turnController.ForceMeeting();

            IsMeeting = true;
            LastEvent = "紧急会议开始 —— 选择一个怀疑对象投票。";
            Changed?.Invoke();
        }

        /// <summary>
        /// 会议投票（玩家手动投票）。
        /// 将投票同步到 GameController 并淘汰目标。
        /// </summary>
        public void CastTurnVote(SocialCharacter target)
        {
            if (!IsMeeting || target == null || !target.IsAlive) return;

            // 通过 GameController 走官方会议投票流程（双向渗透模型）
            if (turnController != null && turnController.State.Phase == GamePhase.Meeting)
            {
                turnController.PlayerCastVote(target.Role);
            }

            target.Kill();
            IsMeeting = false;
            RemoveBodiesFor(target);

            string outcome = target.CharacterName + " 被投出局，身份是：" + RoleName(target.Role) + "。";
            LastEvent = outcome;
            AddCaseLog("会议投票", outcome);

            if (target.IsPlayer)
            {
                FinishGame(PlayerRole == SocialRole.Gang
                    ? "警方胜利：你的黑帮身份被投出局。"
                    : "行动失败：你被投出局，港区收网失去关键执行人。");
                Changed?.Invoke();
                return;
            }

            CheckVictory();
            Changed?.Invoke();
        }

        /// <summary>
        /// 跳过投票。
        /// </summary>
        public void SkipTurnVote()
        {
            IsMeeting = false;
            LastEvent = "会议无结果，继续行动。";
            AddCaseLog("会议投票", "跳过投票。");
            Changed?.Invoke();
        }

        /// <summary>
        /// 同步回合制淘汰结果到实时 3D 世界：按 SocialRole 逐个处理淘汰角色。
        /// （双向渗透模型：会议投票淘汰单个角色而非整个阵营）
        /// </summary>
        private void SyncTurnElimination()
        {
            GameState state = turnController.State;

            foreach (SocialCharacter character in characters)
            {
                if (!character.IsAlive) continue;

                bool shouldEliminate = false;
                switch (character.Role)
                {
                    case SocialRole.Gang when state.GangEliminated: shouldEliminate = true; break;
                    case SocialRole.Police when state.PoliceEliminated: shouldEliminate = true; break;
                    case SocialRole.Undercover when state.UndercoverEliminated: shouldEliminate = true; break;
                    case SocialRole.Mole when state.MoleEliminated: shouldEliminate = true; break;
                }

                if (!shouldEliminate) continue;

                character.Kill();
                RemoveBodiesFor(character);
                string roleLabel = RoleName(character.Role);
                LastEvent = character.CharacterName + "（" + roleLabel + "）在会议中被淘汰。";
                AddCaseLog("会议投票", character.CharacterName + " 被投出局，身份：" + roleLabel + "。");

                if (character.IsPlayer)
                {
                    bool isPlayerGangSide = PlayerRole == SocialRole.Gang || PlayerRole == SocialRole.Mole;
                    FinishGame(isPlayerGangSide
                        ? "警方胜利：你的黑帮身份在会议中被投出局。"
                        : "行动失败：你在会议中被淘汰，港区行动失去关键执行人。");
                    return;
                }

                break;
            }

            CheckVictory();
        }

        /// <summary>
        /// 区域名 → DistrictType。
        /// </summary>
        private static DistrictType GetDistrictForZone(string zoneName)
        {
            switch (zoneName)
            {
                case "货柜码头": return DistrictType.Dockyard;
                case "夜市巷": return DistrictType.NightMarket;
                case "专案办公室": return DistrictType.PolicePrecinct;
                case "证物库": return DistrictType.WarehouseRow;
                case "地下诊所": return DistrictType.Clinic;
                case "主街": return DistrictType.TenementBlock;
                default: return DistrictType.Dockyard;
            }
        }

        /// <summary>
        /// DistrictType → 3D 世界坐标。
        /// </summary>
        private static Vector3 GetDistrictWorldPosition(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Dockyard: return new Vector3(-3.25f, 1.85f, 0f);
                case DistrictType.WarehouseRow: return new Vector3(-2.8f, -1.9f, 0f);
                case DistrictType.NightMarket: return new Vector3(0f, 2.05f, 0f);
                case DistrictType.PolicePrecinct: return new Vector3(3.25f, 1.25f, 0f);
                case DistrictType.Clinic: return new Vector3(2.65f, -2f, 0f);
                case DistrictType.TenementBlock: return new Vector3(0f, 0.05f, 0f);
                default: return Vector3.zero;
            }
        }

        private static Faction GetFactionForRole(SocialRole role)
        {
            switch (role)
            {
                case SocialRole.Gang: return Faction.Gang;
                case SocialRole.Police: return Faction.Police;
                case SocialRole.Undercover: return Faction.Undercover;
                case SocialRole.Mole: return Faction.Gang;
                default: return Faction.Police;
            }
        }

        private static SocialRole GetRoleForFaction(Faction faction)
        {
            switch (faction)
            {
                case Faction.Gang: return SocialRole.Gang;
                case Faction.Police: return SocialRole.Police;
                case Faction.Undercover: return SocialRole.Undercover;
                default: return SocialRole.Police;
            }
        }

        private sealed class FootprintTrail : MonoBehaviour
        {
            private MeshRenderer meshRenderer;
            private Color baseColor;

            public SocialCharacter Owner { get; private set; }
            public float RemainingSeconds { get; set; }

            public void Bind(SocialCharacter owner, float lifetimeSeconds)
            {
                Owner = owner;
                RemainingSeconds = lifetimeSeconds;
                meshRenderer = GetComponent<MeshRenderer>();

                if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                {
                    baseColor = meshRenderer.sharedMaterial.color;
                }
            }

            public void Refresh(float normalizedLifetime)
            {
                if (meshRenderer == null || meshRenderer.sharedMaterial == null)
                {
                    return;
                }

                Color color = baseColor;
                color.a = Mathf.Lerp(0.04f, baseColor.a, normalizedLifetime);
                meshRenderer.sharedMaterial.color = color;
            }
        }

        private sealed class RouteEntry
        {
            public RouteEntry(SocialCharacter character, string areaName, Vector3 position, float roundTime)
            {
                Character = character;
                AreaName = areaName;
                Position = position;
                RoundTime = roundTime;
            }

            public SocialCharacter Character { get; }
            public string AreaName { get; }
            public Vector3 Position { get; }
            public float RoundTime { get; }
        }

        private sealed class NamedZone
        {
            private readonly Vector2 halfSize;

            public NamedZone(string name, Vector3 center, Vector2 size)
            {
                Name = name;
                Center = center;
                halfSize = size * 0.5f;
            }

            public string Name { get; }
            public Vector3 Center { get; }

            public bool Contains(Vector3 position)
            {
                return Mathf.Abs(position.x - Center.x) <= halfSize.x && Mathf.Abs(position.y - Center.y) <= halfSize.y;
            }
        }

        private sealed class SurveillanceNode
        {
            public SurveillanceNode(string nodeName, Vector3 position, float radius)
            {
                NodeName = nodeName;
                Position = position;
                Radius = radius;
            }

            public string NodeName { get; }
            public Vector3 Position { get; }
            public float Radius { get; }
        }

        // ─── 阵营判断辅助 ──────────────────────────────

        /// <summary>判断两个角色是否属于同一阵营（卧底+警察为友方，黑帮为敌方）。</summary>
        private static bool IsSameFaction(SocialRole a, SocialRole b)
        {
            Faction fa = GetFaction(a);
            Faction fb = GetFaction(b);
            return fa == fb;
        }

        /// <summary>将 SocialRole 映射到 Faction。</summary>
        private static Faction GetFaction(SocialRole role)
        {
            return role switch
            {
                SocialRole.Gang       => Faction.Gang,
                SocialRole.Undercover => Faction.Undercover,
                SocialRole.Police     => Faction.Police,
                SocialRole.Mole       => Faction.Gang,
                _                    => Faction.None,
            };
        }
    }
}
