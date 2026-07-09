using MenuViewer.Food;
using NUnit.Framework;
using UnityEngine;

namespace MenuViewer.Tests.EditMode
{
    public sealed class FoodMatchStabilizerTests
    {
        [Test]
        public void Update_RequiresTwoHitsInsideWindow()
        {
            var stabilizer = new FoodMatchStabilizer();
            var firstHit = new[]
            {
                CreateMatch(0.1)
            };
            var secondHit = new[]
            {
                CreateMatch(0.8)
            };

            Assert.AreEqual(0, stabilizer.Update(firstHit, 0.1).Count);
            var stable = stabilizer.Update(secondHit, 0.8);

            Assert.AreEqual(1, stable.Count);
            Assert.IsTrue(stable[0].isStable);
            Assert.AreEqual(2, stable[0].hitCount);
        }

        [Test]
        public void Update_ExpiresStaleMatches()
        {
            var stabilizer = new FoodMatchStabilizer();

            stabilizer.Update(new[] { CreateMatch(0.1) }, 0.1);
            stabilizer.Update(new[] { CreateMatch(0.5) }, 0.5);

            Assert.AreEqual(1, stabilizer.Update(null, 1.0).Count);
            Assert.AreEqual(0, stabilizer.Update(null, 3.0).Count);
        }

        [Test]
        public void Update_FadesStableMatchesBeforeExpiry()
        {
            var stabilizer = new FoodMatchStabilizer();

            stabilizer.Update(new[] { CreateMatch(0.1) }, 0.1);
            stabilizer.Update(new[] { CreateMatch(0.5) }, 0.5);

            var stable = stabilizer.Update(null, 1.25);

            Assert.AreEqual(1, stable.Count);
            Assert.Greater(stable[0].visibleAlpha, 0f);
            Assert.Less(stable[0].visibleAlpha, 1f);
        }

        private static FoodMatch CreateMatch(double timestamp)
        {
            return new FoodMatch(
                "caesar_salad",
                "Caesar Salad",
                "Caesar Salad",
                1f,
                new Rect(0.1f, 0.1f, 0.4f, 0.1f),
                timestamp,
                1);
        }
    }
}
