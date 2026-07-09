using UnityEngine;
using UnityEngine.UI;

namespace VisionARyMenu.UI
{
    // Minimal functional overlay shown while scanning/fetching a QR-linked menu. The camera
    // feed itself is already visible full-screen via ARCameraBackground (set up in
    // VisionARyBootstrap.cs) - this only draws a scan-frame cue, status text, and a retry
    // button on failure. Deliberately plain UGUI (default font/colors), not the polished
    // design-system pass from the original brief.
    public sealed class QrScanScreen : MonoBehaviour
    {
        private Text statusText;
        private Button retryButton;
        private GameObject retryRoot;

        public static QrScanScreen Create(Transform parent)
        {
            var root = new GameObject("QR Scan Screen", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var screen = root.AddComponent<QrScanScreen>();
            screen.Build();
            return screen;
        }

        private void Build()
        {
            var frame = CreateImage("Scan Frame", transform, new Color(1f, 1f, 1f, 0.9f));
            var frameRect = frame.rectTransform;
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(260f, 260f);
            frame.color = new Color(0f, 0f, 0f, 0f);

            AddBorder(frameRect, 4f, new Color(1f, 1f, 1f, 0.95f));

            statusText = CreateText("Status Text", transform, "Point the camera at the table's QR code.");
            var statusRect = statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.1f, 0f);
            statusRect.anchorMax = new Vector2(0.9f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 140f);
            statusRect.sizeDelta = new Vector2(0f, 80f);

            retryRoot = new GameObject("Retry Button", typeof(RectTransform), typeof(Image), typeof(Button));
            retryRoot.transform.SetParent(transform, false);
            var retryRect = (RectTransform)retryRoot.transform;
            retryRect.anchorMin = new Vector2(0.5f, 0f);
            retryRect.anchorMax = new Vector2(0.5f, 0f);
            retryRect.pivot = new Vector2(0.5f, 0f);
            retryRect.anchoredPosition = new Vector2(0f, 60f);
            retryRect.sizeDelta = new Vector2(200f, 56f);
            retryRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
            retryButton = retryRoot.GetComponent<Button>();

            var retryLabel = CreateText("Label", retryRoot.transform, "Try again");
            retryLabel.color = Color.black;
            retryLabel.alignment = TextAnchor.MiddleCenter;
            var retryLabelRect = retryLabel.rectTransform;
            retryLabelRect.anchorMin = Vector2.zero;
            retryLabelRect.anchorMax = Vector2.one;
            retryLabelRect.offsetMin = Vector2.zero;
            retryLabelRect.offsetMax = Vector2.zero;

            retryRoot.SetActive(false);
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        public void ShowRetry(string message, System.Action onRetry)
        {
            SetStatus(message);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => onRetry?.Invoke());
            retryRoot.SetActive(true);
        }

        public void HideRetry()
        {
            retryRoot.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private static void AddBorder(RectTransform frameRect, float thickness, Color color)
        {
            CreateBorderBar(frameRect, color, new Vector2(0f, 1f), new Vector2(1f, 1f), thickness, true);
            CreateBorderBar(frameRect, color, new Vector2(0f, 0f), new Vector2(1f, 0f), thickness, true);
            CreateBorderBar(frameRect, color, new Vector2(0f, 0f), new Vector2(0f, 1f), thickness, false);
            CreateBorderBar(frameRect, color, new Vector2(1f, 0f), new Vector2(1f, 1f), thickness, false);
        }

        private static void CreateBorderBar(RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, float thickness, bool horizontal)
        {
            var bar = CreateImage("Border", parent, color);
            var rect = bar.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = horizontal ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);
            rect.anchoredPosition = Vector2.zero;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            // "Arial.ttf" was renamed to "LegacyRuntime.ttf" as a builtin resource in Unity
            // 2022.2+; the old name throws instead of returning a font.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            return text;
        }
    }
}
