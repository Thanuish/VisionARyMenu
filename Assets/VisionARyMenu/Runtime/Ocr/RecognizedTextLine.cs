using System;
using UnityEngine;

namespace MenuViewer.Ocr
{
    [Serializable]
    public struct RecognizedTextLine
    {
        public string text;
        public float confidence;
        public Rect normalizedRect;
        public double timestamp;
        public int frameId;

        public RecognizedTextLine(string text, float confidence, Rect normalizedRect, double timestamp, int frameId)
        {
            this.text = text;
            this.confidence = confidence;
            this.normalizedRect = normalizedRect;
            this.timestamp = timestamp;
            this.frameId = frameId;
        }
    }
}
