using MenuViewer.AR;
using MenuViewer.Food;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VisionARyMenu.UI;

namespace VisionARyMenu.QrScan
{
    // Glues QR scanning -> remote menu fetch -> tappable list -> the existing (already
    // verified) AR placement flow. State machine: Scanning -> Fetching -> ShowingMenu, after
    // which ArFoodAnchorPresenter.SelectPendingItem/BeginSurfaceScan (already exercised in
    // Phase 1) take over placement exactly as they did for the hardcoded stub dish.
    public sealed class QrMenuFlowController : MonoBehaviour
    {
        private enum FlowState
        {
            Scanning,
            Fetching,
            ShowingMenu
        }

        private ArQrScanService qrService;
        private RemoteMenuLoader menuLoader;
        private ArFoodAnchorPresenter presenter;
        private QrScanScreen scanScreen;
        private MenuListScreen menuScreen;
        private FlowState state;

        public void Configure(ArQrScanService qrScanService, ArFoodAnchorPresenter anchorPresenter)
        {
            qrService = qrScanService;
            presenter = anchorPresenter;
            menuLoader = gameObject.AddComponent<RemoteMenuLoader>();

            var canvasRoot = CreateCanvas();
            scanScreen = QrScanScreen.Create(canvasRoot.transform);
            menuScreen = MenuListScreen.Create(canvasRoot.transform);
            menuScreen.SetVisible(false);
            menuScreen.ItemTapped += HandleItemTapped;

            // Raw Input.touchCount-based presenter taps (tap-to-place/tap-to-remove) don't
            // automatically respect UGUI hit testing the way Button.onClick does, so without
            // this a tap on a menu row would also register as a world tap underneath it -
            // exactly what MenuViewer's IMGUI-era TapBlockedAt/IsGuiPointOverUi wiring guarded
            // against, just via UGUI's EventSystem instead of a custom blocking-rect registry.
            presenter.TapBlockedAt = _ => IsPointerOverUi();

            EnterScanning();
        }

        private void HandleItemTapped(string itemId)
        {
            if (state != FlowState.ShowingMenu)
            {
                return;
            }

            if (presenter.SelectPendingItem(itemId))
            {
                presenter.BeginSurfaceScan();
                menuScreen.SetVisible(false);
            }
        }

        private void EnterScanning()
        {
            state = FlowState.Scanning;
            menuScreen.SetVisible(false);
            scanScreen.SetVisible(true);
            scanScreen.HideRetry();
            scanScreen.SetStatus("Point the camera at the table's QR code.");

            qrService.QrCodeDecoded -= HandleQrCodeDecoded;
            qrService.QrCodeDecoded += HandleQrCodeDecoded;
            qrService.StartScanning();
        }

        private void HandleQrCodeDecoded(string payload)
        {
            state = FlowState.Fetching;
            scanScreen.SetStatus("Loading menu...");
            menuLoader.LoadFromQrPayload(payload, HandleMenuLoaded);
        }

        private void HandleMenuLoaded(RemoteMenuLoader.Result result)
        {
            if (!result.success)
            {
                scanScreen.ShowRetry(result.errorMessage, EnterScanning);
                return;
            }

            state = FlowState.ShowingMenu;
            presenter.SetCatalog(result.catalog);
            scanScreen.SetVisible(false);
            menuScreen.SetItems(result.restaurantName, result.catalog.Items);
            menuScreen.SetVisible(true);
        }

        private static bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (Input.touchCount > 0)
            {
                return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private static GameObject CreateCanvas()
        {
            if (EventSystem.current == null)
            {
                var eventSystemObject = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                Object.DontDestroyOnLoad(eventSystemObject);
            }

            var canvasObject = new GameObject("VisionARy UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject;
        }
    }
}
