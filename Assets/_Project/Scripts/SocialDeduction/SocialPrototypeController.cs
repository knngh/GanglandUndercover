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

        private readonly List<SocialCharacter> characters = new List<SocialCharacter>();
        private readonly List<TaskStation> taskStations = new List<TaskStation>();
        private readonly List<BodyMarker> bodies = new List<BodyMarker>();
        private readonly List<GameObject> generatedObjects = new List<GameObject>();

        private SocialCharacter player;
        private EmergencyButton emergencyButton;
        private float playerKillCooldown;
        private float aiKillCooldown;
        private float blackoutTimer;

        public event Action Changed;

        public GameLanguage Language { get; private set; } = GameLanguage.Chinese;
        public bool HasStarted { get; private set; }
        public bool IsMeeting { get; private set; }
        public bool IsGameOver { get; private set; }
        public SocialRole PlayerRole { get; private set; } = SocialRole.Police;
        public string LastEvent { get; private set; } = "选择身份开始游戏。";
        public string MeetingReason { get; private set; } = string.Empty;
        public string ResultText { get; private set; } = string.Empty;
        public string CurrentClue { get; private set; } = string.Empty;
        public IReadOnlyList<SocialCharacter> Characters => characters;
        public int CompletedTasks => taskStations.Count(task => task.IsCompleted);
        public int TotalTasks => taskStations.Count;
        public float PlayerKillCooldown => playerKillCooldown;
        public bool IsBlackout => blackoutTimer > 0f;
        public float BlackoutTimer => blackoutTimer;

        private void Awake()
        {
            BuildHud();
        }

        private void Update()
        {
            if (!HasStarted || IsMeeting || IsGameOver)
            {
                return;
            }

            TickCooldowns();
            MovePlayer();
            MoveBots();
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
            IsMeeting = false;
            IsGameOver = false;
            ResultText = string.Empty;
            MeetingReason = string.Empty;
            playerKillCooldown = 0f;
            aiKillCooldown = AiKillCooldownSeconds;
            blackoutTimer = 0f;
            CurrentClue = string.Empty;

            BuildWorld();
            LastEvent = role == SocialRole.Gang
                ? "你是黑帮。伪装、击倒、阻止警方完成任务。"
                : "你是警方阵营。完成任务，报告尸体，找出黑帮。";
            Changed?.Invoke();
        }

        public void CastVote(SocialCharacter target)
        {
            if (!IsMeeting || target == null || !target.IsAlive)
            {
                return;
            }

            target.Kill();
            IsMeeting = false;
            bodies.RemoveAll(body => body == null || body.Victim == target);
            CurrentClue = string.Empty;
            LastEvent = target.CharacterName + " 被投出局。";
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
            LastEvent = "会议无结果，所有人继续行动。";
            Changed?.Invoke();
        }

        private void TickCooldowns()
        {
            if (playerKillCooldown > 0f)
            {
                playerKillCooldown -= Time.deltaTime;
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
                    Changed?.Invoke();
                }
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

                if (character.BotDecisionTimer <= 0f)
                {
                    character.BotDirection = UnityEngine.Random.insideUnitCircle.normalized;
                    character.BotDecisionTimer = UnityEngine.Random.Range(1.2f, 2.8f);
                }

                Vector3 direction = new Vector3(character.BotDirection.x, character.BotDirection.y, 0f);
                float speed = IsBlackout ? BotSpeed * 0.75f : BotSpeed;
                character.transform.position = ClampToMap(character.transform.position + direction * speed * Time.deltaTime);
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
                    task.ResetTask();
                    TriggerBlackout();
                    LastEvent = "你破坏了任务点并制造断电。趁混乱制造不在场证明。";
                }
                else if (!task.IsCompleted)
                {
                    task.Complete();
                    if (IsBlackout)
                    {
                        blackoutTimer = 0f;
                        LastEvent = "你修复了断电。视野恢复。";
                    }
                    else
                    {
                        LastEvent = "任务完成。继续完成任务或留意可疑人员。";
                    }

                    CheckVictory();
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
            LastEvent = "你击倒了 " + target.CharacterName + "。尽快离开现场。";
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

            StartMeeting("发现 " + body.Victim.CharacterName + " 的尸体。");
        }

        private void StartMeeting(string reason)
        {
            IsMeeting = true;
            MeetingReason = reason;
            LastEvent = reason + " 选择一个怀疑对象投票。";
            Changed?.Invoke();
        }

        private void KillCharacter(SocialCharacter target)
        {
            target.Kill();

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = target.CharacterName + " Body";
            generatedObjects.Add(bodyObject);
            bodyObject.transform.position = target.transform.position;
            bodyObject.transform.localScale = new Vector3(0.45f, 0.22f, 0.18f);
            bodyObject.GetComponent<MeshRenderer>().material.color = new Color(0.75f, 0.05f, 0.04f, 1f);

            BodyMarker marker = bodyObject.AddComponent<BodyMarker>();
            marker.Bind(target);
            bodies.Add(marker);
            CurrentClue = BuildClue(target);
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
                return "线索：尸体附近没有明显目击者。";
            }

            float distance = Vector3.Distance(nearestAlive.transform.position, victim.transform.position);
            string certainty = distance < 1.6f ? "强" : "弱";
            return "线索(" + certainty + ")：尸体附近最后看到 " + nearestAlive.CharacterName + "。这不是铁证。";
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

            if (CompletedTasks >= TotalTasks && TotalTasks > 0)
            {
                FinishGame(PlayerRole == SocialRole.Gang ? "警方胜利：所有任务完成。" : "警方阵营胜利：所有任务完成。");
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
            IsMeeting = false;
            ResultText = result;
            LastEvent = result;
        }

        private TaskStation FindNearestTask()
        {
            return taskStations
                .Where(task => Vector3.Distance(player.transform.position, task.transform.position) <= InteractRange)
                .OrderBy(task => Vector3.Distance(player.transform.position, task.transform.position))
                .FirstOrDefault();
        }

        private void FollowCamera()
        {
            if (Camera.main == null || player == null)
            {
                return;
            }

            Vector3 position = player.transform.position;
            Camera.main.transform.position = new Vector3(position.x, position.y, -10f);
            Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, IsBlackout ? 2.75f : 4.6f, Time.deltaTime * 4f);
        }

        private void BuildHud()
        {
            GameObject hudObject = new GameObject("Social Prototype HUD");
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
            CreateFloor();
            CreateWalls();
            CreateTask("仓库接线", new Vector3(-4f, 1.8f, 0f));
            CreateTask("调取监控", new Vector3(0.1f, 2.6f, 0f));
            CreateTask("修复电闸", new Vector3(3.9f, 1.2f, 0f));
            CreateTask("扫描证据", new Vector3(-2.7f, -2.6f, 0f));
            CreateTask("上传档案", new Vector3(2.8f, -2.5f, 0f));
            CreateEmergencyButton(new Vector3(0f, 0f, 0f));
            CreateCharacters();
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

            CreateCharacter("阿辉", botRoles[0], false, new Vector3(-1.6f, 1.1f, 0f));
            CreateCharacter("老周", botRoles[1], false, new Vector3(1.6f, 1.2f, 0f));
            CreateCharacter("小林", botRoles[2], false, new Vector3(2.3f, -1.3f, 0f));
            CreateCharacter("刀疤", botRoles[3], false, new Vector3(-2.2f, -1.7f, 0f));
        }

        private SocialCharacter CreateCharacter(string characterName, SocialRole role, bool isPlayer, Vector3 position)
        {
            GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            characterObject.name = characterName;
            generatedObjects.Add(characterObject);
            characterObject.transform.position = position;
            characterObject.transform.localScale = new Vector3(0.55f, 0.55f, 0.2f);

            TextMesh label = CreateWorldLabel(characterObject.transform, new Vector3(0f, 0.55f, -0.12f), 0.14f);
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
            taskObject.transform.position = position;
            taskObject.transform.localScale = new Vector3(0.75f, 0.75f, 0.18f);

            CreateWorldLabel(taskObject.transform, new Vector3(0f, 0.68f, -0.12f), 0.12f);

            TaskStation station = taskObject.AddComponent<TaskStation>();
            station.Bind(taskName);
            taskStations.Add(station);
        }

        private void CreateEmergencyButton(Vector3 position)
        {
            GameObject buttonObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buttonObject.name = "Emergency Button";
            generatedObjects.Add(buttonObject);
            buttonObject.transform.position = position;
            buttonObject.transform.localScale = new Vector3(0.55f, 0.12f, 0.55f);
            buttonObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            buttonObject.GetComponent<MeshRenderer>().material.color = new Color(0.78f, 0.08f, 0.05f, 1f);

            CreateWorldLabel(buttonObject.transform, new Vector3(0f, 0.62f, -0.12f), 0.12f);
            emergencyButton = buttonObject.AddComponent<EmergencyButton>();
        }

        private static TextMesh CreateWorldLabel(Transform parent, Vector3 localPosition, float characterSize)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;

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
            floor.transform.position = new Vector3(0f, 0f, 0.25f);
            floor.transform.localScale = new Vector3(10f, 7f, 0.1f);
            floor.GetComponent<MeshRenderer>().material.color = new Color(0.1f, 0.11f, 0.1f, 1f);
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
            wall.GetComponent<MeshRenderer>().material.color = new Color(0.22f, 0.2f, 0.16f, 1f);
        }

        private void ClearWorld()
        {
            foreach (GameObject generated in generatedObjects)
            {
                if (generated != null)
                {
                    Destroy(generated);
                }
            }

            generatedObjects.Clear();
            characters.Clear();
            taskStations.Clear();
            bodies.Clear();
            player = null;
            emergencyButton = null;
        }

        private static Vector3 ClampToMap(Vector3 position)
        {
            return new Vector3(
                Mathf.Clamp(position.x, -4.55f, 4.55f),
                Mathf.Clamp(position.y, -3.05f, 3.05f),
                0f);
        }
    }
}
