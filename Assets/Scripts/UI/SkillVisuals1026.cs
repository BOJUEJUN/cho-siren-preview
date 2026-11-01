using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 魅语迷心 seal (2026-10-26, 赵金: "要有链条的设计，去封印对面的形象"): two glowing chains cross the sealed enemy and a
    /// heart lock clasps them. They tighten in when the seal lands, idle with a soft pulse, and fade when it ends.
    /// </summary>
    public sealed class SealChains1026 : MonoBehaviour
    {
        static Texture2D chainTexture;
        RawImage[] chains;
        Image lockIcon, glow;
        Func<bool> sealedProbe;
        float shown, snap = 1f;

        public static SealChains1026 Ensure(RectTransform figure, Sprite heartLock, Func<bool> isSealed)
        {
            if (figure == null) return null;
            var existing = figure.GetComponentInChildren<SealChains1026>(true);
            if (existing != null) { existing.sealedProbe = isSealed; return existing; }
            var go = new GameObject("SealChains1026", typeof(RectTransform));
            go.transform.SetParent(figure, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var seal = go.AddComponent<SealChains1026>();
            seal.Build(heartLock, isSealed);
            return seal;
        }

        /// <summary>Replays the tighten-in (the seal was just applied or refreshed).</summary>
        public void Snap() => snap = 0f;

        void Build(Sprite heartLock, Func<bool> isSealed)
        {
            sealedProbe = isSealed;
            // A soft round glow behind the figure (a sprite-less Image drew a hard pink square).
            glow = NewImage("Glow", SoftDisc());
            glow.color = new Color(1f, .35f, .75f, 0f);
            Stretch(glow.rectTransform, .05f);
            chains = new RawImage[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Chain-" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(transform, false);
                chains[i] = go.GetComponent<RawImage>();
                chains[i].texture = ChainTexture(); chains[i].raycastTarget = false;
                var r = chains[i].rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.pivot = new Vector2(.5f, .5f);
            }
            lockIcon = NewImage("Lock", heartLock);
            lockIcon.preserveAspect = true;
            var lr = lockIcon.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(.5f, .5f); lr.pivot = new Vector2(.5f, .5f);
            SetVisible(0f);
        }

        Image NewImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform r, float pad)
        {
            r.anchorMin = new Vector2(pad, pad); r.anchorMax = new Vector2(1f - pad, 1f - pad); r.offsetMin = r.offsetMax = Vector2.zero;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool on = sealedProbe != null && sealedProbe();
            if (on && shown <= 0f) snap = 0f;
            shown = Mathf.MoveTowards(shown, on ? 1f : 0f, dt / (on ? .12f : .35f));
            snap = Mathf.Min(1f, snap + dt / .35f);
            SetVisible(shown);
        }

        void SetVisible(float alpha)
        {
            bool any = alpha > 0f;
            foreach (var chain in chains) chain.enabled = any;
            lockIcon.enabled = any && lockIcon.sprite != null; glow.enabled = any;
            if (!any) return;
            var area = ((RectTransform)transform).rect;
            float w = Mathf.Max(40f, area.width), h = Mathf.Max(40f, area.height);
            float length = Mathf.Sqrt(w * w + h * h) * .8f, thick = Mathf.Clamp(w * .09f, 12f, 30f);
            float ease = 1f - Mathf.Pow(1f - snap, 3f);
            float pulse = BattlePresentationOptions.ReduceMotion ? 1f : .85f + .15f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < 2; i++)
            {
                var r = chains[i].rectTransform;
                float angle = (i == 0 ? 1f : -1f) * Mathf.Atan2(h, w) * Mathf.Rad2Deg;
                // the chains sweep in from far out and tighten onto the figure
                r.sizeDelta = new Vector2(thick, length * Mathf.Lerp(1.6f, 1f, ease));
                r.localEulerAngles = new Vector3(0f, 0f, 90f - angle + (1f - ease) * (i == 0 ? 25f : -25f));
                r.anchoredPosition = Vector2.zero;
                chains[i].uvRect = new Rect(0f, Time.unscaledTime * (1f - ease) * 2f, 1f, r.sizeDelta.y / (thick * 2.2f));
                chains[i].color = new Color(1f, 1f, 1f, alpha);
            }
            float lockSize = Mathf.Clamp(w * .32f, 44f, 120f);
            lockIcon.rectTransform.sizeDelta = Vector2.one * lockSize * Mathf.Lerp(2f, 1f, ease) * (snap >= 1f ? pulse * .1f + .9f : 1f);
            lockIcon.color = new Color(1f, 1f, 1f, alpha * Mathf.Clamp01(snap * 3f));
            glow.color = new Color(1f, .35f, .75f, .35f * alpha * pulse);
        }

        static Sprite softDisc;
        internal static Sprite SoftDisc()
        {
            if (softDisc != null) return softDisc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * a * a));
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return softDisc = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100f);
        }

        /// <summary>A vertical run of interlocking oval links, steel white with a dark rim (tiles along V).</summary>
        static Texture2D ChainTexture()
        {
            if (chainTexture != null) return chainTexture;
            const int w = 32, h = 72;
            chainTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = 0f, light = 0f;
                // link A: an upright oval ring centred in the tile; link B: a narrow side-on link at the seam
                float ax = (x + .5f - w * .5f) / (w * .44f), ay = (y + .5f - h * .5f) / (h * .36f);
                float ra = Mathf.Sqrt(ax * ax + ay * ay);
                float ringA = 1f - Mathf.Abs(ra - .78f) / .2f;
                float by = (Mathf.Repeat(y + h * .5f, h) + .5f - h * .5f) / (h * .3f), bx = (x + .5f - w * .5f) / (w * .14f);
                float rb = Mathf.Sqrt(bx * bx + by * by);
                float ringB = 1f - Mathf.Abs(rb - .8f) / .25f;
                float ring = Mathf.Max(ringA, ringB);
                if (ring > 0f) { a = Mathf.Clamp01(ring * 2.2f); light = Mathf.Clamp01(.55f + .45f * ring - ax * .15f); }
                byte v = (byte)(255 * light);
                px[y * w + x] = new Color32((byte)Mathf.Min(255, v + 20), v, (byte)Mathf.Min(255, v + 30), (byte)(255 * a));
            }
            chainTexture.SetPixels32(px); chainTexture.Apply(false, true);
            return chainTexture;
        }
    }

    /// <summary>
    /// 深海佑域 (2026-10-26, 赵金: "人鱼加盾的方式要有区别…现在看不出加了盾"): a wobbling sea-teal water bubble over the
    /// member while her ward lasts — distinct from the 应急守护 crest.
    /// </summary>
    public sealed class WaterWard1026 : MonoBehaviour
    {
        static Sprite bubble;
        Image image;
        Func<bool> wardProbe;
        float shown, phase;

        public static WaterWard1026 Ensure(RectTransform host, Rect localArea, Func<bool> isWarded)
        {
            if (host == null) return null;
            var existing = host.GetComponentInChildren<WaterWard1026>(true);
            if (existing != null) { existing.wardProbe = isWarded; return existing; }
            var go = new GameObject("WaterWard1026", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(host, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = localArea.size; rect.anchoredPosition = new Vector2(localArea.center.x, -localArea.center.y);
            var ward = go.AddComponent<WaterWard1026>();
            ward.image = go.GetComponent<Image>(); ward.image.sprite = Bubble(); ward.image.raycastTarget = false;
            ward.wardProbe = isWarded; ward.phase = UnityEngine.Random.value * 6f;
            ward.image.color = new Color(1, 1, 1, 0);
            return ward;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            bool on = wardProbe != null && wardProbe();
            shown = Mathf.MoveTowards(shown, on ? 1f : 0f, dt / (on ? .25f : .4f));
            image.enabled = shown > 0f;
            if (!image.enabled) return;
            phase += dt;
            bool still = BattlePresentationOptions.ReduceMotion;
            float wob = still ? 0f : Mathf.Sin(phase * 2.6f) * .025f;
            float pop = 1f + (1f - shown) * .15f;
            transform.localScale = new Vector3((1f + wob) * pop, (1f - wob) * pop, 1f);
            image.color = new Color(1f, 1f, 1f, shown * (still ? .85f : .75f + .15f * Mathf.Sin(phase * 1.7f)));
            transform.SetAsLastSibling();
        }

        static Sprite Bubble()
        {
            if (bubble != null) return bubble;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            Color32 teal = Brand1025.Teal;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 1f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                float rim = Mathf.Pow(Mathf.Clamp01((r - .62f) / .38f), 2.2f);           // bright water edge
                float body = .12f;                                                         // faint tinted inside
                float shine = Mathf.Clamp01(1f - Mathf.Sqrt((dx + .38f) * (dx + .38f) + (dy - .42f) * (dy - .42f)) / .22f); // gloss
                float ripple = .06f * Mathf.Max(0f, Mathf.Sin(r * 26f));
                float a = Mathf.Clamp01(body + rim * .85f + ripple + shine * .7f) * Mathf.Clamp01((1f - r) * 18f);
                Color c = Color.Lerp(new Color(teal.r / 255f, teal.g / 255f, teal.b / 255f), Color.white, Mathf.Clamp01(shine + rim * .35f));
                px[y * n + x] = new Color(c.r, c.g, c.b, a);
            }
            tex.SetPixels32(px); tex.Apply(false, true);
            return bubble = Sprite.Create(tex, new Rect(0, 0, n, n), Vector2.one * .5f, 100f);
        }
    }

    /// <summary>
    /// 汐愈微光 heal (2026-10-26, 赵金: "十字符号再扩大一点…每个人加多少血…框会是散绿"): a big green plus rises from the
    /// member and her card flashes green.
    /// </summary>
    public sealed class HealBurst1026 : MonoBehaviour
    {
        Image plus, flash;
        float t;

        public static void Play(RectTransform card, Sprite plusSprite, Sprite flashSprite)
        {
            if (card == null) return;
            var go = new GameObject("HealBurst1026", typeof(RectTransform));
            go.transform.SetParent(card, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var burst = go.AddComponent<HealBurst1026>();
            burst.flash = burst.Add("Flash", flashSprite, Vector2.zero, Vector2.one);
            burst.plus = burst.Add("Plus", plusSprite, new Vector2(.5f, .55f), new Vector2(.5f, .55f));
            burst.plus.preserveAspect = true;
            float size = Mathf.Max(60f, rect.rect.width * .55f);
            burst.plus.rectTransform.sizeDelta = Vector2.one * size;
            go.transform.SetAsLastSibling();
        }

        Image Add(string name, Sprite sprite, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.raycastTarget = false;
            var r = image.rectTransform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            return image;
        }

        void LateUpdate()
        {
            t += Mathf.Min(Time.unscaledDeltaTime, .1f) / .9f;
            if (t >= 1f) { Destroy(gameObject); return; }
            float rise = 1f - Mathf.Pow(1f - t, 3f);
            plus.rectTransform.anchoredPosition = new Vector2(0f, rise * 60f);
            plus.rectTransform.localScale = Vector3.one * (BattlePresentationOptions.ReduceMotion ? 1f : Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(t * 4f)));
            plus.color = new Color(.35f, 1f, .55f, Mathf.Clamp01((1f - t) * 2.5f));
            flash.color = new Color(.3f, 1f, .5f, .45f * Mathf.Clamp01(1f - t * 1.6f));
        }
    }
}
