using MenuViewer.Food;
using NUnit.Framework;

namespace MenuViewer.Tests.EditMode
{
    public sealed class FoodTextNormalizerTests
    {
        [Test]
        public void Normalize_RemovesPricesPunctuationAndAccents()
        {
            var normalized = FoodTextNormalizer.Normalize("Cr\u00e8me Br\u00fbl\u00e9e - 8.50!");
            Assert.AreEqual("creme brulee", normalized);
        }

        [Test]
        public void Normalize_HandlesEmptyText()
        {
            Assert.AreEqual(string.Empty, FoodTextNormalizer.Normalize("  "));
        }

        [Test]
        public void Normalize_HandlesMixedSeparators()
        {
            var normalized = FoodTextNormalizer.Normalize("Pad Thai / prawns + tamarind   $13.50");
            Assert.AreEqual("pad thai prawns tamarind", normalized);
        }

        [Test]
        public void IsLikelyDescription_FiltersLongMenuDescriptions()
        {
            const string description = "Fresh tomatoes with basil olive oil garlic toasted bread and sea salt";
            Assert.IsTrue(FoodTextNormalizer.IsLikelyDescription(description));
        }
    }
}
