using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using GanglandUndercover;
using GanglandUndercover.Core;
using GanglandUndercover.Online;
using GanglandUndercover.Online.Surveillance;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {

        // --- CountActiveNamedWorldObjects ---
        public int StageTwoActiveMeetingPoseCount => CountActiveNamedWorldObjects("Stage2 Meeting");
        public int StageTwoActiveDownedStateCount => CountActiveNamedWorldObjects("Stage2 Downed");
        public int StageTwoActiveVoiceRadiusCount => CountActiveNamedWorldObjects("Stage2 VoiceRadius");
        public int StageTwoActiveReportFeedbackCount => CountActiveNamedWorldObjects("Stage2 Report");
        public int StageTwoActiveVoteFeedbackCount => CountActiveNamedWorldObjects("Stage2 Vote");
        public int StageTwoForensicSceneCount => CountNamedWorldObjects("Stage2 Forensic");
        public int StageTwoRuntimeRigCount => worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<StageTwoCharacterRig>(true).Length;
        public int StageTwoConfiguredRigCount => CountConfiguredStageTwoRigs();
        public int Character2DAnimationControllerCount => CountCharacter2DAnimationControllers();
        public int Character2DReadyRendererCount => CountCharacter2DReadyRenderers();
        public int Character2DDirectionIndicatorCount => CountNamedWorldObjects("2DDir_");
        public int Character2DVisibleDirectionCount => CountCharacter2DVisibleDirections();
        public int Character2DWalkingFrameCount => CountCharacter2DWalkingFrames();
        public int TaskMiniGameCanvasElementCount => onlineHud == null ? 0 : onlineHud.TaskMiniGameCanvasElementCount;
        public int TaskOverlayVisualElementCount => onlineHud == null ? 0 : onlineHud.TaskOverlayVisualElementCount;
        public int TaskOverlay2DAssetElementCount => onlineHud == null ? 0 : onlineHud.TaskOverlay2DAssetElementCount;
        public int MeetingSeatCanvasElementCount => onlineHud == null ? 0 : onlineHud.MeetingSeatCanvasElementCount;
        public int MeetingOverlayVisualElementCount => onlineHud == null ? 0 : onlineHud.MeetingOverlayVisualElementCount;
        public int MeetingOverlay2DAssetElementCount => onlineHud == null ? 0 : onlineHud.MeetingOverlay2DAssetElementCount;
        public int ChatPanelCanvasElementCount => onlineHud == null ? 0 : onlineHud.ChatPanelCanvasElementCount;
        public int HudButtonSfxFeedbackCount => onlineHud == null ? 0 : onlineHud.ButtonSfxFeedbackCount;
        public int ChatSafetyCanvasActionCount => onlineHud == null ? 0 : onlineHud.ChatSafetyCanvasActionCount;
        public bool CanvasHudLayoutComplete => onlineHud != null && onlineHud.HasCompleteLayout;
        public int LargePortVistaCount => CountNamedWorldObjects("大场景港区层");
        public int UnderworldPassageNodeCount => worldRoot == null ? 0 : CountNamedWorldObjects("暗线节点");
        public bool HasRuntimeAudio => audioSource != null;
        public int RoomMinPlayers => roomMinPlayers;
        public int RoomMaxPlayers => roomMaxPlayers;
        public int MinimumRoomPlayersValue => ruleSet.MinimumRoomPlayers;
        public int MaximumRoomPlayersValue => ruleSet.MaximumRoomPlayers;
        public int MinEvidenceTargetValue => ruleSet.MinEvidenceTarget;
        public int MaxEvidenceTargetValue => ruleSet.MaxEvidenceTarget;
        public int EvidenceScore => taskService.EvidenceScore;
        public int EvidenceTarget => taskService.EvidenceTarget;
        public float MatchElapsedSeconds => matchElapsedSeconds;
        public float MapHalfWidthValue => mapService.MapHalfWidth;
        public float MapHalfHeightValue => mapService.MapHalfHeight;
        public OnlineMapService MapService => mapService;
        public OnlineTaskService TaskService => taskService;

        // M8.4: 对局统计数据暴露（供 MatchStatsCollector 读取）
        public int MeetingCount => _meetingCount;
        public int KillCount => killSystem != null ? killSystem.killCount : 0;
        public OnlineBotController BotController => _botController;

        // E4: 破坏 VFX 系统
        internal GanglandUndercover.Art.SabotageVFX sabotageVFX;
        public NetworkManager NetworkManager => networkManager;
        public OnlineRuleSet RuleSet => ruleSet;
        public int TargetMatchMinutesMin => Mathf.RoundToInt(ruleSet.MatchTargetMinSeconds / 60f);
        public int TargetMatchMinutesMax => Mathf.RoundToInt(ruleSet.MatchHardLimitSeconds / 60f);
        public string ResultSummary => resultSummary;
        public bool AutoFillAi => roomAutoFillAi;
        public bool RevealRoleOnEject => revealRoleOnEject;
        public bool ProximityVoiceEnabled => false;
        public bool LocalReady => localReady;
        public bool CanStartMatch => CanStartLobbyMatch();
        public bool RelayOperationInProgress => relayOperationInProgress;
        public int EmergencyMeetingsLeft => emergencyMeetingsLeft;
        public float EmergencyCooldownTimer => emergencyCooldownTimer;
        public float ReportCooldownTimer => killSystem != null ? killSystem.reportCooldownTimer : 0f;
        internal int NextBodyId => killSystem != null ? killSystem.nextBodyId : 0;
        internal void IncrementNextBodyId() { if (killSystem != null) killSystem.nextBodyId++; }

        // --- TickCharacterAnimators ---
        private void TickCharacterAnimators()
        {
            if (players == null) return;

            foreach (var kv in players)
            {
                OnlinePlayerState state = kv.Value;
                var socialChar = state.SocialChar;
                if (socialChar == null) continue;

                // 死亡状态（通过 SocialCharacter.Kill 设置 Dead bool）
                if (!state.Alive)
                {
                    socialChar.Kill();

                    // M3: Ghost transparency for 2D characters
                    if (state.Character2DDirectionIndicator != null)
                    {
                        SpriteRenderer bodyRenderer = state.Character2DDirectionIndicator
                            .transform.parent?.GetComponent<SpriteRenderer>();
                        SpriteRenderer dirRenderer = state.Character2DDirectionIndicator.GetComponent<SpriteRenderer>();
                        if (bodyRenderer != null)
                        {
                            // B2: 淘汰 = 半透明灰紫
                            bodyRenderer.color = new Color(0.28f, 0.28f, 0.35f, 0.4f);
                        }
                        if (dirRenderer != null)
                        {
                            dirRenderer.color = new Color(0.28f, 0.28f, 0.35f, 0.4f);
                        }
                    }
                    if (state.HasPendingAction)
                    {
                        state.HasPendingAction = false;
                        players[kv.Key] = state;
                    }
                    continue;
                }

                // 移动速度（通过 SocialCharacter.SetMoveSpeed）
                float speed = state.Input.magnitude;
                socialChar.SetMoveSpeed(speed);

                // M3: Update 2D direction indicator rotation based on movement input
                if (state.Character2DDirectionIndicator != null && speed > 0.01f)
                {
                    float angle = Mathf.Atan2(state.Input.y, state.Input.x) * Mathf.Rad2Deg - 90f;
                    state.Character2DDirectionIndicator.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                // Action trigger（通过 SocialCharacter.TriggerAction）
                if (state.HasPendingAction)
                {
                    socialChar.TriggerAction();
                    state.HasPendingAction = false;
                    players[kv.Key] = state;
                }
            }
        }

        // --- LocalCameraTarget ---
        private Vector3 LocalCameraTarget()
        {
            EnsureCameraRig();
            return _cameraRig.GetTarget(players, LocalClientId(), localPosition);
        }

        // --- BuildDefaultTasks ---
        private void BuildDefaultTasks()
        {
            EnsureCoreServices();

            tasks.Clear();
            // 大地图固定任务站点始终铺满（关卡内容，与人数无关）。
            // 人数缩放（TotalTaskCount = 非Gang人数 × tasksPerNonGangPlayer）作用于
            // 每位玩家需完成的任务配额/证据目标，而不是地图上存在的站点数量。
            for (int id = 0; id < OnlineMapService.TaskStationCount; id++)
            {
                tasks.Add(new OnlineTaskState(id, OnlineWorldBuilder.TaskNameFor(id), mapService.TaskPositionFor(id), 0, OnlineMatchUtils.TaskRequiredProgress(id), false, false));
            }
        }

        // --- SetTask ---
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

        // --- ConfigureMainCamera ---
        private void ConfigureMainCamera()
        {
            _cameraRig.Configure(phase, tacticalMapOpen, activeTaskId,
                taskService.BlackoutTimer, players, LocalClientId(), localPosition);
        }

        // --- UpdateWorldVisuals ---
        private void UpdateWorldVisuals()
        {
            UpdateTaskVisuals();
            UpdatePlayerVisuals();
            UpdateBodyVisuals();
            UpdateAreaLabelVisibility();
            BillboardWorldLabels();
        }

        // --- UpdateTaskVisuals ---
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
                OnlineWorldBuilder.SetSortingFromZ(visual);
            }

            OnlineMatchUtils.RemoveStaleVisuals(taskVisuals, seen);
        }

        // --- UpdatePlayerVisuals ---
        private void UpdatePlayerVisuals()
        {
            HashSet<ulong> seen = new HashSet<ulong>();
            ulong localClientId = LocalClientId();
            _cameraRig.SetSubject(localClientId);

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
                SyncCharacterAnimationState(visual, state);
                OnlineWorldBuilder.SetPlayerVisualColors(visual, state, isLocalPlayer);
                UpdatePlayerStageTwoStateLayer(visual, state, isLocalPlayer);
                OnlineWorldBuilder.SetSortingFromZ(visual);

                TextMesh[] labels = visual.GetComponentsInChildren<TextMesh>(true);

                for (int i = 0; i < labels.Length; i++)
                {
                    TextMesh label = labels[i];
                    label.text = BuildPlayerWorldLabel(state, isLocalPlayer);
                    bool showLabel = ShouldShowPlayerWorldLabel(state, isLocalPlayer) && IsNearCameraSubject(state.Position);
                    OnlineWorldBuilder.SetTextMeshVisible(label, showLabel);
                    OnlineWorldBuilder.BillboardLabel(label.transform);
                }
            }

            RemoveStalePlayerVisuals(seen);
        }

        private static void SyncCharacterAnimationState(GameObject visual, OnlinePlayerState state)
        {
            if (visual == null)
            {
                return;
            }

            GanglandUndercover.Art.CharacterAnimController[] animators =
                visual.GetComponentsInChildren<GanglandUndercover.Art.CharacterAnimController>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].RefreshState(state);
                }
            }
        }

        // --- RemoveStalePlayerVisuals ---
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

        // --- UpdateBodyVisuals ---
        private void UpdateBodyVisuals()
        {
            if (killSystem != null)
                killSystem.UpdateBodyVisuals();
        }

        private int CountCharacter2DAnimationControllers()
        {
            return worldRoot == null ? 0 : worldRoot.GetComponentsInChildren<GanglandUndercover.Art.CharacterAnimController>(true).Length;
        }

        private int CountCharacter2DReadyRenderers()
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;
            GanglandUndercover.Art.CharacterAnimController[] animators =
                worldRoot.GetComponentsInChildren<GanglandUndercover.Art.CharacterAnimController>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                GanglandUndercover.Art.CharacterAnimController animator = animators[i];

                if (animator != null
                    && animator.BodyRenderer != null
                    && animator.DirRenderer != null
                    && animator.CurrentBodySprite != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountCharacter2DVisibleDirections()
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;
            GanglandUndercover.Art.CharacterAnimController[] animators =
                worldRoot.GetComponentsInChildren<GanglandUndercover.Art.CharacterAnimController>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].DirectionVisible)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountCharacter2DWalkingFrames()
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;
            GanglandUndercover.Art.CharacterAnimController[] animators =
                worldRoot.GetComponentsInChildren<GanglandUndercover.Art.CharacterAnimController>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i].WalkFrameIndex > 0)
                {
                    count++;
                }
            }

            return count;
        }

        // --- AnimatePlayerVisual ---
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

        // --- UpdateAreaLabelVisibility ---
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

                OnlineWorldBuilder.SetTextMeshVisible(label, visible);
            }
        }

        // --- BillboardWorldLabels ---
        private void BillboardWorldLabels()
        {
            foreach (TextMesh label in worldLabels)
            {
                if (label == null)
                {
                    continue;
                }

                OnlineWorldBuilder.BillboardLabel(label.transform);
            }
        }

        // --- CreateTaskVisual ---
        private GameObject CreateTaskVisual(OnlineTaskState task)
        {
            return worldBuilder.CreateTaskVisual(task, worldRoot.transform);
        }

        // --- SetTaskVisualState ---
        private void SetTaskVisualState(GameObject visual, OnlineTaskState task)
        {
            worldBuilder.SetTaskVisualState(visual, task);
        }

        // --- CreateTaskEquipment ---
        private void CreateTaskEquipment(Transform parent, int taskId)
        {
            worldBuilder.CreateTaskEquipment(parent, taskId);
        }

        // --- CreatePlayerVisual ---
        private GameObject CreatePlayerVisual(OnlinePlayerState state)
        {
            GameObject visual = worldBuilder.CreatePlayerVisual(state, state.ClientId == LocalClientId());
            if (visual != null)
            {
                CreateFreeCharacterAdapter(visual.transform, state);
            }

            return visual;
        }

        // --- CreateStageTwoCharacterRig ---
        private void CreateStageTwoCharacterRig(GameObject playerObject, OnlinePlayerState state)
        {
            worldBuilder.CreateStageTwoCharacterRig(playerObject, state);
        }

        // --- CreateStageTwoCharacterStateLayer ---
        private void CreateStageTwoCharacterStateLayer(Transform parent, OnlinePlayerState state)
        {
            worldBuilder.CreateStageTwoCharacterStateLayer(parent, state);
        }

        // --- CreateProfessionAccessory ---
        private void CreateProfessionAccessory(Transform parent, OnlinePlayerState state)
        {
            worldBuilder.CreateProfessionAccessory(parent, state);
        }

        // --- CreateBodyVisual ---
        private GameObject CreateBodyVisual(OnlineBodyState body)
        {
            // P2-1: Use victim's character sprite for the body
            Sprite bodySprite = GetBodySpriteForClient(body.VictimClientId);
            return worldBuilder.CreateBodyVisual(body, bodySprite);
        }

        internal Sprite GetBodySpriteForClient(ulong clientId)
        {
            OnlineProfession prof = players.TryGetValue(clientId, out var victim)
                ? victim.Profession : OnlineProfession.Inspector;
            return GetBodySpriteForProfession(prof);
        }

        private Sprite GetBodySpriteForProfession(OnlineProfession prof)
        {
            // Try to get the front idle frame from CC0 character set
            if (GanglandUndercover.Art.Sprite2DAssetCache.CharacterSets.TryGetValue(prof, out var set))
            {
                return set.Front_Frame0 ?? set.Front_Frame1 ?? set.Front_Frame2;
            }
            return null;
        }

        // --- CreateSolidPrimitiveProp ---
        private GameObject CreateSolidPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color)
        {
            return worldBuilder.CreateSolidPrimitiveProp(propName, primitiveType, position, scale, color);
        }

        // --- CreateProp ---
        private GameObject CreateProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            return worldBuilder.CreateProp(propName, position, scale, color);
        }

        // --- CreateSolidProp ---
        private GameObject CreateSolidProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            return worldBuilder.CreateSolidProp(propName, position, scale, color);
        }

        // --- CreateShapeProp ---
        private GameObject CreateShapeProp(string propName, Sprite sprite, Vector3 position, Vector3 scale, Color color)
        {
            return worldBuilder.CreateShapeProp(propName, sprite, position, scale, color);
        }

        // --- CreateRotatedProp ---
        private GameObject CreateRotatedProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees)
        {
            return worldBuilder.CreateRotatedProp(propName, position, scale, color, rotationDegrees);
        }

        // --- CreateMeshBoxProp ---
        private GameObject CreateMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return worldBuilder.CreateMeshBoxProp(propName, position, scale, color, rotationDegrees);
        }

        // --- CreateSolidMeshBoxProp ---
        private GameObject CreateSolidMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return worldBuilder.CreateSolidMeshBoxProp(propName, position, scale, color, rotationDegrees);
        }

        // --- CreateMeshBoxChild ---
        private GameObject CreateMeshBoxChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale, Color color, float rotationDegrees = 0f)
        {
            return worldBuilder.CreateMeshBoxChild(parent, propName, localPosition, scale, color, rotationDegrees);
        }

        // --- CreateMeshPrimitiveChild ---
        private GameObject CreateMeshPrimitiveChild(Transform parent, string propName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 scale, Color color, Quaternion localRotation)
        {
            return worldBuilder.CreateMeshPrimitiveChild(parent, propName, primitiveType, localPosition, scale, color, localRotation);
        }

        // --- CreateMeshPrimitiveProp ---
        private GameObject CreateMeshPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Color color, Quaternion rotation)
        {
            return worldBuilder.CreateMeshPrimitiveProp(propName, primitiveType, position, scale, color, rotation);
        }

        // --- ConfigureRuntimeMesh ---
        private void ConfigureRuntimeMesh(GameObject prop, Color color)
        {
            worldBuilder.ConfigureRuntimeMesh(prop, color);
        }

        // --- RuntimeMeshMaterial ---
        private Material RuntimeMeshMaterial(Color color)
        {
            return worldBuilder.RuntimeMeshMaterial(color);
        }

        // --- RegisterSolidObstacle ---
        private void RegisterSolidObstacle(Vector3 position, Vector3 scale)
        {
            worldBuilder.RegisterSolidObstacle(position, scale);
        }

        // --- RegisterWalkableArea ---
        private void RegisterWalkableArea(Vector3 position, Vector3 scale)
        {
            worldBuilder.RegisterWalkableArea(position, scale);
        }

        // --- CreatePropChild ---
        private GameObject CreatePropChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale, Color color, PrimitiveType primitiveType)
        {
            return worldBuilder.CreatePropChild(parent, propName, localPosition, scale, color, primitiveType);
        }

        // --- CreateSpriteChild ---
        private GameObject CreateSpriteChild(Transform parent, string objectName, Sprite sprite, Vector3 localPosition, Vector3 scale, Color color)
        {
            return worldBuilder.CreateSpriteChild(parent, objectName, sprite, localPosition, scale, color);
        }

        // --- CreateAssetStoreProp ---
        private GameObject CreateAssetStoreProp(string propName, string resourcePath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false, bool preserveMaterials = true)
        {
            return worldBuilder.CreateAssetStoreProp(propName, resourcePath, position, footprint, rotationDegrees, stretchToFootprint, preserveMaterials);
        }

        // --- CreateSolidAssetStoreProp ---
        private GameObject CreateSolidAssetStoreProp(string propName, string resourcePath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false, bool preserveMaterials = true)
        {
            return worldBuilder.CreateSolidAssetStoreProp(propName, resourcePath, position, footprint, rotationDegrees, stretchToFootprint, preserveMaterials);
        }

        // --- CreateModelProp ---
        private GameObject CreateModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            return worldBuilder.CreateModelProp(propName, relativeFbxPath, position, footprint, rotationDegrees, stretchToFootprint);
        }

        // --- CreateModelFallbackProp ---
        private GameObject CreateModelFallbackProp(string propName, Vector3 position, Vector3 footprint, float rotationDegrees, Color color)
        {
            return worldBuilder.CreateModelFallbackProp(propName, position, footprint, rotationDegrees, color);
        }

        // --- CreateSolidModelProp ---
        private GameObject CreateSolidModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            return worldBuilder.CreateSolidModelProp(propName, relativeFbxPath, position, footprint, rotationDegrees, stretchToFootprint);
        }

        // --- CreateWallModelOverlay ---
        private void CreateWallModelOverlay(string wallName, Vector3 position, Vector3 scale)
        {
            worldBuilder.CreateWallModelOverlay(wallName, position, scale);
        }

        // --- CreateDoorModelOverlay ---
        private void CreateDoorModelOverlay(string markerName, Vector3 position, Vector3 scale)
        {
            worldBuilder.CreateDoorModelOverlay(markerName, position, scale);
        }

        // --- LoadQuaterniusModel ---
        private GameObject LoadQuaterniusModel(string relativeFbxPath)
        {
            return worldBuilder.LoadQuaterniusModel(relativeFbxPath);
        }

        // --- LoadResourcePrefab ---
        private GameObject LoadResourcePrefab(string resourcePath)
        {
            return worldBuilder.LoadResourcePrefab(resourcePath);
        }

        // --- FitModelToFootprint ---
        private void FitModelToFootprint(GameObject model, Vector3 targetPosition, Vector3 footprint, bool stretchToFootprint)
        {
            worldBuilder.FitModelToFootprint(model, targetPosition, footprint, stretchToFootprint);
        }

        // --- CreateNeonLight ---
        private void CreateNeonLight(string lightName, Vector3 position, Color color, float intensity, float range)
        {
            worldBuilder.CreateNeonLight(lightName, position, color, intensity, range);
        }

        // --- ConfigureSceneLighting ---
        private void ConfigureSceneLighting()
        {
            worldBuilder.ConfigureSceneLighting();
        }

        // --- CreateEmergencyBell ---
        private void CreateEmergencyBell()
        {
            worldBuilder.CreateEmergencyBell();
            // M4.3: 绑定 EmergencyButton 到控制器
            EmergencyButton[] buttons = worldRoot.GetComponentsInChildren<EmergencyButton>();
            foreach (var btn in buttons)
                btn.BindController(this);
        }

        // --- SpawnSurveillanceCameras ---
        private void SpawnSurveillanceCameras()
        {
            surveillanceCameras.Clear();
            var zones = mapService.SurveillanceZones();
            if (zones == null || zones.Length == 0)
            {
                Debug.LogWarning("[M6] No surveillance zones defined.");
                return;
            }

            foreach (var zone in zones)
            {
                Vector3 worldPos = mapService.ScaleMapPosition(zone.Center);
                Vector3 worldSize = mapService.ScaleMapSize(zone.Size);

                // 可视化标记（半透明区域）
                worldBuilder.CreateShapeProp(
                    $"SurveillanceZone_{zone.Label}",
                    worldBuilder.SoftCircleSprite,
                    zone.Center,
                    zone.Size,
                    new Color(0.08f, 0.45f, 0.65f, 0.18f));

                // 实例化 NetworkBehaviour（运行时由 Netcode 管理）
                if (Application.isPlaying && networkManager != null && networkManager.IsServer)
                {
                    CreateSurveillanceCameraNetworkObject(zone);
                }
            }

            Debug.Log($"[M6] Spawned {zones.Length} surveillance cameras.");
        }

        // --- EnsureSurveillanceCameraNetworkObjects ---
        private void EnsureSurveillanceCameraNetworkObjects()
        {
            if (!Application.isPlaying || networkManager == null || !networkManager.IsServer || worldRoot == null)
            {
                return;
            }

            if (surveillanceCameras.Count > 0)
            {
                return;
            }

            var zones = mapService.SurveillanceZones();
            if (zones == null || zones.Length == 0)
            {
                return;
            }

            foreach (var zone in zones)
            {
                CreateSurveillanceCameraNetworkObject(zone);
            }
        }

        // --- CreateSurveillanceCameraNetworkObject ---
        private void CreateSurveillanceCameraNetworkObject(OnlineMapService.SurveillanceZoneSpec zone)
        {
            Vector3 worldPos = mapService.ScaleMapPosition(zone.Center);
            Vector3 worldSize = mapService.ScaleMapSize(zone.Size);

            // A1 修复：从注册的 NetworkPrefab 模板实例化，避免 AddComponent<NetworkObject>.Spawn()
            // 导致 globalObjectIdHash=0、远端 "NetworkPrefab could not be found"。
            if (surveillanceCameraTemplate == null)
            {
                Debug.LogError("[A1] Surveillance camera template not registered!");
                return;
            }

            NetworkObject netObj = NetworkObject.InstantiateAndSpawn(
                surveillanceCameraTemplate,
                networkManager,
                ownerClientId: NetworkManager.ServerClientId,
                destroyWithScene: false,
                isPlayerObject: false,
                forceOverride: false,
                position: worldPos);

            if (netObj == null)
            {
                Debug.LogError("[A1] Failed to spawn surveillance camera NetworkPrefab.");
                return;
            }

            GameObject cameraObj = netObj.gameObject;
            cameraObj.name = $"SurveillanceCamera_{zone.Label}";
            DontDestroyOnLoad(cameraObj);

            var camera = cameraObj.GetComponent<Online.Surveillance.OnlineSecurityCamera>();
            camera.BindController(this);
            camera.ZoneCenter = new Vector2(worldPos.x, worldPos.y);
            camera.ZoneSize = new Vector2(worldSize.x, worldSize.y);
            camera.CameraLabel = zone.Label;
            surveillanceCameras.Add(camera);
        }

        // --- EnsureRuntimeSprites ---
        private void EnsureRuntimeSprites()
        {
            worldBuilder.EnsureRuntimeSprites();
        }

        // --- CreateSpriteObject ---
        private GameObject CreateSpriteObject(string objectName, Sprite sprite, Color color)
        {
            EnsureRuntimeSprites();
            GameObject spriteObject = new GameObject(objectName);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : roundedRectSprite;
            renderer.color = color;
            renderer.sortingOrder = OnlineMatchUtils.SortingOrderForZ(spriteObject.transform.position.z);
            return spriteObject;
        }

        // --- ShouldShowPlayerWorldLabel ---
        private bool ShouldShowPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            return worldBuilder.ShouldShowPlayerWorldLabel(state, isLocal, phase, tacticalMapOpen);
        }

        // --- IsNearCameraSubject ---
        private bool IsNearCameraSubject(Vector3 position)
        {
            return _cameraRig.IsNearSubject(position, LocalCameraTarget(), tacticalMapOpen, phase);
        }

        // --- SetChildActive ---
        private static void SetChildActive(GameObject root, string childName, bool active)
        {
            OnlineWorldBuilder.SetChildActive(root, childName, active);
        }

        // --- CountActiveNamedWorldObjects ---
        private int CountActiveNamedWorldObjects(string prefix)
        {
            if (worldRoot == null)
            {
                return 0;
            }

            int count = 0;

            foreach (Transform child in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.activeSelf && child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
