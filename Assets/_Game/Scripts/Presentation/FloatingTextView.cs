using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class FloatingTextView
    {
        private readonly GameObject root;
        private readonly TextMesh text;
        private float remaining;
        private bool reducedMotion;
        public bool IsFinished => remaining <= 0;

        public FloatingTextView(Transform parent)
        {
            root = new GameObject("FloatingText");
            root.transform.SetParent(parent, false);
            text = root.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.fontSize = 44;
            text.fontStyle = FontStyle.Bold;
            root.GetComponent<MeshRenderer>().sortingOrder = 10;
            root.SetActive(false);
        }

        public void Show(Vector3 position, string value, bool ultimate, bool reduceMotion)
        {
            root.transform.position = position + Vector3.up * 0.6f;
            text.text = value;
            text.color = ultimate ? new Color(1f, 0.75f, 0.15f) : Color.white;
            remaining = 0.85f;
            reducedMotion = reduceMotion;
        }

        public void Tick(float deltaTime)
        {
            remaining -= deltaTime;
            if (!reducedMotion) root.transform.position += Vector3.up * (deltaTime * 0.7f);
            var color = text.color;
            color.a = Mathf.Clamp01(remaining / 0.35f);
            text.color = color;
        }

        public void SetActive(bool active) => root.SetActive(active);
    }
}

