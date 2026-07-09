using System;
using System.Collections.Generic;
using MenuViewer.Ocr;
using UnityEngine;

namespace MenuViewer.Simulation
{
    public sealed class FakeOcrService : MonoBehaviour, IOcrService, IOcrDiagnosticsSource
    {
        [SerializeField] private float emitIntervalSeconds = 0.75f;
        [SerializeField] private bool autoStart = true;

        private bool running;
        private float nextEmitTime;
        private int frameId;

        public event Action<IReadOnlyList<RecognizedTextLine>> OnLinesRecognized;
        public event Action<OcrRecognitionResult> OnRecognitionResult;

        public OcrRecognitionResult LastResult { get; private set; }
        public bool IsRunning => running;
        public bool IsAvailable => true;

        public void Start()
        {
            running = true;
        }

        public void Stop()
        {
            running = false;
        }

        public void Emit(IReadOnlyList<RecognizedTextLine> lines)
        {
            var timestamp = lines != null && lines.Count > 0 ? lines[0].timestamp : Time.realtimeSinceStartupAsDouble;
            var emittedFrameId = lines != null && lines.Count > 0 ? lines[0].frameId : ++frameId;
            Emit(new OcrRecognitionResult(emittedFrameId, timestamp, lines, 0, 1));
        }

        public void Emit(OcrRecognitionResult result)
        {
            LastResult = result;
            OnRecognitionResult?.Invoke(result);
            OnLinesRecognized?.Invoke(result.lines);
        }

        private void OnEnable()
        {
            if (autoStart)
            {
                Start();
            }
        }

        private void Update()
        {
            if (!running || Time.realtimeSinceStartup < nextEmitTime)
            {
                return;
            }

            nextEmitTime = Time.realtimeSinceStartup + emitIntervalSeconds;
            Emit(CreateDemoLines(++frameId, Time.realtimeSinceStartupAsDouble));
        }

        private static IReadOnlyList<RecognizedTextLine> CreateDemoLines(int frameId, double timestamp)
        {
            return new[]
            {
                new RecognizedTextLine("Margherita Pizza 12.50", 1f, new Rect(0.14f, 0.18f, 0.38f, 0.07f), timestamp, frameId),
                new RecognizedTextLine("Caesar Salad 9.90", 1f, new Rect(0.14f, 0.31f, 0.32f, 0.07f), timestamp, frameId),
                new RecognizedTextLine("Pad Thai 13.50", 1f, new Rect(0.14f, 0.44f, 0.28f, 0.07f), timestamp, frameId),
                new RecognizedTextLine("Chocolate Cake 6.50", 1f, new Rect(0.14f, 0.57f, 0.36f, 0.07f), timestamp, frameId)
            };
        }
    }
}
