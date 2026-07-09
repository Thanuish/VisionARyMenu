using System;
using System.Collections.Generic;
using UnityEngine;

namespace MenuViewer.Food
{
    [CreateAssetMenu(menuName = "Menu Viewer/Food Catalog", fileName = "FoodCatalog")]
    public sealed class FoodCatalog : ScriptableObject
    {
        [SerializeField] private FoodItemDefinition[] items = Array.Empty<FoodItemDefinition>();

        private Dictionary<string, FoodItemDefinition> itemsById;

        public IReadOnlyList<FoodItemDefinition> Items => items;

        public void Initialize(FoodItemDefinition[] seedItems)
        {
            items = seedItems ?? Array.Empty<FoodItemDefinition>();
            itemsById = null;
        }

        public bool TryGetItem(string id, out FoodItemDefinition item)
        {
            EnsureIndex();
            return itemsById.TryGetValue(id ?? string.Empty, out item);
        }

        private void OnEnable()
        {
            itemsById = null;
        }

        private void EnsureIndex()
        {
            if (itemsById != null)
            {
                return;
            }

            itemsById = new Dictionary<string, FoodItemDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                itemsById[item.Id] = item;
            }
        }
    }
}
