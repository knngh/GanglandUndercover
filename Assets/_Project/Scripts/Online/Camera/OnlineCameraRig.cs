using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Camera rig extracted from OnlineMatchController for M2 controller slimming.
    /// Handles camera configuration, follow, and subject switching.
    /// Pure class — no MonoBehaviour dependency.
    /// </summary>
    public class OnlineCameraRig
    {
        // ====================================================================
        //  Camera Constants
        // ====================================================================

        public const float PreviewSize = 13.4f;
        public const float ActionSize = 4.25f;
        public const float BlackoutSize = 3.05f;
        public const float TaskSize = 4.1f;
        public const float ActionYOffset = -4.42f;
        public const float ActionZOffset = -6.85f;
        public const float PreviewYOffset = -13.6f;
        public const float PreviewZOffset = -16.2f;
        public const float ActionFieldOfView = 42f;
        public const float PreviewFieldOfView = 52f;
        public const float ActionLookAheadY = 0.88f;
        public const float ActionLookHeight = 0.42f;

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
        /// Determines preview vs action mode, follows the current subject, smoothly interpolates.
        /// </summary>
        public void Configure(
            Camera camera,
            OnlineMatchPhase phase,
            bool tacticalMapOpen,
            int activeTaskId,
            float blackoutTimer,
            IReadOnlyDictionary<ulong, OnlinePlayerState> players,
            ulong localClientId,
            Vector3 localPosition)
        {
            if (camera == null)
            {
                return;
            }

            // --- Static camera settings ---
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.085f, 1f);

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
            float yOffset = preview ? PreviewYOffset : ActionYOffset;
            float zOffset = preview ? PreviewZOffset : ActionZOffset;
            Vector3 desiredPosition = new Vector3(target.x, target.y + yOffset, zOffset);

            // --- Detect projection change ---
            if (camera.orthographic != preview)
            {
                _wasConfigured = false;
            }

            camera.orthographic = preview;

            if (preview)
            {
                camera.fieldOfView = PreviewFieldOfView;
                camera.orthographicSize = _wasConfigured
                    ? Mathf.Lerp(camera.orthographicSize, targetSize, Time.deltaTime * 4f)
                    : targetSize;
            }
            else
            {
                camera.fieldOfView = _wasConfigured
                    ? Mathf.Lerp(camera.fieldOfView, ActionFieldOfView, Time.deltaTime * 4f)
                    : ActionFieldOfView;
                desiredPosition += new Vector3(0f, 0.18f, 0.15f);
            }

            // --- Smooth follow ---
            Vector3 lookTarget = preview
                ? target
                : target + new Vector3(0f, ActionLookAheadY, ActionLookHeight);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - desiredPosition, Vector3.up);

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
