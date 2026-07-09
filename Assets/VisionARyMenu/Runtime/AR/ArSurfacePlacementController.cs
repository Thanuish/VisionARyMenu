using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MenuViewer.AR
{
    public enum ArSurfaceTrackingStatus
    {
        Unavailable,
        Scanning,
        Preview,
        Ready,
        Locked
    }

    public readonly struct ArSurfacePlacementState
    {
        public readonly Pose pose;
        public readonly float confidence;
        public readonly TrackableType hitType;
        public readonly int stableFrameCount;
        public readonly ArSurfaceTrackingStatus status;

        public ArSurfacePlacementState(
            Pose pose,
            float confidence,
            TrackableType hitType,
            int stableFrameCount,
            ArSurfaceTrackingStatus status)
        {
            this.pose = pose;
            this.confidence = confidence;
            this.hitType = hitType;
            this.stableFrameCount = stableFrameCount;
            this.status = status;
        }

        public bool HasPose =>
            status == ArSurfaceTrackingStatus.Preview
            || status == ArSurfaceTrackingStatus.Ready
            || status == ArSurfaceTrackingStatus.Locked;

        public bool CanPlace => status == ArSurfaceTrackingStatus.Ready;

        public static ArSurfacePlacementState Unavailable =>
            new ArSurfacePlacementState(Pose.identity, 0f, TrackableType.None, 0, ArSurfaceTrackingStatus.Unavailable);

        public static ArSurfacePlacementState Scanning =>
            new ArSurfacePlacementState(Pose.identity, 0f, TrackableType.None, 0, ArSurfaceTrackingStatus.Scanning);
    }

    public readonly struct ArSurfacePlacementCandidate
    {
        public readonly Pose pose;
        public readonly float confidence;
        public readonly TrackableType hitType;
        public readonly bool canFinalize;
        public readonly bool hasPose;
        public readonly TrackableId trackableId;

        public ArSurfacePlacementCandidate(
            Pose pose,
            float confidence,
            TrackableType hitType,
            bool canFinalize,
            bool hasPose = true,
            TrackableId trackableId = default)
        {
            this.pose = pose;
            this.confidence = confidence;
            this.hitType = hitType;
            this.canFinalize = canFinalize;
            this.hasPose = hasPose;
            this.trackableId = trackableId;
        }
    }

    public sealed class ArSurfacePlacementController : MonoBehaviour
    {
        public static readonly TrackableType PreviewCandidateMask =
            ArFoodAnchorPresenter.PlacementCandidateMask | TrackableType.FeaturePoint;

        public const int DefaultStableFrameThreshold = 6;
        // Estimated planes / feature points may never be promoted to a tracked plane on
        // difficult surfaces (glossy or textureless tables), so allow them to finalize after
        // a much longer steady hold instead of blocking placement forever.
        public const int EstimatedSurfaceStableFrameMultiplier = 5;
        public const float StablePositionDeltaMeters = 0.035f;
        public const float StableRotationDeltaDegrees = 10f;
        public const float JumpRejectDistanceMeters = 0.35f;
        public const float SimulatedSurfaceDistanceMeters = 1.2f;
        public const float SimulatedSurfaceDropMeters = 0.5f;

        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARMeshManager meshManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private int stableFrameThreshold = DefaultStableFrameThreshold;
        [SerializeField] private float poseSmoothingSpeed = 16f;
        [SerializeField] private bool verboseLogging;

        private static readonly List<ARRaycastHit> RaycastHits = new List<ARRaycastHit>();

        private bool manualStateOverride;
        private ArSurfacePlacementState currentState = ArSurfacePlacementState.Unavailable;
        // Plane chosen last frame; preferred during candidate selection (hysteresis).
        private TrackableId stickyPlaneId = TrackableId.invalidId;

        public event Action<ArSurfacePlacementState> StateChanged;

        public ArSurfacePlacementState CurrentState => currentState;

        public void Configure(ARRaycastManager arRaycastManager, Camera camera, ARMeshManager arMeshManager = null)
        {
            raycastManager = arRaycastManager;
            arCamera = camera;
            meshManager = arMeshManager;
            manualStateOverride = false;
            stickyPlaneId = TrackableId.invalidId;
            SetState(ArSurfacePlacementState.Scanning);
        }

        public void SetCurrentState(ArSurfacePlacementState state)
        {
            manualStateOverride = true;
            SetState(state);
        }

        public void ClearManualState()
        {
            manualStateOverride = false;
        }

        public void SetLocked(Pose pose)
        {
            manualStateOverride = false;
            SetState(new ArSurfacePlacementState(
                pose,
                1f,
                currentState.hitType,
                currentState.stableFrameCount,
                ArSurfaceTrackingStatus.Locked));
        }

        public void ClearLocked()
        {
            if (currentState.status == ArSurfaceTrackingStatus.Locked)
            {
                SetState(ArSurfacePlacementState.Scanning);
            }
        }

        public bool TryGetReadyPose(out Pose pose)
        {
            if (currentState.CanPlace)
            {
                pose = currentState.pose;
                return true;
            }

            pose = default;
            return false;
        }

        public static bool TrySelectBestCandidate(IReadOnlyList<ARRaycastHit> hits, out ArSurfacePlacementCandidate candidate)
        {
            return TrySelectBestCandidate(hits, TrackableId.invalidId, out candidate);
        }

        public static bool TrySelectBestCandidate(
            IReadOnlyList<ARRaycastHit> hits,
            TrackableId preferredPlaneId,
            out ArSurfacePlacementCandidate candidate)
        {
            candidate = default;
            if (hits == null)
            {
                return false;
            }

            var found = false;
            var bestKey = float.PositiveInfinity;

            foreach (var hit in hits)
            {
                if (!TryCreateCandidate(hit, out var next, out var priority, out var distance))
                {
                    continue;
                }

                var isPreferredPlane = preferredPlaneId != TrackableId.invalidId
                    && hit.trackableId == preferredPlaneId;
                var sortKey = ArFoodAnchorPresenter.GetPlacementCandidateSortKey(
                    priority,
                    distance,
                    isPreferredPlane,
                    ArFoodAnchorPresenter.HasTableClassification(hit.trackable as ARPlane));
                if (found && sortKey >= bestKey)
                {
                    continue;
                }

                candidate = next;
                bestKey = sortKey;
                found = true;
            }

            return found;
        }

        public static ArSurfacePlacementState ResolveNextState(
            ArSurfacePlacementState previous,
            ArSurfacePlacementCandidate candidate,
            float smoothingFactor,
            int stableFrameThreshold = DefaultStableFrameThreshold,
            float stablePositionDeltaMeters = StablePositionDeltaMeters,
            float stableRotationDeltaDegrees = StableRotationDeltaDegrees,
            float jumpRejectDistanceMeters = JumpRejectDistanceMeters)
        {
            if (!candidate.hasPose)
            {
                return ArSurfacePlacementState.Scanning;
            }

            var clampedSmoothing = Mathf.Clamp01(smoothingFactor);
            var hadPose = previous.HasPose && previous.status != ArSurfaceTrackingStatus.Locked;
            var targetPose = candidate.pose;
            var smoothedPose = targetPose;
            var stableFrames = 1;

            if (hadPose)
            {
                var positionDelta = Vector3.Distance(previous.pose.position, targetPose.position);
                var rotationDelta = Quaternion.Angle(previous.pose.rotation, targetPose.rotation);
                var jumped = positionDelta > jumpRejectDistanceMeters;

                if (!jumped)
                {
                    smoothedPose = new Pose(
                        Vector3.Lerp(previous.pose.position, targetPose.position, clampedSmoothing),
                        Quaternion.Slerp(previous.pose.rotation, targetPose.rotation, clampedSmoothing));
                }

                if (!jumped
                    && positionDelta <= stablePositionDeltaMeters
                    && rotationDelta <= stableRotationDeltaDegrees)
                {
                    stableFrames = previous.stableFrameCount + 1;
                }
            }

            var requiredStableFrames = Mathf.Max(1, candidate.canFinalize
                ? stableFrameThreshold
                : stableFrameThreshold * EstimatedSurfaceStableFrameMultiplier);
            var status = stableFrames >= requiredStableFrames
                ? ArSurfaceTrackingStatus.Ready
                : ArSurfaceTrackingStatus.Preview;
            var stabilityBoost = Mathf.Clamp01(stableFrames / (float)Mathf.Max(1, stableFrameThreshold));
            var confidence = Mathf.Clamp01(candidate.confidence * Mathf.Lerp(0.72f, 1f, stabilityBoost));

            return new ArSurfacePlacementState(
                smoothedPose,
                confidence,
                candidate.hitType,
                stableFrames,
                status);
        }

        private void Update()
        {
            if (manualStateOverride)
            {
                return;
            }

            if (currentState.status == ArSurfaceTrackingStatus.Locked)
            {
                return;
            }

            if (arCamera == null)
            {
                stickyPlaneId = TrackableId.invalidId;
                SetState(ArSurfacePlacementState.Unavailable);
                return;
            }

            var sessionTracking = ARSession.state == ARSessionState.SessionTracking;
            if (!sessionTracking && !SimulateWithoutArSession)
            {
                stickyPlaneId = TrackableId.invalidId;
                SetState(ArSurfacePlacementState.Unavailable);
                return;
            }

            var screenPoint = ResolveTrackingScreenPoint();
            if (!TryResolveCandidate(screenPoint, out var candidate)
                && !(SimulateWithoutArSession && !sessionTracking && TryCreateSimulatedCandidate(out candidate)))
            {
                stickyPlaneId = TrackableId.invalidId;
                SetState(ArSurfacePlacementState.Scanning);
                return;
            }

            stickyPlaneId = candidate.trackableId;
            var smoothingFactor = ResolveSmoothingFactor(Time.deltaTime, poseSmoothingSpeed);
            SetState(ResolveNextState(
                currentState,
                candidate,
                smoothingFactor,
                stableFrameThreshold,
                StablePositionDeltaMeters,
                StableRotationDeltaDegrees,
                JumpRejectDistanceMeters));
        }

        // Editor Play mode has no AR session (FakeOcrService path); without a pose source the
        // placement flow could never be exercised outside a device. Never active on device.
        private static bool SimulateWithoutArSession => Application.isEditor;

        private bool TryCreateSimulatedCandidate(out ArSurfacePlacementCandidate candidate)
        {
            var cameraTransform = arCamera.transform;
            var point = cameraTransform.position + cameraTransform.forward * SimulatedSurfaceDistanceMeters;
            point.y = cameraTransform.position.y - SimulatedSurfaceDropMeters;
            candidate = new ArSurfacePlacementCandidate(
                new Pose(point, Quaternion.identity),
                0.85f,
                TrackableType.None,
                true);
            return true;
        }

        private Vector2 ResolveTrackingScreenPoint()
        {
            var width = Screen.width > 0 ? Screen.width : arCamera.pixelWidth;
            var height = Screen.height > 0 ? Screen.height : arCamera.pixelHeight;
            return new Vector2(width * 0.5f, height * 0.5f);
        }

        private bool TryResolveCandidate(Vector2 screenPoint, out ArSurfacePlacementCandidate candidate)
        {
            candidate = default;
            RaycastHits.Clear();
            var hasRaycastCandidate = false;
            var raycastCandidate = default(ArSurfacePlacementCandidate);

            if (raycastManager != null
                && raycastManager.subsystem != null
                && raycastManager.Raycast(screenPoint, RaycastHits, PreviewCandidateMask)
                && TrySelectBestCandidate(RaycastHits, stickyPlaneId, out raycastCandidate))
            {
                if (raycastCandidate.canFinalize)
                {
                    candidate = raycastCandidate;
                    return true;
                }

                hasRaycastCandidate = true;
            }

            if (TryResolveMeshCandidate(screenPoint, out candidate))
            {
                return true;
            }

            if (hasRaycastCandidate)
            {
                candidate = raycastCandidate;
                return true;
            }

            return false;
        }

        private bool TryResolveMeshCandidate(Vector2 screenPoint, out ArSurfacePlacementCandidate candidate)
        {
            candidate = default;
            if (!ArFoodAnchorPresenter.TryResolveMeshFallbackPose(meshManager, arCamera, screenPoint, out var pose))
            {
                return false;
            }

            candidate = new ArSurfacePlacementCandidate(pose, 0.72f, TrackableType.None, true);
            return true;
        }

        private static bool TryCreateCandidate(
            ARRaycastHit hit,
            out ArSurfacePlacementCandidate candidate,
            out int priority,
            out float distance)
        {
            candidate = default;
            distance = ArFoodAnchorPresenter.ResolveHitDistance(hit);
            priority = int.MaxValue;

            if (!ArFoodAnchorPresenter.IsReasonablePlacementDistance(
                distance,
                ArFoodAnchorPresenter.MaximumPlacementDistanceMeters))
            {
                return false;
            }

            var hitType = hit.hitType;
            var plane = hit.trackable as ARPlane;
            var hasKnownPlane = plane != null;
            var hasUsablePlane = hasKnownPlane && ArFoodAnchorPresenter.IsUsablePlacementPlane(plane);
            if (hasKnownPlane && !hasUsablePlane)
            {
                return false;
            }

            var canFinalize = false;
            var confidence = 0f;

            if ((hitType & TrackableType.PlaneWithinPolygon) != 0)
            {
                priority = 0;
                canFinalize = !hasKnownPlane || hasUsablePlane;
                confidence = canFinalize ? 1f : 0.5f;
            }
            else if ((hitType & TrackableType.PlaneWithinBounds) != 0)
            {
                priority = 1;
                canFinalize = !hasKnownPlane || hasUsablePlane;
                confidence = canFinalize ? 0.9f : 0.48f;
            }
            else if ((hitType & TrackableType.PlaneWithinInfinity) != 0)
            {
                priority = 2;
                canFinalize = hasUsablePlane
                    && ArFoodAnchorPresenter.IsWithinExpandedPlaneBounds(
                        plane.transform.InverseTransformPoint(hit.pose.position),
                        plane.extents);
                confidence = canFinalize ? 0.68f : 0.45f;
            }
            else if ((hitType & TrackableType.PlaneEstimated) != 0)
            {
                priority = 3;
                confidence = 0.36f;
            }
            else if ((hitType & TrackableType.FeaturePoint) != 0)
            {
                priority = 4;
                confidence = 0.24f;
            }

            if (confidence <= 0f)
            {
                return false;
            }

            candidate = new ArSurfacePlacementCandidate(
                hit.pose,
                confidence,
                hitType,
                canFinalize,
                hasPose: true,
                trackableId: hit.trackableId);
            return true;
        }

        private void SetState(ArSurfacePlacementState state)
        {
            var previous = currentState;
            currentState = state;
            if (previous.status != state.status
                || previous.stableFrameCount != state.stableFrameCount
                || previous.hitType != state.hitType
                || Vector3.Distance(previous.pose.position, state.pose.position) > 0.002f)
            {
                StateChanged?.Invoke(currentState);
                LogVerbose($"Surface state: {state.status} confidence={state.confidence:0.00} stable={state.stableFrameCount}");
            }
        }

        private static float ResolveSmoothingFactor(float deltaTime, float speed)
        {
            var dt = deltaTime > 0f ? deltaTime : 1f / 60f;
            return 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * dt);
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
            {
                Debug.Log("[ArSurfacePlacementController] " + message);
            }
        }
    }
}
