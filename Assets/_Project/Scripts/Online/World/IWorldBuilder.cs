using System.Collections.Generic;
using UnityEngine;
using GanglandUndercover.Online;

namespace GanglandUndercover.Online.World
{
    /// <summary>
    /// World / prop / visual builder abstraction for the online match scene.
    /// Extracted from <see cref="OnlineWorldBuilder"/> to enable testing and alternative implementations.
    /// Covers world construction, player/body/task visual creation, prop factories, and scene helpers.
    /// </summary>
    public interface IWorldBuilder
    {
        // ================================================================
        //  Core Properties
        // ================================================================

        // 注：Use2DBackend 在 OnlineWorldBuilder 中是 public field（Unity 序列化），
        //     不能放入 interface。直接通过 OnlineWorldBuilder.Use2DBackend 访问。

        /// <summary>Root GameObject that all world objects are parented under.</summary>
        GameObject WorldRoot { get; }

        /// <summary>Map coordinate service used for design-to-world scaling.</summary>
        OnlineMapService MapService { get; }

        /// <summary>Shared rounded-rectangle sprite used by 2D backend.</summary>
        Sprite RoundedRectSprite { get; }

        /// <summary>Shared circle sprite used by 2D backend.</summary>
        Sprite CircleSprite { get; }

        /// <summary>Shared soft-edge circle sprite used by 2D backend.</summary>
        Sprite SoftCircleSprite { get; }

        /// <summary>Shared diamond sprite used by 2D backend.</summary>
        Sprite DiamondSprite { get; }

        /// <summary>Shared capsule sprite used by 2D backend.</summary>
        Sprite CapsuleSprite { get; }

        /// <summary>Number of underworld passages configured for this match.</summary>
        int UnderworldPassageCount { get; }

        /// <summary>Solid obstacle rectangles for collision detection.</summary>
        IReadOnlyList<Rect> SolidObstacleRects { get; }

        /// <summary>Walkable area rectangles for navigation.</summary>
        IReadOnlyList<Rect> WalkableRects { get; }

        // ================================================================
        //  Initialization
        // ================================================================

        /// <summary>Initialize the builder with root, map service, and shared lists.</summary>
        void Initialize(GameObject worldRoot, OnlineMapService mapService,
            List<Rect> solidObstacleRects, List<Rect> walkableRects, List<TextMesh> worldLabels,
            int underworldPassageCount = 8);

        /// <summary>Set the task list used for task visual creation.</summary>
        void SetTasks(IReadOnlyList<OnlineTaskState> taskList);

        /// <summary>Ensure all runtime sprites (rounded rect, circle, etc.) are created.</summary>
        void EnsureRuntimeSprites();

        // ================================================================
        //  World Construction
        // ================================================================

        /// <summary>Build the full district map (rooms, corridors, props, lighting).</summary>
        void BuildDistrictMap();

        /// <summary>Build the legacy ship-style map layout.</summary>
        void BuildLegacyShipMap();

        /// <summary>Create floor background plane for the world.</summary>
        void CreateFloorBackground();

        // ================================================================
        //  Task Visuals
        // ================================================================

        /// <summary>Create a task station visual in the world.</summary>
        GameObject CreateTaskVisual(OnlineTaskState task, Transform parent);

        /// <summary>Update a task visual to reflect its current state (completed, sabotaged).</summary>
        void SetTaskVisualState(GameObject visual, OnlineTaskState task);

        /// <summary>Create task equipment props attached to a task station.</summary>
        void CreateTaskEquipment(Transform parent, int taskId);

        // ================================================================
        //  Player & Body Visuals
        // ================================================================

        /// <summary>Create a player character visual in the world.</summary>
        GameObject CreatePlayerVisual(OnlinePlayerState state, bool isLocal);

        /// <summary>Create a body/corpse visual in the world.</summary>
        GameObject CreateBodyVisual(OnlineBodyState body, Sprite characterSprite = null);

        /// <summary>Add a StageTwoCharacterRig component to the given root.</summary>
        void CreateStageTwoCharacterRig(GameObject root);

        /// <summary>Add and configure a StageTwoCharacterRig with player state.</summary>
        void CreateStageTwoCharacterRig(GameObject root, OnlinePlayerState state);

        /// <summary>Create character state layer (ghost, report indicators, etc.) on a GameObject.</summary>
        void CreateStageTwoCharacterStateLayer(GameObject root);

        /// <summary>Create character state layer on a Transform with player state.</summary>
        void CreateStageTwoCharacterStateLayer(Transform parent, OnlinePlayerState state);

        /// <summary>Create a profession-specific accessory on a player visual.</summary>
        void CreateProfessionAccessory(GameObject root, OnlinePlayerState state);

        /// <summary>Create a profession-specific accessory under a Transform.</summary>
        void CreateProfessionAccessory(Transform parent, OnlinePlayerState state);

        // ================================================================
        //  Prop Factories
        // ================================================================

        /// <summary>Create a primitive-based prop in the world.</summary>
        GameObject CreatePrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color);

        /// <summary>Create a solid primitive prop that registers as an obstacle.</summary>
        GameObject CreateSolidPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color);

        /// <summary>Create a simple box prop in the world.</summary>
        GameObject CreateProp(string propName, Vector3 position, Vector3 scale, Color color);

        /// <summary>Create a solid prop that registers as a collision obstacle.</summary>
        GameObject CreateSolidProp(string propName, Vector3 position, Vector3 scale, Color color);

        /// <summary>Create a sprite-based shape prop in the world.</summary>
        GameObject CreateShapeProp(string propName, Sprite sprite, Vector3 position, Vector3 scale, Color color);

        /// <summary>Create a rotated box prop in the world.</summary>
        GameObject CreateRotatedProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees);

        /// <summary>Create a mesh box prop with optional rotation.</summary>
        GameObject CreateMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f);

        /// <summary>Create a solid mesh box prop that registers as an obstacle.</summary>
        GameObject CreateSolidMeshBoxProp(string propName, Vector3 position, Vector3 scale, Color color,
            float rotationDegrees = 0f);

        /// <summary>Create a mesh box child under a parent Transform.</summary>
        GameObject CreateMeshBoxChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, float rotationDegrees = 0f);

        /// <summary>Create a mesh primitive child under a parent Transform.</summary>
        GameObject CreateMeshPrimitiveChild(Transform parent, string propName, PrimitiveType primitiveType,
            Vector3 localPosition, Vector3 scale, Color color, Quaternion localRotation);

        /// <summary>Create a mesh primitive prop in the world with rotation.</summary>
        GameObject CreateMeshPrimitiveProp(string propName, PrimitiveType primitiveType, Vector3 position,
            Vector3 scale, Color color, Quaternion rotation);

        /// <summary>Create a prop child under a parent Transform.</summary>
        GameObject CreatePropChild(Transform parent, string propName, Vector3 localPosition, Vector3 scale,
            Color color, PrimitiveType primitiveType);

        /// <summary>Create a sprite-based child under a parent Transform.</summary>
        GameObject CreateSpriteChild(Transform parent, string objectName, Sprite sprite, Vector3 localPosition,
            Vector3 scale, Color color);

        /// <summary>Create a tiled floor prop at the given position.</summary>
        void CreateTiledFloor(string name, Vector3 position, Vector2 size, Color tint);

        // ================================================================
        //  Model / Asset Store Props
        // ================================================================

        /// <summary>Create a prop from an Asset Store resource.</summary>
        GameObject CreateAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true);

        /// <summary>Create a solid Asset Store prop that registers as an obstacle.</summary>
        GameObject CreateSolidAssetStoreProp(string propName, string resourcePath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false,
            bool preserveMaterials = true);

        /// <summary>Create a prop from a Quaternius FBX model.</summary>
        GameObject CreateModelProp(string propName, string relativeFbxPath, Vector3 position, Vector3 footprint,
            float rotationDegrees = 0f, bool stretchToFootprint = false);

        /// <summary>Create a solid model prop that registers as an obstacle.</summary>
        GameObject CreateSolidModelProp(string propName, string relativeFbxPath, Vector3 position,
            Vector3 footprint, float rotationDegrees = 0f, bool stretchToFootprint = false);

        /// <summary>Create a fallback box-based prop when model loading fails.</summary>
        GameObject CreateModelFallbackProp(string propName, Vector3 position, Vector3 footprint,
            float rotationDegrees, Color color);

        /// <summary>Create wall overlay models along a wall segment.</summary>
        void CreateWallModelOverlay(string wallName, Vector3 position, Vector3 scale);

        /// <summary>Create door overlay models at a door marker.</summary>
        void CreateDoorModelOverlay(string markerName, Vector3 position, Vector3 scale);

        // ================================================================
        //  Model Loading
        // ================================================================

        /// <summary>Load a Quaternius model prefab by relative FBX path.</summary>
        GameObject LoadQuaterniusModel(string relativeFbxPath);

        /// <summary>Load a prefab from Unity Resources.</summary>
        GameObject LoadResourcePrefab(string resourcePath);

        /// <summary>Fit a loaded model to a target footprint.</summary>
        void FitModelToFootprint(GameObject model, Vector3 targetPosition, Vector3 footprint,
            bool stretchToFootprint);

        // ================================================================
        //  Scene Helpers
        // ================================================================

        /// <summary>Create a neon point light in the world.</summary>
        void CreateNeonLight(string lightName, Vector3 position, Color color, float intensity, float range);

        /// <summary>Configure scene-wide ambient and directional lighting.</summary>
        void ConfigureSceneLighting();

        /// <summary>Create the emergency meeting bell in the world center.</summary>
        void CreateEmergencyBell();

        /// <summary>Create a ground decal corpse marker at the given position.</summary>
        GameObject CreateCorpseMarker(Vector3 position);

        // ================================================================
        //  Obstacle / Walkable Registration
        // ================================================================

        /// <summary>Register a rectangular solid obstacle for collision.</summary>
        void RegisterSolidObstacle(Vector3 position, Vector3 scale);

        /// <summary>Register a rectangular walkable area for navigation.</summary>
        void RegisterWalkableArea(Vector3 position, Vector3 scale);

        // ================================================================
        //  Material Helpers
        // ================================================================

        /// <summary>Configure a prop's mesh renderer with a runtime shared material.</summary>
        void ConfigureRuntimeMesh(GameObject prop, Color color);

        /// <summary>Get or create a shared runtime mesh material for the given color.</summary>
        Material RuntimeMeshMaterial(Color color);

        // ================================================================
        //  Label Helpers
        // ================================================================

        /// <summary>Create a world-space TextMesh label at the given position.</summary>
        TextMesh CreateWorldLabelAt(string text, Vector3 position, float characterSize);

        /// <summary>Create a world-space TextMesh label as a child of a parent Transform.</summary>
        TextMesh CreateWorldLabel(Transform parent, string text, Vector3 localPosition, float characterSize);

        /// <summary>Build the text content for a player's world label.</summary>
        string BuildPlayerWorldLabel(OnlinePlayerState state, bool isLocal);

        /// <summary>Determine whether a player's world label should be visible.</summary>
        bool ShouldShowPlayerWorldLabel(OnlinePlayerState state, bool isLocal, OnlineMatchPhase phase,
            bool tacticalMapOpen);
    }
}
