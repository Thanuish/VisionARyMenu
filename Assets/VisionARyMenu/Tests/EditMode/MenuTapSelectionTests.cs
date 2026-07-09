using System;
using System.Collections.Generic;
using MenuViewer.Food;
using MenuViewer.Ocr;
using MenuViewer.Overlay;
using NUnit.Framework;
using UnityEngine;

namespace MenuViewer.Tests.EditMode
{
    public sealed class MenuTapSelectionTests
    {
        private const int Width = 1000;
        private const int Height = 1000;

        private static readonly IReadOnlyList<FoodMatch> NoCandidates = Array.Empty<FoodMatch>();

        [Test]
        public void HandleTap_PicksSmallestAreaLineOnOverlap()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new List<RecognizedTextLine>
            {
                new RecognizedTextLine("Big Box", 1f, new Rect(0f, 0f, 1f, 1f), 0.1, 1),
                new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1)
            };

            // GUI rect for the small Caesar Salad line: (100, 600, 200, 100); tap at (150, 650) is inside both.
            controller.HandleTap(new Vector2(150f, 650f), Width, Height, lines, NoCandidates, catalog, 0.5);

            Assert.IsTrue(controller.Current.HasValue);
            Assert.AreEqual("Caesar Salad", controller.Current.Value.match.sourceText);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleTap_OutsideAnyLine_ClearsPriorSelection()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new List<RecognizedTextLine>
            {
                new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1)
            };

            controller.HandleTap(new Vector2(150f, 650f), Width, Height, lines, NoCandidates, catalog, 0.5);
            Assert.IsTrue(controller.Current.HasValue);

            controller.HandleTap(new Vector2(900f, 900f), Width, Height, lines, NoCandidates, catalog, 0.6);

            Assert.IsFalse(controller.Current.HasValue);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleTap_FallsBackToLowConfidenceFuzzyMatch()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new List<RecognizedTextLine>
            {
                new RecognizedTextLine("caes sal", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1)
            };

            controller.HandleTap(new Vector2(150f, 650f), Width, Height, lines, NoCandidates, catalog, 0.5);

            Assert.IsTrue(controller.Current.HasValue);
            var sel = controller.Current.Value;
            Assert.IsTrue(sel.hasItem);
            Assert.AreEqual("caesar_salad", sel.match.itemId);
            Assert.IsTrue(sel.isLowConfidence, "Score below default threshold should mark selection as low-confidence");
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleTap_UsesExistingCandidateWhenAvailable()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var line = new RecognizedTextLine("Caesar Salad 9.90", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1);
            var candidate = new FoodMatch(
                "caesar_salad",
                "Caesar Salad",
                "Caesar Salad 9.90",
                0.95f,
                line.normalizedRect,
                line.timestamp,
                line.frameId);

            controller.HandleTap(
                new Vector2(150f, 650f),
                Width,
                Height,
                new[] { line },
                new[] { candidate },
                catalog,
                0.5);

            var sel = controller.Current.Value;
            Assert.AreEqual("caesar_salad", sel.match.itemId);
            Assert.IsFalse(sel.isLowConfidence, "High-confidence candidate matches should not be flagged as low confidence");
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleTap_UsesOcrImageAspectFillMapping()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var line = new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.7f, 0.4f, 0.05f, 0.1f), 0.1, 1);
            var status = new OcrRecognitionResult(1, 0.1, new[] { line }, 0, 1, null, 2000, 1000);

            controller.HandleTap(
                new Vector2(925f, 450f),
                Width,
                Height,
                new[] { line },
                NoCandidates,
                catalog,
                0.5,
                status);

            Assert.IsTrue(controller.Current.HasValue);
            Assert.AreEqual("Caesar Salad", controller.Current.Value.match.sourceText);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleTap_NoMatchAnywhere_ProducesNoInfoSelection()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new List<RecognizedTextLine>
            {
                new RecognizedTextLine("APPETIZERS", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1)
            };

            controller.HandleTap(new Vector2(150f, 650f), Width, Height, lines, NoCandidates, catalog, 0.5);

            var sel = controller.Current.Value;
            Assert.IsFalse(sel.hasItem);
            Assert.AreEqual("APPETIZERS", sel.match.sourceText);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleFrameUpdate_ReanchorsToShiftedLineWithSameText()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var initial = new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1);
            controller.HandleTap(new Vector2(150f, 650f), Width, Height, new[] { initial }, NoCandidates, catalog, 0.5);

            var shifted = new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.4f, 0.5f, 0.2f, 0.1f), 0.6, 2);
            controller.HandleFrameUpdate(new[] { shifted }, 0.6);

            Assert.IsTrue(controller.Current.HasValue);
            Assert.AreEqual(shifted.normalizedRect, controller.Current.Value.match.normalizedRect);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        [Test]
        public void HandleFrameUpdate_ClearsSelectionAfterIdleTimeout()
        {
            var controller = NewController();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var line = new RecognizedTextLine("Caesar Salad", 1f, new Rect(0.1f, 0.6f, 0.2f, 0.1f), 0.1, 1);
            controller.HandleTap(new Vector2(150f, 650f), Width, Height, new[] { line }, NoCandidates, catalog, 0.0);

            controller.HandleFrameUpdate(Array.Empty<RecognizedTextLine>(), MenuTapSelectionController.IdleTimeoutSeconds + 0.5);

            Assert.IsFalse(controller.Current.HasValue);
            UnityEngine.Object.DestroyImmediate(controller.gameObject);
        }

        private static MenuTapSelectionController NewController()
        {
            var go = new GameObject("TapControllerTest");
            return go.AddComponent<MenuTapSelectionController>();
        }
    }
}
