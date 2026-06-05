using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

using GanglandUndercover.Core;
using GanglandUndercover.SocialDeduction;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// World / prop / task / body / player visual builder.
    /// Extracted from OnlineMatchController for M2 controller slimming.
    /// Holds all rendering layer state (sprites, materials, model caches, collision rects, labels).
    /// Pure class — no MonoBehaviour dependency.
    /// M3: Use2DBackend defaults to true — all world props render via SpriteRenderer.
    /// </summary>
    public class OnlineWorldBuilder
    {
        // --- M3 Backend Toggle ---
        /// <summary>
        /// When true, all world building uses SpriteRenderer exclusively.
        /// When false, legacy 3D MeshRenderer path is preserved for comparison.
        /// Set before calling BuildWorld() / BuildPoliceStation().
        /// </summary>
        public bool Use2DBackend = true;

        // --- References (set at init) ---
        private GameObject _worldRoot;
        private OnlineMapService _mapService;
        private List<Rect> _solidObstacleRects;
        private List<Rect> _walkableRects;
        private List<TextMesh> _worldLabels;

        // --- Caches ---
        private readonly Dictionary<string, GameObject> _modelPrefabCache = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Material> _runtimeMeshMaterials = new Dictionary<string, Material>();

        // --- Runtime sprites ---
        private Sprite _roundedRectSprite;
        private Sprite _circleSprite;
        private Sprite _softCircleSprite;
        private Sprite _diamondSprite;
        private Sprite _capsuleSprite;

        // --- Public accessors ---
        public GameObject WorldRoot => _worldRoot;
        public OnlineMapService MapService => _mapService;
        public IReadOnlyList<Rect> SolidObstacleRects => _solidObstacleRects;
        public IReadOnlyList<Rect> WalkableRects => _walkableRects;
        public IReadOnlyList<TextMesh> WorldLabels => _worldLabels;
        public Sprite RoundedRectSprite => _roundedRectSprite;
        public Sprite CircleSprite => _circleSprite;
        public Sprite SoftCircleSprite => _softCircleSprite;
        public Sprite DiamondSprite => _diamondSprite;
        public int ModelPrefabCacheCount => _modelPrefabCache.Count;
        public int RuntimeMeshMaterialCount => _runtimeMeshMaterials.Count;

        public void Initialize(GameObject worldRoot, OnlineMapService mapService,
            List<Rect> solidObstacleRects, List<Rect> walkableRects, List<TextMesh> worldLabels)
        {
            _worldRoot = worldRoot;
            _mapService = mapService;
            _solidObstacleRects = solidObstacleRects;
            _walkableRects = walkableRects;
            _worldLabels = worldLabels;
        }

        // ====================================================================
        //  SPRITES
        // ====================================================================

        public void EnsureRuntimeSprites()
        {
            if (_roundedRectSprite != null && _circleSprite != null && _softCircleSprite != null &&
                _diamondSprite != null && _capsuleSprite != null)
                return;

            // E1: 使用美术增强版程序化 sprite 替代纯色矩形
            GanglandUndercover.Art.Sprite2DAssetCache.Ensure();

            _roundedRectSprite = GanglandUndercover.Art.Sprite2DAssetCache.WallBlock;
            _circleSprite       = GanglandUndercover.Art.Sprite2DAssetCache.FloorTile;
            _softCircleSprite   = GanglandUndercover.Art.Sprite2DAssetCache.TaskGlow;
            _diamondSprite      = GanglandUndercover.Art.Sprite2DAssetCache.CharDirectionArrow;
            _capsuleSprite      = GanglandUndercover.Art.Sprite2DAssetCache.CorridorTile;
        }

        private GameObject CreateSpriteObject(string objectName, Sprite sprite, Color color)
        {
            EnsureRuntimeSprites();
            GameObject spriteObject = new GameObject(objectName);
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : _roundedRectSprite;
            renderer.color = color;
            renderer.sortingOrder = SortingOrderForZ(spriteObject.transform.position.z);
            return spriteObject;
        }

        public static Sprite CreateRoundedRectSprite(string spriteName, int size, int radius)
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

        public static Sprite CreateCircleSprite(string spriteName, int size, bool softEdge)
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
                    float alpha = softEdge
                        ? Mathf.Clamp01((radius - distance) / (radius * 0.18f))
                        : distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public static Sprite CreateDiamondSprite(string spriteName, int size)
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

        // ====================================================================
        //  PRIMITIVE BUILDING BLOCKS
        // ====================================================================

        public GameObject CreatePrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, _circleSprite, color)
                : CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color)
        {
            GameObject prop = CreatePrimitiveProp(propName, primitiveType, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale,
                primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere);
            return prop;
        }

        public GameObject CreateProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidProp(string propName, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        public GameObject CreateShapeProp(string propName, Sprite sprite, Vector3 position, Vector3 scale, Color color)
        {
            GameObject prop = CreateSpriteObject(propName, sprite, color);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateRotatedProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees)
        {
            GameObject prop = CreateProp(propName, position, scale, color);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreateRotatedProp(propName, position, scale, color, rotationDegrees);

            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            prop.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSolidMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreateSolidProp(propName, position, scale, color);

            GameObject prop = CreateMeshBoxProp(propName, position, scale, color, rotationDegrees);
            RegisterSolidObstacle(position, scale);
            AttachPhysicsCollider(prop, scale, false);
            return prop;
        }

        public GameObject CreateMeshBoxChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, float rotationDegrees = 0f)
        {
            // M3: Delegate to sprite child path when 2D backend is active
            if (Use2DBackend)
                return CreatePropChild(parent, propName, localPosition, scale, color, PrimitiveType.Cube);

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

        public GameObject CreateMeshPrimitiveChild(Transform parent, string propName, PrimitiveType primitiveType,
            Vector3 localPosition, Vector3 scale, Color color, Quaternion localRotation)
        {
            // M3: Delegate to sprite child path when 2D backend is active
            if (Use2DBackend)
                return CreatePropChild(parent, propName, localPosition, scale, color, primitiveType);

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

        public GameObject CreateMeshPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color, Quaternion rotation)
        {
            // M3: Delegate to sprite path when 2D backend is active
            if (Use2DBackend)
                return CreatePrimitiveProp(propName, primitiveType, position, scale, color);

            GameObject prop = GameObject.CreatePrimitive(primitiveType);
            prop.name = propName;
            Remove3DCollider(prop);
            prop.transform.SetParent(_worldRoot.transform, false);
            prop.transform.position = _mapService.ScaleMapPosition(position);
            prop.transform.localScale = _mapService.ScaleMapSize(scale);
            prop.transform.rotation = rotation;
            ConfigureRuntimeMesh(prop, color);
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreatePropChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, PrimitiveType primitiveType)
        {
            GameObject prop = primitiveType == PrimitiveType.Cylinder || primitiveType == PrimitiveType.Sphere
                ? CreateSpriteObject(propName, _circleSprite, color)
                : CreateSpriteObject(propName, _roundedRectSprite, color);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPosition;
            prop.transform.localScale = scale;
            SetSortingFromZ(prop);
            return prop;
        }

        public GameObject CreateSpriteChild(Transform parent, string objectName, Sprite sprite, Vector3 localPosition,
            Vector3 scale, Color color)
        {
            GameObject child = CreateSpriteObject(objectName, sprite, color);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = scale;
            SetSortingFromZ(child);
            return child;
        }

        // ====================================================================
        //  3D HELPER (shared)
        // ====================================================================

        public static void Remove3DCollider(GameObject prop)
        {
            Collider collider = prop.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) UnityEngine.Object.DestroyImmediate(collider);
            else UnityEngine.Object.DestroyImmediate(collider);
        }

        public void ConfigureRuntimeMesh(GameObject prop, Color color)
        {
            Renderer renderer = prop.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = RuntimeMeshMaterial(color);
        }

        public Material RuntimeMeshMaterial(Color color)
        {
            string key = Mathf.RoundToInt(color.r * 255f) + "-" +
                         Mathf.RoundToInt(color.g * 255f) + "-" +
                         Mathf.RoundToInt(color.b * 255f) + "-" +
                         Mathf.RoundToInt(color.a * 255f);
            if (_runtimeMeshMaterials.TryGetValue(key, out Material cached)) return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ??
                            Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (color.a < 0.99f) ConfigureTransparentMaterial(material);

            _runtimeMeshMaterials[key] = material;
            return material;
        }

        public static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null) return;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        }

        // ====================================================================
        //  COLLISION
        // ====================================================================

        public void RegisterSolidObstacle(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPos = _mapService.ScaleMapPosition(position);
            Vector3 scaledScale = _mapService.ScaleMapSize(scale);
            float w = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float h = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            _solidObstacleRects.Add(new Rect(scaledPos.x - w * 0.5f, scaledPos.y - h * 0.5f, w, h));
        }

        public void RegisterWalkableArea(Vector3 position, Vector3 scale)
        {
            Vector3 scaledPos = _mapService.ScaleMapPosition(position);
            Vector3 scaledScale = _mapService.ScaleMapSize(scale);
            float w = Mathf.Max(0.01f, Mathf.Abs(scaledScale.x));
            float h = Mathf.Max(0.01f, Mathf.Abs(scaledScale.y));
            _walkableRects.Add(new Rect(scaledPos.x - w * 0.5f, scaledPos.y - h * 0.5f, w, h));
        }

        public static void AttachPhysicsCollider(GameObject prop, Vector3 designScale, bool round)
        {
            if (prop == null) return;
            Remove3DCollider(prop);
            Rigidbody2D body = prop.GetComponent<Rigidbody2D>();
            if (body == null) body = prop.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.simulated = true;

            if (round)
            {
                CircleCollider2D circle = prop.GetComponent<CircleCollider2D>();
                if (circle == null) circle = prop.AddComponent<CircleCollider2D>();
                circle.radius = 0.5f;
                circle.isTrigger = false;
                return;
            }

            BoxCollider2D box = prop.GetComponent<BoxCollider2D>();
            if (box == null) box = prop.AddComponent<BoxCollider2D>();
            float width = Mathf.Abs(designScale.x) <= 0.18f ? 0.82f : 1f;
            float height = Mathf.Abs(designScale.y) <= 0.18f ? 0.82f : 1f;
            box.size = new Vector2(width, height);
            box.isTrigger = false;
        }

        // ====================================================================
        //  COLOR HELPERS
        // ====================================================================

        public static Color Darken(Color color, float multiplier)
        {
            return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
        }

        public static Color FallbackColorForModel(string relativeFbxPath)
        {
            if (relativeFbxPath.IndexOf("Light", StringComparison.Ordinal) >= 0)
                return new Color(0.08f, 0.78f, 0.92f, 1f);
            if (relativeFbxPath.IndexOf("Crate", StringComparison.Ordinal) >= 0 ||
                relativeFbxPath.IndexOf("Chest", StringComparison.Ordinal) >= 0)
                return new Color(0.72f, 0.52f, 0.14f, 1f);
            if (relativeFbxPath.IndexOf("Door", StringComparison.Ordinal) >= 0)
                return new Color(0.42f, 0.48f, 0.5f, 1f);
            if (relativeFbxPath.IndexOf("Computer", StringComparison.Ordinal) >= 0 ||
                relativeFbxPath.IndexOf("AccessPoint", StringComparison.Ordinal) >= 0)
                return new Color(0.08f, 0.32f, 0.42f, 1f);
            return new Color(0.24f, 0.28f, 0.3f, 1f);
        }

        // ====================================================================
        //  MODEL LOADING
        // ====================================================================

        private const string QuaterniusFbxRoot = "Assets/_Project/Art/ThirdParty/Quaternius/ModularSciFiMegaKit/FBX/";
        private const string RuntimeResourcesRoot = "Assets/_Project/Resources/";

        public GameObject CreateAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true)
        {
            // M3: Skip 3D model loading entirely when 2D backend is active
            if (Use2DBackend)
                return CreateModelFallbackProp(propName + " (2D)", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            GameObject prefab = LoadResourcePrefab(resourcePath);
            if (prefab == null || _worldRoot == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            GameObject model = InstantiateModelPrefab(prefab);
            if (model == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(resourcePath));

            model.name = propName;
            model.transform.SetParent(_worldRoot.transform, false);
            model.transform.position = _mapService.ScaleMapPosition(position);
            model.transform.rotation =
                Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, _mapService.ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, preserveMaterials);
            SetSortingFromZ(model);
            return model;
        }

        public GameObject CreateSolidAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true)
        {
            GameObject model = CreateAssetStoreProp(propName, resourcePath, position, footprint, rotationDegrees,
                stretchToFootprint, preserveMaterials);
            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        public GameObject CreateModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint,
            float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            // M3: Skip 3D model loading entirely when 2D backend is active
            if (Use2DBackend)
                return CreateModelFallbackProp(propName + " (2D)", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            GameObject prefab = LoadQuaterniusModel(relativeFbxPath);
            if (prefab == null || _worldRoot == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            GameObject model = InstantiateModelPrefab(prefab);
            if (model == null)
                return CreateModelFallbackProp(propName + " Fallback", position, footprint, rotationDegrees,
                    FallbackColorForModel(relativeFbxPath));

            model.name = propName;
            model.transform.SetParent(_worldRoot.transform, false);
            model.transform.position = _mapService.ScaleMapPosition(position);
            model.transform.rotation =
                Quaternion.Euler(0f, 0f, rotationDegrees) * Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            FitModelToFootprint(model, _mapService.ScaleMapPosition(position), footprint, stretchToFootprint);
            ConfigureModelRenderers(model, false);
            SetSortingFromZ(model);
            return model;
        }

        public GameObject CreateSolidModelProp(string propName, string relativeFbxPath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false)
        {
            GameObject model = CreateModelProp(propName, relativeFbxPath, position, footprint, rotationDegrees,
                stretchToFootprint);
            if (model != null)
            {
                RegisterSolidObstacle(position, footprint);
                AttachPhysicsCollider(model, footprint, false);
            }

            return model;
        }

        public void CreateWallModelOverlay(string wallName, Vector3 position, Vector3 scale)
        {
            if (Mathf.Abs(scale.x) < 0.2f && Mathf.Abs(scale.y) < 0.2f) return;
            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string modelPath = horizontal ? "Walls/ShortWall_Metal2_Straight.fbx" : "Walls/WallAstra_Straight.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = new Vector3(Mathf.Max(Mathf.Abs(scale.x), 0.18f), Mathf.Max(Mathf.Abs(scale.y), 0.18f),
                Mathf.Max(Mathf.Abs(scale.z), 0.16f));
            CreateModelProp(wallName + " CC0 Wall Module", modelPath,
                position + new Vector3(0f, 0f, 0.08f), footprint, rotation, true);
        }

        public void CreateDoorModelOverlay(string markerName, Vector3 position, Vector3 scale)
        {
            bool horizontal = Mathf.Abs(scale.x) >= Mathf.Abs(scale.y);
            string doorPath = markerName.Contains("黑市") || markerName.Contains("维修") || markerName.Contains("暗")
                ? "Platforms/Door_DarkMetal.fbx"
                : "Platforms/Door_Frame_A.fbx";
            float rotation = horizontal ? 0f : 90f;
            Vector3 footprint = horizontal
                ? new Vector3(Mathf.Max(0.72f, Mathf.Abs(scale.x)), 0.34f, 0.32f)
                : new Vector3(0.34f, Mathf.Max(0.72f, Mathf.Abs(scale.y)), 0.32f);
            CreateModelProp(markerName + " CC0 Door Frame", doorPath, position + new Vector3(0f, 0f, 0.1f), footprint,
                rotation, true);
        }

        public GameObject CreateModelFallbackProp(string propName, Vector3 position, Vector3 footprint,
            float rotationDegrees, Color color)
        {
            // M3: Use simple 2D sprite fallback instead of 3D mesh stack
            if (Use2DBackend)
                return CreateRotatedProp(propName, position, footprint, color, rotationDegrees);

            GameObject fallback = new GameObject(propName);
            fallback.transform.SetParent(_worldRoot.transform, false);
            fallback.transform.position = _mapService.ScaleMapPosition(position);
            fallback.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
            Vector3 size = _mapService.ScaleMapSize(footprint);
            float width = Mathf.Max(0.08f, Mathf.Abs(size.x));
            float depth = Mathf.Max(0.08f, Mathf.Abs(size.y));
            float height = Mathf.Max(0.08f, Mathf.Abs(footprint.z));

            CreateMeshBoxChild(fallback.transform, "Fallback Base", new Vector3(0f, 0f, height * 0.35f),
                new Vector3(width, depth, height * 0.7f), Darken(color, 0.8f));
            CreateMeshBoxChild(fallback.transform, "Fallback Face",
                new Vector3(0f, depth * 0.38f, height * 0.76f),
                new Vector3(width * 0.72f, Mathf.Max(0.025f, depth * 0.08f), height * 0.26f), color);

            if (footprint.z > 0.18f)
            {
                CreateMeshBoxChild(fallback.transform, "Fallback Light Strip",
                    new Vector3(0f, -depth * 0.38f, height * 1.02f),
                    new Vector3(width * 0.52f, Mathf.Max(0.02f, depth * 0.08f), height * 0.12f),
                    new Color(0.08f, 0.78f, 0.92f, 1f));
            }

            SetSortingFromZ(fallback);
            return fallback;
        }

        public GameObject LoadQuaterniusModel(string relativeFbxPath)
        {
            string cacheKey = "Quaternius/" + relativeFbxPath;
            if (_modelPrefabCache.TryGetValue(cacheKey, out GameObject cached)) return cached;

            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusFbxRoot + relativeFbxPath);
#endif
            if (prefab == null)
            {
                string resourcePath = "Quaternius/ModularSciFiMegaKit/FBX/" +
                                      relativeFbxPath.Replace(".fbx", string.Empty);
                prefab = Resources.Load<GameObject>(resourcePath);
            }

            _modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        public GameObject LoadResourcePrefab(string resourcePath)
        {
            string normalized = NormalizeResourcePath(resourcePath);
            string cacheKey = "Resource/" + normalized;
            if (_modelPrefabCache.TryGetValue(cacheKey, out GameObject cached)) return cached;

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
            if (prefab == null) prefab = Resources.Load<GameObject>(normalized);

            _modelPrefabCache[cacheKey] = prefab;
            return prefab;
        }

        public static string NormalizeResourcePath(string resourcePath)
        {
            string normalized = resourcePath.Replace('\\', '/').Trim();
            if (normalized.StartsWith("Assets/_Project/Resources/", StringComparison.Ordinal))
                normalized = normalized.Substring("Assets/_Project/Resources/".Length);
            if (normalized.StartsWith("/", StringComparison.Ordinal)) normalized = normalized.Substring(1);
            string extension = System.IO.Path.GetExtension(normalized);
            if (!string.IsNullOrEmpty(extension))
                normalized = normalized.Substring(0, normalized.Length - extension.Length);
            return normalized;
        }

        public static GameObject InstantiateModelPrefab(GameObject prefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
#endif
            return UnityEngine.Object.Instantiate(prefab);
        }

        public void FitModelToFootprint(GameObject model, Vector3 targetPosition, Vector3 footprint,
            bool stretchToFootprint)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds)) return;
            Vector3 desired = _mapService.ScaleMapSize(footprint);
            float desiredX = Mathf.Max(0.04f, Mathf.Abs(desired.x));
            float desiredY = Mathf.Max(0.04f, Mathf.Abs(desired.y));
            float desiredZ = Mathf.Max(0.05f, Mathf.Abs(footprint.z));

            if (stretchToFootprint)
            {
                Vector3 ls = model.transform.localScale;
                float xF = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yF = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float zF = bounds.size.z > 0.001f ? desiredZ / bounds.size.z : Mathf.Min(xF, yF);
                model.transform.localScale = new Vector3(ls.x * xF, ls.y * zF, ls.z * yF);
            }
            else
            {
                float xF = bounds.size.x > 0.001f ? desiredX / bounds.size.x : 1f;
                float yF = bounds.size.y > 0.001f ? desiredY / bounds.size.y : 1f;
                float factor = Mathf.Clamp(Mathf.Min(xF, yF), 0.02f, 3.0f);
                model.transform.localScale *= factor;
            }
        }

        public static void AlignModelBounds(GameObject model, Vector3 targetPosition)
        {
            if (!TryGetRendererBounds(model, out Bounds bounds)) return;
            Vector3 offset = new Vector3(targetPosition.x - bounds.center.x, targetPosition.y - bounds.center.y,
                targetPosition.z - bounds.min.z);
            model.transform.position += offset;
        }

        public static bool TryGetRendererBounds(GameObject model, out Bounds bounds)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(model.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            return hasBounds;
        }

        public static void ConfigureModelRenderers(GameObject model, bool preserveMaterials)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer.sharedMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    renderer.sharedMaterial = new Material(shader);
                }

                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (material == null) continue;
                if (preserveMaterials)
                {
                    Color sourceColor = ReadMaterialColor(material, Color.white);
                    if (material.HasProperty("_BaseColor"))
                    {
                        Color bc = material.GetColor("_BaseColor");
                        material.SetColor("_BaseColor",
                            new Color(Mathf.Clamp01(bc.r * 1.04f), Mathf.Clamp01(bc.g * 1.04f),
                                Mathf.Clamp01(bc.b * 1.04f), bc.a));
                    }

                    SetMaterialColor(material,
                        new Color(Mathf.Clamp01(sourceColor.r * 1.04f), Mathf.Clamp01(sourceColor.g * 1.04f),
                            Mathf.Clamp01(sourceColor.b * 1.04f), sourceColor.a));
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color color = ReadMaterialColor(material, Color.white);
                    material.SetColor("_BaseColor",
                        new Color(Mathf.Clamp01(color.r * 1.1f), Mathf.Clamp01(color.g * 1.1f),
                            Mathf.Clamp01(color.b * 1.1f), color.a));
                }

                SetMaterialColor(material, new Color(0.92f, 0.94f, 0.98f, 1f));
            }
        }

        public static Color ReadMaterialColor(Material material, Color fallback)
        {
            if (material == null) return fallback;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return fallback;
        }

        public static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        // ====================================================================
        //  TASK VISUALS
        // ====================================================================

        /// <summary>RGB accent colour for each task-type template.</summary>
        public static Color TaskPanelAccent(int taskId)
        {
            int mode = taskId % 7;
            switch (mode)
            {
                case 0: return new Color(0.08f, 0.58f, 0.92f, 1f);
                case 1: return new Color(0.92f, 0.22f, 0.12f, 1f);
                case 2: return new Color(0.88f, 0.72f, 0.08f, 1f);
                case 3: return new Color(0.18f, 0.78f, 0.32f, 1f);
                case 4: return new Color(0.72f, 0.18f, 0.84f, 1f);
                case 5: return new Color(0.95f, 0.48f, 0.08f, 1f);
                default: return new Color(0.28f, 0.62f, 0.88f, 1f);
            }
        }

        public GameObject CreateTaskVisual(OnlineTaskState task, Transform parent)
        {
            if (parent == null) return null;
            string label = string.IsNullOrWhiteSpace(task.Name) ? TaskNameFor(task.Id) : task.Name;
            Sprite sprite = TaskVisualSprite(task.Id);
            Color color = TaskPanelAccent(task.Id);
            Vector3 pos = _mapService.ScaleMapPosition(task.Position);
            Vector3 scale = TaskScale(task.Id);

            GameObject root = new GameObject("任务视觉 " + label + " #" + task.Id);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.localScale = scale;

            GameObject body = CreateSpriteObject("任务底座", sprite, color);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one;
            SetSortingFromZ(root);

            CreateWorldLabel(root.transform, label, new Vector3(0f, 0.3f, 0.02f), 0.08f);
            CreateTaskEquipment(root, task);

            // M3: Interactive highlight halo — expands when player is near
            GameObject halo = CreateSpriteObject("交互光晕", _softCircleSprite, new Color(color.r, color.g, color.b, 0.12f));
            halo.transform.SetParent(root.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
            SpriteRenderer haloRenderer = halo.GetComponent<SpriteRenderer>();
            if (haloRenderer != null) haloRenderer.sortingOrder = -1; // Behind task body

            return root;
        }

        public void SetTaskVisualState(GameObject visual, OnlineTaskState task)
        {
            if (visual == null) return;

            Color color = task.Completed
                ? new Color(0.12f, 0.38f, 0.14f, 0.7f)
                : task.Sabotaged
                    ? new Color(0.55f, 0.08f, 0.06f, 0.9f)
                    : TaskPanelAccent(task.Id);

            SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer r in renderers)
            {
                r.color = new Color(color.r, color.g, color.b, r.color.a);
            }

            Transform halo = visual.transform.Find("交互光晕");
            if (halo != null) halo.gameObject.SetActive(!task.Completed && !task.Sabotaged);

            Transform damaged = visual.transform.Find("破坏标记");
            if (damaged != null) damaged.gameObject.SetActive(task.Sabotaged);
        }

        public static Sprite TaskVisualSprite(int taskId)
        {
            // Placeholder: returns null so CreateSpriteObject will use default roundedRectSprite.
            // M3 2D rendering switch will provide real per-task sprites here.
            return null;
        }

        private void CreateTaskEquipment(GameObject root, OnlineTaskState task)
        {
            if (root == null) return;
            CreateTaskEquipment(root.transform, task.Id);
        }

        public void CreateTaskEquipment(Transform parent, int taskId)
        {
            if (parent == null) return;
            Color accent = TaskPanelAccent(taskId);
            int mode = taskId % 7;

            switch (mode)
            {
                case 0: // Wire / panel
                    CreatePropChild(parent, "终端面板", new Vector3(0f, 0.06f, 0.04f),
                        new Vector3(0.28f, 0.14f, 0.08f), Darken(accent, 0.7f), PrimitiveType.Cube);
                    CreatePropChild(parent, "终端屏幕", new Vector3(0f, 0.08f, 0.06f),
                        new Vector3(0.18f, 0.08f, 0.04f), accent, PrimitiveType.Cube);
                    CreatePropChild(parent, "线缆L", new Vector3(-0.1f, -0.06f, 0.02f),
                        new Vector3(0.02f, 0.1f, 0.02f), new Color(0.82f, 0.22f, 0.22f, 1f), PrimitiveType.Cylinder);
                    CreatePropChild(parent, "线缆R", new Vector3(0.1f, -0.06f, 0.02f),
                        new Vector3(0.02f, 0.1f, 0.02f), new Color(0.22f, 0.22f, 0.82f, 1f), PrimitiveType.Cylinder);
                    break;
                case 1: // Keypad
                    CreatePropChild(parent, "键盘基座", new Vector3(0f, 0.02f, 0.04f),
                        new Vector3(0.22f, 0.12f, 0.06f), Darken(accent, 0.6f), PrimitiveType.Cube);
                    for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        CreatePropChild(parent, "键" + r + c,
                            new Vector3(-0.06f + c * 0.06f, 0.06f - r * 0.04f, 0.06f),
                            new Vector3(0.03f, 0.02f, 0.02f), accent, PrimitiveType.Cube);
                    break;
                case 2: // Scanner
                    CreatePropChild(parent, "扫描台", new Vector3(0f, 0.02f, 0.05f),
                        new Vector3(0.28f, 0.08f, 0.1f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "扫描线", new Vector3(0f, 0.06f, 0.08f),
                        new Vector3(0.04f, 0.04f, 0.02f), new Color(accent.r, accent.g, accent.b, 0.6f),
                        PrimitiveType.Cylinder);
                    break;
                case 3: // Download / screen
                    CreatePropChild(parent, "显示器基座", new Vector3(0f, 0f, 0.04f),
                        new Vector3(0.12f, 0.06f, 0.08f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "显示器屏幕", new Vector3(0f, 0.02f, 0.08f),
                        new Vector3(0.18f, 0.1f, 0.02f), accent, PrimitiveType.Cube);
                    break;
                case 4: // Memory / card
                    CreatePropChild(parent, "读卡器", new Vector3(0f, 0.02f, 0.05f),
                        new Vector3(0.1f, 0.06f, 0.04f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "卡片", new Vector3(0f, 0.06f, 0.06f),
                        new Vector3(0.06f, 0.04f, 0.01f), accent, PrimitiveType.Cube);
                    break;
                case 5: // Breaker / switch
                    CreatePropChild(parent, "开关面板", new Vector3(0f, 0.04f, 0.04f),
                        new Vector3(0.14f, 0.08f, 0.04f), Darken(accent, 0.6f), PrimitiveType.Cube);
                    CreatePropChild(parent, "开关杆", new Vector3(0f, 0.06f, 0.06f),
                        new Vector3(0.02f, 0.08f, 0.02f), accent, PrimitiveType.Cylinder);
                    break;
                default: // Evidence tray
                    CreatePropChild(parent, "证物盘", new Vector3(0f, 0.02f, 0.04f),
                        new Vector3(0.16f, 0.1f, 0.06f), Darken(accent, 0.5f), PrimitiveType.Cube);
                    CreatePropChild(parent, "证物袋", new Vector3(0f, 0.04f, 0.05f),
                        new Vector3(0.08f, 0.06f, 0.03f), accent, PrimitiveType.Cube);
                    break;
            }
        }

        // ====================================================================
        //  PLAYER & BODY VISUALS
        // ====================================================================

        public GameObject CreatePlayerVisual(OnlinePlayerState state, bool isLocal)
        {
            string name = "玩家 " + state.DisplayName + " cid" + state.ClientId;
            GameObject root = new GameObject(name);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = _mapService.ScaleMapPosition(state.Position);
            root.transform.localScale = Vector3.one * (state.Alive ? 1.12f : 1.04f);

            // Base body
            CreateSpriteChild(root.transform, "角色基座", _roundedRectSprite, Vector3.zero,
                new Vector3(0.28f, 0.24f, 0.12f), PlayerColor(state, isLocal));

            // Coat / uniform
            CreateSpriteChild(root.transform, "警服外套", _roundedRectSprite, new Vector3(0f, 0.04f, 0.02f),
                new Vector3(0.22f, 0.18f, 0.04f), Darken(PlayerColor(state, isLocal), 0.8f));

            // Helmet
            CreateSpriteChild(root.transform, "头盔", _circleSprite, new Vector3(0f, 0.12f, 0.04f),
                new Vector3(0.12f, 0.12f, 0.04f), PlayerAccentColor(state));

            CreateStageTwoCharacterRig(root, state);
            CreateStageTwoCharacterStateLayer(root);
            CreateProfessionAccessory(root, state);

            // Local ring
            CreateSpriteChild(root.transform, "本地指示圈", _circleSprite, new Vector3(0f, -0.04f, 0.01f),
                new Vector3(0.36f, 0.36f, 0.02f), new Color(0.08f, 0.72f, 0.95f, 0.62f));
            SetChildActive(root, "本地指示圈", isLocal && state.Alive);

            SetSortingFromZ(root);
            return root;
        }

        public GameObject CreateBodyVisual(OnlineBodyState body)
        {
            GameObject root = new GameObject("尸体 cid" + body.VictimClientId + " bid" + body.Id);
            root.transform.SetParent(_worldRoot.transform, false);
            root.transform.position = _mapService.ScaleMapPosition(body.Position);
            root.transform.localScale = new Vector3(1.04f, 0.52f, 0.08f);

            CreateSpriteChild(root.transform, "尸体轮廓", _roundedRectSprite, Vector3.zero,
                new Vector3(0.26f, 0.16f, 0.06f), new Color(0.72f, 0.08f, 0.06f, 0.8f));

            CreateSpriteChild(root.transform, "尸体标记", _diamondSprite, new Vector3(0f, 0.14f, 0.02f),
                new Vector3(0.08f, 0.08f, 0.02f), new Color(1f, 0.12f, 0.08f, 0.9f));

            CreateSpriteChild(root.transform, "Stage2 Downed body scene marker", _roundedRectSprite,
                new Vector3(0f, -0.02f, 0.03f), new Vector3(0.32f, 0.12f, 0.02f),
                new Color(1f, 1f, 1f, 0.34f));
            CreateSpriteChild(root.transform, "Stage2 Report body proximity prompt", _circleSprite,
                new Vector3(0f, 0.18f, 0.05f), new Vector3(0.16f, 0.16f, 0.02f),
                new Color(1f, 0.22f, 0.12f, 0.62f));
            CreateSpriteChild(root.transform, "Stage2 Forensic evidence tag", _diamondSprite,
                new Vector3(-0.12f, 0.08f, 0.04f), new Vector3(0.05f, 0.05f, 0.01f),
                new Color(1f, 0.86f, 0.18f, 0.9f));
            CreateSpriteChild(root.transform, "Stage2 Forensic chalk trace", _roundedRectSprite,
                new Vector3(0.1f, -0.07f, 0.04f), new Vector3(0.14f, 0.035f, 0.01f),
                new Color(1f, 1f, 1f, 0.42f));
            CreateSpriteChild(root.transform, "Stage2 Forensic sample vial", _circleSprite,
                new Vector3(0.15f, 0.08f, 0.04f), new Vector3(0.035f, 0.035f, 0.01f),
                new Color(0.16f, 0.72f, 0.95f, 0.86f));
            CreateSpriteChild(root.transform, "Stage2 Forensic scene boundary", _circleSprite,
                new Vector3(0f, 0f, 0.01f), new Vector3(0.46f, 0.32f, 0.01f),
                new Color(1f, 0.72f, 0.08f, 0.16f));

            SetSortingFromZ(root);
            return root;
        }

        public void CreateStageTwoCharacterRig(GameObject root)
        {
            if (root == null) return;
            StageTwoCharacterRig rig = root.GetComponent<StageTwoCharacterRig>();
            if (rig == null) rig = root.AddComponent<StageTwoCharacterRig>();
        }

        public void CreateStageTwoCharacterRig(GameObject root, OnlinePlayerState state)
        {
            if (root == null) return;

            StageTwoCharacterRig rig = root.GetComponent<StageTwoCharacterRig>();
            if (rig == null) rig = root.AddComponent<StageTwoCharacterRig>();

            rig.Configure("online-" + state.ClientId, "runtime/online-player/" + state.Profession);
            rig.BodyRoot = EnsureRigPart(root.transform, "BodyRoot", "警服外套",
                _roundedRectSprite, new Vector3(0f, 0.04f, 0.05f), new Vector3(0.22f, 0.18f, 0.04f),
                Darken(PlayerColor(state, false), 0.78f));
            rig.HeadRoot = EnsureRigPart(root.transform, "HeadRoot", "头盔",
                _circleSprite, new Vector3(0f, 0.2f, 0.07f), new Vector3(0.13f, 0.13f, 0.04f),
                PlayerAccentColor(state));
            rig.LeftArm = EnsureRigPart(root.transform, "LeftArm", null,
                _roundedRectSprite, new Vector3(-0.16f, 0.02f, 0.06f), new Vector3(0.05f, 0.14f, 0.03f),
                PlayerAccentColor(state));
            rig.RightArm = EnsureRigPart(root.transform, "RightArm", null,
                _roundedRectSprite, new Vector3(0.16f, 0.02f, 0.06f), new Vector3(0.05f, 0.14f, 0.03f),
                PlayerAccentColor(state));
            rig.LeftFoot = EnsureRigPart(root.transform, "LeftFoot", null,
                _circleSprite, new Vector3(-0.08f, -0.14f, 0.04f), new Vector3(0.05f, 0.04f, 0.02f),
                Darken(PlayerAccentColor(state), 0.62f));
            rig.RightFoot = EnsureRigPart(root.transform, "RightFoot", null,
                _circleSprite, new Vector3(0.08f, -0.14f, 0.04f), new Vector3(0.05f, 0.04f, 0.02f),
                Darken(PlayerAccentColor(state), 0.62f));
            rig.StateRoot = EnsureStateRoot(root.transform);
            rig.ApplyState(StageTwoCharacterVisualState.Idle);
        }

        private Transform EnsureRigPart(Transform parent, string rigName, string existingName, Sprite sprite,
            Vector3 localPosition, Vector3 scale, Color color)
        {
            Transform part = parent.Find(rigName);
            if (part == null && !string.IsNullOrEmpty(existingName))
            {
                part = parent.Find(existingName);
            }

            if (part == null)
            {
                part = CreateSpriteChild(parent, rigName, sprite, localPosition, scale, color).transform;
            }
            else
            {
                part.name = rigName;
            }

            return part;
        }

        private static Transform EnsureStateRoot(Transform parent)
        {
            Transform stateRoot = parent.Find("StateRoot");
            if (stateRoot != null)
            {
                return stateRoot;
            }

            GameObject stateRootObject = new GameObject("StateRoot");
            stateRootObject.transform.SetParent(parent, false);
            return stateRootObject.transform;
        }

        public void CreateStageTwoCharacterStateLayer(GameObject root)
        {
            if (root == null) return;
            CreateSpriteChild(root.transform, "Stage2 Character interaction radius", _circleSprite,
                new Vector3(0f, 0f, 0.02f), new Vector3(0.82f, 0.82f, 0.02f),
                new Color(0.2f, 0.8f, 0.2f, 0.15f));
            SetChildActive(root, "Stage2 Character interaction radius", false);

            CreateSpriteChild(root.transform, "Stage2 VoiceRadius action proximity", _circleSprite,
                new Vector3(0f, 0f, 0.01f), new Vector3(2.35f, 1.36f, 0.02f),
                new Color(0.3f, 0.3f, 0.9f, 0.08f));
            SetChildActive(root, "Stage2 VoiceRadius action proximity", false);

            CreateSpriteChild(root.transform, "Stage2 Downed chalk silhouette", _roundedRectSprite,
                new Vector3(0f, -0.02f, 0.01f), new Vector3(0.3f, 0.12f, 0.02f),
                new Color(1f, 1f, 1f, 0.25f));
            SetChildActive(root, "Stage2 Downed chalk silhouette", false);

            CreateSpriteChild(root.transform, "Stage2 Downed personal item", _circleSprite,
                new Vector3(0.06f, 0.02f, 0.02f), new Vector3(0.04f, 0.04f, 0.02f),
                new Color(0.8f, 0.3f, 0.1f, 0.6f));
            SetChildActive(root, "Stage2 Downed personal item", false);

            CreateSpriteChild(root.transform, "Stage2 Character facing wedge", _roundedRectSprite,
                new Vector3(0f, 0.12f, 0.03f), new Vector3(0.06f, 0.04f, 0.02f),
                new Color(1f, 1f, 1f, 0.3f));
            SetChildActive(root, "Stage2 Character facing wedge", true);

            CreateSpriteChild(root.transform, "Stage2 Character action hand prop", _circleSprite,
                new Vector3(0.08f, 0.04f, 0.04f), new Vector3(0.03f, 0.03f, 0.02f),
                new Color(0.9f, 0.7f, 0.1f, 0.8f));
            SetChildActive(root, "Stage2 Character action hand prop", false);

            CreateSpriteChild(root.transform, "Stage2 Character report beacon", _circleSprite,
                new Vector3(0f, 0.2f, 0.05f), new Vector3(0.08f, 0.08f, 0.02f),
                new Color(1f, 0.2f, 0.1f, 0.7f));
            SetChildActive(root, "Stage2 Character report beacon", false);

            CreateSpriteChild(root.transform, "Stage2 Report proximity ping", _circleSprite,
                new Vector3(0f, 0f, 0.03f), new Vector3(0.5f, 0.5f, 0.02f),
                new Color(1f, 0.2f, 0.1f, 0.12f));
            SetChildActive(root, "Stage2 Report proximity ping", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting seated pad", _roundedRectSprite,
                new Vector3(0f, 0.04f, 0.01f), new Vector3(0.2f, 0.14f, 0.02f),
                new Color(0.2f, 0.3f, 0.4f, 0.5f));
            SetChildActive(root, "Stage2 Meeting seated pad", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting vote tablet", _roundedRectSprite,
                new Vector3(0.06f, 0.08f, 0.03f), new Vector3(0.05f, 0.04f, 0.01f),
                new Color(0.1f, 0.6f, 0.9f, 0.8f));
            SetChildActive(root, "Stage2 Meeting vote tablet", false);

            CreateSpriteChild(root.transform, "Stage2 Meeting voice mic", _circleSprite,
                new Vector3(0.04f, 0.08f, 0.03f), new Vector3(0.02f, 0.02f, 0.01f),
                new Color(0.8f, 0.1f, 0.1f, 0.8f));
            SetChildActive(root, "Stage2 Meeting voice mic", false);

            CreateSpriteChild(root.transform, "Stage2 Vote locked marker", _circleSprite,
                new Vector3(0.08f, 0.08f, 0.04f), new Vector3(0.04f, 0.04f, 0.01f),
                new Color(1f, 0.6f, 0.1f, 0.9f));
            SetChildActive(root, "Stage2 Vote locked marker", false);

            CreateSpriteChild(root.transform, "Stage2 Character footstep L", _circleSprite,
                new Vector3(-0.06f, -0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.01f),
                new Color(0.3f, 0.3f, 0.3f, 0.3f));
            SetChildActive(root, "Stage2 Character footstep L", false);

            CreateSpriteChild(root.transform, "Stage2 Character footstep R", _circleSprite,
                new Vector3(0.06f, -0.12f, 0.01f), new Vector3(0.03f, 0.03f, 0.01f),
                new Color(0.3f, 0.3f, 0.3f, 0.3f));
            SetChildActive(root, "Stage2 Character footstep R", false);
        }

        public void CreateStageTwoCharacterStateLayer(Transform parent, OnlinePlayerState state)
        {
            CreateStageTwoCharacterStateLayer(parent == null ? null : parent.gameObject);
        }

        public void CreateProfessionAccessory(GameObject root, OnlinePlayerState state)
        {
            if (root == null) return;
            Color accent = PlayerAccentColor(state);

            switch (state.Profession)
            {
                case OnlineProfession.Inspector:
                    CreateSpriteChild(root.transform, "警徽", _diamondSprite, new Vector3(0.06f, 0.14f, 0.05f),
                        new Vector3(0.04f, 0.04f, 0.01f), new Color(1f, 0.84f, 0.1f, 1f));
                    break;
                case OnlineProfession.Forensics:
                    CreateSpriteChild(root.transform, "证物袋", _roundedRectSprite, new Vector3(0.08f, 0.06f, 0.04f),
                        new Vector3(0.04f, 0.03f, 0.01f), accent);
                    break;
                case OnlineProfession.Tech:
                    CreateSpriteChild(root.transform, "平板", _roundedRectSprite, new Vector3(0.08f, 0.06f, 0.04f),
                        new Vector3(0.05f, 0.03f, 0.01f), new Color(0.1f, 0.6f, 0.9f, 1f));
                    break;
                case OnlineProfession.Enforcer:
                    CreateSpriteChild(root.transform, "指虎", _circleSprite, new Vector3(0.08f, -0.04f, 0.04f),
                        new Vector3(0.03f, 0.03f, 0.01f), new Color(0.6f, 0.1f, 0.1f, 1f));
                    break;
                case OnlineProfession.Fixer:
                    CreateSpriteChild(root.transform, "工具包", _roundedRectSprite, new Vector3(0.08f, 0.04f, 0.04f),
                        new Vector3(0.04f, 0.03f, 0.01f), new Color(0.5f, 0.3f, 0.1f, 1f));
                    break;
                case OnlineProfession.UndercoverAgent:
                    CreateSpriteChild(root.transform, "线人笔记", _roundedRectSprite,
                        new Vector3(0.06f, 0.08f, 0.04f), new Vector3(0.03f, 0.04f, 0.01f),
                        new Color(0.4f, 0.2f, 0.6f, 1f));
                    break;
            }
        }

        public void CreateProfessionAccessory(Transform parent, OnlinePlayerState state)
        {
            CreateProfessionAccessory(parent == null ? null : parent.gameObject, state);
        }

        // ====================================================================
        //  PLAYER COLOR HELPERS
        // ====================================================================

        public static Color PlayerColor(OnlinePlayerState state, bool isLocal)
        {
            if (isLocal) return new Color(0.12f, 0.78f, 0.32f, 1f);
            return state.Profession switch
            {
                OnlineProfession.Inspector => new Color(0.15f, 0.35f, 0.7f, 1f),
                OnlineProfession.Forensics => new Color(0.1f, 0.5f, 0.5f, 1f),
                OnlineProfession.Tech => new Color(0.2f, 0.4f, 0.7f, 1f),
                OnlineProfession.Enforcer => new Color(0.7f, 0.15f, 0.15f, 1f),
                OnlineProfession.Fixer => new Color(0.6f, 0.4f, 0.15f, 1f),
                OnlineProfession.UndercoverAgent => new Color(0.5f, 0.3f, 0.6f, 1f),
                OnlineProfession.Driver => new Color(0.3f, 0.3f, 0.3f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        public static Color PlayerAccentColor(OnlinePlayerState state)
        {
            return state.Profession switch
            {
                OnlineProfession.Inspector => new Color(0.9f, 0.8f, 0.2f, 1f),
                OnlineProfession.Forensics => new Color(0.1f, 0.7f, 0.8f, 1f),
                OnlineProfession.Tech => new Color(0.2f, 0.6f, 0.9f, 1f),
                OnlineProfession.Enforcer => new Color(0.8f, 0.1f, 0.1f, 1f),
                OnlineProfession.Fixer => new Color(0.7f, 0.5f, 0.1f, 1f),
                OnlineProfession.UndercoverAgent => new Color(0.6f, 0.3f, 0.7f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        // ====================================================================
        //  LABELS
        // ====================================================================

        public TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize)
        {
            GameObject labelObject = new GameObject("Label " + text);
            labelObject.transform.SetParent(_worldRoot.transform, false);
            labelObject.transform.position = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = characterSize;
            label.fontSize = 48;
            label.color = new Color(0.72f, 0.78f, 0.72f, 0.96f);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = SortingOrderForZ(position.z) + 20;
            if (Camera.main != null) BillboardLabel(labelObject.transform);
            _worldLabels.Add(label);
            return label;
        }

        public TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize)
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
            if (renderer != null) renderer.sortingOrder = 900;
            if (Camera.main != null) BillboardLabel(labelObject.transform);
            return label;
        }

        public static void BillboardLabel(Transform labelTransform)
        {
            if (labelTransform == null || Camera.main == null) return;
            Vector3 direction = Camera.main.transform.position - labelTransform.position;
            if (direction.sqrMagnitude <= 0.0001f) return;
            labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Camera.main.transform.up);
        }

        public string BuildPlayerWorldLabel(OnlinePlayerState state, bool isLocal)
        {
            if (!state.Alive) return "出局";
            if (isLocal) return "你\n" + ProfessionName(state.Profession);
            return state.DisplayName.Length > 4 ? state.DisplayName.Substring(0, 4) : state.DisplayName;
        }

        public bool ShouldShowPlayerWorldLabel(OnlinePlayerState state, bool isLocal, OnlineMatchPhase phase,
            bool tacticalMapOpen)
        {
            if (phase == OnlineMatchPhase.Action)
            {
                if (tacticalMapOpen) return true;
                return isLocal || !state.Alive;
            }

            return true;
        }

        private static string ProfessionName(OnlineProfession p)
        {
            return p switch
            {
                OnlineProfession.Inspector => "督察",
                OnlineProfession.Forensics => "法证",
                OnlineProfession.Tech => "技术",
                OnlineProfession.Enforcer => "打手",
                OnlineProfession.Fixer => "善后",
                OnlineProfession.UndercoverAgent => "卧底",
                OnlineProfession.Driver => "车手",
                _ => "未知"
            };
        }

        // ====================================================================
        //  SORTING
        // ====================================================================

        public static void SetSortingFromZ(GameObject target)
        {
            int sortingOrder = SortingOrderForZ(target.transform.position.z);
            foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = sortingOrder;
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
                if (renderer is not SpriteRenderer)
                    renderer.sortingOrder = sortingOrder;
        }

        public static int SortingOrderForZ(float z)
        {
            return Mathf.RoundToInt(-z * 1000f);
        }

        public static int SortingOrderForLocalZ(float localZ)
        {
            return Mathf.RoundToInt(-localZ * 1000f);
        }

        // ====================================================================
        //  SHARED HELPERS
        // ====================================================================

        public static void SetTextMeshVisible(TextMesh label, bool visible)
        {
            MeshRenderer renderer = label == null ? null : label.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = visible;
        }

        public static void SetColor(GameObject target, Color color)
        {
            SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) { spriteRenderer.color = color; return; }

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
                if (material == null)
                {
                    material = new Material(
                        Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    if (Application.isPlaying) renderer.material = material;
                    else renderer.sharedMaterial = material;
                }

                material.color = color;
            }
        }

        public static void SetPlayerVisualColors(GameObject visual, OnlinePlayerState state, bool isLocal)
        {
            SpriteRenderer rootRenderer = visual.GetComponent<SpriteRenderer>();
            if (rootRenderer != null) rootRenderer.color = new Color(1f, 1f, 1f, 0f);

            Transform coat = visual.transform.Find("Coat");
            if (coat != null) SetColor(coat.gameObject, PlayerColor(state, isLocal));
            Transform bodyVol = visual.transform.Find("Body Volume");
            if (bodyVol != null) SetColor(bodyVol.gameObject, PlayerColor(state, isLocal));
            Transform helmet = visual.transform.Find("Helmet Volume");
            if (helmet != null) SetColor(helmet.gameObject, PlayerColor(state, isLocal));
            Transform pack = visual.transform.Find("Pack Volume");
            Transform localRing = visual.transform.Find("Local Ring");
            Transform localArrow = visual.transform.Find("Local Arrow");

            Color accent = PlayerAccentColor(state);
            Transform torso = visual.transform.Find("Torso");
            Transform armL = visual.transform.Find("Arm L");
            Transform armR = visual.transform.Find("Arm R");
            if (torso != null) SetColor(torso.gameObject, accent);
            if (armL != null) SetColor(armL.gameObject, accent);
            if (armR != null) SetColor(armR.gameObject, accent);
            if (pack != null) SetColor(pack.gameObject, Darken(accent, 0.7f));
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

        public static Transform FindChildTransform(Transform root, params string[] names)
        {
            if (root == null || names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = root.Find(names[i]);
                if (found != null) return found;
            }

            return null;
        }

        public static void SetChildActive(GameObject root, string childName, bool active)
        {
            Transform child = root == null ? null : root.transform.Find(childName);
            if (child != null && child.gameObject.activeSelf != active) child.gameObject.SetActive(active);
        }

        // ====================================================================
        //  TASK SCALE / NAMES
        // ====================================================================

        public static Vector3 TaskScale(int taskId)
        {
            return (taskId % 7) switch
            {
                0 => new Vector3(0.42f, 0.24f, 0.22f),
                1 => new Vector3(0.32f, 0.26f, 0.18f),
                2 => new Vector3(0.46f, 0.18f, 0.24f),
                3 => new Vector3(0.38f, 0.3f, 0.16f),
                4 => new Vector3(0.28f, 0.2f, 0.2f),
                5 => new Vector3(0.34f, 0.22f, 0.18f),
                _ => new Vector3(0.36f, 0.24f, 0.2f),
            };
        }

        public static string TaskNameFor(int taskId)
        {
            return (taskId % 7) switch
            {
                0 => "线缆拼接",
                1 => "密码键盘",
                2 => "扫描验证",
                3 => "数据下载",
                4 => "记忆卡牌",
                5 => "断路器",
                _ => "证物整理",
            };
        }

        public static string TaskDistrictName(int taskId)
        {
            return (taskId / 7) switch
            {
                0 => "码头区",
                1 => "海关楼",
                2 => "监控室",
                3 => "茶餐厅",
                4 => "夜市",
                5 => "财务室",
                6 => "电力房",
                7 => "天台",
                8 => "指挥部",
                9 => "证物室",
                10 => "后巷",
                11 => "诊所",
                _ => "港区",
            };
        }

        // ====================================================================
        //  NEON LIGHT & SCENE LIGHTING
        // ====================================================================

        public void CreateNeonLight(string lightName, Vector3 position, Color color, float intensity, float range)
        {
            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(_worldRoot.transform, false);
            lightObject.transform.position = _mapService.ScaleMapPosition(position);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        public void ConfigureSceneLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.68f, 1f);

            GameObject lightObject = new GameObject("CC0 Model Fill Light");
            lightObject.transform.SetParent(_worldRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.8f, 0.88f, 1f, 1f);
        }

        public void CreateEmergencyBell()
        {
            GameObject bell =
                CreateSpriteObject("紧急铃", _circleSprite, new Color(0.72f, 0.08f, 0.06f, 1f));
            bell.transform.SetParent(_worldRoot.transform, false);
            bell.transform.position = _mapService.ScaleMapPosition(new Vector3(0f, 0f, 0.12f));
            bell.transform.localScale = new Vector3(0.58f, 0.58f, 0.34f);
            SetSortingFromZ(bell);

            // M4.3: 附挂 EmergencyButton 组件
            EmergencyButton emergencyBtn = bell.AddComponent<EmergencyButton>();
            emergencyBtn.InteractionRadius = 0.85f;

            CreatePropChild(bell.transform, "Bell Highlight", new Vector3(0f, 0f, 0.08f),
                new Vector3(0.52f, 0.52f, 0.08f), new Color(1f, 0.34f, 0.22f, 0.9f), PrimitiveType.Cylinder);
            CreateWorldLabelAt("紧急铃", _mapService.ScaleMapPosition(new Vector3(0f, 0.48f, -0.16f)), 0.075f);
        }

        // ====================================================================
        //  REMOVE STALE VISUALS (shared generic)
        // ====================================================================

        public static void RemoveStaleVisuals<T>(Dictionary<T, GameObject> visuals, HashSet<T> seen)
        {
            List<T> stale = new List<T>();
            foreach (KeyValuePair<T, GameObject> pair in visuals)
                if (!seen.Contains(pair.Key))
                    stale.Add(pair.Key);

            foreach (T key in stale)
            {
                if (visuals[key] != null) UnityEngine.Object.Destroy(visuals[key]);
                visuals.Remove(key);
            }
        }
        // ====================================================================
        //  M3: Corpse Marker (ground decal at death position)
        // ====================================================================

        /// <summary>
        /// Creates a blood-like ground decal at the death position.
        /// Semi-transparent soft circle, visible from top-down orthographic camera.
        /// </summary>
        public GameObject CreateCorpseMarker(Vector3 position)
        {
            EnsureRuntimeSprites();
            GameObject marker = new GameObject("CorpseMarker");
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _softCircleSprite;
            renderer.color = new Color(0.55f, 0.04f, 0.04f, 0.35f);
            renderer.sortingOrder = 5; // On ground, below characters

            marker.transform.SetParent(_worldRoot != null ? _worldRoot.transform : null, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            return marker;
        }
    }
}
