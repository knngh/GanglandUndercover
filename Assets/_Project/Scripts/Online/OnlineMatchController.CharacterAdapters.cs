using GanglandUndercover.SocialDeduction;
using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {
        private void CreateFreeCharacterAdapter(Transform parent, OnlinePlayerState state)
        {
            // M3: 2D character path — simple circle + direction arrow
            if (worldBuilder != null && worldBuilder.Use2DBackend)
            {
                Create2DCharacterView(parent, state);
                return;
            }

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

            ConfigureCharacterAnimator(model, state);
        }

        private static void ConfigureCharacterAnimator(GameObject model, OnlinePlayerState state)
        {
            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.keepAnimatorStateOnDisable = false;

            state.CharacterAnimator = animator;

            // 通过 SocialCharacter 封装动画驱动，支持 SetMoveSpeed / TriggerAction / Kill
            var socialChar = model.GetComponent<GanglandUndercover.SocialDeduction.SocialCharacter>();
            if (socialChar == null)
            {
                socialChar = model.AddComponent<GanglandUndercover.SocialDeduction.SocialCharacter>();
            }

            socialChar.BindAnimator(animator);
            state.SocialChar = socialChar;
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
            // M3: Delegate to 2D path when 2D backend is active
            if (worldBuilder != null && worldBuilder.Use2DBackend)
            {
                Create2DCharacterView(parent, state);
                return;
            }

            Color accent = PlayerAccentColor(state);
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback coat panel", new Vector3(0f, -0.08f, 0.58f), new Vector3(0.28f, 0.035f, 0.3f), Darken(accent, 0.72f));
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback face strip", new Vector3(0.13f, 0.34f, 0.68f), new Vector3(0.2f, 0.035f, 0.09f), new Color(0.94f, 0.84f, 0.66f, 1f));
            CreateMeshBoxChild(parent, "FreeCharacterAdapter fallback role prop", new Vector3(-0.24f, -0.2f, 0.54f), new Vector3(0.1f, 0.04f, 0.18f), accent);
        }

        // ====================================================================
        //  M3: 2D Character View (top-down circle + direction indicator)
        // ====================================================================

        /// <summary>
        /// Creates a 2D character representation for orthographic top-down view.
        /// Body = colored circle (profession-based), Direction = small arrow.
        /// Replaces 3D prefab loading when Use2DBackend is active.
        /// </summary>
        private void Create2DCharacterView(Transform parent, OnlinePlayerState state)
        {
            if (worldBuilder == null) return;

            Color bodyColor = PlayerAccentColor(state);

            // --- Body circle ---
            // Use circleSprite for round, soften color slightly for readability。
            // 命名前缀沿用 "FreeCharacterAdapter"：2D 主路径下它就是每位玩家的角色适配层，
            // 让 FreeCharacterAdapterCount 计数对 2D 后端同样成立。
            GameObject body = new GameObject("FreeCharacterAdapter 2D " + state.Profession);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = worldBuilder.CircleSprite;
            bodyRenderer.color = new Color(bodyColor.r * 0.85f, bodyColor.g * 0.85f, bodyColor.b * 0.85f, 1f);
            bodyRenderer.sortingOrder = 100; // Players always on top of world props

            body.transform.SetParent(parent, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(0.64f, 0.64f, 1f);

            // --- Direction indicator (arrow) ---
            GameObject dir = new GameObject("2DDir_" + state.Profession);
            SpriteRenderer dirRenderer = dir.AddComponent<SpriteRenderer>();
            dirRenderer.sprite = worldBuilder.DiamondSprite;
            dirRenderer.color = Color.white;
            dirRenderer.sortingOrder = 101; // Above body

            dir.transform.SetParent(body.transform, false);
            dir.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            dir.transform.localScale = new Vector3(0.20f, 0.30f, 1f);

            // Store the direction indicator so movement rotation can update it
            state.Character2DDirectionIndicator = dir;

            // --- Stub SocialChar for compatibility with existing systems ---
            var socialChar = body.AddComponent<GanglandUndercover.SocialDeduction.SocialCharacter>();
            state.SocialChar = socialChar;
        }
    }
}
