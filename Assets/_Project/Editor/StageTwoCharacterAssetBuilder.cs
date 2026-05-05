using System.IO;
using GanglandUndercover.Online;
using UnityEditor;
using UnityEngine;

namespace GanglandUndercover.Editor
{
    public static class StageTwoCharacterAssetBuilder
    {
        public const string AssetRoot = "Assets/_Project/Resources/Stage2/Characters";
        public const string PolicePrefabPath = AssetRoot + "/Stage2_Police.prefab";
        public const string UndercoverPrefabPath = AssetRoot + "/Stage2_Undercover.prefab";
        public const string GangPrefabPath = AssetRoot + "/Stage2_Gang.prefab";
        public const string CivilianPrefabPath = AssetRoot + "/Stage2_Civilian.prefab";
        public const string StampPath = "Assets/_Project/Docs/Stage2CharacterBakeStamp.txt";

        [MenuItem("Gangland/Build Stage2 Character Assets")]
        public static void BuildStageTwoCharacterAssets()
        {
            EnsureDirectory(AssetRoot);
            EnsureDirectory("Assets/_Project/Docs");

            BuildRigCatalog();
            BuildCharacterPrefab(PolicePrefabPath, "Stage2 Police Rig", "police", "AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01", new Color(0.16f, 0.48f, 0.9f, 1f), new Color(0.95f, 0.8f, 0.12f, 1f));
            BuildCharacterPrefab(UndercoverPrefabPath, "Stage2 Undercover Rig", "undercover", "AssetStore/DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G", new Color(0.36f, 0.28f, 0.66f, 1f), new Color(0.1f, 0.76f, 0.7f, 1f));
            BuildCharacterPrefab(GangPrefabPath, "Stage2 Gang Rig", "gang", "AssetStore/DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K", new Color(0.58f, 0.1f, 0.12f, 1f), new Color(0.95f, 0.34f, 0.14f, 1f));
            BuildCharacterPrefab(CivilianPrefabPath, "Stage2 Civilian Rig", "civilian", "AssetStore/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Female_01", new Color(0.25f, 0.42f, 0.34f, 1f), new Color(0.86f, 0.72f, 0.32f, 1f));

            WriteStamp();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Stage2 character rig assets built under " + AssetRoot);
        }

        [MenuItem("Gangland/Build Stage2 Character Assets", true)]
        private static bool CanBuildStageTwoCharacterAssets()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static bool StageTwoCharacterAssetsExist()
        {
            return File.Exists(StageTwoCharacterRig.RigCatalogAssetPath)
                && File.Exists(PolicePrefabPath)
                && File.Exists(UndercoverPrefabPath)
                && File.Exists(GangPrefabPath)
                && File.Exists(CivilianPrefabPath)
                && File.Exists(StampPath);
        }

        private static void BuildRigCatalog()
        {
            StageTwoCharacterRigCatalog catalog = AssetDatabase.LoadAssetAtPath<StageTwoCharacterRigCatalog>(StageTwoCharacterRig.RigCatalogAssetPath);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageTwoCharacterRigCatalog>();
                AssetDatabase.CreateAsset(catalog, StageTwoCharacterRig.RigCatalogAssetPath);
            }

            catalog.Poses = new[]
            {
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Idle),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Walk),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Interact),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Downed),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Report),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Meeting),
                StageTwoCharacterPose.DefaultFor(StageTwoCharacterVisualState.Vote)
            };

            EditorUtility.SetDirty(catalog);
        }

        private static void BuildCharacterPrefab(string prefabPath, string prefabName, string rigKey, string sourceResourcePath, Color primary, Color accent)
        {
            GameObject root = new GameObject(prefabName);
            StageTwoCharacterRig rig = root.AddComponent<StageTwoCharacterRig>();
            rig.Configure(rigKey, sourceResourcePath);

            GameObject bodyRoot = CreatePart(root.transform, "BodyRoot", PrimitiveType.Capsule, new Vector3(0f, -0.04f, 0.3f), new Vector3(0.28f, 0.28f, 0.58f), primary, Quaternion.Euler(90f, 0f, 0f));
            GameObject head = CreatePart(root.transform, "HeadRoot", PrimitiveType.Sphere, new Vector3(0.03f, 0.32f, 0.58f), new Vector3(0.28f, 0.24f, 0.24f), primary, Quaternion.identity);
            GameObject armL = CreatePart(root.transform, "LeftArm", PrimitiveType.Capsule, new Vector3(-0.24f, -0.04f, 0.34f), new Vector3(0.07f, 0.07f, 0.3f), accent, Quaternion.Euler(90f, 0f, 12f));
            GameObject armR = CreatePart(root.transform, "RightArm", PrimitiveType.Capsule, new Vector3(0.24f, -0.04f, 0.34f), new Vector3(0.07f, 0.07f, 0.3f), accent, Quaternion.Euler(90f, 0f, -12f));
            GameObject footL = CreatePart(root.transform, "LeftFoot", PrimitiveType.Cube, new Vector3(-0.1f, -0.46f, 0.14f), new Vector3(0.14f, 0.16f, 0.08f), Darken(accent, 0.62f), Quaternion.identity);
            GameObject footR = CreatePart(root.transform, "RightFoot", PrimitiveType.Cube, new Vector3(0.1f, -0.46f, 0.14f), new Vector3(0.14f, 0.16f, 0.08f), Darken(accent, 0.62f), Quaternion.identity);
            GameObject stateRoot = new GameObject("StateRoot");
            stateRoot.transform.SetParent(root.transform, false);
            stateRoot.transform.localPosition = Vector3.zero;

            CreatePart(head.transform, "FaceStrip", PrimitiveType.Cube, new Vector3(0.08f, 0.12f, 0.04f), new Vector3(0.22f, 0.035f, 0.08f), new Color(0.78f, 0.92f, 0.96f, 1f), Quaternion.identity);
            CreatePart(bodyRoot.transform, "RoleAccent", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0.1f), new Vector3(0.18f, 0.035f, 0.08f), accent, Quaternion.identity);
            CreatePart(stateRoot.transform, "Prefab State Ring", PrimitiveType.Cylinder, new Vector3(0f, -0.42f, -0.12f), new Vector3(0.34f, 0.025f, 0.22f), new Color(accent.r, accent.g, accent.b, 0.48f), Quaternion.Euler(90f, 0f, 0f));

            rig.BodyRoot = bodyRoot.transform;
            rig.HeadRoot = head.transform;
            rig.LeftArm = armL.transform;
            rig.RightArm = armR.transform;
            rig.LeftFoot = footL.transform;
            rig.RightFoot = footR.transform;
            rig.StateRoot = stateRoot.transform;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static GameObject CreatePart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, Quaternion localRotation)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;

            Collider collider = part.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();

            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material material = new Material(shader)
                {
                    color = color
                };

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return part;
        }

        private static Color Darken(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        private static void EnsureDirectory(string assetPath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            Directory.CreateDirectory(fullPath);
        }

        private static void WriteStamp()
        {
            string stamp = "Stage2 character rig asset bake\n"
                + "Time: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n"
                + "Catalog: " + StageTwoCharacterRig.RigCatalogAssetPath + "\n"
                + "Prefabs: 4\n"
                + "States: Idle, Walk, Interact, Downed, Report, Meeting, Vote\n";
            File.WriteAllText(StampPath, stamp);
        }
    }
}
