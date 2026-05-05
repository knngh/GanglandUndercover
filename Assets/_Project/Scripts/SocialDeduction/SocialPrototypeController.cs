using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GanglandUndercover.Core;
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
        private TaskStation activeTaskChallenge;
        private float playerKillCooldown;
        private float aiKillCooldown;
        private float blackoutTimer;
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

        public event Action Changed;

        public GameLanguage Language { get; private set; } = GameLanguage.Chinese;
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
        public bool IsTaskChallengeVisible => activeTaskChallenge != null;
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

        private void Awake()
        {
            BuildHud();
            StartGame(SocialRole.Undercover);
        }

        private void OnDestroy()
        {
            ClearWorld();

            if (hudObject != null)
            {
                DestroyGenerated(hudObject);
                hudObject = null;
            }
        }

        private void Update()
        {
            if (activeTaskChallenge != null)
            {
                HandleTaskChallengeInput();
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
            HandleInput();
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
                ? "你是黑帮线人。伪装巡逻、制造断电、阻止专案组收网。"
                : role == SocialRole.Undercover
                    ? "你是潜伏探员。完成取证任务，报告倒下的人，别暴露路线。"
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
            if (PlayerRole != SocialRole.Gang)
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

        private void StartTaskChallenge(TaskStation task, bool sabotage)
        {
            activeTaskChallenge = task;
            activeTaskIsSabotage = sabotage;
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
            Changed?.Invoke();
        }

        private void KillCharacter(SocialCharacter target)
        {
            target.Kill();

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
                FinishGame("黑帮胜利：你被击倒，港区证据链中断。");
            }
        }

        private void TriggerBlackout()
        {
            blackoutTimer = BlackoutDurationSeconds;
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

            if (PlayerRole == SocialRole.Gang && target != null)
            {
                return playerKillCooldown <= 0f
                    ? "Q 击倒：" + target.CharacterName
                    : "击倒冷却：" + Mathf.CeilToInt(playerKillCooldown) + "s";
            }

            TaskStation task = FindNearestTask();

            if (task != null)
            {
                if (PlayerRole == SocialRole.Gang)
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

            return "靠近蓝色任务点取证，红色按钮开会，发现尸体按 R。";
        }

        private void FollowCamera()
        {
            if (Camera.main == null || player == null)
            {
                return;
            }

            Vector3 position = player.transform.position;
            Vector3 target = new Vector3(position.x, position.y, CameraTargetZ);
            Vector3 desired = new Vector3(position.x, position.y - CameraFollowDistance, -CameraFollowHeight);

            Camera.main.transform.position = Vector3.Lerp(
                Camera.main.transform.position,
                desired,
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
            ConfigureSceneLighting();
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
            CreateCharacters();
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
            player = CreateCharacter("你", PlayerRole, true, new Vector3(-3.2f, -0.8f, 0f));

            List<SocialRole> botRoles = new List<SocialRole>
            {
                SocialRole.Police,
                SocialRole.Police,
                SocialRole.Undercover,
                SocialRole.Gang
            };

            if (PlayerRole == SocialRole.Gang)
            {
                botRoles[3] = SocialRole.Police;
            }

            CreateCharacter("巡警陈", botRoles[0], false, new Vector3(-1.6f, 1.1f, 0f));
            CreateCharacter("技侦周", botRoles[1], false, new Vector3(1.6f, 1.2f, 0f));
            CreateCharacter("线人林", botRoles[2], false, new Vector3(2.3f, -1.3f, 0f));
            CreateCharacter("疤脸", botRoles[3], false, new Vector3(-2.2f, -1.7f, 0f));
        }

        private SocialCharacter CreateCharacter(string characterName, SocialRole role, bool isPlayer, Vector3 position)
        {
            GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            characterObject.name = characterName;
            generatedObjects.Add(characterObject);
            characterObject.transform.position = new Vector3(position.x, position.y, CharacterZ);
            characterObject.transform.localScale = new Vector3(0.42f, 0.42f, 0.82f);
            characterObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name = characterName + " Shadow";
            generatedObjects.Add(shadow);
            shadow.transform.SetParent(characterObject.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.08f, 0.52f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(0.74f, 0.035f, 0.48f);
            SetColor(shadow, new Color(0f, 0f, 0f, 0.32f));

            TextMesh label = CreateWorldLabel(characterObject.transform, new Vector3(0f, 0.86f, -0.32f), 0.13f);
            label.text = characterName;

            SocialCharacter character = characterObject.AddComponent<SocialCharacter>();
            character.Bind(characterName, role, isPlayer);
            characters.Add(character);
            return character;
        }

        private void CreateTask(string taskName, Vector3 position)
        {
            GameObject taskObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            taskObject.name = taskName;
            generatedObjects.Add(taskObject);
            taskObject.transform.position = new Vector3(position.x, position.y, CharacterZ + 0.1f);
            taskObject.transform.localScale = new Vector3(0.82f, 0.62f, 0.42f);

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
            GameObject room = GameObject.CreatePrimitive(PrimitiveType.Cube);
            room.name = roomName;
            generatedObjects.Add(room);
            room.transform.position = position;
            room.transform.localScale = scale;
            SetColor(room, color);

            TextMesh label = CreateWorldLabel(room.transform, new Vector3(0f, 0f, LabelZ), 0.12f);
            label.text = roomName;
            label.color = new Color(0.86f, 0.82f, 0.68f, 1f);

            CreateRoomTrim(room.transform, roomName + " North Trim", new Vector3(0f, 0.52f, -0.72f), new Vector3(1f, 0.05f / Mathf.Max(0.01f, scale.y), 0.58f / Mathf.Max(0.01f, scale.z)));
            CreateRoomTrim(room.transform, roomName + " South Trim", new Vector3(0f, -0.52f, -0.72f), new Vector3(1f, 0.05f / Mathf.Max(0.01f, scale.y), 0.58f / Mathf.Max(0.01f, scale.z)));
            CreateRoomTrim(room.transform, roomName + " West Trim", new Vector3(-0.52f, 0f, -0.72f), new Vector3(0.05f / Mathf.Max(0.01f, scale.x), 1f, 0.58f / Mathf.Max(0.01f, scale.z)));
            CreateRoomTrim(room.transform, roomName + " East Trim", new Vector3(0.52f, 0f, -0.72f), new Vector3(0.05f / Mathf.Max(0.01f, scale.x), 1f, 0.58f / Mathf.Max(0.01f, scale.z)));
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
    }
}
