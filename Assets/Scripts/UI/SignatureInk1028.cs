using System.Collections.Generic;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// A name drawn like a pen signature on the contract (2026-10-28, 少爷: "字能不能做手写的特效"): ink-blue slanted
    /// glyphs, each a little off in size, height and angle (seeded per character so a name always signs the same
    /// way), squeezed together like handwriting, with a closing swoosh. <see cref="Write"/> signs it stroke by stroke:
    /// every character is revealed left to right behind a moving pen edge, then the swoosh is drawn.
    /// </summary>
    public sealed class SignatureInk1028 : MonoBehaviour
    {
        public static readonly Color32 Ink = new Color32(30, 38, 92, 255);
        private const float GlyphSeconds = .26f, SwooshSeconds = .22f;

        private sealed class Glyph
        {
            public RectTransform Clip;
            public float Width;
        }

        private readonly List<Glyph> glyphs = new List<Glyph>();
        private RectTransform line, swoosh, tail;
        private Font font;
        private int size;
        private string shown = string.Empty;
        private float writeStart = -1f, swooshWidth, tailWidth;

        /// <summary>True while a <see cref="Write"/> is still putting ink down.</summary>
        public bool Writing => writeStart >= 0f;

        public static SignatureInk1028 Create(RectTransform parent, Font font, int size)
        {
            var root = new GameObject("SignatureInk1028", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var ink = root.gameObject.AddComponent<SignatureInk1028>();
            ink.font = font;
            ink.size = size;
            ink.line = new GameObject("Glyphs", typeof(RectTransform)).GetComponent<RectTransform>();
            ink.line.SetParent(root, false);
            ink.line.anchorMin = ink.line.anchorMax = ink.line.pivot = new Vector2(0f, .5f);
            ink.line.sizeDelta = Vector2.zero; // a zero-size pen point: glyphs and strokes are placed from it
            ink.line.anchoredPosition = new Vector2(6f, 4f);
            ink.swoosh = ink.Stroke("Swoosh", 3.2f);
            ink.tail = ink.Stroke("Tail", 2.2f);
            return ink;
        }

        private RectTransform Stroke(string name, float thickness)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(line, false);
            image.sprite = PunkUi.SlantFill;
            image.color = Ink;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.sizeDelta = new Vector2(0f, thickness);
            image.gameObject.SetActive(false);
            return rect;
        }

        /// <summary>-1..1, fixed for a character at a position so a name always signs the same way.</summary>
        private static float Jitter(int index, char c, int channel)
        {
            unchecked
            {
                uint h = (uint)(index * 73856093) ^ (uint)(c * 19349663) ^ (uint)(channel * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h % 2001) / 1000f - 1f;
            }
        }

        /// <summary>Shows <paramref name="name"/> already signed (no animation).</summary>
        public void SetName(string name)
        {
            Build(name);
            writeStart = -1f;
            Reveal(float.MaxValue);
        }

        /// <summary>Signs <paramref name="name"/> stroke by stroke, starting now.</summary>
        public void Write(string name)
        {
            Build(name);
            writeStart = Time.unscaledTime;
            Reveal(0f);
        }

        private void Build(string name)
        {
            name = name ?? string.Empty;
            foreach (Glyph glyph in glyphs) Destroy(glyph.Clip.gameObject);
            glyphs.Clear();
            shown = name;
            float x = 0f;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                float s = size * (1f + Jitter(i, c, 0) * .1f) * (i == 0 ? 1.12f : 1f); // the first stroke is the boldest
                float advance = c == ' ' ? s * .35f : c < 128 ? s * .56f : s * .84f;
                float boxW = s * 1.3f, boxH = s * 1.6f;
                // The clip grows left to right over the character: the pen edge.
                var clip = new GameObject("Stroke-" + i, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                clip.SetParent(line, false);
                clip.anchorMin = clip.anchorMax = new Vector2(0f, .5f);
                clip.pivot = new Vector2(0f, .5f);
                float left = x + advance * .5f - boxW * .5f;
                clip.anchoredPosition = new Vector2(left, Jitter(i, c, 1) * 5f + i * -1.2f);
                clip.sizeDelta = new Vector2(boxW, boxH);
                var text = new GameObject("Glyph", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                text.transform.SetParent(clip, false);
                text.font = font;
                text.fontSize = Mathf.RoundToInt(s);
                text.fontStyle = FontStyle.Italic;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.supportRichText = false;
                text.raycastTarget = false;
                text.color = Ink;
                text.text = c.ToString();
                RectTransform rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(boxW, boxH);
                rect.anchoredPosition = new Vector2(boxW * .5f, 0f);
                rect.localEulerAngles = new Vector3(0f, 0f, Jitter(i, c, 2) * 7f - 3f);
                glyphs.Add(new Glyph { Clip = clip, Width = boxW });
                x += advance;
            }
            bool any = glyphs.Count > 0;
            swoosh.gameObject.SetActive(any);
            tail.gameObject.SetActive(any);
            swooshWidth = x + 26f;
            tailWidth = size * .55f;
            if (any)
            {
                // One long stroke under the name that lifts at its end, and a short flick off the last character.
                swoosh.SetAsLastSibling();
                tail.SetAsLastSibling();
                swoosh.anchoredPosition = new Vector2(-4f, -size * .8f);
                swoosh.localEulerAngles = new Vector3(0f, 0f, 3.5f);
                tail.anchoredPosition = new Vector2(x + 2f, -size * .62f);
                tail.localEulerAngles = new Vector3(0f, 0f, 22f);
            }
            // A long name shrinks to stay on the signature line.
            float room = ((RectTransform)transform).rect.width - 12f;
            line.localScale = Vector3.one * (room > 0f && x > room ? room / x : 1f);
        }

        /// <summary>Ink laid down <paramref name="elapsed"/> seconds into the signing.</summary>
        private void Reveal(float elapsed)
        {
            for (int i = 0; i < glyphs.Count; i++)
            {
                float t = Mathf.Clamp01((elapsed - i * GlyphSeconds) / GlyphSeconds);
                float e = 1f - (1f - t) * (1f - t);
                glyphs[i].Clip.sizeDelta = new Vector2(glyphs[i].Width * e, glyphs[i].Clip.sizeDelta.y);
            }
            float afterGlyphs = elapsed - glyphs.Count * GlyphSeconds;
            float s = Mathf.Clamp01(afterGlyphs / SwooshSeconds);
            swoosh.sizeDelta = new Vector2(swooshWidth * s, swoosh.sizeDelta.y);
            float flick = Mathf.Clamp01((afterGlyphs - SwooshSeconds * .6f) / (SwooshSeconds * .6f));
            tail.sizeDelta = new Vector2(tailWidth * flick, tail.sizeDelta.y);
        }

        private void Update()
        {
            if (writeStart < 0f) return;
            float elapsed = Time.unscaledTime - writeStart;
            Reveal(elapsed);
            if (elapsed > glyphs.Count * GlyphSeconds + SwooshSeconds * 1.3f) writeStart = -1f;
        }
    }
}
