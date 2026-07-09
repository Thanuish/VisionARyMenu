using System;
using System.Collections.Generic;

namespace MenuViewer.Ocr
{
    public interface IOcrService
    {
        event Action<IReadOnlyList<RecognizedTextLine>> OnLinesRecognized;

        void Start();

        void Stop();
    }
}
