using MenuViewer.AR;
using MenuViewer.Food;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VisionARyMenu.UI;

namespace VisionARyMenu.QrScan
{
    // Loads the demo menu immediately at launch and shows the tappable list, skipping camera
    // scanning entirely (QR decode of a dense embedded payload proved unreliable scanning off
    // a monitor - see the feature/qr-scan-menu branch for that flow, kept intact for later).
    // Everything downstream of the loaded FoodCatalog is unchanged: ArFoodAnchorPresenter.
    // SelectPendingItem/BeginSurfaceScan is the same call MenuListScreen has always made.
    public sealed class QrMenuFlowController : MonoBehaviour
    {
        // Same 4-dish menu that was going to be QR-encoded, loaded through the same
        // RemoteMenuLoader.LoadFromQrPayload parsing path - only the trigger changed.
        private const string BuiltInMenuJson = @"{
            ""restaurantName"": ""Demo Bistro"",
            ""items"": [
                { ""id"": ""caesar_salad"", ""displayName"": ""Caesar Salad"", ""aliases"": [""caesar salad""], ""caloriesKcal"": 520, ""allergens"": [""Gluten"", ""Dairy"", ""Egg"", ""Fish""], ""dietTags"": [], ""defaultScaleMeters"": 0.12 },
                { ""id"": ""chicken_ramen"", ""displayName"": ""Chicken Ramen"", ""aliases"": [""chicken ramen""], ""caloriesKcal"": 720, ""allergens"": [""Gluten"", ""Egg"", ""Soy""], ""dietTags"": [], ""defaultScaleMeters"": 0.12 },
                { ""id"": ""chocolate_cake"", ""displayName"": ""Chocolate Cake"", ""aliases"": [""chocolate cake""], ""caloriesKcal"": 430, ""allergens"": [""Gluten"", ""Dairy"", ""Egg"", ""Nuts""], ""dietTags"": [""Vegetarian""], ""defaultScaleMeters"": 0.12 },
                { ""id"": ""salmon_sushi"", ""displayName"": ""Salmon Sushi"", ""aliases"": [""salmon sushi""], ""caloriesKcal"": 410, ""allergens"": [""Fish"", ""Soy""], ""dietTags"": [], ""defaultScaleMeters"": 0.12 }
            ]
        }";

        private RemoteMenuLoader menuLoader;
        private ArFoodAnchorPresenter presenter;
        private MenuListScreen menuScreen;

        public void Configure(ArFoodAnchorPresenter anchorPresenter)
        {
            presenter = anchorPresenter;
            menuLoader = gameObject.AddComponent<RemoteMenuLoader>();

            var canvasRoot = CreateCanvas();
            menuScreen = MenuListScreen.Create(canvasRoot.transform);
            menuScreen.ItemTapped += HandleItemTapped;

            // Raw Input.touchCount-based presenter taps (tap-to-place/tap-to-remove) don't
            // automatically respect UGUI hit testing the way Button.onClick does, so without
            // this a tap on a menu row would also register as a world tap underneath it.
            presenter.TapBlockedAt = _ => IsPointerOverUi();

            menuLoader.LoadFromQrPayload(BuiltInMenuJson, HandleMenuLoaded);
        }

        private void HandleItemTapped(string itemId)
        {
            if (presenter.SelectPendingItem(itemId))
            {
                presenter.BeginSurfaceScan();
                menuScreen.SetVisible(false);
            }
        }

        private void HandleMenuLoaded(RemoteMenuLoader.Result result)
        {
            if (!result.success)
            {
                Debug.LogError("[QrMenuFlowController] Built-in menu failed to load: " + result.errorMessage);
                return;
            }

            presenter.SetCatalog(result.catalog);
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
