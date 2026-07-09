using MenuViewer.Food;
using UnityEngine;

namespace MenuViewer.AR
{
    public static class ProceduralFoodModelFactory
    {
        public static GameObject CreateView(FoodItemDefinition item)
        {
            var root = new GameObject(item != null ? item.DisplayName + " AR View" : "Food AR View");
            if (item == null)
            {
                EnsureInteractiveCollider(root);
                return root;
            }

            var content = item.ModelPrefab != null
                ? Object.Instantiate(item.ModelPrefab)
                : CreateProceduralView(item);

            content.name = item.ModelPrefab != null ? item.ModelPrefab.name : "Procedural Model";
            content.transform.SetParent(root.transform, false);

            NormalizeBottomCentered(root);
            EnsureInteractiveCollider(root);
            root.AddComponent<SlowSpin>();
            return root;
        }

        private static GameObject CreateProceduralView(FoodItemDefinition item)
        {
            var root = new GameObject(item.DisplayName + " AR View");
            CreatePlate(root);

            switch (item.Id)
            {
                case "margherita_pizza":
                    CreatePizza(root);
                    break;
                case "caesar_salad":
                    CreateSalad(root);
                    break;
                case "veggie_burger":
                    CreateBurger(root);
                    break;
                case "vegan_bowl":
                    CreateBowl(root, new Color(0.28f, 0.68f, 0.32f), new Color(0.93f, 0.76f, 0.36f));
                    break;
                case "chicken_ramen":
                    CreateNoodleBowl(root, new Color(0.93f, 0.69f, 0.35f), new Color(0.76f, 0.52f, 0.25f));
                    break;
                case "salmon_sushi":
                    CreateSushi(root);
                    break;
                case "beef_tacos":
                    CreateTacos(root);
                    break;
                case "pad_thai":
                    CreateNoodleBowl(root, new Color(0.86f, 0.54f, 0.23f), new Color(0.68f, 0.42f, 0.18f));
                    break;
                case "mushroom_risotto":
                    CreateBowl(root, new Color(0.86f, 0.79f, 0.63f), new Color(0.45f, 0.30f, 0.20f));
                    break;
                case "chocolate_cake":
                    CreateCake(root);
                    break;
                case "tomato_soup":
                    CreateBowl(root, new Color(0.84f, 0.19f, 0.11f), new Color(0.20f, 0.58f, 0.22f));
                    break;
                case "chicken_curry":
                    CreateBowl(root, new Color(0.93f, 0.58f, 0.15f), new Color(0.95f, 0.83f, 0.52f));
                    break;
                default:
                    CreateGenericFood(root, item.Id);
                    break;
            }

            CreateLabel(root, item.DisplayName);
            return root;
        }

        private static void CreatePlate(GameObject root)
        {
            AddPrimitive(root, PrimitiveType.Cylinder, "Plate", Vector3.zero, new Vector3(1.45f, 0.05f, 1.45f), new Color(0.94f, 0.92f, 0.86f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Plate Rim", new Vector3(0f, 0.05f, 0f), new Vector3(1.55f, 0.025f, 1.55f), new Color(0.82f, 0.80f, 0.74f));
        }

        private static void CreatePizza(GameObject root)
        {
            AddPrimitive(root, PrimitiveType.Cylinder, "Pizza Base", new Vector3(0f, 0.13f, 0f), new Vector3(1.05f, 0.055f, 1.05f), new Color(0.96f, 0.72f, 0.28f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Tomato Sauce", new Vector3(0f, 0.18f, 0f), new Vector3(0.92f, 0.025f, 0.92f), new Color(0.78f, 0.13f, 0.08f));
            for (var i = 0; i < 7; i++)
            {
                var angle = i * Mathf.PI * 2f / 7f;
                var radius = i % 2 == 0 ? 0.28f : 0.42f;
                AddPrimitive(root, PrimitiveType.Sphere, "Mozzarella", new Vector3(Mathf.Cos(angle) * radius, 0.23f, Mathf.Sin(angle) * radius), new Vector3(0.16f, 0.035f, 0.16f), new Color(0.98f, 0.92f, 0.75f));
            }
        }

        private static void CreateSalad(GameObject root)
        {
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                var radius = 0.18f + 0.3f * ((i % 3) / 2f);
                AddPrimitive(root, PrimitiveType.Sphere, "Lettuce", new Vector3(Mathf.Cos(angle) * radius, 0.17f + 0.02f * (i % 2), Mathf.Sin(angle) * radius), new Vector3(0.22f, 0.055f, 0.16f), new Color(0.30f, 0.68f, 0.28f));
            }

            AddPrimitive(root, PrimitiveType.Cube, "Crouton", new Vector3(-0.28f, 0.27f, 0.1f), new Vector3(0.16f, 0.08f, 0.16f), new Color(0.85f, 0.58f, 0.26f));
            AddPrimitive(root, PrimitiveType.Cube, "Chicken", new Vector3(0.18f, 0.28f, -0.15f), new Vector3(0.34f, 0.07f, 0.18f), new Color(0.91f, 0.72f, 0.50f));
        }

        private static void CreateBurger(GameObject root)
        {
            AddPrimitive(root, PrimitiveType.Sphere, "Top Bun", new Vector3(0f, 0.39f, 0f), new Vector3(0.82f, 0.24f, 0.82f), new Color(0.93f, 0.62f, 0.27f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Patty", new Vector3(0f, 0.25f, 0f), new Vector3(0.76f, 0.08f, 0.76f), new Color(0.35f, 0.18f, 0.10f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Lettuce Ring", new Vector3(0f, 0.32f, 0f), new Vector3(0.86f, 0.035f, 0.86f), new Color(0.27f, 0.62f, 0.25f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Bottom Bun", new Vector3(0f, 0.16f, 0f), new Vector3(0.82f, 0.08f, 0.82f), new Color(0.90f, 0.56f, 0.22f));
        }

        private static void CreateBowl(GameObject root, Color baseColor, Color accentColor)
        {
            AddPrimitive(root, PrimitiveType.Cylinder, "Bowl", new Vector3(0f, 0.16f, 0f), new Vector3(0.92f, 0.16f, 0.92f), new Color(0.88f, 0.90f, 0.86f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Food Surface", new Vector3(0f, 0.30f, 0f), new Vector3(0.78f, 0.035f, 0.78f), baseColor);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI * 2f / 6f;
                AddPrimitive(root, PrimitiveType.Sphere, "Topping", new Vector3(Mathf.Cos(angle) * 0.27f, 0.36f, Mathf.Sin(angle) * 0.27f), new Vector3(0.14f, 0.045f, 0.14f), i % 2 == 0 ? accentColor : Color.Lerp(baseColor, Color.white, 0.35f));
            }
        }

        private static void CreateNoodleBowl(GameObject root, Color noodleColor, Color toppingColor)
        {
            CreateBowl(root, noodleColor, toppingColor);
            for (var i = 0; i < 5; i++)
            {
                AddPrimitive(root, PrimitiveType.Cube, "Noodle Strand", new Vector3(-0.3f + i * 0.15f, 0.42f, 0.08f * (i % 2)), new Vector3(0.38f, 0.035f, 0.045f), noodleColor);
            }
        }

        private static void CreateSushi(GameObject root)
        {
            AddSushiPiece(root, new Vector3(-0.32f, 0.19f, 0f));
            AddSushiPiece(root, new Vector3(0.0f, 0.19f, 0.02f));
            AddSushiPiece(root, new Vector3(0.32f, 0.19f, -0.02f));
        }

        private static void AddSushiPiece(GameObject root, Vector3 position)
        {
            AddPrimitive(root, PrimitiveType.Cube, "Rice", position, new Vector3(0.26f, 0.13f, 0.34f), new Color(0.96f, 0.95f, 0.90f));
            AddPrimitive(root, PrimitiveType.Cube, "Salmon", position + Vector3.up * 0.1f, new Vector3(0.28f, 0.055f, 0.36f), new Color(0.96f, 0.45f, 0.32f));
        }

        private static void CreateTacos(GameObject root)
        {
            AddPrimitive(root, PrimitiveType.Cylinder, "Taco Shell Left", new Vector3(-0.22f, 0.22f, 0f), new Vector3(0.46f, 0.06f, 0.7f), new Color(0.93f, 0.70f, 0.30f));
            AddPrimitive(root, PrimitiveType.Cylinder, "Taco Shell Right", new Vector3(0.22f, 0.22f, 0f), new Vector3(0.46f, 0.06f, 0.7f), new Color(0.93f, 0.70f, 0.30f));
            AddPrimitive(root, PrimitiveType.Sphere, "Beef Filling", new Vector3(0f, 0.32f, 0f), new Vector3(0.56f, 0.14f, 0.36f), new Color(0.38f, 0.19f, 0.09f));
            AddPrimitive(root, PrimitiveType.Sphere, "Salsa", new Vector3(0.05f, 0.43f, -0.03f), new Vector3(0.28f, 0.06f, 0.22f), new Color(0.78f, 0.10f, 0.08f));
        }

        private static void CreateCake(GameObject root)
        {
            AddPrimitive(root, PrimitiveType.Cube, "Cake Slice", new Vector3(0f, 0.25f, 0f), new Vector3(0.72f, 0.28f, 0.52f), new Color(0.26f, 0.11f, 0.06f));
            AddPrimitive(root, PrimitiveType.Cube, "Frosting", new Vector3(0f, 0.41f, 0f), new Vector3(0.75f, 0.055f, 0.55f), new Color(0.11f, 0.05f, 0.03f));
            AddPrimitive(root, PrimitiveType.Sphere, "Berry", new Vector3(0.18f, 0.49f, 0.04f), new Vector3(0.12f, 0.08f, 0.12f), new Color(0.72f, 0.04f, 0.12f));
        }

        private static void CreateGenericFood(GameObject root, string id)
        {
            AddPrimitive(root, PrimitiveType.Sphere, "Food", new Vector3(0f, 0.18f, 0f), new Vector3(0.9f, 0.3f, 0.9f), ColorFromId(id));
        }

        private static void CreateLabel(GameObject root, string text)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.14f;
            label.color = Color.black;
        }

        private static GameObject AddPrimitive(GameObject parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            ApplyColor(child, color);
            return child;
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        private static void EnsureInteractiveCollider(GameObject view)
        {
            if (view.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            var bounds = default(Bounds);
            var hasBounds = false;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            var collider = view.AddComponent<BoxCollider>();
            if (!hasBounds)
            {
                collider.center = new Vector3(0f, 0.25f, 0f);
                collider.size = new Vector3(1f, 0.5f, 1f);
                return;
            }

            collider.center = view.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, view.transform.lossyScale.x),
                bounds.size.y / Mathf.Max(0.0001f, view.transform.lossyScale.y),
                bounds.size.z / Mathf.Max(0.0001f, view.transform.lossyScale.z));
        }

        private static void NormalizeBottomCentered(GameObject root)
        {
            if (!TryCalculateContentBounds(root, out var bounds))
            {
                return;
            }

            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            foreach (Transform child in root.transform)
            {
                child.localPosition += offset;
            }
        }

        private static bool TryCalculateContentBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;

            foreach (var collider in root.GetComponentsInChildren<Collider>())
            {
                EncapsulateWorldBounds(root.transform, collider.bounds, ref bounds, ref hasBounds);
            }

            if (hasBounds)
            {
                return true;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                EncapsulateWorldBounds(root.transform, renderer.bounds, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateWorldBounds(
            Transform root,
            Bounds worldBounds,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            var min = worldBounds.min;
            var max = worldBounds.max;

            for (var corner = 0; corner < 8; corner++)
            {
                var worldPoint = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                EncapsulateLocalPoint(root, worldPoint, ref localBounds, ref hasBounds);
            }
        }

        private static void EncapsulateLocalPoint(
            Transform root,
            Vector3 worldPoint,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            var localPoint = root.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(localPoint);
        }

        public static Color ColorFromId(string id)
        {
            var hash = string.IsNullOrEmpty(id) ? 0 : id.GetHashCode();
            var hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.55f, 0.9f);
        }
    }
}
