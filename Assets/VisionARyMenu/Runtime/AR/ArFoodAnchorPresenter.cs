using System;
using System.Collections.Generic;
using MenuViewer.Food;
using MenuViewer.Overlay;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MenuViewer.AR
{
    public sealed class ArFoodAnchorPresenter : MonoBehaviour
    {
        public static readonly TrackableType PlacementPlaneMask =
            TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.PlaneWithinInfinity;

        public static readonly TrackableType PlacementCandidateMask =
            PlacementPlaneMask | TrackableType.PlaneEstimated;

        public const float MinimumPlacementPlaneDimensionMeters = 0.12f;
        public const float MinimumPlacementPlaneAreaSquareMeters = 0.02f;
        public const float MaximumPlacementDistanceMeters = 4f;
        public const float InfinitePlaneBoundsMarginMeters = 0.18f;
        public const float MeshFallbackMaxDistanceMeters = 3.5f;
        public const float MeshSurfaceMinimumUpDot = 0.82f;

        [SerializeField] private FoodCatalog catalog;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private ARMeshManager meshManager;
        [SerializeField] private ArSurfacePlacementController surfacePlacementController;
        [SerializeField] private Camera arCamera;
        [SerializeField] private MenuTapSelectionController tapController;
        [SerializeField] private float verticalLiftMeters = 0.02f;
        [SerializeField] private float previewAlpha = 0.38f;
        [SerializeField] private Color previewReticleColor = new Color(0.1f, 0.6f, 1f, 0.72f);
        [SerializeField] private Color readyReticleColor = new Color(0.18f, 0.95f, 0.45f, 0.9f);
        [SerializeField] private bool verboseLogging;

        private static readonly List<ARRaycastHit> RaycastHits = new List<ARRaycastHit>();

        public const float MinScaleMultiplier = 0.2f;
        public const float MaxScaleMultiplier = 5f;
        public const float ScrollWheelScaleSpeed = 0.1f;

        private GameObject activeAnchorRoot;
        private GameObject activeView;
        private string activeItemId;
        private float activeBaseScale;
        private float activeScaleMultiplier = 1f;
        private float lastPinchDistance = -1f;
        private string pendingItemId;
        private bool scanArmed;
        private int placedAtFrame = -1;
        private int pendingSetAtFrame = -1;
        private bool lastPlacementAttemptMissed;
        private GameObject previewRoot;
        private GameObject previewReticle;
        private GameObject previewView;
        private string previewItemId;
        private Material previewReticleMaterial;

        public event Action PlacementChanged;
        public event Action PendingChanged;

        public bool HasPlacement => activeView != null;
        public string PendingItemId => pendingItemId;
        // True once the user has explicitly asked to scan for the table (see BeginSurfaceScan).
        // Plane detection and tap-to-place only activate after this, so the camera being aimed
        // at the menu while the dish is merely selected can never seed a bad table plane.
        public bool IsScanArmed => scanArmed;
        public bool LastPlacementAttemptMissed => pendingItemId != null && lastPlacementAttemptMissed;
        public bool HasPreview => previewRoot != null && previewRoot.activeSelf;
        public Func<bool> InputBlocked { get; set; }

        // Screen-space (bottom-left origin) hit test against on-screen UI. Taps that land on
        // IMGUI controls must not place or remove models — IMGUI reacts on mouse-up while this
        // presenter reacts on mouse-down, so without this guard a button press also places.
        public Func<Vector2, bool> TapBlockedAt { get; set; }

        public void Configure(
            FoodCatalog foodCatalog,
            ARRaycastManager arRaycastManager,
            Camera camera,
            MenuTapSelectionController tapSelectionController = null,
            ARAnchorManager arAnchorManager = null,
            ARMeshManager arMeshManager = null,
            ArSurfacePlacementController arSurfacePlacementController = null)
        {
            if (tapController != null)
            {
                tapController.SelectionChanged -= HandleSelectionChanged;
            }

            catalog = foodCatalog;
            raycastManager = arRaycastManager;
            anchorManager = arAnchorManager;
            meshManager = arMeshManager;
            surfacePlacementController = arSurfacePlacementController;
            arCamera = camera;
            tapController = tapSelectionController;

            ClearPlacement();
            ClearPending();

            if (tapController != null)
            {
                tapController.SelectionChanged += HandleSelectionChanged;
                HandleSelectionChanged(tapController.Current);
            }
        }

        // Swaps the active catalog without touching raycast/anchor/tap-controller wiring — for
        // callers (e.g. QrMenuFlowController) that Configure() once against an empty/starter
        // catalog and then load the real one later (a QR-scanned remote menu, for instance).
        public void SetCatalog(FoodCatalog foodCatalog)
        {
            catalog = foodCatalog;
        }

        public void ClearPlacement()
        {
            var wasPresent = activeView != null;

            if (activeAnchorRoot != null)
            {
                Destroy(activeAnchorRoot);
            }
            else if (activeView != null)
            {
                Destroy(activeView);
            }

            activeAnchorRoot = null;
            activeView = null;
            activeItemId = null;
            activeBaseScale = 0f;
            activeScaleMultiplier = 1f;
            lastPinchDistance = -1f;
            placedAtFrame = -1;

            if (wasPresent)
            {
                surfacePlacementController?.ClearLocked();
                LogVerbose("ClearPlacement: destroyed existing placement.");
                PlacementChanged?.Invoke();
            }
        }

        public void ClearPending()
        {
            if (pendingItemId == null)
            {
                return;
            }

            pendingItemId = null;
            scanArmed = false;
            lastPlacementAttemptMissed = false;
            ClearPreview();
            LogVerbose("ClearPending: pending item cleared.");
            PendingChanged?.Invoke();
        }

        // Explicit user action (a "Scan table" button) that starts plane detection and allows
        // tap-to-place. Required because the camera is still pointed at the menu the instant a
        // dish is selected — arming immediately on selection let ARKit fold the menu sheet into
        // the table's plane, leaving it tilted. The user only presses this once they've actually
        // moved the camera over the table.
        public bool BeginSurfaceScan()
        {
            if (pendingItemId == null || scanArmed)
            {
                return false;
            }

            scanArmed = true;
            LogVerbose($"BeginSurfaceScan: armed surface scan for {pendingItemId}.");
            PendingChanged?.Invoke();
            return true;
        }

        public bool TryPlacePending(Pose surfacePose)
        {
            if (pendingItemId == null || catalog == null || !catalog.TryGetItem(pendingItemId, out var item))
            {
                return false;
            }

            var itemId = pendingItemId;
            ClearPlacement();

            activeItemId = itemId;
            pendingItemId = null;
            scanArmed = false;
            lastPlacementAttemptMissed = false;
            activeBaseScale = item.DefaultScaleMeters;
            activeScaleMultiplier = 1f;
            lastPinchDistance = -1f;

            var rotation = ResolveFaceCameraRotation(surfacePose.position);
            activeAnchorRoot = new GameObject($"AR Anchor ({activeItemId})");
            activeAnchorRoot.transform.SetPositionAndRotation(surfacePose.position, rotation);
            activeAnchorRoot.AddComponent<ARAnchor>();

            activeView = CreateView(item);
            activeView.transform.SetParent(activeAnchorRoot.transform, false);
            activeView.transform.localPosition = Vector3.up * verticalLiftMeters;
            activeView.transform.localRotation = Quaternion.identity;
            activeView.transform.localScale = Vector3.one * activeBaseScale;

            SetViewAlpha(activeView, 0.92f);
            placedAtFrame = Time.frameCount;
            ClearPreview();
            surfacePlacementController?.SetLocked(surfacePose);

            LogVerbose(anchorManager == null
                ? $"TryPlacePending: anchored {activeItemId} at worldPos={surfacePose.position}."
                : $"TryPlacePending: anchored {activeItemId} with ARAnchorManager present at worldPos={surfacePose.position}.");

            PendingChanged?.Invoke();
            PlacementChanged?.Invoke();
            return true;
        }

        public bool TryPlaceFromRaycastHits(IReadOnlyList<ARRaycastHit> hits)
        {
            if (TryResolvePlacementPose(hits, out var placementPose))
            {
                return TryPlacePending(placementPose);
            }

            MarkPlacementAttemptMissed("no stable horizontal placement surface hit.");
            return false;
        }

        public static bool IsSupportedPlacementHitType(TrackableType hitType)
        {
            return (hitType & PlacementPlaneMask) != 0;
        }

        public static bool IsUsablePlaneSize(Vector2 size)
        {
            var width = Mathf.Abs(size.x);
            var depth = Mathf.Abs(size.y);
            return IsFinitePositive(width)
                && IsFinitePositive(depth)
                && width >= MinimumPlacementPlaneDimensionMeters
                && depth >= MinimumPlacementPlaneDimensionMeters
                && width * depth >= MinimumPlacementPlaneAreaSquareMeters;
        }

        public static bool IsWithinExpandedPlaneBounds(Vector3 planeLocalPosition, Vector2 planeExtents, float marginMeters = InfinitePlaneBoundsMarginMeters)
        {
            var margin = Mathf.Max(0f, marginMeters);
            var extentX = Mathf.Abs(planeExtents.x);
            var extentZ = Mathf.Abs(planeExtents.y);
            return IsFinitePositive(extentX)
                && IsFinitePositive(extentZ)
                && Mathf.Abs(planeLocalPosition.x) <= extentX + margin
                && Mathf.Abs(planeLocalPosition.z) <= extentZ + margin;
        }

        public static bool IsUsablePlacementPlane(ARPlane plane)
        {
            return ArPlaneVisualizationController.IsHorizontalTrackingPlane(plane)
                && IsUsablePlaneSize(plane.size);
        }

        public static bool HasTableClassification(ARPlane plane)
        {
            return plane != null && (plane.classifications & PlaneClassifications.Table) != 0;
        }

        // Lower key wins. Hit-type priority dominates; within a tier prefer the plane already
        // being tracked (hysteresis — keeps the reticle from flickering between overlapping
        // planes), then ARKit table-classified planes, then the closer hit. Distance is clamped
        // below the tier steps so it can only break ties.
        public static float GetPlacementCandidateSortKey(
            int priority,
            float distanceMeters,
            bool isCurrentPlane,
            bool isTableSurface)
        {
            var tieBreaker = Mathf.Clamp(distanceMeters, 0f, 9.9f);
            return priority * 1000f
                + (isCurrentPlane ? 0f : 100f)
                + (isTableSurface ? 0f : 10f)
                + tieBreaker;
        }

        public static int GetPlacementHitPriority(TrackableType hitType)
        {
            if ((hitType & TrackableType.PlaneWithinPolygon) != 0)
            {
                return 0;
            }

            if ((hitType & TrackableType.PlaneWithinBounds) != 0)
            {
                return 1;
            }

            if ((hitType & TrackableType.PlaneWithinInfinity) != 0)
            {
                return 2;
            }

            return int.MaxValue;
        }

        public static bool TryResolvePlacementPose(IReadOnlyList<ARRaycastHit> hits, out Pose pose)
        {
            pose = default;
            if (hits == null)
            {
                return false;
            }

            var bestKey = float.PositiveInfinity;
            var found = false;

            foreach (var hit in hits)
            {
                if (!TryGetPlacementHitQuality(hit, out var priority, out var distance))
                {
                    continue;
                }

                var sortKey = GetPlacementCandidateSortKey(
                    priority,
                    distance,
                    isCurrentPlane: false,
                    isTableSurface: HasTableClassification(hit.trackable as ARPlane));
                if (found && sortKey >= bestKey)
                {
                    continue;
                }

                pose = hit.pose;
                bestKey = sortKey;
                found = true;
            }

            return found;
        }

        public static bool IsSupportedMeshSurface(Vector3 normal, float distanceMeters)
        {
            if (!IsReasonablePlacementDistance(distanceMeters, MeshFallbackMaxDistanceMeters))
            {
                return false;
            }

            var normalized = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.zero;
            return normalized.y >= MeshSurfaceMinimumUpDot;
        }

        private void OnDestroy()
        {
            if (tapController != null)
            {
                tapController.SelectionChanged -= HandleSelectionChanged;
            }

            ClearPlacement();
            ClearPreview();
        }

        private void Update()
        {
            if (activeView == null && activeItemId != null)
            {
                LogVerbose($"Update: activeView was destroyed externally (itemId={activeItemId}). Resetting state.");
                if (activeAnchorRoot != null)
                {
                    Destroy(activeAnchorRoot);
                }

                activeAnchorRoot = null;
                activeItemId = null;
                activeBaseScale = 0f;
                activeScaleMultiplier = 1f;
                lastPinchDistance = -1f;
                placedAtFrame = -1;
                surfacePlacementController?.ClearLocked();
                PlacementChanged?.Invoke();
            }

            UpdatePinchScale();
            UpdatePendingPreview();

            if (activeView == null && pendingItemId == null)
            {
                return;
            }

            if (arCamera == null)
            {
                return;
            }

            if (Time.frameCount == placedAtFrame)
            {
                return;
            }

            if (InputBlocked != null && InputBlocked())
            {
                return;
            }

            if (!TryConsumeTap(out var tapPoint))
            {
                return;
            }

            if (TapBlockedAt != null && TapBlockedAt(tapPoint))
            {
                return;
            }

            if (pendingItemId != null)
            {
                // Taps don't place anything until the user explicitly starts the table scan
                // (see BeginSurfaceScan) — otherwise a tap right after selecting the dish, while
                // the camera is still aimed at the menu, could place using whatever surface the
                // mesh fallback finds there.
                if (scanArmed && Time.frameCount != pendingSetAtFrame)
                {
                    TryHandleTapToPlace(tapPoint);
                }

                return;
            }

            if (activeView != null && TryHandleTapToRemove(tapPoint))
            {
                return;
            }
        }

        private void UpdatePinchScale()
        {
            if (activeView == null)
            {
                lastPinchDistance = -1f;
                return;
            }

            float scaleDelta = 0f;

#if UNITY_EDITOR
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0f)
            {
                scaleDelta = scroll * ScrollWheelScaleSpeed;
            }
#else
            if (Input.touchCount == 2)
            {
                var t0 = Input.GetTouch(0);
                var t1 = Input.GetTouch(1);
                var currentDistance = Vector2.Distance(t0.position, t1.position);

                if (lastPinchDistance > 0f)
                {
                    scaleDelta = (currentDistance - lastPinchDistance) / Screen.dpi;
                }

                lastPinchDistance = currentDistance;
            }
            else
            {
                lastPinchDistance = -1f;
            }
#endif

            if (scaleDelta == 0f)
            {
                return;
            }

            activeScaleMultiplier = Mathf.Clamp(
                activeScaleMultiplier + scaleDelta,
                MinScaleMultiplier,
                MaxScaleMultiplier);
            activeView.transform.localScale = Vector3.one * (activeBaseScale * activeScaleMultiplier);
        }

        private bool TryHandleTapToPlace(Vector2 tapPoint)
        {
            // In the editor there is no AR session; the surface controller supplies a
            // simulated reticle pose instead, so let the fallback chain run.
            if (ARSession.state != ARSessionState.SessionTracking && !Application.isEditor)
            {
                MarkPlacementAttemptMissed("AR session is not tracking.");
                return false;
            }

            // Fallback chain: tap plane raycast → AR mesh raycast → screen-center reticle.
            if (TryResolveTapRaycastPose(tapPoint, out var pose)
                || TryResolveMeshFallbackPose(meshManager, arCamera, tapPoint, out pose)
                || (surfacePlacementController != null && surfacePlacementController.TryGetReadyPose(out pose)))
            {
                return TryPlacePending(pose);
            }

            MarkPlacementAttemptMissed("no stable horizontal placement surface hit.");
            return false;
        }

        private bool TryResolveTapRaycastPose(Vector2 tapPoint, out Pose pose)
        {
            pose = default;
            RaycastHits.Clear();

            if (raycastManager == null
                || raycastManager.subsystem == null
                || !raycastManager.Raycast(tapPoint, RaycastHits, PlacementCandidateMask))
            {
                return false;
            }

            return TryResolvePlacementPose(RaycastHits, out pose);
        }

        public bool TryPlaceFromSurfaceState()
        {
            if (surfacePlacementController == null)
            {
                MarkPlacementAttemptMissed("surface preview is unavailable.");
                return false;
            }

            if (!surfacePlacementController.TryGetReadyPose(out var placementPose))
            {
                MarkPlacementAttemptMissed("surface preview is not ready.");
                return false;
            }

            return TryPlacePending(placementPose);
        }

        private static bool TryGetPlacementHitQuality(ARRaycastHit hit, out int priority, out float distance)
        {
            priority = GetPlacementHitPriority(hit.hitType);
            distance = ResolveHitDistance(hit);

            if (!IsSupportedPlacementHitType(hit.hitType))
            {
                return false;
            }

            if (!IsReasonablePlacementDistance(distance, MaximumPlacementDistanceMeters))
            {
                return false;
            }

            var plane = hit.trackable as ARPlane;
            if (plane == null)
            {
                return priority != int.MaxValue && (hit.hitType & TrackableType.PlaneWithinInfinity) == 0;
            }

            if (!IsUsablePlacementPlane(plane))
            {
                return false;
            }

            if ((hit.hitType & TrackableType.PlaneWithinInfinity) == 0)
            {
                return priority != int.MaxValue;
            }

            var localPoint = plane.transform.InverseTransformPoint(hit.pose.position);
            return IsWithinExpandedPlaneBounds(localPoint, plane.extents);
        }

        public static bool TryResolveMeshFallbackPose(ARMeshManager manager, Camera camera, Vector2 tapPoint, out Pose pose)
        {
            pose = default;
            if (manager == null || !manager.enabled || camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(tapPoint);
            var meshes = manager.meshes;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            var bestPoint = Vector3.zero;
            var bestNormal = Vector3.up;

            for (var i = 0; i < meshes.Count; i++)
            {
                var meshFilter = meshes[i];
                if (meshFilter == null || !meshFilter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var meshCollider = meshFilter.GetComponent<MeshCollider>();
                if (meshCollider == null || !meshCollider.enabled)
                {
                    continue;
                }

                if (!meshCollider.Raycast(ray, out var hit, MeshFallbackMaxDistanceMeters)
                    || !IsSupportedMeshSurface(hit.normal, hit.distance)
                    || hit.distance >= bestDistance)
                {
                    continue;
                }

                found = true;
                bestDistance = hit.distance;
                bestPoint = hit.point;
                bestNormal = hit.normal;
            }

            if (!found)
            {
                return false;
            }

            pose = new Pose(bestPoint, ResolveSurfaceRotation(ray.direction, bestNormal));
            return true;
        }

        private static Quaternion ResolveSurfaceRotation(Vector3 rayDirection, Vector3 surfaceNormal)
        {
            var up = surfaceNormal.sqrMagnitude > 1e-4f ? surfaceNormal.normalized : Vector3.up;
            var forward = Vector3.ProjectOnPlane(-rayDirection, up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            }

            if (forward.sqrMagnitude < 1e-4f)
            {
                return Quaternion.FromToRotation(Vector3.up, up);
            }

            return Quaternion.LookRotation(forward.normalized, up);
        }

        public static float ResolveHitDistance(ARRaycastHit hit)
        {
            if (IsFiniteNonNegative(hit.distance))
            {
                return hit.distance;
            }

            return hit.pose.position.magnitude;
        }

        public static bool IsReasonablePlacementDistance(float distanceMeters, float maxDistanceMeters)
        {
            return IsFiniteNonNegative(distanceMeters) && distanceMeters <= maxDistanceMeters;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void MarkPlacementAttemptMissed(string reason)
        {
            lastPlacementAttemptMissed = true;
            LogVerbose("Placement missed: " + reason);
        }

        public const float TapMaxDurationSeconds = 0.35f;
        public const float TapMaxMovementPixels = 24f;

        private int trackedTapFingerId = -1;
        private Vector2 trackedTapStartPosition;
        private float trackedTapStartTime;

        // A single finger's Began phase cannot be treated as a tap immediately: a pinch gesture
        // also starts with one finger landing slightly before the second. Acting on Began alone
        // (the old behaviour) deleted the placed model the instant the first pinch finger touched
        // it. Instead, track the finger and only resolve it as a tap once it lifts (Ended) within
        // a short time/movement budget, and only while it remains the sole touch the whole way
        // through — a second finger joining (touchCount > 1) invalidates it so pinch-to-scale can
        // take over.
        private bool TryConsumeTap(out Vector2 tapPoint)
        {
            tapPoint = default;

            if (Input.touchCount == 0)
            {
                trackedTapFingerId = -1;
                if (Input.GetMouseButtonDown(0))
                {
                    tapPoint = Input.mousePosition;
                    return true;
                }

                return false;
            }

            if (Input.touchCount > 1)
            {
                trackedTapFingerId = -1;
                return false;
            }

            var touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                trackedTapFingerId = touch.fingerId;
                trackedTapStartPosition = touch.position;
                trackedTapStartTime = Time.unscaledTime;
                return false;
            }

            if (trackedTapFingerId != touch.fingerId)
            {
                return false;
            }

            if (touch.phase == TouchPhase.Canceled)
            {
                trackedTapFingerId = -1;
                return false;
            }

            var elapsed = Time.unscaledTime - trackedTapStartTime;
            var moved = Vector2.Distance(touch.position, trackedTapStartPosition);

            if (elapsed > TapMaxDurationSeconds || moved > TapMaxMovementPixels)
            {
                trackedTapFingerId = -1;
                return false;
            }

            if (touch.phase == TouchPhase.Ended)
            {
                trackedTapFingerId = -1;
                tapPoint = trackedTapStartPosition;
                return true;
            }

            return false;
        }

        private bool TryHandleTapToRemove(Vector2 tapPoint)
        {
            var ray = arCamera.ScreenPointToRay(tapPoint);
            if (!Physics.Raycast(ray, out var hit, 25f) || hit.transform == null)
            {
                return false;
            }

            if (hit.transform == activeView.transform || hit.transform.IsChildOf(activeView.transform))
            {
                LogVerbose("TryHandleTapToRemove: tap hit active placement.");
                ClearPlacement();
                return true;
            }

            return false;
        }

        private void UpdatePendingPreview()
        {
            if (pendingItemId == null
                || catalog == null
                || !catalog.TryGetItem(pendingItemId, out var item)
                || surfacePlacementController == null)
            {
                ClearPreview();
                return;
            }

            var state = surfacePlacementController.CurrentState;
            if (!state.HasPose || state.status == ArSurfaceTrackingStatus.Locked)
            {
                SetPreviewVisible(false);
                return;
            }

            EnsurePreview(item);
            SetPreviewVisible(true);

            var rotation = ResolveFaceCameraRotation(state.pose.position);
            previewRoot.transform.SetPositionAndRotation(state.pose.position, rotation);
            previewReticle.transform.localPosition = Vector3.up * 0.003f;

            if (previewView != null)
            {
                previewView.transform.localPosition = Vector3.up * verticalLiftMeters;
                previewView.transform.localRotation = Quaternion.identity;
                previewView.transform.localScale = Vector3.one * item.DefaultScaleMeters;
            }

            SetReticleColor(state.status == ArSurfaceTrackingStatus.Ready ? readyReticleColor : previewReticleColor);
        }

        private void EnsurePreview(FoodItemDefinition item)
        {
            if (previewRoot == null)
            {
                previewRoot = new GameObject("AR Placement Preview");
                previewReticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                previewReticle.name = "AR Placement Reticle";
                previewReticle.transform.SetParent(previewRoot.transform, false);
                previewReticle.transform.localScale = new Vector3(0.18f, 0.004f, 0.18f);
                var collider = previewReticle.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            if (previewView != null && previewItemId == item.Id)
            {
                return;
            }

            if (previewView != null)
            {
                Destroy(previewView);
            }

            previewItemId = item.Id;
            previewView = CreateView(item);
            previewView.name = item.DisplayName + " AR Preview";
            previewView.transform.SetParent(previewRoot.transform, false);
            DisableColliders(previewView);
            SetViewAlpha(previewView, previewAlpha);
        }

        private void SetPreviewVisible(bool visible)
        {
            if (previewRoot != null && previewRoot.activeSelf != visible)
            {
                previewRoot.SetActive(visible);
            }
        }

        private void ClearPreview()
        {
            if (previewRoot != null)
            {
                Destroy(previewRoot);
            }

            if (previewReticleMaterial != null)
            {
                Destroy(previewReticleMaterial);
            }

            previewRoot = null;
            previewReticle = null;
            previewView = null;
            previewItemId = null;
            previewReticleMaterial = null;
        }

        private void SetReticleColor(Color color)
        {
            if (previewReticle == null)
            {
                return;
            }

            if (previewReticleMaterial == null)
            {
                previewReticleMaterial = CreateTransparentMaterial(color);
                var renderer = previewReticle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = previewReticleMaterial;
                }
            }

            previewReticleMaterial.color = color;
        }

        private static void DisableColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }
        }

        private void HandleSelectionChanged(MenuTapSelectionController.TapSelection? selection)
        {
            // Selection loss (idle timeout while the camera pans to the table, tap on empty
            // space, card close) must NOT cancel an armed placement — the user is mid-flow.
            // Pending is replaced by a new dish selection, consumed by placement, or
            // cancelled explicitly via ClearPending().
            if (!selection.HasValue || !selection.Value.hasItem)
            {
                return;
            }

            SelectPendingItem(selection.Value.match.itemId);
        }

        // Direct entry point for callers that pick a dish without going through
        // MenuTapSelectionController (e.g. a clickable digital-menu UI with no OCR/tap-line
        // matching involved, or a Phase 1 verification harness with no UI at all). Mirrors what
        // HandleSelectionChanged does for an OCR-tap selection.
        public bool SelectPendingItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || catalog == null || !catalog.TryGetItem(itemId, out _))
            {
                return false;
            }

            if (activeView != null && activeItemId == itemId)
            {
                return false;
            }

            if (pendingItemId == itemId)
            {
                return false;
            }

            LogVerbose($"SelectPendingItem: pending={itemId} ARSession.state={ARSession.state}");
            pendingItemId = itemId;
            scanArmed = false;
            pendingSetAtFrame = Time.frameCount;
            lastPlacementAttemptMissed = false;
            PendingChanged?.Invoke();
            return true;
        }

        private Quaternion ResolveFaceCameraRotation(Vector3 modelWorldPosition)
        {
            if (arCamera == null)
            {
                return Quaternion.identity;
            }

            var toCamera = arCamera.transform.position - modelWorldPosition;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-4f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(-toCamera, Vector3.up);
        }

        private static GameObject CreateView(FoodItemDefinition item)
        {
            return ProceduralFoodModelFactory.CreateView(item);
        }

        private static void SetViewAlpha(GameObject view, float alpha)
        {
            var renderers = view.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var material = renderer.material;
                var color = material.color;
                color.a = alpha;
                material.color = color;
                ConfigureTransparentMaterial(material);
            }

            var textMeshes = view.GetComponentsInChildren<TextMesh>();
            foreach (var textMesh in textMeshes)
            {
                var color = textMesh.color;
                color.a = alpha;
                textMesh.color = color;
            }
        }

        public static Material CreateTransparentMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                color = color,
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent,
            };
            ConfigureTransparentMaterial(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private void LogVerbose(string message)
        {
            if (verboseLogging)
            {
                Debug.Log("[ArFoodAnchorPresenter] " + message);
            }
        }
    }
}
