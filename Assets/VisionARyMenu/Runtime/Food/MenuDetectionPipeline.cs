using System;
using System.Collections.Generic;
using MenuViewer.Ocr;
using UnityEngine;

namespace MenuViewer.Food
{
    public sealed class MenuDetectionPipeline : MonoBehaviour
    {
        [SerializeField] private FoodCatalog catalog;
        [SerializeField] private float matchThreshold = FoodMatcher.DefaultMatchThreshold;

        private IOcrService ocrService;
        private IOcrDiagnosticsSource diagnosticsSource;
        private FoodMatcher matcher;
        private FoodMatchStabilizer stabilizer;
        private IReadOnlyList<RecognizedTextLine> currentRawLines = Array.Empty<RecognizedTextLine>();
        private IReadOnlyList<FoodMatch> currentCandidateMatches = Array.Empty<FoodMatch>();
        private IReadOnlyList<FoodMatch> currentStableMatches = Array.Empty<FoodMatch>();
        private OcrRecognitionResult currentOcrStatus;
        private double stabilizerClock;
        private double stabilizerClockSetAtRealtime;
        private bool hasStabilizerClock;

        public event Action<IReadOnlyList<FoodMatch>> CandidateMatchesUpdated;
        public event Action<IReadOnlyList<FoodMatch>> StableMatchesUpdated;
        public event Action<IReadOnlyList<RecognizedTextLine>> RawLinesUpdated;

        public FoodCatalog Catalog => catalog;
        public IReadOnlyList<RecognizedTextLine> CurrentRawLines => currentRawLines;
        public IReadOnlyList<FoodMatch> CurrentCandidateMatches => currentCandidateMatches;
        public IReadOnlyList<FoodMatch> CurrentStableMatches => currentStableMatches;
        public OcrRecognitionResult CurrentOcrStatus => currentOcrStatus;

        public void Configure(IOcrService service, FoodCatalog foodCatalog)
        {
            if (ocrService != null)
            {
                ocrService.OnLinesRecognized -= HandleLinesRecognized;
                ocrService.Stop();
            }

            if (diagnosticsSource != null)
            {
                diagnosticsSource.OnRecognitionResult -= HandleRecognitionResult;
            }

            catalog = foodCatalog != null ? foodCatalog : CreateEmptyCatalog();
            ocrService = service;
            diagnosticsSource = service as IOcrDiagnosticsSource;
            matcher = new FoodMatcher(matchThreshold);
            stabilizer = new FoodMatchStabilizer();
            currentRawLines = Array.Empty<RecognizedTextLine>();
            currentCandidateMatches = Array.Empty<FoodMatch>();
            currentStableMatches = Array.Empty<FoodMatch>();
            currentOcrStatus = default;
            hasStabilizerClock = false;

            if (ocrService != null)
            {
                ocrService.OnLinesRecognized += HandleLinesRecognized;
                if (diagnosticsSource != null)
                {
                    diagnosticsSource.OnRecognitionResult += HandleRecognitionResult;
                    currentOcrStatus = diagnosticsSource.LastResult;
                }

                ocrService.Start();
            }
        }

        private void Awake()
        {
            matcher = new FoodMatcher(matchThreshold);
            stabilizer = new FoodMatchStabilizer();
        }

        private void Update()
        {
            if (stabilizer == null)
            {
                return;
            }

            // Drives the stabilizer's fade-out tick each frame even when no new OCR arrives.
            // OCR line timestamps and Time.realtimeSinceStartupAsDouble are not guaranteed to
            // share an epoch, so advance the last OCR 'now' by elapsed real time rather than
            // mixing the two clocks (which would expire every track instantly).
            var realNow = Time.realtimeSinceStartupAsDouble;
            var now = hasStabilizerClock
                ? stabilizerClock + (realNow - stabilizerClockSetAtRealtime)
                : realNow;
            currentStableMatches = stabilizer.Update(null, now);
            StableMatchesUpdated?.Invoke(currentStableMatches);
        }

        private void OnDestroy()
        {
            if (ocrService != null)
            {
                ocrService.OnLinesRecognized -= HandleLinesRecognized;
                ocrService.Stop();
            }

            if (diagnosticsSource != null)
            {
                diagnosticsSource.OnRecognitionResult -= HandleRecognitionResult;
            }
        }

        private void HandleRecognitionResult(OcrRecognitionResult result)
        {
            currentOcrStatus = result;
        }

        private void HandleLinesRecognized(IReadOnlyList<RecognizedTextLine> lines)
        {
            currentRawLines = lines ?? Array.Empty<RecognizedTextLine>();
            RawLinesUpdated?.Invoke(currentRawLines);

            if (diagnosticsSource == null)
            {
                var frameTimestamp = currentRawLines.Count > 0 ? currentRawLines[0].timestamp : Time.realtimeSinceStartupAsDouble;
                var frameId = currentRawLines.Count > 0 ? currentRawLines[0].frameId : 0;
                currentOcrStatus = new OcrRecognitionResult(frameId, frameTimestamp, currentRawLines, 0, 1);
            }

            if (catalog == null)
            {
                catalog = CreateEmptyCatalog();
            }

            var now = lines != null && lines.Count > 0 ? lines[0].timestamp : Time.realtimeSinceStartupAsDouble;
            stabilizerClock = now;
            stabilizerClockSetAtRealtime = Time.realtimeSinceStartupAsDouble;
            hasStabilizerClock = true;
            var rawMatches = matcher.Match(lines, catalog);
            currentCandidateMatches = rawMatches;
            CandidateMatchesUpdated?.Invoke(currentCandidateMatches);
            currentStableMatches = stabilizer.Update(rawMatches, now);
            StableMatchesUpdated?.Invoke(currentStableMatches);
        }

        // No demo/production catalog factory is ported in Phase 1 (stub catalogs are built
        // inline by callers); this is only a null-safety fallback so the pipeline never runs
        // against a null catalog.
        private static FoodCatalog CreateEmptyCatalog()
        {
            var emptyCatalog = ScriptableObject.CreateInstance<FoodCatalog>();
            emptyCatalog.name = "Empty Food Catalog";
            emptyCatalog.Initialize(Array.Empty<FoodItemDefinition>());
            return emptyCatalog;
        }
    }
}
