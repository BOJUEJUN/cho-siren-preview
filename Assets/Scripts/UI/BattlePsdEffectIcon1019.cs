using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>One optional authored sprite in an existing semantic glyph slot; no extra cue or number.</summary>
    public sealed class BattlePsdEffectIcon1019 : MonoBehaviour
    {
        SkillIconGraphic fallback;
        SkillIconKind authoredKind;
        string spriteKey;
        Image authored;

        public void Configure(SkillIconGraphic glyph, SkillIconKind kind, string key)
        {
            fallback = glyph; authoredKind = kind; spriteKey = key;
            if (authored == null)
            {
                var go = new GameObject("AuthoredEffectIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(glyph.transform, false);
                authored = go.GetComponent<Image>();
                var rect = authored.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                authored.preserveAspect = true; authored.raycastTarget = false;
            }
            Clear();
        }

        public Graphic Apply(SkillIconKind kind, Color fallbackColor)
        {
            Clear();
            fallback.Kind = kind; fallback.color = fallbackColor;
            Sprite sprite = kind == authoredKind && !string.IsNullOrEmpty(spriteKey)
                ? BattlePsdArt928.Sprite(spriteKey) : null;
            if (sprite == null) return fallback;
            authored.sprite = sprite;
            authored.color = Color.white; // Preserve the PSD's green/purple; only the caller's alpha may animate.
            authored.preserveAspect = true;
            authored.gameObject.SetActive(true);
            fallback.enabled = false;
            return authored;
        }

        public void Clear()
        {
            if (authored != null)
            {
                authored.gameObject.SetActive(false);
                authored.sprite = null;
                authored.color = Color.white;
            }
            if (fallback != null) fallback.enabled = true;
        }

        void OnDisable() => Clear();
    }
}
