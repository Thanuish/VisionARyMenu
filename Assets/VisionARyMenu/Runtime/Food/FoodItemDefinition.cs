using System;
using UnityEngine;

namespace MenuViewer.Food
{
    [Serializable]
    public sealed class FoodItemDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string[] aliases;
        [SerializeField] private int caloriesKcal;
        [SerializeField] private AllergyTag[] allergens;
        [SerializeField] private DietTag[] dietTags;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private float defaultScaleMeters = 0.12f;

        public string Id => id;
        public string DisplayName => displayName;
        public string[] Aliases => aliases ?? Array.Empty<string>();
        public int CaloriesKcal => caloriesKcal;
        public AllergyTag[] Allergens => allergens ?? Array.Empty<AllergyTag>();
        public DietTag[] DietTags => dietTags ?? Array.Empty<DietTag>();
        public Sprite Thumbnail => thumbnail;
        public GameObject ModelPrefab => modelPrefab;
        public float DefaultScaleMeters => defaultScaleMeters <= 0f ? 0.12f : defaultScaleMeters;

        public static FoodItemDefinition Create(
            string id,
            string displayName,
            string[] aliases,
            int caloriesKcal,
            AllergyTag[] allergens,
            DietTag[] dietTags,
            float defaultScaleMeters = 0.12f,
            Sprite thumbnail = null,
            GameObject modelPrefab = null)
        {
            return new FoodItemDefinition
            {
                id = id,
                displayName = displayName,
                aliases = aliases ?? Array.Empty<string>(),
                caloriesKcal = caloriesKcal,
                allergens = allergens ?? Array.Empty<AllergyTag>(),
                dietTags = dietTags ?? Array.Empty<DietTag>(),
                defaultScaleMeters = defaultScaleMeters,
                thumbnail = thumbnail,
                modelPrefab = modelPrefab
            };
        }
    }
}
