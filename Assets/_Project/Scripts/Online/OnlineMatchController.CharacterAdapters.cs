using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {
        private void CreateFreeCharacterAdapter(Transform parent, OnlinePlayerState state)
        {
            string prefabPath = FreeCharacterPrefabPath(state);
            GameObject prefab = LoadResourcePrefab(prefabPath);

            if (prefab == null)
            {
                CreateFallbackCharacterIdentity(parent, state);
                return;
            }

            GameObject model = InstantiateModelPrefab(prefab);

            if (model == null)
            {
                CreateFallbackCharacterIdentity(parent, state);
                return;
            }

            model.name = "FreeCharacterAdapter " + state.Profession;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0f, -0.16f, 0.02f);
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 180f);
            model.transform.localScale = Vector3.one;
            ConfigureModelRenderers(model, true);
            FitCharacterAdapterToPlayer(model);
            TintCharacterAdapter(model, state);
            SetSortingFromZ(model);

            foreach (UnityEngine.Collider collider in model.GetComponentsInChildren<UnityEngine.Collider>(true))
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            foreach (Rigidbody rigidbody in model.GetComponentsInChildren<Rigidbody>(true))
            {
                if (Application.isPlaying)
                {
                    Destroy(rigidbody);
                }
                else
                {
                    DestroyImmediate(rigidbody);
                }
            }
        }

        private static string FreeCharacterPrefabPath(OnlinePlayerState state)
        {
            switch (state.Profession)
            {
                case OnlineProfession.Inspector:
                case OnlineProfession.Tech:
                    return AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Cop_01";
                case OnlineProfession.Forensics:
                    return AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Female_01";
                case OnlineProfession.UndercoverAgent:
                    return AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/city/casual_Male_G";
                case OnlineProfession.Enforcer:
                    return AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/downtown/casual_Male_K";
                case OnlineProfession.Fixer:
                    return AssetStoreResourceRoot + "DenysAlmaral/CityPeople/Prefabs/city/casual_Female_G";
                case OnlineProfession.Driver:
                    return AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01";
                default:
                    return AssetStoreResourceRoot + "Synty/PolygonStarter/Prefabs/Characters/SM_Bean_Female_01";
            }
        }

        private static void FitCharacterAdapterToPlayer(GameObject model)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds))
            {
                model.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
                return;
            }

            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float factor = largest > 0.001f ? 0.82f / largest : 0.18f;
            model.transform.localScale *= Mathf.Clamp(factor, 0.04f, 0.32f);
        }

        private void TintCharacterAdapter(GameObject model, OnlinePlayerState state)
        {
            Color accent = PlayerAccentColor(state);
            Color roleColor = PlayerColor(state, false);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;

                if (material == null)
                {
                    continue;
                }

                Color current = ReadMaterialColor(material, Color.white);
                Color mixed = Color.Lerp(current, Color.Lerp(roleColor, accent, 0.42f), 0.28f);
                SetMaterialColor(material, new Color(mixed.r, mixed.g, mixed.b, current.a));
            }
        }

        private void CreateFallbackCharacterIdentity(Transform parent, OnlinePlayerState state)
        {
            Color accent = PlayerAccentColor(state);
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback coat panel", new Vector3(0f, -0.08f, 0.58f), new Vector3(0.28f, 0.035f, 0.3f), Darken(accent, 0.72f));
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback face strip", new Vector3(0.13f, 0.34f, 0.68f), new Vector3(0.2f, 0.035f, 0.09f), new Color(0.94f, 0.84f, 0.66f, 1f));
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback role prop", new Vector3(-0.24f, -0.2f, 0.54f), new Vector3(0.1f, 0.04f, 0.18f), accent);
        }
    }
}
