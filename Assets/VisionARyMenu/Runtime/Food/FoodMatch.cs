using System;
using UnityEngine;

namespace MenuViewer.Food
{
    [Serializable]
    public struct FoodMatch
    {
        public string itemId;
        public string displayName;
        public string sourceText;
        public float score;
        public Rect normalizedRect;
        public double timestamp;
        public int frameId;
        public bool isStable;
        public int hitCount;
        public float visibleAlpha;

        public FoodMatch(
            string itemId,
            string displayName,
            string sourceText,
            float score,
            Rect normalizedRect,
            double timestamp,
            int frameId,
            bool isStable = false,
            int hitCount = 1,
            float visibleAlpha = 1f)
        {
            this.itemId = itemId;
            this.displayName = displayName;
            this.sourceText = sourceText;
            this.score = score;
            this.normalizedRect = normalizedRect;
            this.timestamp = timestamp;
            this.frameId = frameId;
            this.isStable = isStable;
            this.hitCount = hitCount;
            this.visibleAlpha = visibleAlpha;
        }
    }
}
