using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CasebookGame.UI
{
    public static class PresentationPolishCatalog
    {
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Load(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
                return null;

            if (Cache.TryGetValue(resourceKey, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>($"PresentationPolish/{resourceKey}");
            Cache[resourceKey] = sprite;
            return sprite;
        }

        public static void ApplySprite(Image image, string resourceKey, Color tint, bool preserveAspect = false)
        {
            if (image == null)
                return;

            var sprite = Load(resourceKey);
            if (sprite == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = tint;
            image.preserveAspect = preserveAspect;
        }

        public static void ApplyTextPlate(TMP_Text text, string resourceKey, Color textColor, Vector4 margin)
        {
            if (text == null)
                return;

            var sprite = Load(resourceKey);
            if (sprite == null)
                return;

            var image = text.GetComponent<Image>() ?? text.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            text.margin = margin;
            text.color = textColor;
        }

        public static Image EnsureChildImage(
            Transform parent,
            string childName,
            string resourceKey,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color tint,
            bool preserveAspect = true)
        {
            if (parent == null)
                return null;

            var child = parent.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                child = go.transform;
                go.AddComponent<RectTransform>();
                go.AddComponent<Image>();
            }

            var rt = child.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var image = child.GetComponent<Image>();
            ApplySprite(image, resourceKey, tint, preserveAspect);
            image.raycastTarget = false;
            return image;
        }
    }
}
