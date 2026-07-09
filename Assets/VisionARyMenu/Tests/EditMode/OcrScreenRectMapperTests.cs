using MenuViewer.Ocr;
using NUnit.Framework;
using UnityEngine;

namespace MenuViewer.Tests.EditMode
{
    public sealed class OcrScreenRectMapperTests
    {
        [Test]
        public void NormalizedToScreenRect_SameAspect_MapsTopLeftDirectly()
        {
            var rect = OcrScreenRectMapper.NormalizedToScreenRect(
                new Rect(0.1f, 0.6f, 0.2f, 0.1f),
                1000,
                1000,
                1000,
                1000,
                0f);

            AssertRect(rect, 100f, 600f, 200f, 100f);
        }

        [Test]
        public void NormalizedToScreenRect_WideImage_AppliesHorizontalCrop()
        {
            var rect = OcrScreenRectMapper.NormalizedToScreenRect(
                new Rect(0.5f, 0.25f, 0.1f, 0.1f),
                1000,
                1000,
                2000,
                1000,
                0f);

            AssertRect(rect, 500f, 250f, 200f, 100f);
        }

        [Test]
        public void NormalizedToScreenRect_TallImage_AppliesVerticalCrop()
        {
            var rect = OcrScreenRectMapper.NormalizedToScreenRect(
                new Rect(0.2f, 0.5f, 0.2f, 0.1f),
                1000,
                1000,
                1000,
                2000,
                0f);

            AssertRect(rect, 200f, 500f, 200f, 200f);
        }

        [Test]
        public void NormalizedToScreenRect_InsetKeepsPositiveSize()
        {
            var rect = OcrScreenRectMapper.NormalizedToScreenRect(
                new Rect(0.1f, 0.6f, 0.02f, 0.01f),
                1000,
                1000,
                1000,
                1000);

            Assert.Greater(rect.width, 0f);
            Assert.Greater(rect.height, 0f);
        }

        private static void AssertRect(Rect actual, float x, float y, float width, float height)
        {
            Assert.AreEqual(x, actual.x, 0.001f);
            Assert.AreEqual(y, actual.y, 0.001f);
            Assert.AreEqual(width, actual.width, 0.001f);
            Assert.AreEqual(height, actual.height, 0.001f);
        }
    }
}
