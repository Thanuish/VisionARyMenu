using System;

namespace MenuViewer.Ocr
{
    public interface IOcrDiagnosticsSource
    {
        event Action<OcrRecognitionResult> OnRecognitionResult;

        OcrRecognitionResult LastResult { get; }
        bool IsRunning { get; }
        bool IsAvailable { get; }
    }
}
