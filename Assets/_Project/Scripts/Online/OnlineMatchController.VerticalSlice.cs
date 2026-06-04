using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {
        private void CreateVerticalSliceProductionLayer()
        {
            CreateVerticalSliceGroundPlan();
            CreateVerticalSliceRoomIdentities();
            CreateVerticalSliceTaskMiniGames();
            CreateVerticalSliceStreetLife();
            CreateVerticalSliceCollisionAndOcclusion();
            CreateVerticalSliceStageOneAuthoringLayer();
            CreateVerticalSliceLightingAndCameraGuides();
        }

        private void CreateVerticalSliceGroundPlan()
        {
            Color asphalt = new Color(0.085f, 0.1f, 0.102f, 1f);
            Color sidewalk = new Color(0.18f, 0.19f, 0.18f, 1f);
            Color wetReflection = new Color(0.08f, 0.18f, 0.2f, 0.72f);
            Color guide = new Color(0.82f, 0.68f, 0.2f, 1f);

            CreateShapeProp("VerticalSlice Ground central wet plaza", roundedRectSprite, new Vector3(-1.25f, -0.56f, -0.31f), new Vector3(5.4f, 2.58f, 0.08f), asphalt);
            CreateShapeProp("VerticalSlice Ground meeting ring stone apron", circleSprite, new Vector3(-1.18f, -0.72f, -0.285f), new Vector3(1.62f, 1.18f, 0.08f), new Color(0.28f, 0.3f, 0.28f, 1f));
            CreateShapeProp("VerticalSlice Ground meeting ring wet core", circleSprite, new Vector3(-1.18f, -0.72f, -0.27f), new Vector3(1.05f, 0.78f, 0.08f), wetReflection);
            RegisterWalkableArea(new Vector3(-1.25f, -0.56f, 0f), new Vector3(5.7f, 2.78f, 0.08f));

            CreateShapeProp("VerticalSlice Ground cctv corridor", roundedRectSprite, new Vector3(-6.6f, 0.68f, -0.3f), new Vector3(3.5f, 1.02f, 0.08f), sidewalk);
            CreateShapeProp("VerticalSlice Ground cha chaan teng threshold", roundedRectSprite, new Vector3(-4.42f, 1.34f, -0.29f), new Vector3(2.55f, 1.24f, 0.08f), new Color(0.26f, 0.16f, 0.08f, 1f));
            CreateShapeProp("VerticalSlice Ground night market bend", roundedRectSprite, new Vector3(-0.4f, 2.56f, -0.3f), new Vector3(4.4f, 1.08f, 0.08f), new Color(0.19f, 0.12f, 0.1f, 1f));
            CreateRotatedProp("VerticalSlice Ground non-square diagonal market cut", new Vector3(1.72f, 2.05f, -0.295f), new Vector3(2.6f, 0.78f, 0.08f), new Color(0.16f, 0.12f, 0.1f, 1f), -18f);
            CreateShapeProp("VerticalSlice Ground alley approach", roundedRectSprite, new Vector3(3.88f, -1.25f, -0.305f), new Vector3(3.7f, 0.96f, 0.08f), new Color(0.13f, 0.11f, 0.1f, 1f));
            CreateRotatedProp("VerticalSlice Ground service lane diagonal", new Vector3(5.45f, 0.42f, -0.302f), new Vector3(2.95f, 0.7f, 0.08f), new Color(0.11f, 0.13f, 0.13f, 1f), 22f);
            RegisterWalkableArea(new Vector3(-6.6f, 0.68f, 0f), new Vector3(3.8f, 1.18f, 0.08f));
            RegisterWalkableArea(new Vector3(-4.42f, 1.34f, 0f), new Vector3(2.85f, 1.44f, 0.08f));
            RegisterWalkableArea(new Vector3(-0.4f, 2.56f, 0f), new Vector3(4.65f, 1.24f, 0.08f));
            RegisterWalkableArea(new Vector3(3.88f, -1.25f, 0f), new Vector3(3.9f, 1.12f, 0.08f));
            RegisterWalkableArea(new Vector3(5.45f, 0.42f, 0f), new Vector3(3.2f, 0.86f, 0.08f));

            for (int i = 0; i < 13; i++)
            {
                float x = -3.8f + i * 0.43f;
                CreateProp("VerticalSlice Ground plaza paving joint " + i, new Vector3(x, -0.02f + Mathf.Sin(i * 0.8f) * 0.08f, -0.18f), new Vector3(0.22f, 0.025f, 0.04f), new Color(0.36f, 0.38f, 0.34f, 0.82f));
            }

            for (int i = 0; i < 10; i++)
            {
                CreateRotatedProp("VerticalSlice Ground yellow route paint " + i, new Vector3(-5.35f + i * 0.86f, 0.42f + Mathf.Sin(i * 0.65f) * 0.12f, -0.16f), new Vector3(0.42f, 0.035f, 0.04f), guide, i % 2 == 0 ? -6f : 8f);
            }
        }

        private void CreateVerticalSliceRoomIdentities()
        {
            CreateVerticalSliceRoomShell("VerticalSlice Room 监控室 playable shell", new Vector3(-8.88f, 1.72f, 0.06f), new Vector3(2.35f, 1.48f, 0.62f), new Color(0.08f, 0.15f, 0.2f, 1f), "CCTV");
            CreateVerticalSliceRoomShell("VerticalSlice Room 茶餐厅 playable shell", new Vector3(-4.64f, 1.58f, 0.06f), new Vector3(2.52f, 1.46f, 0.54f), new Color(0.32f, 0.18f, 0.08f, 1f), "茶餐厅");
            CreateVerticalSliceRoomShell("VerticalSlice Room 夜市 playable shell", new Vector3(-0.72f, 2.88f, 0.06f), new Vector3(3.64f, 1.3f, 0.46f), new Color(0.28f, 0.12f, 0.08f, 1f), "夜市");
            CreateVerticalSliceRoomShell("VerticalSlice Room 后巷 playable shell", new Vector3(4.92f, -1.48f, 0.06f), new Vector3(2.72f, 1.34f, 0.5f), new Color(0.17f, 0.12f, 0.09f, 1f), "后巷");
            CreateVerticalSliceRoomShell("VerticalSlice Room 电房 playable shell", new Vector3(7.88f, 4.82f, 0.06f), new Vector3(2.28f, 1.52f, 0.66f), new Color(0.1f, 0.17f, 0.22f, 1f), "电房");
            CreateVerticalSliceRoomShell("VerticalSlice Room 会议点 playable shell", new Vector3(-1.18f, -0.72f, 0.05f), new Vector3(2.15f, 1.42f, 0.38f), new Color(0.12f, 0.19f, 0.2f, 1f), "集合");

            CreateAssetStoreProp("VerticalSlice Room 茶餐厅 free building front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Coffee Shop", new Vector3(-4.72f, 2.4f, 0.12f), new Vector3(1.1f, 0.58f, 0.72f), -3f, false);
            CreateAssetStoreProp("VerticalSlice Room 夜市 restaurant front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Restaurant", new Vector3(-1.02f, 3.82f, 0.12f), new Vector3(1.38f, 0.58f, 0.76f), 4f, false);
            CreateSolidAssetStoreProp("VerticalSlice Room 后巷 garage front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Auto Service", new Vector3(6.02f, -2.26f, 0.12f), new Vector3(1.18f, 0.62f, 0.78f), -8f, false);
            CreateSolidAssetStoreProp("VerticalSlice Room 电房 lowpoly utility front", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Buildings/Building_Factory", new Vector3(8.5f, 5.85f, 0.14f), new Vector3(1.22f, 0.66f, 0.86f), 5f, false);

            CreateWorldLabelAt("监控室", ScaleMapPosition(new Vector3(-8.88f, 2.54f, -0.08f)), 0.065f);
            CreateWorldLabelAt("茶餐厅", ScaleMapPosition(new Vector3(-4.64f, 2.34f, -0.08f)), 0.065f);
            CreateWorldLabelAt("夜市线人街", ScaleMapPosition(new Vector3(-0.72f, 3.62f, -0.08f)), 0.065f);
            CreateWorldLabelAt("后巷", ScaleMapPosition(new Vector3(4.92f, -0.66f, -0.08f)), 0.065f);
            CreateWorldLabelAt("电房入口", ScaleMapPosition(new Vector3(7.88f, 5.72f, -0.08f)), 0.065f);
        }

        private void CreateVerticalSliceRoomShell(string name, Vector3 center, Vector3 size, Color color, string sign)
        {
            Color wall = new Color(0.045f, 0.052f, 0.055f, 1f);
            Color trim = new Color(0.64f, 0.58f, 0.42f, 1f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;

            CreateShapeProp(name + " floor", roundedRectSprite, center + new Vector3(0f, 0f, -0.11f), size, Darken(color, 0.82f));
            CreateMeshBoxProp(name + " back wall volume", center + new Vector3(0f, halfHeight, 0.28f), new Vector3(size.x, 0.1f, size.z), wall);
            CreateMeshBoxProp(name + " left return wall", center + new Vector3(-halfWidth, 0f, 0.24f), new Vector3(0.1f, size.y, size.z * 0.86f), wall);
            CreateMeshBoxProp(name + " right return wall", center + new Vector3(halfWidth, 0f, 0.24f), new Vector3(0.1f, size.y * 0.72f, size.z * 0.8f), wall);
            CreateMeshBoxProp(name + " gold trim back", center + new Vector3(0f, halfHeight - 0.08f, 0.56f), new Vector3(size.x * 0.78f, 0.04f, 0.06f), trim);
            CreateMeshBoxProp(name + " sign board " + sign, center + new Vector3(0f, halfHeight - 0.15f, 0.72f), new Vector3(Mathf.Min(size.x * 0.55f, 1.2f), 0.045f, 0.18f), new Color(0.08f, 0.64f, 0.82f, 1f));
            RegisterWalkableArea(center, new Vector3(size.x * 0.82f, size.y * 0.78f, 0.08f));
        }

        private void CreateVerticalSliceTaskMiniGames()
        {
            CreateVerticalSliceCctvReviewTask(new Vector3(-9.45f, 2.12f, 0.18f));
            CreateVerticalSliceInformantRecordingTask(new Vector3(-4.8f, 1.32f, 0.18f));
            CreateVerticalSliceBreakerRepairTask(new Vector3(8.72f, 5.18f, 0.18f));
            CreateVerticalSlicePlateTrackingTask(new Vector3(1.74f, -3.62f, 0.18f));
        }

        private void CreateVerticalSliceCctvReviewTask(Vector3 position)
        {
            GameObject root = CreateVerticalSliceTaskRoot("VerticalSlice Task 监控调取 minigame set", position, new Color(0.08f, 0.62f, 0.86f, 1f));
            CreateAssetStoreProp("VerticalSlice Task CCTV screen model", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Screen_01", position + new Vector3(-0.25f, 0.13f, 0.12f), new Vector3(0.34f, 0.22f, 0.32f), 0f, false);
            CreateAssetStoreProp("VerticalSlice Task CCTV keypad model", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Keypad_01", position + new Vector3(0.36f, -0.1f, 0.08f), new Vector3(0.22f, 0.2f, 0.18f), -12f, false);
            CreateMeshBoxChild(root.transform, "VerticalSlice Task CCTV timeline strip", new Vector3(0f, -0.31f, 0.42f), new Vector3(0.86f, 0.035f, 0.08f), new Color(0.98f, 0.82f, 0.18f, 1f));

            for (int i = 0; i < 6; i++)
            {
                CreateMeshBoxChild(root.transform, "VerticalSlice Task CCTV camera tile " + i, new Vector3(-0.45f + i * 0.18f, 0.28f, 0.5f), new Vector3(0.12f, 0.028f, 0.1f), i == 2 ? new Color(0.92f, 0.18f, 0.12f, 1f) : new Color(0.1f, 0.62f, 0.72f, 1f));
            }
        }

        private void CreateVerticalSliceInformantRecordingTask(Vector3 position)
        {
            GameObject root = CreateVerticalSliceTaskRoot("VerticalSlice Task 线人录音 minigame set", position, new Color(0.92f, 0.48f, 0.12f, 1f));
            CreateAssetStoreProp("VerticalSlice Task recorder papers", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_03", position + new Vector3(-0.32f, 0.1f, 0.12f), new Vector3(0.28f, 0.22f, 0.16f), 15f, false);
            CreateAssetStoreProp("VerticalSlice Task recorder table", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Table_01", position + new Vector3(0f, -0.1f, 0.08f), new Vector3(0.58f, 0.34f, 0.28f), 0f, false);
            CreateAssetStoreProp("VerticalSlice Task informant chair", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Coffee shop chair", position + new Vector3(0.44f, 0.2f, 0.08f), new Vector3(0.22f, 0.2f, 0.26f), -18f, false);

            for (int i = 0; i < 7; i++)
            {
                float height = 0.08f + Mathf.Abs(Mathf.Sin(i * 1.4f)) * 0.18f;
                CreateMeshBoxChild(root.transform, "VerticalSlice Task audio waveform bar " + i, new Vector3(-0.42f + i * 0.14f, 0.36f, 0.45f), new Vector3(0.06f, 0.028f, height), new Color(0.12f, 0.78f, 0.64f, 1f));
            }
        }

        private void CreateVerticalSliceBreakerRepairTask(Vector3 position)
        {
            GameObject root = CreateVerticalSliceTaskRoot("VerticalSlice Task 电闸修复 minigame set", position, new Color(0.96f, 0.76f, 0.12f, 1f));
            CreateAssetStoreProp("VerticalSlice Task breaker switch model", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01", position + new Vector3(-0.26f, 0.08f, 0.12f), new Vector3(0.22f, 0.2f, 0.28f), 0f, false);
            CreateAssetStoreProp("VerticalSlice Task breaker lever model", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Lever_01", position + new Vector3(0.22f, -0.04f, 0.12f), new Vector3(0.2f, 0.2f, 0.26f), -16f, false);
            CreateAssetStoreProp("VerticalSlice Task fuse box model", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Switch_01", position + new Vector3(0.48f, 0.2f, 0.08f), new Vector3(0.28f, 0.2f, 0.32f), 5f, false);

            for (int i = 0; i < 5; i++)
            {
                CreateMeshBoxChild(root.transform, "VerticalSlice Task colored wire " + i, new Vector3(-0.36f + i * 0.18f, 0.34f, 0.42f), new Vector3(0.13f, 0.028f, 0.045f), i % 2 == 0 ? new Color(0.96f, 0.12f, 0.08f, 1f) : new Color(0.08f, 0.58f, 0.95f, 1f));
                CreateMeshBoxChild(root.transform, "VerticalSlice Task wire socket " + i, new Vector3(-0.36f + i * 0.18f, -0.34f, 0.42f), new Vector3(0.1f, 0.028f, 0.06f), new Color(0.14f, 0.16f, 0.16f, 1f));
            }
        }

        private void CreateVerticalSlicePlateTrackingTask(Vector3 position)
        {
            GameObject root = CreateVerticalSliceTaskRoot("VerticalSlice Task 车牌追踪 minigame set", position, new Color(0.36f, 0.72f, 0.95f, 1f));
            CreateAssetStoreProp("VerticalSlice Task plate tracking police car", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Police Car", position + new Vector3(-0.52f, -0.12f, 0.08f), new Vector3(0.72f, 0.32f, 0.3f), 8f, false);
            CreateAssetStoreProp("VerticalSlice Task plate tracking taxi", AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/Vehicle_Taxi", position + new Vector3(0.52f, 0.18f, 0.08f), new Vector3(0.64f, 0.3f, 0.28f), -12f, false);
            CreateAssetStoreProp("VerticalSlice Task license papers", AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_06", position + new Vector3(0.08f, 0.42f, 0.12f), new Vector3(0.3f, 0.2f, 0.16f), 20f, false);

            for (int i = 0; i < 6; i++)
            {
                CreateMeshBoxChild(root.transform, "VerticalSlice Task plate digit card " + i, new Vector3(-0.45f + i * 0.18f, -0.38f, 0.45f), new Vector3(0.12f, 0.028f, 0.08f), i == 3 ? new Color(0.94f, 0.16f, 0.1f, 1f) : new Color(0.9f, 0.9f, 0.82f, 1f));
            }
        }

        private GameObject CreateVerticalSliceTaskRoot(string name, Vector3 position, Color accent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(worldRoot.transform, false);
            root.transform.position = ScaleMapPosition(position);
            CreateMeshBoxChild(root.transform, "VerticalSlice Task physical base", new Vector3(0f, 0f, 0.12f), new Vector3(0.96f, 0.62f, 0.18f), new Color(0.055f, 0.065f, 0.07f, 1f));
            CreateMeshBoxChild(root.transform, "VerticalSlice Task lit face", new Vector3(0f, 0.29f, 0.3f), new Vector3(0.72f, 0.035f, 0.2f), accent);
            CreateMeshBoxChild(root.transform, "VerticalSlice Task interaction halo", new Vector3(0f, 0f, -0.02f), new Vector3(1.08f, 0.72f, 0.035f), new Color(accent.r, accent.g, accent.b, 0.22f));
            SetSortingFromZ(root);
            return root;
        }

        private void CreateVerticalSliceStreetLife()
        {
            string[] people =
            {
                AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G",
                AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/city/casual_Female_G",
                AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K",
                AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/professions/police_Female_A"
            };

            Vector3[] peoplePositions =
            {
                new Vector3(-5.35f, 1.95f, 0.1f),
                new Vector3(-2.15f, 2.72f, 0.1f),
                new Vector3(0.92f, 2.36f, 0.1f),
                new Vector3(2.62f, -0.46f, 0.1f),
                new Vector3(5.68f, -0.98f, 0.1f),
                new Vector3(-7.82f, 0.88f, 0.1f)
            };

            for (int i = 0; i < peoplePositions.Length; i++)
            {
                CreateAssetStoreProp("VerticalSlice StreetLife readable npc silhouette " + i, people[i % people.Length], peoplePositions[i], new Vector3(0.24f, 0.22f, 0.56f), i * 28f, false);
            }

            string[] props =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Barrel_Metal_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_02",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Sack_Stack_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Bottle_03",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Pot_04",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Dustbin",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bench_1",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light"
            };

            for (int i = 0; i < 32; i++)
            {
                float t = i / 31f;
                float x = Mathf.Lerp(-7.4f, 6.6f, t);
                float y = Mathf.Sin(t * Mathf.PI * 2.7f) * 1.45f + (i % 3 == 0 ? 1.65f : -0.18f);
                Vector3 position = new Vector3(x, y, 0.08f);
                Vector3 footprint = new Vector3(0.22f + (i % 3) * 0.04f, 0.2f + (i % 4) * 0.03f, 0.22f + (i % 2) * 0.08f);
                CreateAssetStoreProp("VerticalSlice StreetLife prop clutter " + i, props[i % props.Length], position, footprint, i * 17f, false);
            }

            for (int i = 0; i < 12; i++)
            {
                CreateMeshBoxProp("VerticalSlice StreetLife neon shop sign " + i, new Vector3(-5.7f + i * 0.82f, 2.9f + Mathf.Sin(i) * 0.15f, 0.52f), new Vector3(0.44f, 0.035f, 0.16f), i % 2 == 0 ? new Color(0.1f, 0.8f, 0.92f, 1f) : new Color(0.95f, 0.28f, 0.12f, 1f), i % 2 == 0 ? -4f : 7f);
            }
        }

        private void CreateVerticalSliceCollisionAndOcclusion()
        {
            Color barrier = new Color(0.11f, 0.12f, 0.12f, 1f);
            Color tape = new Color(0.9f, 0.76f, 0.1f, 1f);

            (Vector3 position, Vector3 size, float rotation)[] blockers =
            {
                (new Vector3(-7.35f, 1.18f, 0.12f), new Vector3(0.92f, 0.16f, 0.28f), 4f),
                (new Vector3(-3.28f, 0.74f, 0.12f), new Vector3(0.72f, 0.16f, 0.24f), -10f),
                (new Vector3(-0.08f, 1.62f, 0.12f), new Vector3(0.92f, 0.14f, 0.24f), 16f),
                (new Vector3(2.62f, -1.02f, 0.12f), new Vector3(0.84f, 0.16f, 0.26f), -8f),
                (new Vector3(5.88f, 0.08f, 0.12f), new Vector3(0.74f, 0.16f, 0.26f), 22f),
                (new Vector3(8.0f, 4.18f, 0.12f), new Vector3(0.78f, 0.16f, 0.28f), -5f)
            };

            for (int i = 0; i < blockers.Length; i++)
            {
                CreateSolidMeshBoxProp("VerticalSlice Collision solid police barrier " + i, blockers[i].position, blockers[i].size, barrier, blockers[i].rotation);
                CreateMeshBoxProp("VerticalSlice Collision yellow tape " + i, blockers[i].position + new Vector3(0f, 0f, 0.22f), new Vector3(blockers[i].size.x * 0.75f, 0.035f, 0.045f), tape, blockers[i].rotation);
            }

            for (int i = 0; i < 8; i++)
            {
                CreateMeshBoxProp("VerticalSlice foreground occluder awning " + i, new Vector3(-5.9f + i * 1.38f, 3.28f + Mathf.Sin(i * 0.7f) * 0.2f, 0.78f), new Vector3(0.92f, 0.12f, 0.16f), i % 2 == 0 ? new Color(0.72f, 0.08f, 0.06f, 0.88f) : new Color(0.08f, 0.18f, 0.44f, 0.88f), i % 2 == 0 ? 5f : -7f);
            }
        }

        private void CreateVerticalSliceLightingAndCameraGuides()
        {
            CreateNeonLight("VerticalSlice Light plaza blue police wash", new Vector3(-1.22f, -0.7f, 1.18f), new Color(0.16f, 0.48f, 1f, 1f), 1.4f, 4.2f);
            CreateNeonLight("VerticalSlice Light night market red sign spill", new Vector3(-0.58f, 2.72f, 1.06f), new Color(1f, 0.24f, 0.12f, 1f), 1.18f, 3.6f);
            CreateNeonLight("VerticalSlice Light cctv cyan monitor spill", new Vector3(-8.9f, 1.9f, 1.02f), new Color(0.12f, 0.88f, 1f, 1f), 1.1f, 3.4f);
            CreateNeonLight("VerticalSlice Light alley amber practical", new Vector3(4.7f, -1.34f, 0.98f), new Color(1f, 0.62f, 0.18f, 1f), 1.0f, 3.1f);
            CreateNeonLight("VerticalSlice Light power room warning", new Vector3(8.7f, 5.08f, 1.08f), new Color(0.96f, 0.78f, 0.12f, 1f), 1.2f, 3.4f);

            CreateMeshBoxProp("VerticalSlice CameraGuide first screen left depth plane", new Vector3(-7.8f, 2.6f, 0.72f), new Vector3(1.25f, 0.08f, 0.34f), new Color(0.04f, 0.08f, 0.1f, 0.72f), -4f);
            CreateMeshBoxProp("VerticalSlice CameraGuide first screen right depth plane", new Vector3(2.82f, 1.72f, 0.68f), new Vector3(1.05f, 0.08f, 0.3f), new Color(0.08f, 0.05f, 0.04f, 0.72f), 12f);
            CreateMeshBoxProp("VerticalSlice CameraGuide playable route highlight A", new Vector3(-2.32f, 0.06f, 0.03f), new Vector3(0.54f, 0.035f, 0.035f), new Color(0.94f, 0.78f, 0.18f, 1f), -12f);
            CreateMeshBoxProp("VerticalSlice CameraGuide playable route highlight B", new Vector3(-0.82f, 0.82f, 0.03f), new Vector3(0.5f, 0.035f, 0.035f), new Color(0.94f, 0.78f, 0.18f, 1f), 18f);
            CreateMeshBoxProp("VerticalSlice CameraGuide playable route highlight C", new Vector3(1.42f, 1.34f, 0.03f), new Vector3(0.5f, 0.035f, 0.035f), new Color(0.94f, 0.78f, 0.18f, 1f), -20f);
        }

        private void CreateVerticalSliceStageOneAuthoringLayer()
        {
            CreateVerticalSliceStageOneFirstScreenComposition();
            CreateVerticalSliceStageOneRoomEntrances();
            CreateVerticalSliceStageOneTaskContext();
            CreateVerticalSliceStageOneDepthAndSightlines();
            CreateVerticalSliceStageOnePlayableLandmarks();
            CreateVerticalSliceStageOneMeetingAndBlackoutFocus();
            CreateVerticalSliceStageOneGameplayAnchors();
            CreateVerticalSliceStageOneCameraShotMarkers();
            CreateVerticalSliceStageOneEditableAnchors();
        }

        private void CreateVerticalSliceStageOneFirstScreenComposition()
        {
            Color shadow = new Color(0.018f, 0.022f, 0.024f, 0.82f);
            Color wetBlue = new Color(0.05f, 0.2f, 0.24f, 0.68f);
            Color tileA = new Color(0.14f, 0.148f, 0.138f, 1f);
            Color tileB = new Color(0.1f, 0.115f, 0.11f, 1f);
            Color brass = new Color(0.82f, 0.58f, 0.14f, 1f);

            (Vector3 position, Vector3 scale, float rotation, Color color)[] groundPieces =
            {
                (new Vector3(-3.85f, -1.02f, -0.245f), new Vector3(3.1f, 0.62f, 0.045f), -12f, tileA),
                (new Vector3(-1.05f, -1.58f, -0.242f), new Vector3(2.35f, 0.55f, 0.045f), 8f, tileB),
                (new Vector3(1.45f, -1.02f, -0.242f), new Vector3(2.65f, 0.52f, 0.045f), -16f, tileA),
                (new Vector3(2.72f, 0.52f, -0.244f), new Vector3(2.2f, 0.48f, 0.045f), 18f, tileB),
                (new Vector3(-3.78f, 0.82f, -0.244f), new Vector3(2.2f, 0.46f, 0.045f), 14f, tileB),
                (new Vector3(-5.92f, 1.45f, -0.246f), new Vector3(2.05f, 0.44f, 0.045f), -8f, tileA),
                (new Vector3(4.42f, -0.3f, -0.246f), new Vector3(2.4f, 0.44f, 0.045f), -22f, tileA),
                (new Vector3(5.42f, 1.16f, -0.246f), new Vector3(1.9f, 0.42f, 0.045f), 24f, tileB)
            };

            for (int i = 0; i < groundPieces.Length; i++)
            {
                CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen non-square paving slab " + i, groundPieces[i].position, groundPieces[i].scale, groundPieces[i].color, groundPieces[i].rotation);
            }

            for (int i = 0; i < 14; i++)
            {
                float x = -6.2f + i * 0.72f;
                float y = -1.38f + Mathf.Sin(i * 0.74f) * 0.42f;
                CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen wet route reflection " + i, new Vector3(x, y, -0.17f), new Vector3(0.46f, 0.03f, 0.035f), i % 2 == 0 ? wetBlue : Darken(wetBlue, 0.78f), i % 2 == 0 ? -11f : 17f);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = -5.5f + i * 0.92f;
                float y = i % 2 == 0 ? 0.28f : -0.52f;
                CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen brass curb marker " + i, new Vector3(x, y, -0.08f), new Vector3(0.38f, 0.026f, 0.032f), brass, i % 2 == 0 ? -9f : 12f);
            }

            Vector3[] shadowBands =
            {
                new Vector3(-6.35f, 2.22f, 0.66f),
                new Vector3(-3.18f, 2.86f, 0.72f),
                new Vector3(0.62f, 2.64f, 0.7f),
                new Vector3(3.72f, 1.92f, 0.68f),
                new Vector3(5.98f, 0.34f, 0.7f),
                new Vector3(-5.25f, -1.96f, 0.66f),
                new Vector3(1.15f, -2.42f, 0.68f)
            };

            for (int i = 0; i < shadowBands.Length; i++)
            {
                CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen authored awning shadow " + i, shadowBands[i], new Vector3(1.72f, 0.12f, 0.28f), shadow, i % 2 == 0 ? -6f : 9f);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 position = new Vector3(-4.7f + i * 1.12f, 1.92f + Mathf.Sin(i * 0.55f) * 0.28f, 0.58f);
                CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen hanging neon strip " + i, position, new Vector3(0.42f, 0.034f, 0.12f), i % 3 == 0 ? new Color(0.04f, 0.7f, 0.94f, 1f) : i % 3 == 1 ? new Color(0.92f, 0.18f, 0.42f, 1f) : brass, i % 2 == 0 ? -5f : 7f);
            }
        }

        private void CreateVerticalSliceStageOneRoomEntrances()
        {
            CreateVerticalSliceStageOneEntrance("CCTV", new Vector3(-8.82f, 0.96f, 0.12f), new Vector3(1.36f, 0.34f, 0.58f), 0f, new Color(0.08f, 0.72f, 0.92f, 1f));
            CreateVerticalSliceStageOneEntrance("Cafe", new Vector3(-4.54f, 0.88f, 0.12f), new Vector3(1.42f, 0.34f, 0.5f), -4f, new Color(0.95f, 0.46f, 0.12f, 1f));
            CreateVerticalSliceStageOneEntrance("Market", new Vector3(-0.72f, 2.02f, 0.12f), new Vector3(1.72f, 0.32f, 0.46f), 7f, new Color(0.92f, 0.2f, 0.42f, 1f));
            CreateVerticalSliceStageOneEntrance("Alley", new Vector3(4.92f, -0.82f, 0.12f), new Vector3(1.32f, 0.3f, 0.48f), -12f, new Color(0.9f, 0.68f, 0.14f, 1f));
            CreateVerticalSliceStageOneEntrance("Power", new Vector3(7.86f, 4.02f, 0.12f), new Vector3(1.28f, 0.32f, 0.62f), 6f, new Color(0.86f, 0.18f, 0.12f, 1f));
            CreateVerticalSliceStageOneEntrance("Meeting", new Vector3(-1.18f, -1.58f, 0.12f), new Vector3(1.52f, 0.3f, 0.42f), 0f, new Color(0.12f, 0.78f, 0.76f, 1f));
        }

        private void CreateVerticalSliceStageOneEntrance(string id, Vector3 center, Vector3 size, float rotation, Color accent)
        {
            Color frame = new Color(0.032f, 0.04f, 0.042f, 1f);
            Color glass = new Color(0.08f, 0.18f, 0.22f, 0.72f);
            float sideOffset = Mathf.Max(0.22f, size.x * 0.48f);

            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " threshold floor", center + new Vector3(0f, 0f, -0.14f), new Vector3(size.x * 1.05f, size.y * 0.72f, 0.05f), Darken(accent, 0.38f), rotation);
            CreateSolidMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " left jamb", center + new Vector3(-sideOffset, 0f, 0.18f), new Vector3(0.12f, size.y * 0.86f, size.z), frame, rotation);
            CreateSolidMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " right jamb", center + new Vector3(sideOffset, 0f, 0.18f), new Vector3(0.12f, size.y * 0.86f, size.z), frame, rotation);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " header", center + new Vector3(0f, size.y * 0.42f, 0.48f), new Vector3(size.x, 0.065f, 0.14f), frame, rotation);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " glass glow", center + new Vector3(0f, size.y * 0.12f, 0.36f), new Vector3(size.x * 0.72f, 0.035f, size.z * 0.34f), glass, rotation);
            CreateMeshBoxProp("VerticalSlice Stage1 Entrance " + id + " role color strip", center + new Vector3(0f, size.y * 0.49f, 0.66f), new Vector3(size.x * 0.64f, 0.034f, 0.055f), accent, rotation);
        }

        private void CreateVerticalSliceStageOneTaskContext()
        {
            string[] contextProps =
            {
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Shelf_02",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Cardboard_Box_03",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Papers_05",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Light_Wall_01",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Aircon_01",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Traffic Control Barrier Fence"
            };

            (string id, Vector3 center, Color accent)[] taskZones =
            {
                ("CCTV", new Vector3(-9.45f, 2.12f, 0.18f), new Color(0.08f, 0.72f, 0.92f, 1f)),
                ("Recorder", new Vector3(-4.8f, 1.32f, 0.18f), new Color(0.92f, 0.46f, 0.12f, 1f)),
                ("Breaker", new Vector3(8.72f, 5.18f, 0.18f), new Color(0.96f, 0.76f, 0.1f, 1f)),
                ("Plate", new Vector3(1.74f, -3.62f, 0.18f), new Color(0.28f, 0.66f, 0.92f, 1f))
            };

            for (int zone = 0; zone < taskZones.Length; zone++)
            {
                Vector3 center = taskZones[zone].center;
                Color accent = taskZones[zone].accent;

                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 60f + zone * 11f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * 0.62f, Mathf.Sin(angle * Mathf.Deg2Rad) * 0.38f, 0f);
                    Vector3 footprint = i % 2 == 0 ? new Vector3(0.24f, 0.18f, 0.28f) : new Vector3(0.18f, 0.16f, 0.22f);
                    CreateAssetStoreProp("VerticalSlice Stage1 TaskContext " + taskZones[zone].id + " evidence prop " + i, contextProps[(zone + i) % contextProps.Length], center + offset + new Vector3(0f, 0f, 0.02f), footprint, angle, false);
                }

                for (int i = 0; i < 5; i++)
                {
                    CreateMeshBoxProp("VerticalSlice Stage1 TaskContext " + taskZones[zone].id + " cable run " + i, center + new Vector3(-0.62f + i * 0.31f, 0.52f, 0.34f), new Vector3(0.23f, 0.028f, 0.035f), i % 2 == 0 ? accent : Darken(accent, 0.5f), i % 2 == 0 ? -8f : 12f);
                }

                CreateMeshBoxProp("VerticalSlice Stage1 TaskContext " + taskZones[zone].id + " readable task pad", center + new Vector3(0f, -0.56f, 0.04f), new Vector3(0.88f, 0.04f, 0.055f), accent, 0f);
                CreateMeshBoxProp("VerticalSlice Stage1 TaskContext " + taskZones[zone].id + " shadow anchor", center + new Vector3(0f, 0.02f, -0.03f), new Vector3(1.22f, 0.72f, 0.035f), new Color(0f, 0f, 0f, 0.38f), 0f);
            }
        }

        private void CreateVerticalSliceStageOneDepthAndSightlines()
        {
            Color deepShadow = new Color(0.004f, 0.006f, 0.008f, 0.74f);
            Color steel = new Color(0.035f, 0.044f, 0.048f, 1f);
            Color blue = new Color(0.06f, 0.42f, 0.88f, 1f);
            Color red = new Color(0.82f, 0.08f, 0.06f, 1f);
            Color amber = new Color(0.9f, 0.62f, 0.12f, 1f);

            (Vector3 position, Vector3 scale, float rotation)[] sightlines =
            {
                (new Vector3(-6.1f, -0.1f, 0.3f), new Vector3(1.24f, 0.14f, 0.5f), -14f),
                (new Vector3(-3.15f, -1.9f, 0.3f), new Vector3(0.16f, 1.08f, 0.48f), 9f),
                (new Vector3(0.9f, 1.08f, 0.32f), new Vector3(1.18f, 0.14f, 0.48f), 18f),
                (new Vector3(3.62f, 0.36f, 0.32f), new Vector3(0.16f, 1.14f, 0.5f), -12f),
                (new Vector3(5.95f, -1.82f, 0.3f), new Vector3(1.16f, 0.14f, 0.48f), 16f),
                (new Vector3(7.52f, 3.45f, 0.32f), new Vector3(0.18f, 1.16f, 0.5f), -8f),
                (new Vector3(-8.42f, 2.82f, 0.32f), new Vector3(1.12f, 0.14f, 0.48f), 6f),
                (new Vector3(1.55f, -3.0f, 0.32f), new Vector3(1.32f, 0.14f, 0.48f), -10f)
            };

            for (int i = 0; i < sightlines.Length; i++)
            {
                CreateSolidMeshBoxProp("VerticalSlice Stage1 Sightline authored blocker " + i, sightlines[i].position, sightlines[i].scale, steel, sightlines[i].rotation);
                CreateMeshBoxProp("VerticalSlice Stage1 Sightline blocker status light " + i, sightlines[i].position + new Vector3(0f, 0.1f, 0.32f), new Vector3(Mathf.Max(0.18f, sightlines[i].scale.x * 0.52f), 0.03f, 0.05f), i % 3 == 0 ? blue : i % 3 == 1 ? red : amber, sightlines[i].rotation);
            }

            for (int i = 0; i < 11; i++)
            {
                float x = -9.2f + i * 1.84f;
                CreateMeshBoxProp("VerticalSlice Stage1 Depth parallax shop roof " + i, new Vector3(x, 4.55f + Mathf.Sin(i * 0.6f) * 0.16f, 0.86f), new Vector3(1.12f, 0.12f, 0.28f), i % 2 == 0 ? deepShadow : Darken(deepShadow, 0.82f), i % 2 == 0 ? -4f : 6f);
                CreateMeshBoxProp("VerticalSlice Stage1 Sightline parallax roof occluder " + i, new Vector3(x, 4.28f + Mathf.Sin(i * 0.6f) * 0.12f, 0.58f), new Vector3(0.88f, 0.08f, 0.18f), deepShadow, i % 2 == 0 ? -4f : 6f);
            }

            for (int i = 0; i < 9; i++)
            {
                float y = -4.62f + i * 1.1f;
                CreateMeshBoxProp("VerticalSlice Stage1 Depth side water reflection " + i, new Vector3(i % 2 == 0 ? -11.15f : 11.12f, y, -0.18f), new Vector3(0.58f, 0.028f, 0.035f), new Color(0.1f, 0.34f, 0.42f, 0.8f), i % 2 == 0 ? -6f : 8f);
            }
        }

        private void CreateVerticalSliceStageOnePlayableLandmarks()
        {
            string[] streetProps =
            {
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_BillBoard_large",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Bus Stop",
                AssetStoreResourceRoot + "SimplePoly City - Low Poly Assets/Prefab/Props/Props_Street Light",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Cafe_table_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Cafe_chair_1",
                AssetStoreResourceRoot + "ModularLowpolyStreetsFree/Prefabs/Other/Traffic_light",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Barrel_Metal_03",
                AssetStoreResourceRoot + "Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Crate_03"
            };

            Vector3[] landmarks =
            {
                new Vector3(-5.62f, 1.82f, 0.14f),
                new Vector3(-4.22f, 2.08f, 0.14f),
                new Vector3(-2.55f, 2.74f, 0.14f),
                new Vector3(-0.22f, 2.68f, 0.14f),
                new Vector3(1.42f, 2.18f, 0.14f),
                new Vector3(3.18f, 0.82f, 0.14f),
                new Vector3(4.92f, -1.12f, 0.14f),
                new Vector3(5.78f, 0.18f, 0.14f),
                new Vector3(-7.42f, 0.46f, 0.14f),
                new Vector3(0.12f, -2.18f, 0.14f),
                new Vector3(2.18f, -3.18f, 0.14f),
                new Vector3(-1.92f, -2.38f, 0.14f)
            };

            for (int i = 0; i < landmarks.Length; i++)
            {
                bool solid = i % 4 == 0 || i % 5 == 0;
                Vector3 footprint = i % 3 == 0 ? new Vector3(0.42f, 0.26f, 0.46f) : new Vector3(0.28f, 0.22f, 0.32f);

                if (solid)
                {
                    CreateSolidAssetStoreProp("VerticalSlice Stage1 FirstScreen landmark prop " + i, streetProps[i % streetProps.Length], landmarks[i], footprint, i * 19f, false);
                }
                else
                {
                    CreateAssetStoreProp("VerticalSlice Stage1 FirstScreen landmark prop " + i, streetProps[i % streetProps.Length], landmarks[i], footprint, i * 19f, false);
                }
            }

            CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen police evidence van lightbar", new Vector3(0.08f, -4.72f, 0.42f), new Vector3(0.84f, 0.04f, 0.07f), new Color(0.08f, 0.32f, 0.95f, 1f), -2f);
            CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen gang handoff red marker", new Vector3(4.88f, -1.92f, 0.38f), new Vector3(0.5f, 0.035f, 0.07f), new Color(0.86f, 0.08f, 0.06f, 1f), 14f);
            CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen undercover route cyan marker", new Vector3(-3.18f, 0.42f, 0.34f), new Vector3(0.54f, 0.035f, 0.07f), new Color(0.08f, 0.72f, 0.82f, 1f), -11f);
            CreateMeshBoxProp("VerticalSlice Stage1 FirstScreen meeting ring readable rim", new Vector3(-1.18f, -0.72f, 0.18f), new Vector3(1.62f, 0.045f, 0.08f), new Color(0.92f, 0.76f, 0.12f, 1f), 0f);
        }

        private void CreateVerticalSliceStageOneMeetingAndBlackoutFocus()
        {
            Color police = new Color(0.08f, 0.32f, 0.9f, 1f);
            Color gang = new Color(0.84f, 0.08f, 0.06f, 1f);
            Color cyan = new Color(0.08f, 0.74f, 0.86f, 1f);
            Color amber = new Color(0.94f, 0.72f, 0.12f, 1f);
            Color table = new Color(0.14f, 0.17f, 0.18f, 1f);
            Color shadow = new Color(0f, 0f, 0f, 0.56f);

            Vector3 meetingCenter = new Vector3(-1.18f, -0.72f, 0.16f);
            CreateMeshPrimitiveProp("VerticalSlice Stage1 Meeting evidence round table", PrimitiveType.Cylinder, meetingCenter + new Vector3(0f, 0f, 0.1f), new Vector3(0.86f, 0.055f, 0.86f), table, Quaternion.Euler(90f, 0f, 0f));
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting voice channel blue strip", meetingCenter + new Vector3(-0.28f, 0.2f, 0.34f), new Vector3(0.78f, 0.035f, 0.06f), police, -8f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting suspicion red strip", meetingCenter + new Vector3(0.26f, -0.22f, 0.34f), new Vector3(0.72f, 0.035f, 0.06f), gang, 10f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall panel", meetingCenter + new Vector3(-1.24f, 0.48f, 0.62f), new Vector3(0.92f, 0.065f, 0.52f), new Color(0.78f, 0.8f, 0.72f, 1f), -6f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall red thread", meetingCenter + new Vector3(-1.34f, 0.52f, 0.86f), new Vector3(0.58f, 0.025f, 0.04f), gang, 13f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting evidence wall blue thread", meetingCenter + new Vector3(-1.08f, 0.52f, 0.74f), new Vector3(0.46f, 0.025f, 0.04f), police, -16f);
            CreateMeshBoxProp("VerticalSlice Stage1 Meeting overhead shadow frame", meetingCenter + new Vector3(0.2f, 0.96f, 0.88f), new Vector3(2.45f, 0.16f, 0.32f), shadow, 0f);

            for (int i = 0; i < 10; i++)
            {
                float angle = i / 10f * Mathf.PI * 2f;
                Vector3 seat = meetingCenter + new Vector3(Mathf.Cos(angle) * 1.36f, Mathf.Sin(angle) * 0.86f, 0.04f);
                CreateMeshPrimitiveProp("VerticalSlice Stage1 Meeting player voice seat " + i, PrimitiveType.Cylinder, seat, new Vector3(0.16f, 0.035f, 0.16f), i % 3 == 0 ? police : i % 3 == 1 ? gang : cyan, Quaternion.Euler(90f, 0f, 0f));
                CreateMeshBoxProp("VerticalSlice Stage1 Meeting vote card " + i, seat + new Vector3(0f, 0.12f, 0.2f), new Vector3(0.22f, 0.03f, 0.06f), amber, i * 13f);
            }

            Vector3 blackoutCore = new Vector3(8.72f, 5.18f, 0.22f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout breaker silhouette wall", blackoutCore + new Vector3(0f, 0.38f, 0.56f), new Vector3(1.42f, 0.1f, 0.78f), new Color(0.018f, 0.022f, 0.026f, 1f), 0f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout red emergency strip", blackoutCore + new Vector3(0f, 0.48f, 0.98f), new Vector3(1.12f, 0.035f, 0.06f), gang, 0f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout repair target amber pad", blackoutCore + new Vector3(0f, -0.58f, 0.06f), new Vector3(0.92f, 0.045f, 0.06f), amber, 0f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout visible cable A", blackoutCore + new Vector3(-0.42f, 0.05f, 0.54f), new Vector3(0.56f, 0.028f, 0.05f), cyan, -14f);
            CreateMeshBoxProp("VerticalSlice Stage1 Blackout visible cable B", blackoutCore + new Vector3(0.34f, -0.06f, 0.5f), new Vector3(0.5f, 0.028f, 0.05f), gang, 16f);

            for (int i = 0; i < 8; i++)
            {
                float x = -0.9f + i * 0.26f;
                CreateMeshBoxProp("VerticalSlice Stage1 Blackout fuse slot " + i, blackoutCore + new Vector3(x, 0.52f, 0.78f), new Vector3(0.12f, 0.028f, 0.08f), i % 2 == 0 ? amber : gang, 0f);
            }

            for (int i = 0; i < 9; i++)
            {
                Vector3 route = new Vector3(7.42f - i * 1.06f, 4.72f - Mathf.Sin(i * 0.55f) * 0.3f, 0.08f);
                CreateMeshBoxProp("VerticalSlice Stage1 Blackout emergency floor arrow " + i, route, new Vector3(0.42f, 0.035f, 0.05f), i % 2 == 0 ? amber : cyan, i % 2 == 0 ? -13f : 12f);
            }
        }

        private void CreateVerticalSliceStageOneGameplayAnchors()
        {
            Color police = new Color(0.08f, 0.32f, 0.92f, 1f);
            Color gang = new Color(0.86f, 0.08f, 0.06f, 1f);
            Color neutral = new Color(0.9f, 0.72f, 0.12f, 1f);
            Color voice = new Color(0.12f, 0.78f, 0.66f, 0.52f);
            Color stealth = new Color(0.52f, 0.22f, 0.72f, 1f);

            (string name, Vector3 position, Vector3 scale, Color color, float rotation)[] anchors =
            {
                ("VerticalSlice Stage1 GameplayAnchor kill sightline corner A", new Vector3(3.62f, 0.36f, 0.32f), new Vector3(0.78f, 0.05f, 0.08f), gang, -12f),
                ("VerticalSlice Stage1 GameplayAnchor kill sightline corner B", new Vector3(5.95f, -1.82f, 0.32f), new Vector3(0.78f, 0.05f, 0.08f), gang, 14f),
                ("VerticalSlice Stage1 GameplayAnchor report route tape A", new Vector3(2.18f, -3.18f, 0.14f), new Vector3(0.92f, 0.04f, 0.06f), neutral, -12f),
                ("VerticalSlice Stage1 GameplayAnchor report route tape B", new Vector3(0.12f, -2.18f, 0.14f), new Vector3(0.92f, 0.04f, 0.06f), neutral, 16f),
                ("VerticalSlice Stage1 GameplayAnchor police patrol lane", new Vector3(-5.35f, 1.95f, 0.16f), new Vector3(0.82f, 0.04f, 0.06f), police, 7f),
                ("VerticalSlice Stage1 GameplayAnchor gang vent hint", new Vector3(4.9f, -0.32f, 0.16f), new Vector3(0.54f, 0.04f, 0.06f), stealth, -18f),
                ("VerticalSlice Stage1 GameplayAnchor undercover route hint", new Vector3(-3.18f, 0.42f, 0.16f), new Vector3(0.58f, 0.04f, 0.06f), new Color(0.08f, 0.72f, 0.82f, 1f), -11f),
                ("VerticalSlice Stage1 GameplayAnchor emergency bell focus", new Vector3(0f, 0f, 0.28f), new Vector3(0.48f, 0.04f, 0.07f), gang, 0f)
            };

            for (int i = 0; i < anchors.Length; i++)
            {
                CreateMeshBoxProp(anchors[i].name, anchors[i].position, anchors[i].scale, anchors[i].color, anchors[i].rotation);
            }

            Vector3[] voiceCenters =
            {
                new Vector3(-1.18f, -0.72f, 0.02f),
                new Vector3(-4.8f, 1.32f, 0.02f),
                new Vector3(4.92f, -0.82f, 0.02f)
            };

            for (int i = 0; i < voiceCenters.Length; i++)
            {
                CreateShapeProp("VerticalSlice Stage1 GameplayAnchor action voice radius " + i, softCircleSprite, voiceCenters[i], new Vector3(2.0f, 1.25f, 0.04f), voice);
            }
        }

        private void CreateVerticalSliceStageOneCameraShotMarkers()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                if (spec.Category != "Camera")
                {
                    continue;
                }

                CreateStageOneShotMarker(spec);
            }
        }

        private void CreateStageOneShotMarker(VerticalSliceStageOneAnchorSpec spec)
        {
            Vector3 center = spec.DesignPosition + new Vector3(0f, 0f, 0.04f);
            Color color = spec.DebugColor;
            CreateShapeProp("VerticalSlice Stage1 CameraShot " + spec.Id + " footprint", softCircleSprite, center, new Vector3(spec.Footprint.x, spec.Footprint.y, 0.04f), new Color(color.r, color.g, color.b, 0.12f));
            CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame top", center + new Vector3(0f, spec.Footprint.y * 0.5f, 0.16f), new Vector3(spec.Footprint.x, 0.035f, 0.05f), color);
            CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame bottom", center + new Vector3(0f, -spec.Footprint.y * 0.5f, 0.16f), new Vector3(spec.Footprint.x, 0.035f, 0.05f), color);
            CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame left", center + new Vector3(-spec.Footprint.x * 0.5f, 0f, 0.16f), new Vector3(0.035f, spec.Footprint.y, 0.05f), color);
            CreateMeshBoxProp("VerticalSlice Stage1 CameraShot " + spec.Id + " frame right", center + new Vector3(spec.Footprint.x * 0.5f, 0f, 0.16f), new Vector3(0.035f, spec.Footprint.y, 0.05f), color);
        }

        private void CreateVerticalSliceStageOneEditableAnchors()
        {
            foreach (VerticalSliceStageOneAnchorSpec spec in VerticalSliceStageOneAnchorCatalog.Specs)
            {
                GameObject anchor = new GameObject("VerticalSlice Stage1 EditableAnchor " + spec.Id);
                anchor.transform.SetParent(worldRoot.transform, false);
                anchor.transform.position = ScaleMapPosition(spec.DesignPosition);
                VerticalSliceStageOneAnchor component = anchor.AddComponent<VerticalSliceStageOneAnchor>();
                component.Configure(spec);

                CreateShapeProp("VerticalSlice Stage1 GameplayAnchor editable footprint " + spec.Id, softCircleSprite, spec.DesignPosition + new Vector3(0f, 0f, -0.02f), new Vector3(spec.Footprint.x, spec.Footprint.y, 0.04f), new Color(spec.DebugColor.r, spec.DebugColor.g, spec.DebugColor.b, 0.08f));
            }
        }
    }
}
