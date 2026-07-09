using MenuViewer.Food;
using NUnit.Framework;
using UnityEngine;

namespace MenuViewer.Tests.EditMode
{
    public sealed class UserAllergenProfileTests
    {
        private const string AllergensKey = "MenuViewer.Allergens.v1";
        private const string FirstLaunchKey = "MenuViewer.FirstLaunchDone.v1";

        [Test]
        public void ResetFirstLaunch_RestoresOutOfBoxState()
        {
            var hadAllergens = PlayerPrefs.HasKey(AllergensKey);
            var savedAllergens = PlayerPrefs.GetString(AllergensKey, string.Empty);
            var hadFirstLaunch = PlayerPrefs.HasKey(FirstLaunchKey);
            var savedFirstLaunch = PlayerPrefs.GetInt(FirstLaunchKey, 0);

            try
            {
                var profile = new UserAllergenProfile();
                profile.Set(AllergyTag.Gluten, true);
                profile.CompleteFirstLaunch();
                Assert.IsTrue(profile.HasCompletedFirstLaunch);
                Assert.IsTrue(profile.Contains(AllergyTag.Gluten));

                profile.ResetFirstLaunch();

                Assert.IsFalse(profile.HasCompletedFirstLaunch);
                Assert.IsFalse(profile.Contains(AllergyTag.Gluten));

                var reloaded = new UserAllergenProfile();
                reloaded.Load();
                Assert.IsFalse(reloaded.Contains(AllergyTag.Gluten));
            }
            finally
            {
                if (hadAllergens)
                {
                    PlayerPrefs.SetString(AllergensKey, savedAllergens);
                }
                else
                {
                    PlayerPrefs.DeleteKey(AllergensKey);
                }

                if (hadFirstLaunch)
                {
                    PlayerPrefs.SetInt(FirstLaunchKey, savedFirstLaunch);
                }
                else
                {
                    PlayerPrefs.DeleteKey(FirstLaunchKey);
                }

                PlayerPrefs.Save();
            }
        }
    }
}
