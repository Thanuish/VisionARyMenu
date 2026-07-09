using MenuViewer.AR;
using MenuViewer.Food;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using VisionARyMenu.QrScan;

namespace VisionARyMenu.Bootstrap
{
    // Builds the ported AR pipeline plus the demo-menu -> tappable-list flow
    // (QrMenuFlowController). The presenter starts against an empty catalog and gets the real
    // one immediately once QrMenuFlowController loads the built-in demo menu — see
    // ArFoodAnchorPresenter.SelectPendingItem/BeginSurfaceScan for how a tapped dish reaches
    // the (already Phase-1-verified) placement flow. QR-code scanning as the menu's entry
    // point lives on the feature/qr-scan-menu branch.
    public static class VisionARyBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntime()
        {
            if (Object.FindAnyObjectByType<ArFoodAnchorPresenter>() != null)
            {
                return;
            }

            var catalog = CreateEmptyCatalog();

            var lightObject = new GameObject("Directional Light");
            Object.DontDestroyOnLoad(lightObject);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var arSessionObject = new GameObject("AR Session");
            var arSession = arSessionObject.AddComponent<ARSession>();
            arSession.matchFrameRateRequested = true;
            arSession.requestedTrackingMode = TrackingMode.PositionAndRotation;
            arSessionObject.AddComponent<ARInputManager>();
            Object.DontDestroyOnLoad(arSessionObject);

            var originObject = new GameObject("XR Origin");
            Object.DontDestroyOnLoad(originObject);
            var origin = originObject.AddComponent<XROrigin>();
            var raycastManager = originObject.AddComponent<ARRaycastManager>();
            var planeManager = originObject.AddComponent<ARPlaneManager>();
            // Detection stays off until BeginSurfaceScan is called below, mirroring the real
            // app's "camera is still aimed elsewhere when a dish is selected" gating.
            planeManager.requestedDetectionMode = PlaneDetectionMode.None;
            var anchorManager = originObject.AddComponent<ARAnchorManager>();
            var planeVisualizer = originObject.AddComponent<ArPlaneVisualizationController>();
            planeVisualizer.Configure(planeManager);
            planeVisualizer.ShowPlanes = true;
            var meshManager = TryCreateMeshManager(originObject);

            var cameraObject = new GameObject("AR Camera");
            cameraObject.transform.SetParent(originObject.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ArCameraPoseDriver>();
            var cameraManager = cameraObject.AddComponent<ARCameraManager>();
            cameraManager.autoFocusRequested = true;
            cameraObject.AddComponent<ARCameraBackground>();
            TryConfigureEnvironmentDepth(cameraObject);
            origin.Camera = camera;

            var servicesObject = new GameObject("VisionARy Services");
            Object.DontDestroyOnLoad(servicesObject);

            var surfacePlacement = servicesObject.AddComponent<ArSurfacePlacementController>();
            surfacePlacement.Configure(raycastManager, camera, meshManager);

            var anchorPresenter = servicesObject.AddComponent<ArFoodAnchorPresenter>();
            anchorPresenter.Configure(catalog, raycastManager, camera, null, anchorManager, meshManager, surfacePlacement);
            planeVisualizer.BindPlacement(anchorPresenter);

            var flowController = servicesObject.AddComponent<QrMenuFlowController>();
            flowController.Configure(anchorPresenter);

            Debug.Log("[VisionARyBootstrap] Running: demo menu loaded, tap a dish to place it.");
        }

        private static FoodCatalog CreateEmptyCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<FoodCatalog>();
            catalog.name = "Empty Starter Catalog";
            catalog.Initialize(System.Array.Empty<FoodItemDefinition>());
            return catalog;
        }

        private static void TryConfigureEnvironmentDepth(GameObject cameraObject)
        {
            var occlusionSubsystem = GetLoadedSubsystem<XROcclusionSubsystem>();
            var descriptor = occlusionSubsystem?.subsystemDescriptor;
            if (descriptor == null
                || !ShouldRequestOptionalArCapability(descriptor.environmentDepthImageSupported))
            {
                return;
            }

            var occlusionManager = cameraObject.AddComponent<AROcclusionManager>();
            occlusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Fastest;
            occlusionManager.environmentDepthTemporalSmoothingRequested =
                ShouldRequestOptionalArCapability(descriptor.environmentDepthTemporalSmoothingSupported);
        }

        private static ARMeshManager TryCreateMeshManager(GameObject originObject)
        {
            if (GetLoadedSubsystem<XRMeshSubsystem>() == null)
            {
                return null;
            }

            var meshObject = new GameObject("AR Mesh Manager");
            meshObject.transform.SetParent(originObject.transform, false);

            var meshManager = meshObject.AddComponent<ARMeshManager>();
            meshManager.meshPrefab = CreateInvisibleMeshColliderPrefab(meshObject.transform);
            meshManager.density = 0.25f;
            meshManager.normals = true;
            meshManager.tangents = false;
            meshManager.textureCoordinates = false;
            meshManager.colors = false;
            meshManager.concurrentQueueSize = 2;
#if UNITY_6000_4_OR_NEWER
            meshManager.submeshClassificationEnabled = true;
#endif
            return meshManager;
        }

        public static bool ShouldRequestOptionalArCapability(Supported support)
        {
            return support == Supported.Supported || support == Supported.Unknown;
        }

        private static MeshFilter CreateInvisibleMeshColliderPrefab(Transform parent)
        {
            var meshPrefabObject = new GameObject("AR Mesh Collider Prefab");
            meshPrefabObject.transform.SetParent(parent, false);
            meshPrefabObject.SetActive(false);
            var meshFilter = meshPrefabObject.AddComponent<MeshFilter>();
            meshPrefabObject.AddComponent<MeshCollider>();
            return meshFilter;
        }

        private static TSubsystem GetLoadedSubsystem<TSubsystem>() where TSubsystem : class, ISubsystem
        {
            var xrManager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            var loader = xrManager != null ? xrManager.activeLoader : null;
            return loader != null ? loader.GetLoadedSubsystem<TSubsystem>() : null;
        }
    }
}
