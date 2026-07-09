using UnityEngine;

namespace MenuViewer.Food
{
    public static class DemoFoodCatalogFactory
    {
        private static GameObject LoadModel(string id)
        {
            return Resources.Load<GameObject>("Models/" + id);
        }

        public static FoodCatalog CreateCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<FoodCatalog>();
            catalog.name = "Demo Food Catalog";
            catalog.Initialize(CreateItems());
            return catalog;
        }

        public static FoodItemDefinition[] CreateItems()
        {
            return new[]
            {
                FoodItemDefinition.Create(
                    "margherita_pizza",
                    "Margherita Pizza",
                    new[] { "margherita pizza", "pizza margherita", "classic margherita" },
                    850,
                    new[] { AllergyTag.Gluten, AllergyTag.Dairy },
                    new[] { DietTag.Vegetarian }),
                FoodItemDefinition.Create(
                    "caesar_salad",
                    "Caesar Salad",
                    new[] { "caesar salad", "chicken caesar salad", "ceasar salad", "caeser salad", "coesar salad" },
                    520,
                    new[] { AllergyTag.Gluten, AllergyTag.Dairy, AllergyTag.Egg, AllergyTag.Fish },
                    new DietTag[0],
                    modelPrefab: LoadModel("caesar_salad")),
                FoodItemDefinition.Create(
                    "veggie_burger",
                    "Veggie Burger",
                    new[] { "veggie burger", "vegetarian burger", "plant burger", "veggle burger", "veggie bunger" },
                    640,
                    new[] { AllergyTag.Gluten, AllergyTag.Soy },
                    new[] { DietTag.Vegetarian }),
                FoodItemDefinition.Create(
                    "vegan_bowl",
                    "Vegan Bowl",
                    new[] { "vegan bowl", "plant power bowl", "green grain bowl" },
                    480,
                    new AllergyTag[0],
                    new[] { DietTag.Vegan, DietTag.Vegetarian }),
                FoodItemDefinition.Create(
                    "chicken_ramen",
                    "Chicken Ramen",
                    new[] { "chicken ramen", "ramen chicken", "shoyu chicken ramen", "chicken rarnen", "chicken rannen" },
                    720,
                    new[] { AllergyTag.Gluten, AllergyTag.Egg, AllergyTag.Soy },
                    new DietTag[0],
                    modelPrefab: LoadModel("chicken_ramen")),
                FoodItemDefinition.Create(
                    "salmon_sushi",
                    "Salmon Sushi",
                    new[] { "salmon sushi", "sake sushi", "salmon nigiri" },
                    410,
                    new[] { AllergyTag.Fish, AllergyTag.Soy },
                    new DietTag[0],
                    modelPrefab: LoadModel("salmon_sushi")),
                FoodItemDefinition.Create(
                    "beef_tacos",
                    "Beef Tacos",
                    new[] { "beef tacos", "tacos de carne", "crispy beef tacos" },
                    680,
                    new[] { AllergyTag.Gluten, AllergyTag.Dairy },
                    new DietTag[0]),
                FoodItemDefinition.Create(
                    "pad_thai",
                    "Pad Thai",
                    new[] { "pad thai", "prawn pad thai", "chicken pad thai" },
                    760,
                    new[] { AllergyTag.Peanuts, AllergyTag.Egg, AllergyTag.Shellfish, AllergyTag.Soy },
                    new[] { DietTag.Spicy }),
                FoodItemDefinition.Create(
                    "mushroom_risotto",
                    "Mushroom Risotto",
                    new[] { "mushroom risotto", "wild mushroom risotto", "risotto funghi" },
                    590,
                    new[] { AllergyTag.Dairy },
                    new[] { DietTag.Vegetarian }),
                FoodItemDefinition.Create(
                    "chocolate_cake",
                    "Chocolate Cake",
                    new[] { "chocolate cake", "dark chocolate cake", "chocolate lava cake" },
                    430,
                    new[] { AllergyTag.Gluten, AllergyTag.Dairy, AllergyTag.Egg, AllergyTag.Nuts },
                    new[] { DietTag.Vegetarian },
                    modelPrefab: LoadModel("chocolate_cake")),
                FoodItemDefinition.Create(
                    "tomato_soup",
                    "Tomato Soup",
                    new[] { "tomato soup", "roasted tomato soup", "tomato basil soup" },
                    220,
                    new AllergyTag[0],
                    new[] { DietTag.Vegan, DietTag.Vegetarian }),
                FoodItemDefinition.Create(
                    "chicken_curry",
                    "Chicken Curry",
                    new[] { "chicken curry", "butter chicken", "curry chicken" },
                    700,
                    new[] { AllergyTag.Dairy },
                    new[] { DietTag.Spicy })
            };
        }
    }
}
