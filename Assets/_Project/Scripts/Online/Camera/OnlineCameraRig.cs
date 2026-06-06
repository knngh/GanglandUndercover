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
            camera.orthographicSize = _wasConfigured
                ? Mathf.Lerp(camera.orthographicSize, targetSize, Time.deltaTime * 4f)
                : targetSize;

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
