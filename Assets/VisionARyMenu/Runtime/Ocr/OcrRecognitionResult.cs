using System;
using System.Collections.Generic;

namespace MenuViewer.Ocr
{
    public readonly struct OcrRecognitionResult
    {
        public OcrRecognitionResult(
            int frameId,
            double timestamp,
            IReadOnlyList<RecognizedTextLine> lines,
            int rotationDegrees,
            int attemptCount,
            string errorMessage = null,
            int imageWidth = 0,
            int imageHeight = 0)
        {
            this.frameId = frameId;
            this.timestamp = timestamp;
            this.lines = lines ?? Array.Empty<RecognizedTextLine>();
            this.rotationDegrees = rotationDegrees;
            this.attemptCount = Math.Max(1, attemptCount);
            this.errorMessage = errorMessage ?? string.Empty;
            this.imageWidth = Math.Max(0, imageWidth);
            this.imageHeight = Math.Max(0, imageHeight);
        }

        public readonly int frameId;
        public readonly double timestamp;
        public readonly IReadOnlyList<RecognizedTextLine> lines;
        public readonly int rotationDegrees;
        public readonly int attemptCount;
        public readonly string errorMessage;
        public readonly int imageWidth;
        public readonly int imageHeight;

        public int LineCount => lines?.Count ?? 0;
        public bool HasError => !string.IsNullOrWhiteSpace(errorMessage);
    }
}
