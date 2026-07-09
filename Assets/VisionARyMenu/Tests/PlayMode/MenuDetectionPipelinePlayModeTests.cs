using System;
using System.Collections;
using System.Collections.Generic;
using MenuViewer.AR;
using MenuViewer.Food;
using MenuViewer.Ocr;
using MenuViewer.Overlay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MenuViewer.Tests.PlayMode
{
    public sealed class MenuDetectionPipelinePlayModeTests
    {
        private static readonly Pose SurfacePose = new Pose(new Vector3(0.2f, 0f, 0.8f), Quaternion.identity);

        [UnityTest]
        public IEnumerator Pipeline_PublishesStableMatchesFromFakeOcr()
        {
            var rig = ArRig.Create("Pipeline Test");

            rig.Ocr.Emit(new[] { Line("Caesar Salad 9.90", 0.2f, 0.1, 1) });
            yield return null;
            Assert.AreEqual(1, rig.Pipeline.CurrentCandidateMatches.Count);
            Assert.AreEqual("caesar_salad", rig.Pipeline.CurrentCandidateMatches[0].itemId);
            Assert.AreEqual(0, rig.Pipeline.CurrentStableMatches.Count);

            rig.Ocr.Emit(new[] { Line("Caesar Salad 9.90", 0.2f, 0.6, 2) });
            yield return null;

            Assert.AreEqual(1, rig.Pipeline.CurrentStableMatches.Count);
            Assert.AreEqual("caesar_salad", rig.Pipeline.CurrentStableMatches[0].itemId);

            rig.Destroy();
        }

        [UnityTest]
        public IEnumerator Pipeline_PublishesRawUnknownTextWithoutFoodMatch()
        {
            var rig = ArRig.Create("Raw OCR Test");

            rig.Ocr.Emit(new[] { Line("Sparkling Water 4.50", 0.2f, 0.1, 1) });
            yield return null;

            Assert.AreEqual(1, rig.Pipeline.CurrentRawLines.Count);
            Assert.AreEqual("Sparkling Water 4.50", rig.Pipeline.CurrentRawLines[0].text);
            Assert.AreEqual(0, rig.Pipeline.CurrentCandidateMatches.Count);
            Assert.AreEqual(0, rig.Pipeline.CurrentStableMatches.Count);

            rig.Destroy();
        }

        // Overlay_FormatsTogglesIntoBadgeText intentionally not ported: it exercised
        // MenuOverlayController.FormatBadgeText, and no UI (MenuOverlayController or its
        // replacement) is ported in Phase 1. Re-add an equivalent test once Mode A's real UI
        // has a badge/info-card formatting method to test.

        [UnityTest]
        public IEnumerator ArPresenter_CreatesProceduralModelOnlyForTappedKnownDish()
        {
            var rig = ArRig.Create("AR Presenter Test");
            rig.EmitDish("Caesar Salad 9.90");
            yield return null;

            Assert.IsNull(GameObject.Find("Caesar Salad AR View"));

            rig.TapLine();
            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsFalse(rig.Presenter.HasPlacement);
            Assert.IsNull(GameObject.Find("Caesar Salad AR View"));

            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            var view = GameObject.Find("Caesar Salad AR View");
            Assert.IsNotNull(view);
            Assert.IsNotNull(FindChild(view.transform, "Lettuce"));
            Assert.IsTrue(rig.Presenter.HasPlacement);
            Assert.IsNull(rig.Presenter.PendingItemId);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_ProceduralFallbackUsesDishSpecificGeometry()
        {
            var rig = ArRig.Create("AR Procedural Model Test");
            rig.EmitDish("Margherita Pizza 14.50");
            yield return null;

            rig.TapLine();
            Assert.AreEqual("margherita_pizza", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));

            var pizzaView = GameObject.Find("Margherita Pizza AR View");
            Assert.IsNotNull(pizzaView);
            Assert.IsNotNull(FindChild(pizzaView.transform, "Pizza Base"));
            Assert.IsNotNull(FindChild(pizzaView.transform, "Tomato Sauce"));

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_ModelPrefabCreatesBottomCenteredScaledView()
        {
            var prefabSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefabSource.name = "Known Dish Prefab";
            var catalog = ScriptableObject.CreateInstance<FoodCatalog>();
            catalog.Initialize(new[]
            {
                FoodItemDefinition.Create(
                    "known_dish",
                    "Known Dish",
                    new[] { "known dish" },
                    300,
                    new AllergyTag[0],
                    new DietTag[0],
                    defaultScaleMeters: 0.25f,
                    modelPrefab: prefabSource)
            });

            var rig = ArRig.Create("AR Prefab Model Test", catalog);
            rig.EmitDish("Known Dish 12.00");
            yield return null;

            rig.TapLine();
            Assert.AreEqual("known_dish", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));

            var view = GameObject.Find("Known Dish AR View");
            Assert.IsNotNull(view);
            Assert.AreEqual(Vector3.one * 0.25f, view.transform.localScale);

            var prefabChild = FindChild(view.transform, "Known Dish Prefab");
            Assert.IsNotNull(prefabChild);
            Assert.That(prefabChild.localPosition.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.IsNotNull(view.GetComponentInChildren<Collider>());

            UnityEngine.Object.Destroy(prefabSource);
            UnityEngine.Object.Destroy(catalog);
            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_PlacementPersistsAfterSelectionClears()
        {
            var rig = ArRig.Create("AR Persist Test");
            rig.SelectCaesarSalad();
            yield return null;

            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            Assert.IsTrue(rig.Presenter.HasPlacement);

            rig.TapController.Clear();
            yield return null;

            Assert.IsTrue(rig.Presenter.HasPlacement, "Placement should survive selection clear.");
            Assert.IsNotNull(GameObject.Find("Caesar Salad AR View"));

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_TappingDifferentDishReplacesPlacement()
        {
            var rig = ArRig.Create("AR Replace Test");
            rig.Ocr.Emit(new[]
            {
                Line("Caesar Salad 9.90", 0.2f, 0.1, 1),
                Line("Caesar Salad 9.90", 0.2f, 0.6, 2),
                Line("Margherita Pizza 14.50", 0.5f, 0.1, 3),
                Line("Margherita Pizza 14.50", 0.5f, 0.6, 4)
            });
            yield return null;

            rig.TapLine(250f);
            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            Assert.IsNotNull(GameObject.Find("Caesar Salad AR View"));

            rig.TapLine(550f, now: 0.8);
            Assert.AreEqual("margherita_pizza", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            yield return null;

            Assert.IsNull(GameObject.Find("Caesar Salad AR View"), "Old placement should be removed.");
            Assert.IsNotNull(GameObject.Find("Margherita Pizza AR View"));
            Assert.IsTrue(rig.Presenter.HasPlacement);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_ClearPlacementRemovesView()
        {
            var rig = ArRig.Create("AR Clear Test");
            rig.SelectCaesarSalad();
            yield return null;

            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            Assert.IsTrue(rig.Presenter.HasPlacement);

            rig.Presenter.ClearPlacement();
            yield return null;

            Assert.IsFalse(rig.Presenter.HasPlacement);
            Assert.IsNull(GameObject.Find("Caesar Salad AR View"));

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPlaneVisualizer_BoundPresenterTracksPendingAndPlacedState()
        {
            var rig = ArRig.Create("AR Visualizer State Test");
            var visualizer = rig.Services.AddComponent<ArPlaneVisualizationController>();
            visualizer.BindPlacement(rig.Presenter);
            Assert.AreEqual(ArPlaneDetectionStatus.NoPlaneFound, visualizer.CurrentStatus);
            Assert.IsFalse(visualizer.PlaneHighlightsActive);

            rig.SelectCaesarSalad();
            yield return null;

            Assert.IsTrue(visualizer.PlacementPending);
            Assert.IsFalse(visualizer.HasPlacement);
            Assert.IsFalse(visualizer.PlaneHighlightsActive);
            Assert.AreEqual(ArPlaneDetectionStatus.NoPlaneFound, visualizer.CurrentStatus);

            visualizer.ShowPlanes = true;
            Assert.IsTrue(visualizer.PlaneHighlightsActive);
            visualizer.ShowPlanes = false;

            Assert.IsTrue(rig.Presenter.TryPlacePending(Pose.identity));
            Assert.IsFalse(visualizer.PlacementPending);
            Assert.IsTrue(visualizer.HasPlacement);
            Assert.IsFalse(visualizer.PlaneHighlightsActive);
            Assert.AreEqual(ArPlaneDetectionStatus.PlacementActive, visualizer.CurrentStatus);

            rig.Presenter.ClearPlacement();
            yield return null;

            Assert.IsFalse(visualizer.PlacementPending);
            Assert.IsFalse(visualizer.HasPlacement);
            Assert.IsFalse(visualizer.PlaneHighlightsActive);
            Assert.AreEqual(ArPlaneDetectionStatus.NoPlaneFound, visualizer.CurrentStatus);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_NoValidPlacementHit_LeavesPlacementPendingAndSetsMissHint()
        {
            var rig = ArRig.Create("AR Missed Placement Test");
            rig.SelectCaesarSalad();
            yield return null;

            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsFalse(rig.Presenter.TryPlaceFromRaycastHits(Array.Empty<ARRaycastHit>()));
            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsFalse(rig.Presenter.HasPlacement);
            Assert.IsTrue(rig.Presenter.LastPlacementAttemptMissed);
            Assert.IsNull(GameObject.Find("Caesar Salad AR View"));

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_PlanePolygonRaycastPlacesPendingDish()
        {
            var rig = ArRig.Create("AR Plane Polygon Test");
            rig.SelectCaesarSalad();
            yield return null;

            var hit = CreateRaycastHit(rig.Services.transform, TrackableType.PlaneWithinPolygon, SurfacePose);

            Assert.IsTrue(rig.Presenter.TryPlaceFromRaycastHits(new[] { hit }));
            Assert.IsTrue(rig.Presenter.HasPlacement);
            Assert.IsNull(rig.Presenter.PendingItemId);
            var view = GameObject.Find("Caesar Salad AR View");
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.transform.parent);
            Assert.AreEqual("AR Anchor (caesar_salad)", view.transform.parent.name);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_PlaneBoundsRaycastPlacesPendingDish()
        {
            var rig = ArRig.Create("AR Plane Bounds Test");
            rig.SelectCaesarSalad();
            yield return null;

            var hit = CreateRaycastHit(rig.Services.transform, TrackableType.PlaneWithinBounds, SurfacePose);

            Assert.IsTrue(rig.Presenter.TryPlaceFromRaycastHits(new[] { hit }));
            Assert.IsTrue(rig.Presenter.HasPlacement);
            Assert.IsNull(rig.Presenter.PendingItemId);
            Assert.IsNotNull(GameObject.Find("Caesar Salad AR View"));

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_InfinitePlaneWithoutKnownPlaneDoesNotPlacePendingDish()
        {
            var rig = ArRig.Create("AR Infinite Plane Test");
            rig.SelectCaesarSalad();
            yield return null;

            var hit = CreateRaycastHit(rig.Services.transform, TrackableType.PlaneWithinInfinity, SurfacePose);

            Assert.IsFalse(rig.Presenter.TryPlaceFromRaycastHits(new[] { hit }));
            Assert.IsFalse(rig.Presenter.HasPlacement);
            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.LastPlacementAttemptMissed);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_NonPlaneRaycastHitsDoNotPlacePendingDish()
        {
            var rig = ArRig.Create("AR Non-Plane Reject Test");
            rig.SelectCaesarSalad();
            yield return null;

            foreach (var hitType in new[] { TrackableType.PlaneEstimated, TrackableType.FeaturePoint, TrackableType.Depth })
            {
                var hit = CreateRaycastHit(rig.Services.transform, hitType, SurfacePose);
                Assert.IsFalse(rig.Presenter.TryPlaceFromRaycastHits(new[] { hit }), hitType.ToString());
                Assert.IsFalse(rig.Presenter.HasPlacement, hitType.ToString());
                Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId, hitType.ToString());
                Assert.IsTrue(rig.Presenter.LastPlacementAttemptMissed, hitType.ToString());
            }

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_SurfacePreviewStateCreatesGhostPreview()
        {
            var rig = ArRig.Create("AR Surface Preview Test", withSurfaceController: true);
            rig.SelectCaesarSalad();
            rig.Surface.SetCurrentState(new ArSurfacePlacementState(
                SurfacePose, 0.55f, TrackableType.PlaneEstimated, 0, ArSurfaceTrackingStatus.Preview));
            yield return null;

            Assert.IsTrue(rig.Presenter.HasPreview);
            Assert.IsNotNull(GameObject.Find("AR Placement Reticle"));
            Assert.IsNotNull(GameObject.Find("Caesar Salad AR Preview"));
            Assert.IsFalse(rig.Presenter.HasPlacement);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_SurfacePreviewStateDoesNotPlacePendingDish()
        {
            var rig = ArRig.Create("AR Surface Preview Reject Test", withSurfaceController: true);
            rig.SelectCaesarSalad();
            rig.Surface.SetCurrentState(new ArSurfacePlacementState(
                SurfacePose, 0.55f, TrackableType.PlaneEstimated, 0, ArSurfaceTrackingStatus.Preview));
            yield return null;

            Assert.IsFalse(rig.Presenter.TryPlaceFromSurfaceState());
            Assert.IsFalse(rig.Presenter.HasPlacement);
            Assert.AreEqual("caesar_salad", rig.Presenter.PendingItemId);
            Assert.IsTrue(rig.Presenter.LastPlacementAttemptMissed);

            rig.Destroy();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArPresenter_SurfaceReadyStatePlacesPendingDish()
        {
            var rig = ArRig.Create("AR Surface Ready Test", withSurfaceController: true);
            rig.SelectCaesarSalad();
            rig.Surface.SetCurrentState(new ArSurfacePlacementState(
                SurfacePose,
                0.95f,
                TrackableType.PlaneWithinPolygon,
                ArSurfacePlacementController.DefaultStableFrameThreshold,
                ArSurfaceTrackingStatus.Ready));
            yield return null;

            Assert.IsTrue(rig.Presenter.TryPlaceFromSurfaceState());
            Assert.IsTrue(rig.Presenter.HasPlacement);
            Assert.IsNull(rig.Presenter.PendingItemId);
            Assert.IsFalse(rig.Presenter.HasPreview);
            Assert.IsNotNull(GameObject.Find("Caesar Salad AR View"));

            rig.Destroy();
            yield return null;
        }

        // Builds the standard pipeline + tap controller + presenter stack every AR test needs.
        private sealed class ArRig
        {
            public GameObject Services;
            public ManualOcrService Ocr;
            public MenuDetectionPipeline Pipeline;
            public FoodCatalog Catalog;
            public MenuTapSelectionController TapController;
            public ArFoodAnchorPresenter Presenter;
            public ArSurfacePlacementController Surface;

            public static ArRig Create(string name, FoodCatalog catalog = null, bool withSurfaceController = false)
            {
                var rig = new ArRig
                {
                    Services = new GameObject(name),
                    Ocr = new ManualOcrService(),
                    Catalog = catalog != null ? catalog : DemoFoodCatalogFactory.CreateCatalog()
                };
                rig.Pipeline = rig.Services.AddComponent<MenuDetectionPipeline>();
                rig.Pipeline.Configure(rig.Ocr, rig.Catalog);
                rig.TapController = rig.Services.AddComponent<MenuTapSelectionController>();
                rig.TapController.Configure(rig.Pipeline, rig.Catalog);

                if (withSurfaceController)
                {
                    rig.Surface = rig.Services.AddComponent<ArSurfacePlacementController>();
                    rig.Surface.Configure(null, null);
                }

                rig.Presenter = rig.Services.AddComponent<ArFoodAnchorPresenter>();
                rig.Presenter.Configure(rig.Catalog, null, null, rig.TapController, null, null, rig.Surface);
                return rig;
            }

            public void EmitDish(string text, float normalizedY = 0.2f)
            {
                Ocr.Emit(new[]
                {
                    Line(text, normalizedY, 0.1, 1),
                    Line(text, normalizedY, 0.6, 2)
                });
            }

            public void TapLine(float guiY = 250f, double now = 0.7)
            {
                TapController.HandleTap(
                    new Vector2(300f, guiY),
                    1000,
                    1000,
                    Pipeline.CurrentRawLines,
                    Pipeline.CurrentCandidateMatches,
                    Catalog,
                    now);
            }

            public void SelectCaesarSalad()
            {
                EmitDish("Caesar Salad 9.90");
                TapLine();
            }

            public void Destroy()
            {
                UnityEngine.Object.Destroy(Services);
            }
        }

        private sealed class ManualOcrService : IOcrService
        {
            public event Action<IReadOnlyList<RecognizedTextLine>> OnLinesRecognized;

            public void Start()
            {
            }

            public void Stop()
            {
            }

            public void Emit(IReadOnlyList<RecognizedTextLine> lines)
            {
                OnLinesRecognized?.Invoke(lines);
            }
        }

        private static RecognizedTextLine Line(string text, float normalizedY, double timestamp, int frameId)
        {
            return new RecognizedTextLine(text, 1f, new Rect(0.1f, normalizedY, 0.4f, 0.1f), timestamp, frameId);
        }

        private static ARRaycastHit CreateRaycastHit(Transform parent, TrackableType hitType, Pose pose)
        {
            var transform = new GameObject("Raycast Hit Transform").transform;
            transform.SetParent(parent, false);
            var hit = new XRRaycastHit(TrackableId.invalidId, pose, pose.position.magnitude, hitType);
            return new ARRaycastHit(hit, hit.distance, transform, null);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
