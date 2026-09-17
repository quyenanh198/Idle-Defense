using IdleHeroDefense.Domain;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class BattleUnitView
    {
        private readonly GameObject root;
        private readonly SpriteRenderer body;
        private readonly Transform healthFill;
        private readonly TextMesh label;
        private int maximumHealth;
        public string UnitId { get; private set; }
        public Vector3 Position => root.transform.position;

        public BattleUnitView(Transform parent, Sprite sprite)
        {
            root = new GameObject("UnitView");
            root.transform.SetParent(parent, false);
            body = root.AddComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.sortingOrder = 1;

            var healthBack = CreateSpriteChild("HealthBack", sprite, new Color(0.12f, 0.12f, 0.16f), 2);
            healthBack.transform.localPosition = new Vector3(0, 0.72f, 0);
            healthBack.transform.localScale = new Vector3(1.05f, 0.12f, 1);
            var fill = CreateSpriteChild("HealthFill", sprite, new Color(0.25f, 0.9f, 0.35f), 3);
            fill.transform.localPosition = new Vector3(0, 0.72f, -0.01f);
            fill.transform.localScale = new Vector3(1f, 0.08f, 1);
            healthFill = fill.transform;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0, -0.78f, 0);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.09f;
            label.fontSize = 40;
            label.color = Color.white;
            labelObject.GetComponent<MeshRenderer>().sortingOrder = 4;
            root.SetActive(false);
        }

        public void Configure(string id, string displayName, int maxHealth, Vector3 position, Color color, Vector3 scale)
        {
            UnitId = id;
            maximumHealth = maxHealth;
            root.name = $"Unit_{id}";
            root.transform.position = position;
            root.transform.localScale = scale;
            body.color = color;
            label.text = displayName;
            UpdateHealth(maxHealth);
        }

        public void UpdateHealth(int health)
        {
            var ratio = maximumHealth <= 0 ? 0f : Mathf.Clamp01((float)health / maximumHealth);
            healthFill.localScale = new Vector3(ratio, 0.08f, 1f);
        }

        public void SetActive(bool active) => root.SetActive(active);

        private SpriteRenderer CreateSpriteChild(string name, Sprite sprite, Color color, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}

