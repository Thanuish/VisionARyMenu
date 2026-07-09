using System;
using System.Collections.Generic;
using MenuViewer.Food;
using MenuViewer.Ocr;
using UnityEngine;

namespace MenuViewer.Overlay
{
    public sealed class MenuTapSelectionController : MonoBehaviour
    {
        public const float LowConfidenceThreshold = 0.55f;
        public const float IdleTimeoutSeconds = 2f;
        public const float ReanchorOverlapThreshold = 0.4f;

        public struct TapSelection
        {
            public FoodMatch match;
            public bool isLowConfidence;
            public bool hasItem;
            // Screen-space (bottom-left origin), captured at the moment of the tap.
            public Vector2 screenTapPoint;
        }

        [SerializeField] private MenuDetectionPipeline pipeline;
        [SerializeField] private FoodCatalog catalog;

        private TapSelection? current;
        private double lastSeenTime;

        public Func<bool> InputBlocked { get; set; }

        // GUI-space (top-left origin) hit test against on-screen UI; taps on IMGUI controls
        // must not select or clear the OCR line behind them.
        public Func<Vector2, bool> TapBlockedAt { get; set; }

        public event Action<TapSelection?> SelectionChanged;
        public TapSelection? Current => current;

        public void Configure(MenuDetectionPipeline detectionPipeline, FoodCatalog foodCatalog)
        {
            pipeline = detectionPipeline;
            var pipelineCatalog = detectionPipeline != null ? detectionPipeline.Catalog : null;
            catalog = foodCatalog != null ? foodCatalog : pipelineCatalog;
        }

        public void Clear()
        {
            if (!current.HasValue)
            {
                return;
            }

            current = null;
            SelectionChanged?.Invoke(null);
        }

        public void HandleTap(
            Vector2 guiPosition,
            int screenWidth,
            int screenHeight,
            IReadOnlyList<RecognizedTextLine> lines,
            IReadOnlyList<FoodMatch> candidates,
            FoodCatalog activeCatalog,
            double now,
            OcrRecognitionResult ocrStatus = default)
        {
            if (lines == null || lines.Count == 0)
            {
                Clear();
                return;
            }

            RecognizedTextLine? hit = null;
            var bestArea = float.MaxValue;
            foreach (var line in lines)
            {
                var rect = OcrScreenRectMapper.NormalizedToScreenRect(line.normalizedRect, screenWidth, screenHeight, ocrStatus);
                if (!rect.Contains(guiPosition))
                {
                    continue;
                }

                var area = rect.width * rect.height;
                if (area < bestArea)
                {
                    bestArea = area;
                    hit = line;
                }
            }

            if (!hit.HasValue)
            {
                Clear();
                return;
            }

            // Convert GUI (top-left) to screen (bottom-left) so the presenter can pass it
            // straight to ARRaycastManager without re-flipping.
            var screenTapPoint = new Vector2(guiPosition.x, screenHeight - guiPosition.y);
            ApplySelection(hit.Value, candidates, activeCatalog, now, screenTapPoint);
        }

        public void HandleFrameUpdate(IReadOnlyList<RecognizedTextLine> lines, double now)
        {
            if (!current.HasValue)
            {
                return;
            }

            var sel = current.Value;
            RecognizedTextLine? best = null;
            var bestOverlap = 0f;

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (string.Equals(line.text, sel.match.sourceText, StringComparison.Ordinal))
                    {
                        best = line;
                        bestOverlap = 1f;
                        break;
                    }

                    var overlap = OverlapFraction(line.normalizedRect, sel.match.normalizedRect);
                    if (overlap > bestOverlap)
                    {
                        bestOverlap = overlap;
                        best = line;
                    }
                }
            }

            if (best.HasValue && bestOverlap >= ReanchorOverlapThreshold)
            {
                lastSeenTime = now;
                var newRect = best.Value.normalizedRect;
                if (sel.match.normalizedRect != newRect)
                {
                    sel.match.normalizedRect = newRect;
                    current = sel;
                    SelectionChanged?.Invoke(current);
                }
                return;
            }

            if (now - lastSeenTime > IdleTimeoutSeconds)
            {
                Clear();
            }
        }

        private void ApplySelection(
            RecognizedTextLine line,
            IReadOnlyList<FoodMatch> candidates,
            FoodCatalog activeCatalog,
            double now,
            Vector2 screenTapPoint)
        {
            FoodMatch resolved;
            bool isLowConfidence;
            bool hasItem;

            if (TryFindCandidate(line, candidates, out var candidateMatch))
            {
                resolved = candidateMatch;
                resolved.normalizedRect = line.normalizedRect;
                resolved.sourceText = line.text;
                isLowConfidence = false;
                hasItem = true;
            }
            else if (activeCatalog != null && FoodMatcher.TryMatchOne(line, activeCatalog, LowConfidenceThreshold, out var fuzzy))
            {
                resolved = fuzzy;
                isLowConfidence = fuzzy.score < FoodMatcher.DefaultMatchThreshold;
                hasItem = true;
            }
            else
            {
                resolved = new FoodMatch(
                    string.Empty,
                    line.text,
                    line.text,
                    0f,
                    line.normalizedRect,
                    line.timestamp,
                    line.frameId);
                isLowConfidence = false;
                hasItem = false;
            }

            current = new TapSelection
            {
                match = resolved,
                isLowConfidence = isLowConfidence,
                hasItem = hasItem,
                screenTapPoint = screenTapPoint
            };
            lastSeenTime = now;
            SelectionChanged?.Invoke(current);
        }

        private static bool TryFindCandidate(
            RecognizedTextLine line,
            IReadOnlyList<FoodMatch> candidates,
            out FoodMatch match)
        {
            match = default;
            if (candidates == null)
            {
                return false;
            }

            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.sourceText, line.text, StringComparison.Ordinal))
                {
                    match = candidate;
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;

            if (InputBlocked != null && InputBlocked()) return;

            if (TryGetPrimaryGuiTap(out var guiPosition)
                && (TapBlockedAt == null || !TapBlockedAt(guiPosition)))
            {
                HandleTap(
                    guiPosition,
                    Screen.width,
                    Screen.height,
                    pipeline != null ? pipeline.CurrentRawLines : null,
                    pipeline != null ? pipeline.CurrentCandidateMatches : null,
                    catalog,
                    now,
                    pipeline != null ? pipeline.CurrentOcrStatus : default);
            }

            HandleFrameUpdate(pipeline != null ? pipeline.CurrentRawLines : null, now);
        }

        private static bool TryGetPrimaryGuiTap(out Vector2 guiPosition)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    guiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                guiPosition = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return true;
            }

            guiPosition = default;
            return false;
        }

        private static float OverlapFraction(Rect a, Rect b)
        {
            var minX = Mathf.Max(a.xMin, b.xMin);
            var maxX = Mathf.Min(a.xMax, b.xMax);
            var minY = Mathf.Max(a.yMin, b.yMin);
            var maxY = Mathf.Min(a.yMax, b.yMax);
            if (maxX <= minX || maxY <= minY)
            {
                return 0f;
            }

            var intersection = (maxX - minX) * (maxY - minY);
            var areaA = a.width * a.height;
            var areaB = b.width * b.height;
            var union = areaA + areaB - intersection;
            return union > 0f ? intersection / union : 0f;
        }
    }
}
