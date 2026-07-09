using System.Linq;
using MenuViewer.Food;
using MenuViewer.Ocr;
using NUnit.Framework;
using UnityEngine;

namespace MenuViewer.Tests.EditMode
{
    public sealed class FoodMatcherTests
    {
        [Test]
        public void Match_FindsExactAliasWithPrice()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Margherita Pizza 12.50", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.AreEqual("margherita_pizza", matches[0].itemId);
            Assert.AreEqual(1f, matches[0].score);
        }

        [Test]
        public void Match_FindsFuzzyTypoAboveThreshold()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Margerita Piza", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.IsTrue(matches.Any(match => match.itemId == "margherita_pizza"));
        }

        [Test]
        public void Match_DeduplicatesSameDishAcrossLines()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Pad Thai", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1),
                new RecognizedTextLine("Prawn Pad Thai 13.50", 1f, new Rect(0.1f, 0.2f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.AreEqual(1, matches.Count(match => match.itemId == "pad_thai"));
        }

        [Test]
        public void Match_FindsDishInsideLongDescriptionLine()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Margherita Pizza fresh mozzarella basil tomato sauce 12.50", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.IsTrue(matches.Any(match => match.itemId == "margherita_pizza"));
        }

        [Test]
        public void Match_FindsTypoInsideLongDescriptionLine()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Margerita Piza fresh mozzarella basil tomato sauce", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.IsTrue(matches.Any(match => match.itemId == "margherita_pizza"));
        }

        [Test]
        public void Match_FindsPhoneOcrVariantFoodNames()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Veggle Burger", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1),
                new RecognizedTextLine("Chlcken Rarnen", 1f, new Rect(0.1f, 0.2f, 0.4f, 0.1f), 0.1, 1),
                new RecognizedTextLine("Coeasar Salad", 1f, new Rect(0.1f, 0.3f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.IsTrue(matches.Any(match => match.itemId == "veggie_burger"));
            Assert.IsTrue(matches.Any(match => match.itemId == "chicken_ramen"));
            Assert.IsTrue(matches.Any(match => match.itemId == "caesar_salad"));
        }

        [Test]
        public void Match_ReturnsNoMatchForUnknownDish()
        {
            var matcher = new FoodMatcher();
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var lines = new[]
            {
                new RecognizedTextLine("Sparkling Water", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1)
            };

            var matches = matcher.Match(lines, catalog);

            Assert.IsFalse(matches.Any());
        }

        [Test]
        public void TryMatchOne_FindsBelowDefaultThresholdWhenLowered()
        {
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var line = new RecognizedTextLine("caes sal", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1);

            var defaultThresholdHit = FoodMatcher.TryMatchOne(line, catalog, FoodMatcher.DefaultMatchThreshold, out _);
            var lowThresholdHit = FoodMatcher.TryMatchOne(line, catalog, 0.55f, out var match);

            Assert.IsFalse(defaultThresholdHit, "Severely garbled OCR should not match at the default threshold");
            Assert.IsTrue(lowThresholdHit, "A 0.55 threshold should still surface a best-guess");
            Assert.AreEqual("caesar_salad", match.itemId);
        }

        [Test]
        public void TryMatchOne_ReturnsFalseForGarbageWithNoCandidate()
        {
            var catalog = DemoFoodCatalogFactory.CreateCatalog();
            var line = new RecognizedTextLine("xyzqwerty", 1f, new Rect(0.1f, 0.1f, 0.4f, 0.1f), 0.1, 1);

            var hit = FoodMatcher.TryMatchOne(line, catalog, 0.55f, out _);

            Assert.IsFalse(hit);
        }
    }
}
