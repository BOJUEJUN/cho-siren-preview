using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-01c 无限骰子 HUD on the reroll bar: one small die per stored 骰子豆 (lit = ready, dim = empty, a pop when one
    /// arrives, a drop when one is spent) and an arc ring around the bar's die that fills towards the next pip. No words.
    /// </summary>
    public sealed class DiceEndlessHud1101 : MonoBehaviour
    {
        private static readonly Color Lit = new Color32(255, 255, 255, 255);
        private static readonly Color Dim = new Color(.32f, .26f, .52f, .55f);
        private static readonly Color RingCharge = new Color32(30, 232, 232, 235);   // #1EE8E8
        private static readonly Color RingFull = new Color32(255, 214, 110, 240);
        private static readonly Color RingTrack = new Color(.05f, .03f, .14f, .78f);

        private Image[] pips = Array.Empty<Image>();
        private Image[] pipGlows = Array.Empty<Image>();
        private float[] pop = Array.Empty<float>();
        private ArcRing1101 ring, track;
        private int shown = -1, capacity;
        private float ringTarget, ringShown;
        private bool full;
        private Func<bool> paused;

        public int ShownPips => Mathf.Max(0, shown);
        public float RingFill => ringShown;
        public ArcRing1101 Ring => ring;

        public static DiceEndlessHud1101 Build(RectTransform pipArea, RectTransform ringArea, Sprite die, Sprite glow,
            int capacity, Func<bool> paused)
        {
            var hud = pipArea.gameObject.AddComponent<DiceEndlessHud1101>();
            hud.paused = paused;
            hud.capacity = Mathf.Max(1, capacity);
            hud.pips = new Image[hud.capacity];
            hud.pipGlows = new Image[hud.capacity];
            hud.pop = new float[hud.capacity];
            float w = pipArea.rect.width, h = pipArea.rect.height;
            float size = Mathf.Min(h * 1.05f, w / (hud.capacity + .35f)); // a touch taller than the old ×N text band
            float gap = (w - size * hud.capacity) / (hud.capacity + 1);
            for (int i = 0; i < hud.capacity; i++)
            {
                var slot = new GameObject("DicePip" + i, typeof(RectTransform)).GetComponent<RectTransform>();
                slot.SetParent(pipArea, false);
                slot.anchorMin = slot.anchorMax = new Vector2(0f, .5f);
                slot.pivot = new Vector2(.5f, .5f);
                slot.sizeDelta = Vector2.one * size;
                slot.anchoredPosition = new Vector2(gap + size * .5f + i * (size + gap), 0f);
                slot.localEulerAngles = new Vector3(0, 0, -8f + i * 6f);
                if (glow != null)
                {
                    var g = NewImage("Glow", slot, glow, new Color(.12f, .9f, .9f, 0f), 1.7f);
                    hud.pipGlows[i] = g;
                }
                hud.pips[i] = NewImage("Die", slot, die, Dim, 1f);
            }
            if (ringArea != null)
            {
                hud.track = NewRing("RerollRingTrack", ringArea, RingTrack, .13f);
                hud.track.Fill = 1f;
                hud.ring = NewRing("RerollRing", ringArea, RingCharge, .13f);
                hud.ring.Fill = 0f;
            }
            return hud;
        }

        /// <summary>Sets what the HUD should show; the motion catches up by itself.</summary>
        public void SetState(int stored, float nextProgress, bool usable)
        {
            stored = Mathf.Clamp(stored, 0, capacity);
            if (shown >= 0 && stored > shown) for (int i = shown; i < stored; i++) pop[i] = 1f;
            shown = stored;
            full = stored >= capacity;
            ringTarget = full ? 1f : Mathf.Clamp01(nextProgress);
            if (ringTarget < ringShown - .5f) ringShown = 0f; // a pip just completed: restart the ring from empty
            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < stored;
                pips[i].color = on ? (usable ? Lit : new Color(1f, 1f, 1f, .7f)) : Dim;
            }
        }

        private void Update()
        {
            if (paused != null && paused()) return;
            float dt = Time.unscaledDeltaTime;
            ringShown = Mathf.MoveTowards(ringShown, ringTarget, dt * 2.5f);
            if (ring != null)
            {
                ring.Fill = ringShown;
                Color c = full ? RingFull : RingCharge;
                if (full) c.a *= .75f + .25f * Mathf.Sin(Time.unscaledTime * 3.2f);
                ring.color = c;
            }
            for (int i = 0; i < pips.Length; i++)
            {
                if (pop[i] > 0f) pop[i] = Mathf.Max(0f, pop[i] - dt * 3f);
                float p = pop[i];
                float scale = BattlePresentationOptions.ReduceMotion ? 1f : 1f + .45f * p * p;
                pips[i].rectTransform.localScale = Vector3.one * scale;
                if (pipGlows[i] != null)
                {
                    bool on = i < shown;
                    float breathe = .22f + .1f * Mathf.Sin(Time.unscaledTime * 2.4f + i);
                    pipGlows[i].color = new Color(.12f, .9f, .9f, on ? breathe + .6f * p : 0f);
                }
            }
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.preserveAspect = true; image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = parent.sizeDelta * scale;
            return image;
        }

        private static ArcRing1101 NewRing(string name, RectTransform parent, Color color, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ArcRing1101));
            go.transform.SetParent(parent, false);
            var ring = go.GetComponent<ArcRing1101>();
            ring.color = color; ring.Thickness = thickness; ring.raycastTarget = false;
            var rect = ring.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return ring;
        }
    }
}
