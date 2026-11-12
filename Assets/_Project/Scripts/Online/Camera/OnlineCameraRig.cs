using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Camera rig extracted from OnlineMatchController for M2 controller slimming.
    /// Handles camera configuration, follow, and subject switching.
    /// MonoBehaviour — accesses Camera.main directly.
    /// </summary>
    public class OnlineCameraRig : MonoBehaviour
    {
        // ====================================================================
        //  Camera Constants
        // ====================================================================

        public const float PreviewSize = 13.4f;
        public const float ActionSize = 3.0f;        // [TUNABLE] M3: orthographic size for action top-down view
        public const float BlackoutSize = 2.4f;
        public const float TaskSize = 3.2f;
        public const float ActionZ = -10f;           // M3: fixed Z for orthographic camera above XY plane
        public const float PreviewZ = -16.2f;

        // 批次 6a：世界 texel 密度（tile PPU=48，1 世界单位 = 48 texel）。
        // 近景（≤CloseupSnapMaxSize）正交尺寸吸附到整数 texel/像素，消除非整数缩放的像素行不均（发糊/闪烁）。
        public const float WorldTexelsPerUnit = 48f;
        public const float CloseupSnapMaxSize = 6f;

        // ====================================================================
        //  State
        // ====================================================================

        private bool _wasConfigured;
        private ulong _currentSubjectId;

        // ====================================================================
        //  Public Accessors
        // ====================================================================

        public ulong CurrentSubjectId => _currentSubjectId;
        public bool WasConfigured => _wasConfigured;

        // ====================================================================
        //  State Mutation
        // ====================================================================

        /// <summary>Reset camera configuration state (forces snap on next Configure call).</summary>
        public void ResetConfiguration()
        {
            _wasConfigured = false;
        }

        /// <summary>Set the camera subject to follow.</summary>
        public void SetSubject(ulong clientId)
        {
            _currentSubjectId = clientId;
        }

        // ====================================================================
        //  Pixel-Crisp Snapping (批次 6a)
        // ====================================================================

        /// <summary>
        /// 把目标正交尺寸吸附到最近的"整数 texel/屏幕像素"档位（k = texelsPerUnit 的整数倍缩放）。
        /// 非整数缩放会让像素行宽窄不均（观感发糊、移动时闪烁）；吸附后每个 texel 占整数个屏幕像素。
        /// 典型值（PPU 48）：1440p 目标 3.0 → 3.0；1080p → 2.8125；900p → 3.125。
        /// </summary>
        public static float SnapOrthoSizeToTexelGrid(float targetSize, float screenPixelHeight, float texelsPerUnit)
        {
            if (targetSize <= 0f || screenPixelHeight <= 0f || texelsPerUnit <= 0f)
            {
                return targetSize;
            }

            float bestSize = targetSize;
            float bestDiff = float.MaxValue;
            for (int k = 1; k <= 12; k++)
            {
                float size = screenPixelHeight / (2f * texelsPerUnit * k);
                if (size <= 0.05f)
                {
                    break;
                }
                float diff = Mathf.Abs(size - targetSize);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestSize = size;
                }
            }
            return bestSize;
        }

        // ====================================================================
        //  Camera Target Resolution
        // ====================================================================

        /// <summary>
        /// Returns the world position the camera should track, based on current subject.
        /// Falls back to local player position, then to the provided fallback.
        /// </summary>
        public Vector3 GetTarget(
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            ulong localClientId,
            Vector3 localPositionFallback)
        {
            if (players.TryGetValue(_currentSubjectId, out OnlinePlayerState subject) && subject.Alive)
            {
                return subject.Position;
            }

            if (players.TryGetValue(localClientId, out OnlinePlayerState state))
            {
                return state.Position;
            }

            return localPositionFallback;
        }

        /// <summary>
        /// Picks the best camera subject during the opening sequence.
        /// Prefers the fallback client if alive and moving; otherwise picks the alive player nearest to the anchor.
        /// </summary>
        public ulong PickOpeningSubject(
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            ulong fallbackClientId,
            OnlineMapService mapService)
        {
            if (players.TryGetValue(fallbackClientId, out OnlinePlayerState localState)
                && localState.Alive && localState.Input.sqrMagnitude > 0.02f)
            {
                return fallbackClientId;
            }

            ulong bestClientId = fallbackClientId;
            float bestDistance = float.MaxValue;
            Vector3 anchor = mapService.ScaleMapPosition(new Vector3(-4.8f, 1.65f, 0f));

            foreach (OnlinePlayerState state in players.Values)
            {
                if (!state.Alive)
                {
                    continue;
                }

                float distance = Vector3.Distance(state.Position, anchor);

                if (distance < bestDistance)
                {
                    bestClientId = state.ClientId;
                    bestDistance = distance;
                }
            }

            return bestClientId;
        }

        // ====================================================================
        //  Camera Configuration (per-frame)
        // ====================================================================

        /// <summary>
        /// Configure the main camera each frame.
        /// M3: Both preview and action now use orthographic projection with straight-down view.
        /// Preview = tactical map / lobby / opening / result (wide ortho).
        /// Action  = gameplay (narrow ortho, track subject).
        /// All camera rotation is identity — the world is rendered on the XY plane.
        /// </summary>
        public void Configure(
            OnlineMatchPhase phase,
            bool tacticalMapOpen,
            int activeTaskId,
            float blackoutTimer,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            ulong localClientId,
            Vector3 localPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // --- Static camera settings ---
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);

            // M3: Camera is always orthographic (straight-down top-down view)
            camera.orthographic = true;

            // --- Determine mode ---
            bool preview = tacticalMapOpen
                || phase == OnlineMatchPhase.Lobby
                || phase == OnlineMatchPhase.Opening
                || phase == OnlineMatchPhase.Result;

            float targetSize = preview
                ? PreviewSize
                : activeTaskId >= 0
                    ? TaskSize
                    : blackoutTimer > 0f
                        ? BlackoutSize
                        : ActionSize;

            Vector3 target = preview ? Vector3.zero : GetTarget(players, localClientId, localPosition);

            // M3: Straight-down orthographic — camera sits at fixed Z above the XY plane
            float zOffset = preview ? PreviewZ : ActionZ;
            Vector3 desiredPosition = new Vector3(target.x, target.y, zOffset);

            // --- Snap or smooth orthographicSize ---
            // 批次 6a：近景档位吸附到整数 texel/像素（预览/战术图不吸附，保取景不变）。
            float effectiveTargetSize = targetSize <= CloseupSnapMaxSize && camera.pixelHeight > 0
                ? SnapOrthoSizeToTexelGrid(targetSize, camera.pixelHeight, WorldTexelsPerUnit)
                : targetSize;
            camera.orthographicSize = _wasConfigured
                ? Mathf.Lerp(camera.orthographicSize, effectiveTargetSize, Time.deltaTime * 4f)
                : effectiveTargetSize;

            // M3: Camera always looks straight down (identity rotation) for 2D rendering
            Quaternion desiredRotation = Quaternion.identity;

            camera.transform.rotation = Quaternion.Slerp(
                camera.transform.rotation,
                desiredRotation,
                _wasConfigured ? Time.deltaTime * 4.5f : 1f);

            camera.transform.position = Vector3.Lerp(
                camera.transform.position,
                desiredPosition,
                _wasConfigured ? Time.deltaTime * 4.8f : 1f);

            // 批次 6a：相机位置吸附到整像素网格（消除跟随移动时的亚像素抖动/闪烁）。
            if (!preview && camera.pixelHeight > 0)
            {
                float worldPerPixel = (camera.orthographicSize * 2f) / camera.pixelHeight;
                if (worldPerPixel > 0f)
                {
                    Vector3 snapped = camera.transform.position;
                    snapped.x = Mathf.Round(snapped.x / worldPerPixel) * worldPerPixel;
                    snapped.y = Mathf.Round(snapped.y / worldPerPixel) * worldPerPixel;
                    camera.transform.position = snapped;
                }
            }

            _wasConfigured = true;
        }

        // ====================================================================
        //  Utility
        // ====================================================================

        /// <summary>
        /// Returns true if the given position is near the current camera subject
        /// (within the label visibility radius). Always true outside the Action phase.
        /// </summary>
        public bool IsNearSubject(Vector3 position, Vector3 cameraTarget, bool tacticalMapOpen, OnlineMatchPhase phase)
        {
            if (tacticalMapOpen || phase != OnlineMatchPhase.Action)
            {
                return true;
            }

            return Vector3.Distance(position, cameraTarget) <= 2.4f;
        }
    }
}
