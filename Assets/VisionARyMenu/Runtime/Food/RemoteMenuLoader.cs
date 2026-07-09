using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MenuViewer.Food
{
    // Fetches a restaurant's menu from a hosted JSON URL (the URL a table QR code encodes)
    // and builds a FoodCatalog from it, reusing FoodItemDefinition/FoodCatalog exactly as the
    // rest of the ported pipeline expects. This is the "swap the JSON source for a real API
    // later without touching app code" seam: everything downstream of the returned FoodCatalog
    // is unchanged from how the Phase 1 stub catalog was consumed.
    public sealed class RemoteMenuLoader : MonoBehaviour
    {
        public sealed class Result
        {
            public bool success;
            public FoodCatalog catalog;
            public string restaurantName;
            public string errorMessage;
        }

        [Serializable]
        private sealed class MenuItemDto
        {
            public string id;
            public string displayName;
            public string[] aliases;
            public int caloriesKcal;
            public string[] allergens;
            public string[] dietTags;
            public float defaultScaleMeters;
        }

        [Serializable]
        private sealed class MenuDto
        {
            public string restaurantName;
            public MenuItemDto[] items;
        }

        // Single entry point for a decoded QR payload: a table QR can either encode a URL to
        // fetch (the hosted-JSON business model) or the menu JSON itself, inline (no hosting
        // needed at all — useful for a quick offline test, and a legitimate option for a small
        // restaurant that doesn't want to host anything). Detected by whether the payload looks
        // like a URL or like JSON.
        public void LoadFromQrPayload(string payload, Action<Result> onComplete)
        {
            var trimmed = payload?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                onComplete?.Invoke(new Result { success = false, errorMessage = "Empty QR payload." });
                return;
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                Result parsed;
                try
                {
                    parsed = ParseMenu(trimmed);
                }
                catch (Exception exception)
                {
                    parsed = new Result { success = false, errorMessage = "Menu data was not valid: " + exception.Message };
                }

                onComplete?.Invoke(parsed);
                return;
            }

            Load(trimmed, onComplete);
        }

        public void Load(string url, Action<Result> onComplete)
        {
            StartCoroutine(LoadRoutine(url, onComplete));
        }

        private IEnumerator LoadRoutine(string url, Action<Result> onComplete)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                onComplete?.Invoke(new Result { success = false, errorMessage = "Empty menu URL." });
                yield break;
            }

            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                var failed = request.result != UnityWebRequest.Result.Success;
#else
                var failed = request.isNetworkError || request.isHttpError;
#endif
                if (failed)
                {
                    onComplete?.Invoke(new Result
                    {
                        success = false,
                        errorMessage = $"Could not load menu ({request.responseCode}): {request.error}"
                    });
                    yield break;
                }

                Result parsed;
                try
                {
                    parsed = ParseMenu(request.downloadHandler.text);
                }
                catch (Exception exception)
                {
                    parsed = new Result { success = false, errorMessage = "Menu file was not valid: " + exception.Message };
                }

                onComplete?.Invoke(parsed);
            }
        }

        private static Result ParseMenu(string json)
        {
            var dto = JsonUtility.FromJson<MenuDto>(json);
            if (dto == null || dto.items == null || dto.items.Length == 0)
            {
                return new Result { success = false, errorMessage = "Menu file had no items." };
            }

            var items = new List<FoodItemDefinition>(dto.items.Length);
            foreach (var itemDto in dto.items)
            {
                if (itemDto == null || string.IsNullOrWhiteSpace(itemDto.id))
                {
                    continue;
                }

                items.Add(FoodItemDefinition.Create(
                    itemDto.id,
                    string.IsNullOrWhiteSpace(itemDto.displayName) ? itemDto.id : itemDto.displayName,
                    itemDto.aliases,
                    itemDto.caloriesKcal,
                    ParseEnumArray<AllergyTag>(itemDto.allergens),
                    ParseEnumArray<DietTag>(itemDto.dietTags),
                    defaultScaleMeters: itemDto.defaultScaleMeters,
                    // Reuse an already-bundled model if this item's id matches one of the 4
                    // known .glb assets (same lookup DemoFoodCatalogFactory uses); otherwise
                    // ProceduralFoodModelFactory's existing fallback covers it.
                    modelPrefab: Resources.Load<GameObject>("Models/" + itemDto.id)));
            }

            if (items.Count == 0)
            {
                return new Result { success = false, errorMessage = "Menu file had no valid items." };
            }

            var catalog = ScriptableObject.CreateInstance<FoodCatalog>();
            catalog.name = string.IsNullOrWhiteSpace(dto.restaurantName) ? "Remote Menu" : dto.restaurantName;
            catalog.Initialize(items.ToArray());

            return new Result { success = true, catalog = catalog, restaurantName = catalog.name };
        }

        // JsonUtility deserializes enum fields from their underlying int value, not by member
        // name, so allergens/dietTags come through the DTO as plain strings (matching the enum
        // member names, e.g. "Gluten") and get parsed here instead — same approach
        // UserAllergenProfile.cs uses to round-trip AllergyTag through PlayerPrefs.
        private static T[] ParseEnumArray<T>(string[] values) where T : struct, Enum
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<T>();
            }

            var results = new List<T>(values.Length);
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<T>(value.Trim(), true, out var parsed))
                {
                    results.Add(parsed);
                }
            }

            return results.ToArray();
        }
    }
}
