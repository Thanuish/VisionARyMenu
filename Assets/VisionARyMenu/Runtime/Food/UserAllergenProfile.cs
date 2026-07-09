using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MenuViewer.Food
{
    public sealed class UserAllergenProfile
    {
        private const string AllergensKey = "MenuViewer.Allergens.v1";
        private const string FirstLaunchKey = "MenuViewer.FirstLaunchDone.v1";

        private readonly HashSet<AllergyTag> _allergens = new HashSet<AllergyTag>();

        public bool Contains(AllergyTag tag) => tag != AllergyTag.None && _allergens.Contains(tag);

        public void Set(AllergyTag tag, bool active)
        {
            if (tag == AllergyTag.None) return;
            if (active) _allergens.Add(tag);
            else _allergens.Remove(tag);
            Save();
        }

        public bool HasAnyOf(AllergyTag[] tags)
        {
            if (tags == null) return false;
            foreach (var tag in tags)
                if (Contains(tag)) return true;
            return false;
        }

        public bool HasCompletedFirstLaunch => PlayerPrefs.GetInt(FirstLaunchKey, 0) == 1;

        public void CompleteFirstLaunch()
        {
            PlayerPrefs.SetInt(FirstLaunchKey, 1);
            Save();
        }

        // Returns the profile to its out-of-the-box state (no allergens, first launch not
        // completed) so the onboarding flow can be demonstrated again.
        public void ResetFirstLaunch()
        {
            _allergens.Clear();
            PlayerPrefs.DeleteKey(AllergensKey);
            PlayerPrefs.DeleteKey(FirstLaunchKey);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _allergens.Clear();
            var raw = PlayerPrefs.GetString(AllergensKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var token in raw.Split(','))
            {
                if (int.TryParse(token, out var value) && System.Enum.IsDefined(typeof(AllergyTag), value))
                {
                    var tag = (AllergyTag)value;
                    if (tag != AllergyTag.None) _allergens.Add(tag);
                }
            }
        }

        public void Save()
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var tag in _allergens)
            {
                if (!first) builder.Append(',');
                builder.Append((int)tag);
                first = false;
            }
            PlayerPrefs.SetString(AllergensKey, builder.ToString());
            PlayerPrefs.Save();
        }
    }
}
