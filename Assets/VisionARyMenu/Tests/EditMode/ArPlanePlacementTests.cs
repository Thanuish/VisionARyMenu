using System;
using System.Reflection;
using MenuViewer.AR;
using NUnit.Framework;
using VisionARyMenu.Bootstrap;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

namespace MenuViewer.Tests.EditMode
{
    public sealed class ArPlanePlacementTests
    {
        [Test]
        public void IsSupportedPlacementHitType_PolygonBoundsOrInfinity_Accepts()
        {
            Assert.IsTrue(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.PlaneWithinPolygon));
            Assert.IsTrue(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.PlaneWithinBounds));
            Assert.IsTrue(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.PlaneWithinInfinity));
        }

        [Test]
        public void IsSupportedPlacementHitType_EstimatedFeaturePointOrDepth_Rejects()
        {
            Assert.IsFalse(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.PlaneEstimated));
            Assert.IsFalse(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.FeaturePoint));
            Assert.IsFalse(ArFoodAnchorPresenter.IsSupportedPlacementHitType(TrackableType.Depth));
        }

        [Test]
        public void TryResolvePlacementPose_StrongerPlaneHitBeatsCloserInfinityHit()
        {
            var root = new GameObject("Placement Resolver Test");
            try
            {
                var infinityHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinInfinity,
                    new Pose(new Vector3(0f, 0f, 0.35f), Quaternion.identity));
                var polygonHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 1.1f), Quaternion.identity));

                Assert.IsTrue(ArFoodAnchorPresenter.TryResolvePlacementPose(
                    new[] { infinityHit, polygonHit },
                    out var pose));
                Assert.That(Vector3.Distance(polygonHit.pose.position, pose.position), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlacementPlaneHelpers_GuardSmallPlanesAndInfinityOvershoot()
        {
            Assert.IsTrue(ArFoodAnchorPresenter.IsUsablePlaneSize(new Vector2(0.2f, 0.2f)));
            Assert.IsFalse(ArFoodAnchorPresenter.IsUsablePlaneSize(new Vector2(0.08f, 0.5f)));

            Assert.IsTrue(ArFoodAnchorPresenter.IsWithinExpandedPlaneBounds(
                new Vector3(0.24f, 0f, 0.04f),
                new Vector2(0.15f, 0.15f),
                0.1f));
            Assert.IsFalse(ArFoodAnchorPresenter.IsWithinExpandedPlaneBounds(
                new Vector3(0.27f, 0f, 0.04f),
                new Vector2(0.15f, 0.15f),
                0.1f));
        }

        [Test]
        public void IsHorizontalPlacementAlignment_OnlyHorizontalUp_Accepts()
        {
            Assert.IsTrue(ArPlaneVisualizationController.IsHorizontalPlacementAlignment(PlaneAlignment.HorizontalUp));
            Assert.IsFalse(ArPlaneVisualizationController.IsHorizontalPlacementAlignment(PlaneAlignment.HorizontalDown));
            Assert.IsFalse(ArPlaneVisualizationController.IsHorizontalPlacementAlignment(PlaneAlignment.Vertical));
            Assert.IsFalse(ArPlaneVisualizationController.IsHorizontalPlacementAlignment(PlaneAlignment.NotAxisAligned));
        }

        [Test]
        public void IsUsablePlacementPlane_RequiresHorizontalUpTrackingPlane()
        {
            var root = new GameObject("Placement Plane Alignment Test");
            try
            {
                var horizontalUp = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.2f, 0.2f));
                var horizontalDown = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalDown,
                    new Vector2(0.2f, 0.2f));
                var vertical = CreatePlane(
                    root.transform,
                    PlaneAlignment.Vertical,
                    new Vector2(0.2f, 0.2f));
                var tooSmall = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.08f, 0.5f));

                Assert.IsTrue(ArFoodAnchorPresenter.IsUsablePlacementPlane(horizontalUp));
                Assert.IsFalse(ArFoodAnchorPresenter.IsUsablePlacementPlane(horizontalDown));
                Assert.IsFalse(ArFoodAnchorPresenter.IsUsablePlacementPlane(vertical));
                Assert.IsFalse(ArFoodAnchorPresenter.IsUsablePlacementPlane(tooSmall));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IsSupportedMeshSurface_UpwardNormalWithinDistance_AcceptsOnlyStableSurfaces()
        {
            Assert.IsTrue(ArFoodAnchorPresenter.IsSupportedMeshSurface(Vector3.up, 1f));
            Assert.IsFalse(ArFoodAnchorPresenter.IsSupportedMeshSurface(Vector3.forward, 1f));
            Assert.IsFalse(ArFoodAnchorPresenter.IsSupportedMeshSurface(
                Vector3.up,
                ArFoodAnchorPresenter.MeshFallbackMaxDistanceMeters + 0.1f));
        }

        [Test]
        public void ShouldRequestOptionalArCapability_SupportedOrUnknown_AllowsRuntimeRequest()
        {
            Assert.IsTrue(VisionARyBootstrap.ShouldRequestOptionalArCapability(Supported.Supported));
            Assert.IsTrue(VisionARyBootstrap.ShouldRequestOptionalArCapability(Supported.Unknown));
            Assert.IsFalse(VisionARyBootstrap.ShouldRequestOptionalArCapability(Supported.Unsupported));
        }

        [Test]
        public void TrySelectBestCandidate_ConfirmedPlaneBeatsCloserEstimatedPreview()
        {
            var root = new GameObject("Surface Candidate Test");
            try
            {
                var estimatedHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneEstimated,
                    new Pose(new Vector3(0f, 0f, 0.4f), Quaternion.identity));
                var polygonHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 1.2f), Quaternion.identity));

                Assert.IsTrue(ArSurfacePlacementController.TrySelectBestCandidate(
                    new[] { estimatedHit, polygonHit },
                    out var candidate));
                Assert.AreEqual(TrackableType.PlaneWithinPolygon, candidate.hitType);
                Assert.IsTrue(candidate.canFinalize);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrySelectBestCandidate_KnownNonHorizontalPlane_DoesNotPreview()
        {
            var root = new GameObject("Surface Candidate Vertical Reject Test");
            try
            {
                var verticalHit = CreatePlaneRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 1f), Quaternion.identity),
                    PlaneAlignment.Vertical);

                Assert.IsFalse(ArSurfacePlacementController.TrySelectBestCandidate(
                    new[] { verticalHit },
                    out _));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetPlacementCandidateSortKey_PriorityThenStickyThenTableThenDistance()
        {
            // Priority tier dominates everything else.
            Assert.Less(
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 3.9f, false, false),
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(1, 0.2f, true, true));

            // Within a tier, the currently tracked plane beats a table-classified one.
            Assert.Less(
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 3.9f, true, false),
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 0.2f, false, true));

            // Then table classification beats distance.
            Assert.Less(
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 3.9f, false, true),
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 0.2f, false, false));

            // All else equal, closer wins.
            Assert.Less(
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 0.5f, false, false),
                ArFoodAnchorPresenter.GetPlacementCandidateSortKey(0, 1.5f, false, false));
        }

        [Test]
        public void HasTableClassification_OnlyTableFlaggedPlanes_Accept()
        {
            var root = new GameObject("Classification Test");
            try
            {
                var tablePlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    PlaneClassifications.Table);
                var floorPlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    PlaneClassifications.Floor);

                Assert.IsTrue(ArFoodAnchorPresenter.HasTableClassification(tablePlane));
                Assert.IsFalse(ArFoodAnchorPresenter.HasTableClassification(floorPlane));
                Assert.IsFalse(ArFoodAnchorPresenter.HasTableClassification(null));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrySelectBestCandidate_TableClassifiedPlaneBeatsCloserUnclassifiedPlane()
        {
            var root = new GameObject("Table Preference Test");
            try
            {
                var unclassifiedPlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    idSeed: 11);
                var tablePlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    PlaneClassifications.Table,
                    idSeed: 12);

                var closerUnclassifiedHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 0.5f), Quaternion.identity),
                    unclassifiedPlane);
                var fartherTableHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 1.4f), Quaternion.identity),
                    tablePlane);

                Assert.IsTrue(ArSurfacePlacementController.TrySelectBestCandidate(
                    new[] { closerUnclassifiedHit, fartherTableHit },
                    out var candidate));
                Assert.AreEqual(tablePlane.trackableId, candidate.trackableId);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrySelectBestCandidate_PreferredPlaneBeatsCloserTablePlane()
        {
            var root = new GameObject("Sticky Plane Test");
            try
            {
                var stickyPlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    idSeed: 21);
                var tablePlane = CreatePlane(
                    root.transform,
                    PlaneAlignment.HorizontalUp,
                    new Vector2(0.4f, 0.4f),
                    PlaneClassifications.Table,
                    idSeed: 22);

                var stickyHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 1.4f), Quaternion.identity),
                    stickyPlane);
                var closerTableHit = CreateRaycastHit(
                    root.transform,
                    TrackableType.PlaneWithinPolygon,
                    new Pose(new Vector3(0f, 0f, 0.5f), Quaternion.identity),
                    tablePlane);

                Assert.IsTrue(ArSurfacePlacementController.TrySelectBestCandidate(
                    new[] { stickyHit, closerTableHit },
                    stickyPlane.trackableId,
                    out var candidate));
                Assert.AreEqual(stickyPlane.trackableId, candidate.trackableId);

                // Without a preferred plane the table-classified hit wins again.
                Assert.IsTrue(ArSurfacePlacementController.TrySelectBestCandidate(
                    new[] { stickyHit, closerTableHit },
                    out var unbiased));
                Assert.AreEqual(tablePlane.trackableId, unbiased.trackableId);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveNextState_EstimatedCandidateNeedsExtendedStabilityToReady()
        {
            var state = ArSurfacePlacementState.Scanning;
            var candidate = new ArSurfacePlacementCandidate(
                new Pose(Vector3.forward, Quaternion.identity),
                0.4f,
                TrackableType.PlaneEstimated,
                canFinalize: false);
            const int threshold = 3;
            const int required = threshold * ArSurfacePlacementController.EstimatedSurfaceStableFrameMultiplier;

            for (var i = 0; i < required - 1; i++)
            {
                state = ArSurfacePlacementController.ResolveNextState(state, candidate, 1f, stableFrameThreshold: threshold);
            }

            Assert.AreEqual(ArSurfaceTrackingStatus.Preview, state.status, "Estimated surface must not finalize at the normal threshold.");

            state = ArSurfacePlacementController.ResolveNextState(state, candidate, 1f, stableFrameThreshold: threshold);
            Assert.AreEqual(ArSurfaceTrackingStatus.Ready, state.status, "A long steady hold on an estimated surface should allow placement.");
        }

        [Test]
        public void ResolveNextState_StableFinalCandidateBecomesReady()
        {
            var state = ArSurfacePlacementState.Scanning;
            var candidate = new ArSurfacePlacementCandidate(
                new Pose(Vector3.forward, Quaternion.identity),
                1f,
                TrackableType.PlaneWithinPolygon,
                canFinalize: true);

            for (var i = 0; i < 3; i++)
            {
                state = ArSurfacePlacementController.ResolveNextState(state, candidate, 1f, stableFrameThreshold: 3);
            }

            Assert.AreEqual(ArSurfaceTrackingStatus.Ready, state.status);
            Assert.AreEqual(3, state.stableFrameCount);
        }

        [Test]
        public void ResolveNextState_SmoothsSmallMotionAndResetsLargeJump()
        {
            var state = new ArSurfacePlacementState(
                new Pose(Vector3.zero, Quaternion.identity),
                1f,
                TrackableType.PlaneWithinPolygon,
                2,
                ArSurfaceTrackingStatus.Preview);
            var smallMove = new ArSurfacePlacementCandidate(
                new Pose(new Vector3(0.08f, 0f, 0f), Quaternion.identity),
                1f,
                TrackableType.PlaneWithinPolygon,
                canFinalize: true);

            var smoothed = ArSurfacePlacementController.ResolveNextState(
                state,
                smallMove,
                0.25f,
                stablePositionDeltaMeters: 0.1f,
                stableFrameThreshold: 4);
            Assert.That(smoothed.pose.position.x, Is.GreaterThan(0f).And.LessThan(0.08f));
            Assert.AreEqual(3, smoothed.stableFrameCount);

            var jump = new ArSurfacePlacementCandidate(
                new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity),
                1f,
                TrackableType.PlaneWithinPolygon,
                canFinalize: true);
            var jumped = ArSurfacePlacementController.ResolveNextState(smoothed, jump, 0.25f);
            Assert.AreEqual(ArSurfaceTrackingStatus.Preview, jumped.status);
            Assert.AreEqual(1, jumped.stableFrameCount);
            Assert.That(jumped.pose.position.x, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ShouldShowPlaneForState_PlacementPendingAndHorizontalTracking_ShowsPlane()
        {
            Assert.IsFalse(ArPlaneVisualizationController.ShouldShowPlaneForState(
                showPlanes: true,
                placementPending: false,
                isHorizontalTracking: true));

            Assert.IsTrue(ArPlaneVisualizationController.ShouldShowPlaneForState(
                showPlanes: true,
                placementPending: true,
                isHorizontalTracking: true));
        }

        [Test]
        public void ShouldShowPlaneForState_HiddenOrNonHorizontal_HidesPlane()
        {
            Assert.IsFalse(ArPlaneVisualizationController.ShouldShowPlaneForState(
                showPlanes: false,
                placementPending: true,
                isHorizontalTracking: true));

            Assert.IsFalse(ArPlaneVisualizationController.ShouldShowPlaneForState(
                showPlanes: true,
                placementPending: true,
                isHorizontalTracking: false));
        }

        [Test]
        public void ResolveDetectionStatus_UsesPlacementThenPlaneAvailability()
        {
            Assert.AreEqual(
                ArPlaneDetectionStatus.PlacementActive,
                ArPlaneVisualizationController.ResolveDetectionStatus(
                    hasPlacement: true,
                    hasPlane: false));

            Assert.AreEqual(
                ArPlaneDetectionStatus.PlaneReady,
                ArPlaneVisualizationController.ResolveDetectionStatus(
                    hasPlacement: false,
                    hasPlane: true));

            Assert.AreEqual(
                ArPlaneDetectionStatus.NoPlaneFound,
                ArPlaneVisualizationController.ResolveDetectionStatus(
                    hasPlacement: false,
                    hasPlane: false));
        }

        private static ARRaycastHit CreateRaycastHit(
            Transform parent,
            TrackableType hitType,
            Pose pose,
            ARTrackable trackable = null)
        {
            var transform = new GameObject("Raycast Hit Transform").transform;
            transform.SetParent(parent, false);
            var trackableId = trackable != null ? trackable.trackableId : TrackableId.invalidId;
            var hit = new XRRaycastHit(trackableId, pose, pose.position.magnitude, hitType);
            return new ARRaycastHit(hit, hit.distance, transform, trackable);
        }

        private static ARRaycastHit CreatePlaneRaycastHit(
            Transform parent,
            TrackableType hitType,
            Pose pose,
            PlaneAlignment alignment)
        {
            var plane = CreatePlane(parent, alignment, new Vector2(0.2f, 0.2f));
            return CreateRaycastHit(parent, hitType, pose, plane);
        }

        private static ARPlane CreatePlane(
            Transform parent,
            PlaneAlignment alignment,
            Vector2 size,
            PlaneClassifications classifications = PlaneClassifications.None,
            ulong idSeed = 1)
        {
            var plane = new GameObject("Tracked Plane").AddComponent<ARPlane>();
            plane.transform.SetParent(parent, false);
            SetPlaneData(plane, alignment, size, classifications, idSeed);
            return plane;
        }

        private static void SetPlaneData(
            ARPlane plane,
            PlaneAlignment alignment,
            Vector2 size,
            PlaneClassifications classifications = PlaneClassifications.None,
            ulong idSeed = 1)
        {
            var data = new BoundedPlane(
                new TrackableId(idSeed, (ulong)alignment + (ulong)(size.x * 1000f)),
                TrackableId.invalidId,
                Pose.identity,
                Vector2.zero,
                size,
                alignment,
                TrackingState.Tracking,
                IntPtr.Zero,
                classifications,
                TrackableId.invalidId);

            var setData = typeof(ARTrackable<BoundedPlane, ARPlane>).GetMethod(
                "SetSessionRelativeData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(setData);
            setData.Invoke(plane, new object[] { data });
        }
    }
}
