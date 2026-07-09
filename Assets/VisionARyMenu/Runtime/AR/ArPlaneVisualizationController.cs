using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MenuViewer.AR
{
    public enum ArPlaneDetectionStatus
    {
        NoPlaneFound,
        PlaneReady,
        PlacementActive
    }

    public sealed class ArPlaneVisualizationController : MonoBehaviour
    {
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private bool showPlanes;
        [SerializeField] private Color fillColor = new Color(0.18f, 0.95f, 0.45f, 0.22f);
        [SerializeField] private Color outlineColor = new Color(0.18f, 0.95f, 0.45f, 0.9f);
        [SerializeField] private float outlineWidthMeters = 0.006f;

        private Material fillMaterial;
        private Material outlineMaterial;
        private MaterialPropertyBlock materialPropertyBlock;
        private ArFoodAnchorPresenter anchorPresenter;
        private bool placementPending;
        private bool hasPlacement;
        private ArPlaneDetectionStatus currentStatus = ArPlaneDetectionStatus.NoPlaneFound;

        public bool ShowPlanes
        {
            get => showPlanes;
            set
            {
                if (showPlanes == value)
                {
                    return;
                }

                showPlanes = value;
                ApplyVisibilityAll();
            }
        }

        public bool PlacementPending => placementPending;
        public bool HasPlacement => hasPlacement;
        public bool PlaneHighlightsActive => showPlanes && placementPending;
        public ArPlaneDetectionStatus CurrentStatus => currentStatus;
        public int PlaneCount { get; private set; }

        public static bool ShouldShowPlaneForState(
            bool showPlanes,
            bool placementPending,
            bool isHorizontalTracking)
        {
            return showPlanes && placementPending && isHorizontalTracking;
        }

        public static ArPlaneDetectionStatus ResolveDetectionStatus(bool hasPlacement, bool hasPlane)
        {
            if (hasPlacement)
            {
                return ArPlaneDetectionStatus.PlacementActive;
            }

            return hasPlane ? ArPlaneDetectionStatus.PlaneReady : ArPlaneDetectionStatus.NoPlaneFound;
        }

        public static bool IsHorizontalPlacementAlignment(PlaneAlignment alignment)
        {
            return alignment == PlaneAlignment.HorizontalUp;
        }

        public static bool IsHorizontalTrackingPlane(ARPlane plane)
        {
            if (plane == null || plane.subsumedBy != null || plane.trackingState != TrackingState.Tracking)
            {
                return false;
            }

            return IsHorizontalPlacementAlignment(plane.alignment);
        }

        public void Configure(ARPlaneManager manager)
        {
            if (planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(HandlePlanesChanged);
            }

            planeManager = manager;

            if (planeManager != null)
            {
                planeManager.trackablesChanged.AddListener(HandlePlanesChanged);
            }

            ApplyDetectionMode();
            ApplyVisibilityAll();
        }

        public void BindPlacement(ArFoodAnchorPresenter presenter)
        {
            if (anchorPresenter != null)
            {
                anchorPresenter.PendingChanged -= HandlePlacementStateChanged;
                anchorPresenter.PlacementChanged -= HandlePlacementStateChanged;
            }

            anchorPresenter = presenter;

            if (anchorPresenter != null)
            {
                anchorPresenter.PendingChanged += HandlePlacementStateChanged;
                anchorPresenter.PlacementChanged += HandlePlacementStateChanged;
            }

            HandlePlacementStateChanged();
        }

        public void SetPlacementState(bool pending, bool placed)
        {
            if (placementPending == pending && hasPlacement == placed)
            {
                return;
            }

            placementPending = pending;
            hasPlacement = placed;
            ApplyDetectionMode();
            ApplyVisibilityAll();
        }

        // Plane detection stays off until the user explicitly arms the table scan (see
        // ArFoodAnchorPresenter.BeginSurfaceScan), not merely once a menu item is selected.
        // The camera is still aimed at the menu the instant a dish is picked, so detection must
        // wait for the user's own confirmation that the camera is now over the table — otherwise
        // ARKit can fold the menu sheet into the table's plane, leaving it tilted.
        private void ApplyDetectionMode()
        {
            if (planeManager == null)
            {
                return;
            }

            var shouldDetect = hasPlacement || (anchorPresenter != null && anchorPresenter.IsScanArmed);
            planeManager.requestedDetectionMode = shouldDetect ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.None;
        }

        private void HandlePlacementStateChanged()
        {
            // ApplyDetectionMode also depends on IsScanArmed, which can flip (BeginSurfaceScan)
            // without placementPending/hasPlacement changing, so it can't rely solely on
            // SetPlacementState's early-return-on-no-change path below.
            ApplyDetectionMode();
            SetPlacementState(
                anchorPresenter != null && anchorPresenter.PendingItemId != null,
                anchorPresenter != null && anchorPresenter.HasPlacement);
        }

        private void OnDestroy()
        {
            if (planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(HandlePlanesChanged);
            }

            if (anchorPresenter != null)
            {
                anchorPresenter.PendingChanged -= HandlePlacementStateChanged;
                anchorPresenter.PlacementChanged -= HandlePlacementStateChanged;
            }

            if (fillMaterial != null)
            {
                Destroy(fillMaterial);
            }

            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }

        private void HandlePlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added)
            {
                EnsureVisualizer(plane);
            }

            foreach (var plane in args.updated)
            {
                EnsureVisualizer(plane);
            }

            ApplyVisibilityAll();
        }

        private void ApplyVisibilityAll()
        {
            if (planeManager == null)
            {
                UpdateDetectionStatus(0);
                return;
            }

            var planeCount = 0;

            foreach (var plane in planeManager.trackables)
            {
                EnsureVisualizer(plane);

                var isUsablePlacementPlane = ArFoodAnchorPresenter.IsUsablePlacementPlane(plane);
                if (isUsablePlacementPlane)
                {
                    planeCount++;
                }

                SetVisible(plane, ShouldShowPlaneForState(showPlanes, placementPending, isUsablePlacementPlane));
            }

            UpdateDetectionStatus(planeCount);
        }

        private void EnsureVisualizer(ARPlane plane)
        {
            if (plane == null)
            {
                return;
            }

            if (plane.GetComponent<MeshFilter>() == null)
            {
                plane.gameObject.AddComponent<MeshFilter>();
            }

            var meshRenderer = plane.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = plane.gameObject.AddComponent<MeshRenderer>();
            }
            meshRenderer.sharedMaterial = GetFillMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            var lineRenderer = plane.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = plane.gameObject.AddComponent<LineRenderer>();
            }
            lineRenderer.sharedMaterial = GetOutlineMaterial();
            lineRenderer.widthMultiplier = outlineWidthMeters;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;

            var visualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
            if (visualizer == null)
            {
                visualizer = plane.gameObject.AddComponent<ARPlaneMeshVisualizer>();
            }
            visualizer.hideSubsumed = true;
        }

        private void SetVisible(ARPlane plane, bool visible)
        {
            if (plane == null)
            {
                return;
            }

            var visualizer = plane.GetComponent<ARPlaneMeshVisualizer>();
            if (visualizer != null)
            {
                visualizer.enabled = visible;
            }

            var meshRenderer = plane.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
                ApplyMeshColor(meshRenderer, fillColor);
            }

            var lineRenderer = plane.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
                lineRenderer.startColor = outlineColor;
                lineRenderer.endColor = outlineColor;
                lineRenderer.widthMultiplier = outlineWidthMeters;
            }
        }

        private void UpdateDetectionStatus(int planeCount)
        {
            PlaneCount = planeCount;
            currentStatus = ResolveDetectionStatus(hasPlacement, planeCount > 0);
        }

        private void ApplyMeshColor(MeshRenderer meshRenderer, Color color)
        {
            if (meshRenderer == null)
            {
                return;
            }

            if (materialPropertyBlock == null)
            {
                materialPropertyBlock = new MaterialPropertyBlock();
            }

            meshRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor("_Color", color);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        private Material GetFillMaterial()
        {
            if (fillMaterial == null)
            {
                fillMaterial = ArFoodAnchorPresenter.CreateTransparentMaterial(fillColor);
            }

            return fillMaterial;
        }

        private Material GetOutlineMaterial()
        {
            if (outlineMaterial == null)
            {
                outlineMaterial = ArFoodAnchorPresenter.CreateTransparentMaterial(outlineColor);
            }

            return outlineMaterial;
        }
    }
}
