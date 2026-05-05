using System;
using System.IO;
using System.Linq;
using GanglandUndercover.Core;
using GanglandUndercover.Gameplay;
using GanglandUndercover.Online;
using GanglandUndercover.SocialDeduction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class PrototypeSmokeTests
    {
        [MenuItem("Gangland/Run Smoke Tests")]
        public static void Run()
        {
            try
            {
                RunFactionPath(Faction.Gang);
                RunFactionPath(Faction.Police);
                RunFactionPath(Faction.Undercover);
                RunLockdownPath();
                ValidateAssetStoreBaseline();
                ValidatePrototypeScene();
                ValidateSocialDemoRuntime();
                ValidateOnlineBootstrap();
                ValidateStageOneAuthoringAssets();
                ValidateStageTwoCharacterAssets();
                WriteSmokeResult("PASS", "Gangland smoke tests passed.");
                Debug.Log("Gangland smoke tests passed.");
            }
            catch (Exception exception)
            {
                WriteSmokeResult("FAIL", exception.ToString());
                Debug.LogException(exception);
                throw;
            }
        }

        [MenuItem("Gangland/Run Smoke Tests", true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void WriteSmokeResult(string status, string detail)
        {
            string logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(logsDirectory);
            File.WriteAllText(
                Path.Combine(logsDirectory, "latest-smoke-result.txt"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + status + Environment.NewLine + detail);
        }

        private static void RunFactionPath(Faction faction)
        {
            GameController controller = new GameController();
            controller.SelectFaction(faction);

            for (int i = 0; i < 6 && controller.State.Phase != GamePhase.GameOver; i++)
            {
                DistrictType district = controller.State.Districts[i % controller.State.Districts.Count].Type;
                PlayerAction action = controller.Actions.GetActionsFor(faction).First();
                controller.RunPlayerAction(district, action);
            }

            if (controller.State.Phase == GamePhase.RoleSelect)
            {
                throw new InvalidOperationException("Smoke test failed: faction was not selected.");
            }

            if (controller.State.Day < 1)
            {
                throw new InvalidOperationException("Smoke test failed: invalid day count.");
            }

            if (controller.State.Log.Count == 0)
            {
                throw new InvalidOperationException("Smoke test failed: no log entries were produced.");
            }
        }

        private static void RunLockdownPath()
        {
            GameController controller = new GameController();
            controller.SelectFaction(Faction.Police);

            PlayerAction lockdown = controller.Actions.GetActionsFor(Faction.Police).First(action => action.Id == "police_lockdown");
            controller.RunPlayerAction(DistrictType.WarehouseRow, lockdown);

            if (!controller.State.GetDistrict(DistrictType.WarehouseRow).IsLockedDown)
            {
                throw new InvalidOperationException("Smoke test failed: police lockdown did not mark the district.");
            }

            GameController gangController = new GameController();
            gangController.SelectFaction(Faction.Gang);
            DistrictState dockyard = gangController.State.GetDistrict(DistrictType.Dockyard);
            dockyard.SetLockdown(true);

            PlayerAction shipment = gangController.Actions.GetActionsFor(Faction.Gang).First(action => action.Id == "gang_ship");
            gangController.RunPlayerAction(DistrictType.Dockyard, shipment);

            if (gangController.State.ShipmentProgress > 0)
            {
                throw new InvalidOperationException("Smoke test failed: lockdown did not stop a direct shipment action.");
            }
        }

        private static void ValidateAssetStoreBaseline()
        {
            const string quarantinedPackagePath = "Assets/_Project/Resources/AssetStore/LowpolyStreetPack";

            if (Directory.Exists(quarantinedPackagePath))
            {
                throw new InvalidOperationException("Smoke test failed: LowpolyStreetPack is still present under the live AssetStore path.");
            }

            string[] scanRoots =
            {
                "Assets/_Project/Scripts",
                "Assets/_Project/Editor",
                "Assets/_Project/Scenes"
            };

            foreach (string scanRoot in scanRoots)
            {
                if (!Directory.Exists(scanRoot))
                {
                    continue;
                }

                foreach (string filePath in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
                {
                    if (ShouldSkipAssetStoreBaselineGuard(filePath) || !IsTextFileForAssetStoreBaselineGuard(filePath))
                    {
                        continue;
                    }

                    string content = File.ReadAllText(filePath);

                    if (content.IndexOf("LowpolyStreetPack", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new InvalidOperationException("Smoke test failed: LowpolyStreetPack reference still exists in " + filePath + ".");
                    }
                }
            }
        }

        private static bool ShouldSkipAssetStoreBaselineGuard(string filePath)
        {
            return filePath.Replace('\\', '/').EndsWith("/Editor/PrototypeSmokeTests.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextFileForAssetStoreBaselineGuard(string filePath)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".cs":
                case ".unity":
                case ".asset":
                case ".prefab":
                case ".mat":
                case ".json":
                case ".asmdef":
                case ".shadergraph":
                case ".uxml":
                case ".uss":
                case ".txt":
                    return true;
                default:
                    return false;
            }
        }

        private static void ValidatePrototypeScene()
        {
            const string scenePath = "Assets/_Project/Scenes/Prototype.unity";
            EditorSceneManager.OpenScene(scenePath);

            if (UnityEngine.Object.FindAnyObjectByType<PrototypeBootstrap>() == null)
            {
                throw new InvalidOperationException("Smoke test failed: Prototype scene has no PrototypeBootstrap.");
            }

            if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != scenePath)
            {
                throw new InvalidOperationException("Smoke test failed: Prototype scene is not in Build Settings.");
            }
        }

        private static void ValidateSocialDemoRuntime()
        {
            GameObject smokeCamera = CreateSmokeCameraIfNeeded();
            GameObject demoObject = new GameObject("Smoke Social Demo");
            SocialPrototypeController controller = demoObject.AddComponent<SocialPrototypeController>();
            controller.StartGame(SocialRole.Undercover);

            if (!controller.HasStarted)
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not start.");
            }

            if (controller.PlayerRole != SocialRole.Undercover)
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not default to undercover.");
            }

            if (!controller.IsRoleRevealVisible)
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not show role reveal.");
            }

            if (controller.TotalTasks < 5)
            {
                throw new InvalidOperationException("Smoke test failed: social demo does not have enough task stations.");
            }

            if (controller.Characters.Count < 5)
            {
                throw new InvalidOperationException("Smoke test failed: social demo does not have enough characters.");
            }

            if (controller.RoundTimer <= 0f)
            {
                throw new InvalidOperationException("Smoke test failed: social demo round timer was not initialized.");
            }

            if (string.IsNullOrEmpty(controller.InteractionPrompt))
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not expose an interaction prompt.");
            }

            if (string.IsNullOrEmpty(controller.RouteIntel))
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not expose route intel.");
            }

            if (string.IsNullOrEmpty(controller.TaskChecklist) || string.IsNullOrEmpty(controller.RosterSummary))
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not expose full match HUD data.");
            }

            if (controller.ActiveFootprintCount != 0)
            {
                throw new InvalidOperationException("Smoke test failed: social demo should not create footprints before simulation ticks.");
            }

            ValidateCameraSeesPlayer(controller);

            controller.BeginRound();

            if (controller.IsRoleRevealVisible)
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not leave role reveal.");
            }

            controller.ToggleLanguage();

            if (controller.Language != GameLanguage.English)
            {
                throw new InvalidOperationException("Smoke test failed: social demo language toggle failed.");
            }

            controller.StartGame(SocialRole.Gang);

            if (controller.PlayerRole != SocialRole.Gang || controller.EmergencyMeetingLimitValue != 2)
            {
                throw new InvalidOperationException("Smoke test failed: social demo did not restart as gang.");
            }

            UnityEngine.Object.DestroyImmediate(demoObject);

            foreach (SocialPrototypeHud hud in UnityEngine.Object.FindObjectsByType<SocialPrototypeHud>(FindObjectsInactive.Exclude))
            {
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
            }

            if (smokeCamera != null)
            {
                UnityEngine.Object.DestroyImmediate(smokeCamera);
            }
        }

        private static GameObject CreateSmokeCameraIfNeeded()
        {
            if (Camera.main != null)
            {
                return null;
            }

            GameObject cameraObject = new GameObject("Smoke Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.tag = "MainCamera";
            return cameraObject;
        }

        private static void ValidateCameraSeesPlayer(SocialPrototypeController controller)
        {
            if (Camera.main == null)
            {
                throw new InvalidOperationException("Smoke test failed: social demo has no main camera.");
            }

            SocialCharacter player = controller.Characters.FirstOrDefault(character => character.IsPlayer);

            if (player == null)
            {
                throw new InvalidOperationException("Smoke test failed: social demo has no player character.");
            }

            Vector3 viewportPoint = Camera.main.WorldToViewportPoint(player.transform.position);
            bool inViewport = viewportPoint.z > Camera.main.nearClipPlane
                && viewportPoint.x > 0.05f
                && viewportPoint.x < 0.95f
                && viewportPoint.y > 0.05f
                && viewportPoint.y < 0.95f;

            if (!inViewport)
            {
                throw new InvalidOperationException("Smoke test failed: social demo camera does not frame the player.");
            }
        }

        private static void ValidateOnlineBootstrap()
        {
            Type onlineType = typeof(OnlineMatchController);

            if (onlineType == null)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller type was not compiled.");
            }

            GameObject smokeCamera = CreateSmokeCameraIfNeeded();
            GameObject onlineObject = new GameObject("Smoke Online Match");
            OnlineMatchController controller = onlineObject.AddComponent<OnlineMatchController>();

            if (controller == null)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller could not be added.");
            }

            if (controller.Phase != OnlineMatchPhase.Lobby)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not start in lobby.");
            }

            if (controller.TaskCount < 28)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not create enough large-map tasks.");
            }

            if (controller.BotCount != 0)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller should not create bots before hosting.");
            }

            if (controller.RoomMinPlayers < 4 || controller.RoomMaxPlayers < controller.RoomMinPlayers)
            {
                throw new InvalidOperationException("Smoke test failed: online room settings are invalid.");
            }

            if (controller.EvidenceTarget < 44)
            {
                throw new InvalidOperationException("Smoke test failed: online evidence target is too low for a 10-20 minute match.");
            }

            if (controller.TargetMatchMinutesMin != 10 || controller.TargetMatchMinutesMax != 20)
            {
                throw new InvalidOperationException("Smoke test failed: online match target duration is not 10-20 minutes.");
            }

            if (!controller.AutoFillAi)
            {
                throw new InvalidOperationException("Smoke test failed: online release candidate should default to AI fill for solo validation.");
            }

            controller.EditorSimulateLocalMatch();

            if (!controller.MatchStarted || controller.Phase != OnlineMatchPhase.Opening)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not enter opening briefing.");
            }

            controller.EditorSkipOpeningForSmokeTest();

            if (!controller.MatchStarted || controller.Phase != OnlineMatchPhase.Action)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not simulate a playable local match.");
            }

            if (controller.BotCount < 7)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not auto-fill AI players.");
            }

            if (controller.CaseLogCount == 0)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not record match events.");
            }

            if (!controller.HasWorld)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not build the port map.");
            }

            if (!controller.HasCanvasHud)
            {
                throw new InvalidOperationException("Smoke test failed: online match did not create the product Canvas HUD.");
            }

            if (!controller.CanvasHudLayoutComplete)
            {
                throw new InvalidOperationException("Smoke test failed: online Canvas HUD did not build all required layout references.");
            }

            if (controller.WorldObjectCount < 520)
            {
                throw new InvalidOperationException("Smoke test failed: online port map is not visually rich enough for a large social deduction map.");
            }

            if (controller.BuildingVolumeCount < 50)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not create enough 2.5D building volume pieces.");
            }

            if (controller.RooftopFeatureCount < 25)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not create enough rooftop architecture.");
            }

            if (controller.ForegroundOccluderCount < 28)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not create enough foreground occlusion pieces for 2.5D readability.");
            }

            if (controller.PremiumTaskSetPieceCount < 120)
            {
                throw new InvalidOperationException("Smoke test failed: online task stations are not visually mature enough.");
            }

            if (controller.OrganicRouteFeatureCount < 18)
            {
                throw new InvalidOperationException("Smoke test failed: online map still reads as too square and lacks organic route language.");
            }

            if (controller.MatureDockyardSetPieceCount < 120)
            {
                throw new InvalidOperationException("Smoke test failed: online map is not using enough free modular art set pieces.");
            }

            if (controller.OfficialFreeAssetSetPieceCount < 50)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not place enough official free Asset Store art.");
            }

            if (controller.DenseOfficialStreetSetPieceCount < 80)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not place enough dense official free street dressing.");
            }

            if (controller.TaskReadabilityMarkerCount < controller.TaskCount * 5)
            {
                throw new InvalidOperationException("Smoke test failed: online tasks do not have enough readable world-space interaction markers.");
            }

            if (controller.ActionViewShowcasePieceCount < 55)
            {
                throw new InvalidOperationException("Smoke test failed: online playable preview lacks a dense action-view showcase layer.");
            }

            if (controller.VerticalSliceSetPieceCount < 120)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not build the new vertical-slice production layer.");
            }

            if (controller.VerticalSliceRoomIdentityCount < 28)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice does not expose enough readable room identity pieces.");
            }

            if (controller.VerticalSliceTaskMiniGameSetPieceCount < 36)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice task minigames are not represented by enough physical set pieces.");
            }

            if (controller.VerticalSliceMapOverlayCount < 15)
            {
                throw new InvalidOperationException("Smoke test failed: tactical map does not expose the vertical-slice core layout.");
            }

            if (controller.VerticalSliceStageOneSetPieceCount < 105)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one authoring layer is not dense enough.");
            }

            if (controller.VerticalSliceStageOneEntranceCount < 36)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one room entrances are not authored enough.");
            }

            if (controller.VerticalSliceStageOneFirstScreenCount < 55)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice first screen still lacks authored composition pieces.");
            }

            if (controller.VerticalSliceStageOneSightlineCount < 25)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one sightline blockers are not strong enough.");
            }

            if (controller.VerticalSliceStageOneCameraShotCount < 20)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one camera shot markers are not authored enough.");
            }

            if (controller.VerticalSliceStageOneGameplayAnchorCount < 24)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one gameplay anchors are not authored enough.");
            }

            if (controller.VerticalSliceStageOneMeetingSetPieceCount < 18)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one meeting set is not authored enough.");
            }

            if (controller.VerticalSliceStageOneBlackoutSetPieceCount < 20)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one blackout set is not authored enough.");
            }

            if (controller.VerticalSliceStageOneEditableAnchorCount < VerticalSliceStageOneAnchorCatalog.Specs.Length)
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one editable anchors are not all present.");
            }

            if (!controller.EditorForceStageOneOpeningShotForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one opening shot did not configure.");
            }

            if (controller.CommercialArtAdapterCount < 8)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not build a commercial art adapter layer.");
            }

            if (controller.LargePortVistaCount < 10)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not build a large port vista layer.");
            }

            if (controller.CollisionObjectCount < 60)
            {
                throw new InvalidOperationException("Smoke test failed: online port map does not expose enough solid obstacles.");
            }

            if (controller.PhysicsColliderCount < 60)
            {
                throw new InvalidOperationException("Smoke test failed: online port map does not expose enough Unity physics colliders.");
            }

            if (controller.UnderworldPassageNodeCount < 4)
            {
                throw new InvalidOperationException("Smoke test failed: online map did not create gang underworld passage nodes.");
            }

            if (controller.TacticalMapLabelCount < controller.TaskCount + 12)
            {
                throw new InvalidOperationException("Smoke test failed: online tactical map does not expose enough labeled gameplay markers.");
            }

            if (string.IsNullOrWhiteSpace(controller.MatchPressureSummary) || !controller.MatchPressureSummary.Contains("警方进度"))
            {
                throw new InvalidOperationException("Smoke test failed: online match did not expose a readable pressure summary.");
            }

            if (string.IsNullOrWhiteSpace(controller.LobbyReadinessSummary) || !controller.LobbyReadinessSummary.Contains("大厅准备"))
            {
                throw new InvalidOperationException("Smoke test failed: online lobby did not expose a readiness summary.");
            }

            if (string.IsNullOrWhiteSpace(controller.LobbyRoadmap) || !controller.LobbyRoadmap.Contains("Action"))
            {
                throw new InvalidOperationException("Smoke test failed: online lobby did not expose the match phase roadmap.");
            }

            if (string.IsNullOrWhiteSpace(controller.LocalObjectiveSummary))
            {
                throw new InvalidOperationException("Smoke test failed: online match did not expose a role objective summary.");
            }

            if (!controller.EditorConfigureActionCameraForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: online action camera did not switch to 2.5D perspective.");
            }

            if (!controller.EditorForceActionPreviewForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: online playable preview did not expose the compact action view.");
            }

            controller.EditorRefreshWorldVisualsForSmokeTest();

            if (controller.FreeCharacterAdapterCount < controller.HumanPlayerCount + controller.BotCount)
            {
                throw new InvalidOperationException("Smoke test failed: online player visuals did not create free character adapter layers.");
            }

            if (controller.StageTwoCharacterStateLayerCount < (controller.HumanPlayerCount + controller.BotCount) * 6)
            {
                throw new InvalidOperationException("Smoke test failed: stage-two character state layers were not created for every online player.");
            }

            if (controller.StageTwoRuntimeRigCount < controller.HumanPlayerCount + controller.BotCount || controller.StageTwoConfiguredRigCount < controller.HumanPlayerCount + controller.BotCount)
            {
                throw new InvalidOperationException("Smoke test failed: stage-two runtime character rigs were not configured for every online player.");
            }

            if (controller.StageTwoActiveVoiceRadiusCount <= 0)
            {
                throw new InvalidOperationException("Smoke test failed: stage-two action voice radius state did not become visible.");
            }

            controller.EditorToggleTacticalMapForSmokeTest();

            if (!controller.TacticalMapOpen)
            {
                throw new InvalidOperationException("Smoke test failed: tactical map hotkey path is not exposed.");
            }

            if (!controller.EditorIsWalkableForSmokeTest(new Vector3(0f, -0.65f, 0f)))
            {
                throw new InvalidOperationException("Smoke test failed: online meeting spawn ring is blocked.");
            }

            Vector3 blockedTarget = new Vector3(-17.8f, 10.95f, 0f);
            Vector3 blockedMove = controller.EditorResolveCollisionForSmokeTest(new Vector3(-14.7f, 9.9f, 0f), blockedTarget);

            if (Vector3.Distance(blockedMove, blockedTarget) < 0.25f)
            {
                throw new InvalidOperationException("Smoke test failed: online map collision did not block a cargo container.");
            }

            if (controller.EmergencyMeetingsLeft <= 0)
            {
                throw new InvalidOperationException("Smoke test failed: online match did not initialize emergency meeting budget.");
            }

            int[] canvasTaskIds = { 0, 5, 2, 18 };

            for (int i = 0; i < canvasTaskIds.Length; i++)
            {
                controller.EditorOpenTaskPanelForSmokeTest(canvasTaskIds[i]);
                controller.EditorRefreshWorldVisualsForSmokeTest();

                if (!controller.CanvasHudLayoutComplete)
                {
                    throw new InvalidOperationException("Smoke test failed: online Canvas HUD lost required layout references while opening task " + canvasTaskIds[i] + ".");
                }

                if (!controller.LocalTaskInputGateActive)
                {
                    throw new InvalidOperationException("Smoke test failed: online task flow did not require the local task minigame panel.");
                }

                if (controller.TaskMiniGameCanvasElementCount < 15)
                {
                    throw new InvalidOperationException("Smoke test failed: online task panel did not build the dedicated Canvas minigame board for task " + canvasTaskIds[i] + ".");
                }
            }

            if (!controller.VoiceRoutingEnabled || string.IsNullOrWhiteSpace(controller.VoiceStatus))
            {
                throw new InvalidOperationException("Smoke test failed: online match did not expose Vivox voice routing.");
            }

            if (!controller.EditorForceDownedStateForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: stage-two downed/death state did not become visible.");
            }

            if (controller.StageTwoForensicSceneCount < 4 || controller.StageTwoActiveReportFeedbackCount <= 0)
            {
                throw new InvalidOperationException("Smoke test failed: stage-two body report and forensic scene feedback did not become visible.");
            }

            controller.EditorForceMeetingForSmokeTest();
            controller.EditorRefreshWorldVisualsForSmokeTest();

            if (!controller.CanvasHudLayoutComplete)
            {
                throw new InvalidOperationException("Smoke test failed: online Canvas HUD lost required layout references during meeting preview.");
            }

            if (controller.Phase != OnlineMatchPhase.Meeting)
            {
                throw new InvalidOperationException("Smoke test failed: online meeting preview did not enter meeting phase.");
            }

            if (controller.MeetingSeatCanvasElementCount < controller.Players.Count + 4)
            {
                throw new InvalidOperationException("Smoke test failed: online meeting overlay did not build the player voice-seat board.");
            }

            if (controller.StageTwoActiveMeetingPoseCount < controller.AlivePlayerCount)
            {
                throw new InvalidOperationException("Smoke test failed: stage-two meeting seated poses did not become visible.");
            }

            if (!controller.EditorForceVoteStateForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: stage-two meeting vote feedback did not become visible.");
            }

            if (!controller.EditorForceActionPreviewForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: online playable preview did not resume after meeting overlay validation.");
            }

            controller.EditorTriggerTaskForSmokeTest(2, true);

            if (controller.BlackoutTimer <= 0f)
            {
                throw new InvalidOperationException("Smoke test failed: online sabotage did not trigger blackout.");
            }

            if (!controller.EditorForceStageOneBlackoutShotForSmokeTest())
            {
                throw new InvalidOperationException("Smoke test failed: vertical slice stage-one blackout camera shot did not configure.");
            }

            if (controller.EvidenceMilestoneIndex < 0)
            {
                throw new InvalidOperationException("Smoke test failed: online evidence milestone state is invalid.");
            }

            if (string.IsNullOrWhiteSpace(controller.LastMeetingReason) || string.IsNullOrWhiteSpace(controller.LastVoteOutcome))
            {
                throw new InvalidOperationException("Smoke test failed: online meeting evidence state was not initialized.");
            }

            controller.EditorTriggerTaskForSmokeTest(7, true);

            if (controller.LockdownTimer <= 0f)
            {
                throw new InvalidOperationException("Smoke test failed: online sabotage did not trigger lockdown.");
            }

            if (!controller.HasRuntimeAudio)
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not create runtime audio.");
            }

            controller.EditorForceRestartForSmokeTest();

            if (!controller.MatchStarted || controller.Phase != OnlineMatchPhase.Opening || string.IsNullOrEmpty(controller.ResultSummary))
            {
                throw new InvalidOperationException("Smoke test failed: online match controller did not support restart flow.");
            }

            UnityEngine.Object.DestroyImmediate(onlineObject);

            if (smokeCamera != null)
            {
                UnityEngine.Object.DestroyImmediate(smokeCamera);
            }
        }

        private static void ValidateStageOneAuthoringAssets()
        {
            if (!VerticalSliceStageOneBuilder.StageOneAuthoringAssetsExist())
            {
                VerticalSliceStageOneBuilder.BuildStageOneVerticalSliceScene();
            }

            if (!File.Exists(VerticalSliceStageOneBuilder.ScenePath))
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice scene was not generated.");
            }

            if (!File.Exists(VerticalSliceStageOneBuilder.PrefabPath))
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice prefab was not generated.");
            }

            if (!File.Exists(VerticalSliceStageOneBuilder.BakeStampPath))
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice bake stamp was not generated.");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VerticalSliceStageOneBuilder.PrefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice prefab could not be loaded.");
            }

            int anchors = prefab.GetComponentsInChildren<VerticalSliceStageOneAnchor>(true).Length;

            if (anchors < VerticalSliceStageOneAnchorCatalog.Specs.Length)
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice prefab does not contain all editable anchors.");
            }

            string stamp = File.ReadAllText(VerticalSliceStageOneBuilder.BakeStampPath);

            if (!stamp.Contains("Editable anchors: " + VerticalSliceStageOneAnchorCatalog.Specs.Length))
            {
                throw new InvalidOperationException("Smoke test failed: Stage1 vertical slice bake stamp does not record the editable anchor count.");
            }
        }

        private static void ValidateStageTwoCharacterAssets()
        {
            if (!StageTwoCharacterAssetBuilder.StageTwoCharacterAssetsExist())
            {
                StageTwoCharacterAssetBuilder.BuildStageTwoCharacterAssets();
            }

            if (!File.Exists(StageTwoCharacterRig.RigCatalogAssetPath))
            {
                throw new InvalidOperationException("Smoke test failed: Stage2 character rig catalog was not generated.");
            }

            string[] prefabPaths =
            {
                StageTwoCharacterAssetBuilder.PolicePrefabPath,
                StageTwoCharacterAssetBuilder.UndercoverPrefabPath,
                StageTwoCharacterAssetBuilder.GangPrefabPath,
                StageTwoCharacterAssetBuilder.CivilianPrefabPath
            };

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);

                if (prefab == null)
                {
                    throw new InvalidOperationException("Smoke test failed: Stage2 character prefab missing at " + prefabPaths[i] + ".");
                }

                StageTwoCharacterRig rig = prefab.GetComponent<StageTwoCharacterRig>();

                if (rig == null || !rig.HasRequiredRuntimeSlots || !rig.HasStateCatalog)
                {
                    throw new InvalidOperationException("Smoke test failed: Stage2 character prefab is missing rig slots or state catalog at " + prefabPaths[i] + ".");
                }
            }

            string stamp = File.Exists(StageTwoCharacterAssetBuilder.StampPath)
                ? File.ReadAllText(StageTwoCharacterAssetBuilder.StampPath)
                : string.Empty;

            if (!stamp.Contains("States: Idle, Walk, Interact, Downed, Report, Meeting, Vote"))
            {
                throw new InvalidOperationException("Smoke test failed: Stage2 character bake stamp does not list the required animation states.");
            }
        }
    }
}
