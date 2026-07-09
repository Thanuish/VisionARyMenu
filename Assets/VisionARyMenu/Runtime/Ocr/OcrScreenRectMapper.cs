using UnityEngine;

namespace MenuViewer.Ocr
{
    public static class OcrScreenRectMapper
    {
        public const float DefaultInsetFraction = 0.06f;

        public static Rect NormalizedToScreenRect(
            Rect normalized,
            int screenWidth,
            int screenHeight,
            OcrRecognitionResult status,
            float insetFraction = DefaultInsetFraction)
        {
            return NormalizedToScreenRect(
                normalized,
                screenWidth,
                screenHeight,
                status.imageWidth,
                status.imageHeight,
                insetFraction);
        }

        public static Rect NormalizedToScreenRect(
            Rect normalized,
            int screenWidth,
            int screenHeight,
            int imageWidth,
            int imageHeight,
            float insetFraction = DefaultInsetFraction)
        {
            var displayRect = ResolveAspectFillRect(screenWidth, screenHeight, imageWidth, imageHeight);
            // Native OCR rects are upright image coordinates with a top-left origin, matching OnGUI.
            var rect = new Rect(
                displayRect.x + normalized.x * displayRect.width,
                displayRect.y + normalized.y * displayRect.height,
                normalized.width * displayRect.width,
                normalized.height * displayRect.height);

            return Inset(rect, insetFraction);
        }

        public static Rect ResolveAspectFillRect(int screenWidth, int screenHeight, int imageWidth, int imageHeight)
        {
            screenWidth = Mathf.Max(1, screenWidth);
            screenHeight = Mathf.Max(1, screenHeight);
            imageWidth = imageWidth > 0 ? imageWidth : screenWidth;
            imageHeight = imageHeight > 0 ? imageHeight : screenHeight;

            var screenAspect = screenWidth / (float)screenHeight;
            var imageAspect = imageWidth / (float)imageHeight;

            if (imageAspect > screenAspect)
            {
                var displayedWidth = screenHeight * imageAspect;
                return new Rect((screenWidth - displayedWidth) * 0.5f, 0f, displayedWidth, screenHeight);
            }

            var displayedHeight = screenWidth / imageAspect;
            return new Rect(0f, (screenHeight - displayedHeight) * 0.5f, screenWidth, displayedHeight);
        }

        private static Rect Inset(Rect rect, float insetFraction)
        {
            var inset = Mathf.Clamp(insetFraction, 0f, 0.45f);
            if (inset <= 0f)
            {
                return rect;
            }

            var insetX = rect.width * inset;
            var insetY = rect.height * inset;
            return new Rect(
                rect.x + insetX,
                rect.y + insetY,
                rect.width - insetX * 2f,
                rect.height - insetY * 2f);
        }
    }
}
