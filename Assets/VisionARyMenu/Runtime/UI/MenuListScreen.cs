using System;
using MenuViewer.Food;
using UnityEngine;
using UnityEngine.UI;

namespace VisionARyMenu.UI
{
    // Minimal functional tappable dish list (the smallest slice of "Mode A" needed to prove
    // QR scan -> real catalog -> AR placement end-to-end). Plain UGUI, default font/colors -
    // not the polished design-system pass from the original brief.
    public sealed class MenuListScreen : MonoBehaviour
    {
        private RectTransform content;
        private Text titleText;

        public event Action<string> ItemTapped;

        public static MenuListScreen Create(Transform parent)
        {
            var root = new GameObject("Menu List Screen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var screen = root.AddComponent<MenuListScreen>();
            screen.Build();
            return screen;
        }

        private void Build()
        {
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(transform, false);
            var backdropRect = (RectTransform)backdrop.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            titleText = CreateText(transform, "Menu");
            titleText.fontSize = 34;
            titleText.fontStyle = FontStyle.Bold;
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(-40f, 60f);

            var scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(transform, false);
            var scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0.05f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.85f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
        }

        public void SetItems(string restaurantName, System.Collections.Generic.IReadOnlyList<FoodItemDefinition> items)
        {
            titleText.text = string.IsNullOrWhiteSpace(restaurantName) ? "Menu" : restaurantName;

            for (var i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

            foreach (var item in items)
            {
                CreateRow(item);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void CreateRow(FoodItemDefinition item)
        {
            var rowGo = new GameObject(item.Id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            rowGo.transform.SetParent(content, false);
            rowGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);
            rowGo.GetComponent<LayoutElement>().preferredHeight = 72f;

            var button = rowGo.GetComponent<Button>();
            var itemId = item.Id;
            button.onClick.AddListener(() => ItemTapped?.Invoke(itemId));

            var nameText = CreateText(rowGo.transform, item.DisplayName);
            nameText.color = Color.black;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontSize = 26;
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(0.7f, 1f);
            nameRect.offsetMin = new Vector2(16f, 0f);
            nameRect.offsetMax = Vector2.zero;

            var caloriesText = CreateText(rowGo.transform, item.CaloriesKcal > 0 ? $"{item.CaloriesKcal} kcal" : string.Empty);
            caloriesText.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            caloriesText.alignment = TextAnchor.MiddleRight;
            caloriesText.fontSize = 20;
            var caloriesRect = caloriesText.rectTransform;
            caloriesRect.anchorMin = new Vector2(0.7f, 0f);
            caloriesRect.anchorMax = new Vector2(1f, 1f);
            caloriesRect.offsetMin = Vector2.zero;
            caloriesRect.offsetMax = new Vector2(-16f, 0f);
        }

        private static Text CreateText(Transform parent, string content)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            // "Arial.ttf" was renamed to "LegacyRuntime.ttf" as a builtin resource in Unity
            // 2022.2+; the old name throws instead of returning a font.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.text = content;
            return text;
        }
    }
}
