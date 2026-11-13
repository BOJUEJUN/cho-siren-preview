using System;
using System.Collections.Generic;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-02 (赵金 visual-battle, 用户: "我是视觉视听享受游戏…用最少文字的方案，多用设计跟图去表达、音效去表达"):
    /// the battle's picture vocabulary — PSD status symbols instead of floating words, race emblems, the hand stamps,
    /// an edge warning light and a ring countdown for charging bosses. Pure presentation; never blocks input.
    /// </summary>
    public static class VisualBattle1101
    {
        static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();
        static Sprite edge;

        /// <summary>The race emblem (the same hexagons the member cards and team picker use).</summary>
        public static Sprite RaceIcon(CombatRace race)
        {
            switch (race)
            {
                case CombatRace.Demon: return Load("Art/Reference038/race-demon");
                case CombatRace.Charm: return Load("Art/Reference038/race-charm");
                case CombatRace.Mermaid: return Load("Art/Reference038/race-sea");
                case CombatRace.BloodElf: return Load("Art/Reference038/race-blood");
                default: return null;
            }
        }

        public static Color RaceColor(CombatRace race)
        {
            switch (race)
            {
                case CombatRace.Demon: return new Color32(176, 104, 255, 255);
                case CombatRace.Mermaid: return new Color32(64, 214, 255, 255);
                case CombatRace.BloodElf: return new Color32(255, 84, 112, 255);
                default: return new Color32(255, 92, 200, 255);
            }
        }

        public static Sprite Load(string path)
        {
            if (loaded.TryGetValue(path, out Sprite cached) && cached != null) return cached;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                    sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100f);
            }
            loaded[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// The PSD symbol a buff/debuff wears when it lands (the same art as the status badges hung on the unit), or
        /// null when the effect has no symbol of its own.
        /// </summary>
        public static string StatusIconKey(string skillId)
        {
            switch (skillId)
            {
                case "rt-seal":
                case "rt-enemy-charm-seal":
                case "rt-boss-mass-silence":
                case "rt-charm-big":
                    return "icon-heart-lock";
                case "rt-stun":
                case "rt-interrupt":
                case "rt-confuse":
                    return "icon-demon-mask";
                case "rt-slow":
                    return "icon-wave-dice";
                case "rt-armor-break":
                case "rt-barrier-break":
                case "rt-shield-strip":
                case "rt-flaw":
                    return "icon-gold-shield";
                case "rt-poison":
                case "rt-enemy-venom":
                case "rt-demon-small":
                case "rt-demon-big":
                    return "icon-purple-drop";
                case "rt-boss-corrode":
                    return "icon-virus-dice";
                case "rt-enemy-guard-shield":
                case "rt-mermaid-big":
                case "rt-tide-hand":
                case "rt-tactical-guard":
                case "rt-control-ward":
                case "rt-elite-harden":
                case "rt-boss-barrier":
                case "rt-resist":
                case "rt-parry":
                case "rt-disguise":
                case "rt-phantom":
                case "rt-decoy":
                case "rt-dodge":
                    return "icon-cyan-shield";
                case "rt-mermaid-small":
                case "rt-cleanse":
                case "rt-enemy-regen":
                    return "icon-water-heal";
                case "rt-attack-up":
                case "rt-bloodelf-small":
                    return "icon-dagger";
                default:
                    return null;
            }
        }

        /// <summary>Glow behind a status symbol: control violet, debuff magenta, shields cyan, heals green.</summary>
        public static Color StatusGlow(string key)
        {
            switch (key)
            {
                case "icon-heart-lock": return new Color32(255, 70, 190, 255);
                case "icon-demon-mask": return new Color32(176, 104, 255, 255);
                case "icon-wave-dice": return new Color32(90, 170, 255, 255);
                case "icon-gold-shield": return new Color32(255, 196, 80, 255);
                case "icon-purple-drop":
                case "icon-virus-dice": return new Color32(196, 80, 255, 255);
                case "icon-cyan-shield": return new Color32(64, 230, 255, 255);
                case "icon-water-heal": return new Color32(80, 240, 170, 255);
                default: return new Color32(255, 92, 200, 255);
            }
        }

        /// <summary>What a charging enemy is about to do, as a symbol (the ring countdown wears it).</summary>
        public static string IntentIconKey(string intent)
        {
            switch (intent)
            {
                case "禁魔": case "封技": return "icon-heart-lock";
                case "侵蚀": return "icon-virus-dice";
                case "剧毒": return "icon-purple-drop";
                case "眩晕": return "icon-demon-mask";
                case "破甲": return "icon-gold-shield";
                case "迟缓": return "icon-wave-dice";
                case "治疗": return "icon-water-heal";
                case "护盾": return "icon-cyan-shield";
                case "突切": return "icon-red-slash";
                case "全攻": return "icon-star-burst";
                default: return "icon-claw-red";
            }
        }

        /// <summary>Warning tint per intent: control violet-pink, team blasts hot red.</summary>
        public static Color IntentColor(string intent)
        {
            switch (intent)
            {
                case "禁魔": case "封技": return new Color32(255, 64, 196, 255);
                case "侵蚀": case "剧毒": return new Color32(196, 80, 255, 255);
                case "治疗": return new Color32(80, 240, 170, 255);
                case "护盾": return new Color32(64, 230, 255, 255);
                default: return new Color32(255, 70, 92, 255);
            }
        }

        /// <summary>Nine-sliced soft frame: transparent centre, light hugging the edges (constant pixel thickness).</summary>
        public static Sprite EdgeSprite
        {
            get
            {
                if (edge != null) return edge;
                const int size = 128, border = 56;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "EdgeWarning1101", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Min(x, size - 1 - x) / (float)border, dy = Mathf.Min(y, size - 1 - y) / (float)border;
                        float d = Mathf.Clamp01(Mathf.Min(dx, dy));
                        float a = Mathf.Pow(1f - d, 2.4f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                edge = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(border, border, border, border));
                edge.name = "EdgeWarning1101";
                return edge;
            }
        }

        static Sprite softCover;

        /// <summary>Nine-sliced patch with feathered edges, so a cover over baked sample words melts into the band.</summary>
        public static Sprite SoftCoverSprite
        {
            get
            {
                if (softCover != null) return softCover;
                const int w = 64, h = 32, bx = 24, by = 4;
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    { name = "SoftCover1101", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float fx = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / (float)bx), fy = Mathf.Clamp01(Mathf.Min(y, h - 1 - y) / (float)by);
                        float a = Mathf.SmoothStep(0f, 1f, fx) * Mathf.SmoothStep(0f, 1f, fy);
                        pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                texture.SetPixels32(pixels); texture.Apply(false, true);
                softCover = Sprite.Create(texture, new Rect(0, 0, w, h), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(bx, by, bx, by));
                return softCover;
            }
        }

        public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; image.preserveAspect = sprite != null;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            return image;
        }

        public static Text NewText(string name, Transform parent, Font font, string value, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = anchor;
            text.fontStyle = FontStyle.BoldAndItalic; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(.03f, .02f, .08f, .95f); outline.effectDistance = new Vector2(1.4f, -1.4f);
            var r = text.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            return text;
        }

        public static void Place(RectTransform rect, Vector2 centre, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = centre; rect.sizeDelta = size;
        }

        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.9f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }
    }

    /// <summary>Runs a panel's per-frame picture refresh after the panel's own Update rewrote its texts.</summary>
    public sealed class VisualBattleHud1101 : MonoBehaviour
    {
        public Action Tick;
        void LateUpdate() { try { Tick?.Invoke(); } catch (Exception e) { Debug.LogWarning("VisualBattleHud1101: " + e.Message); Tick = null; } }
    }

    /// <summary>
    /// A status symbol that lands on a unit: glow burst, the PSD symbol pops in (0 → 1.3 → 1), holds, rises and fades.
    /// Only key controls (禁魔 / 封印) add a two-character P5 tag under it.
    /// </summary>
    public sealed class StatusPop1101 : MonoBehaviour
    {
        const float Life = 1.05f;
        RectTransform icon, glow, tag;
        Image glowImage;
        Color glowTint;
        CanvasGroup group;
        Func<bool> paused;
        float clock, scale = 1f;
        Vector2 home;

        public static StatusPop1101 Spawn(RectTransform layer, Vector2 at, Sprite symbol, Color tint, string word, Font font,
            Func<bool> isPaused, float size = 64f)
        {
            if (layer == null || symbol == null) return null;
            var root = new GameObject("StatusPop1101", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(layer, false); root.SetAsLastSibling();
            VisualBattle1101.Place(root, at, Vector2.zero);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.anchoredPosition = at;
            var pop = root.gameObject.AddComponent<StatusPop1101>();
            pop.home = at; pop.paused = isPaused; pop.scale = size / 64f;
            pop.group = root.GetComponent<CanvasGroup>(); pop.group.blocksRaycasts = false; pop.group.interactable = false;
            Image halo = VisualBattle1101.NewImage("Glow", root, BattleFxArt1021.Dot, new Color(tint.r, tint.g, tint.b, .85f));
            halo.preserveAspect = false;
            if (BattleFxArt1021.Additive != null) halo.material = BattleFxArt1021.Additive;
            VisualBattle1101.Place(halo.rectTransform, Vector2.zero, Vector2.one * size * 2.1f);
            pop.glow = halo.rectTransform; pop.glowImage = halo; pop.glowTint = tint;
            Image shadow = VisualBattle1101.NewImage("SymbolShadow", root, symbol, new Color(.03f, .01f, .08f, .75f));
            VisualBattle1101.Place(shadow.rectTransform, new Vector2(size * .06f, -size * .06f), Vector2.one * size);
            Image art = VisualBattle1101.NewImage("Symbol", root, symbol, Color.white);
            VisualBattle1101.Place(art.rectTransform, Vector2.zero, Vector2.one * size);
            pop.icon = art.rectTransform;
            shadow.transform.SetParent(art.transform, true);
            shadow.transform.SetAsFirstSibling();
            art.transform.SetAsLastSibling();
            if (!string.IsNullOrEmpty(word) && font != null)
            {
                var holder = new GameObject("Tag", typeof(RectTransform)).GetComponent<RectTransform>();
                holder.SetParent(root, false);
                VisualBattle1101.Place(holder, new Vector2(0f, -size * .7f), new Vector2(size * 1.25f, size * .46f));
                holder.localEulerAngles = new Vector3(0f, 0f, 6f);
                Vector2 plate = new Vector2(size * 1.25f, size * .46f);
                P5Letters1023.Plate(holder, "Shadow", new Vector2(4f, -4f), plate, Brand1025.Ink, -16f);
                P5Letters1023.Plate(holder, "Plate", Vector2.zero, plate, tint, -16f);
                Text t = VisualBattle1101.NewText("Word", holder, font, word, Mathf.RoundToInt(size * .34f), Color.white);
                VisualBattle1101.Place(t.rectTransform, Vector2.zero, plate);
                pop.tag = holder;
            }
            pop.Pose();
            return pop;
        }

        void Update()
        {
            if (paused != null && paused()) return;
            clock += Mathf.Min(Time.unscaledDeltaTime, .05f);
            Pose();
            if (clock >= Life) Destroy(gameObject);
        }

        void Pose()
        {
            bool calm = BattlePresentationOptions.ReduceMotion;
            float t = clock;
            float pop = calm ? 1f : t < .12f ? Mathf.Lerp(0f, 1.3f, t / .12f) : t < .22f ? Mathf.Lerp(1.3f, 1f, (t - .12f) / .1f) : 1f;
            icon.localScale = Vector3.one * pop;
            if (tag != null)
            {
                float k = calm ? 1f : Mathf.Clamp01((t - .1f) / .12f);
                tag.localScale = new Vector3(Mathf.Lerp(1.6f, 1f, k), Mathf.Lerp(0f, 1f, k), 1f);
            }
            float g = Mathf.Clamp01(1f - t / .45f);
            glow.localScale = Vector3.one * (calm ? .8f : .55f + (1f - g) * .7f);
            glowImage.color = new Color(glowTint.r, glowTint.g, glowTint.b, .85f * g);
            float rise = Mathf.Clamp01((t - .62f) / (Life - .62f));
            ((RectTransform)transform).anchoredPosition = home + new Vector2(0f, (calm ? 0f : 26f) * rise * rise + 10f * Mathf.Clamp01(t / .22f));
            group.alpha = 1f - rise * rise;
        }
    }

    /// <summary>
    /// A charging boss: a ring around the threat's symbol that drains towards the release (the countdown), a soft
    /// throb, and an optional two-character tag (禁魔). It follows the boss and retires when the charge ends.
    /// </summary>
    public sealed class ChargeWarning1101 : MonoBehaviour
    {
        RectTransform root, art, pulse;
        Image ringFill, ringTrack, halo;
        Func<Vector2> anchor;
        Func<float> remaining;
        Func<bool> paused;
        float clock, fade = 1f;
        bool ending;
        Color tint;

        public bool Ending => ending;

        public static ChargeWarning1101 Show(RectTransform layer, Font font, Sprite symbol, Color color, string word, float size,
            Func<Vector2> follow, Func<float> remaining01, Func<bool> isPaused)
        {
            if (layer == null) return null;
            var go = new GameObject("ChargeWarning1101", typeof(RectTransform), typeof(CanvasGroup));
            var root = (RectTransform)go.transform;
            root.SetParent(layer, false); root.SetAsLastSibling();
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f); root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = Vector2.one * size;
            go.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var w = go.AddComponent<ChargeWarning1101>();
            w.root = root; w.anchor = follow; w.remaining = remaining01; w.paused = isPaused; w.tint = color;
            w.pulse = new GameObject("Pulse", typeof(RectTransform)).GetComponent<RectTransform>();
            w.pulse.SetParent(root, false); VisualBattle1101.Place(w.pulse, Vector2.zero, Vector2.one * size);
            w.halo = VisualBattle1101.NewImage("Halo", w.pulse, BattleFxArt1021.Dot, new Color(color.r, color.g, color.b, .55f));
            w.halo.preserveAspect = false;
            if (BattleFxArt1021.Additive != null) w.halo.material = BattleFxArt1021.Additive;
            VisualBattle1101.Place(w.halo.rectTransform, Vector2.zero, Vector2.one * size * 1.9f);
            Image disc = VisualBattle1101.NewImage("Disc", w.pulse, BattleFxArt1021.Dot, new Color(.04f, .01f, .1f, .92f));
            disc.preserveAspect = false;
            VisualBattle1101.Place(disc.rectTransform, Vector2.zero, Vector2.one * size * 1.18f);
            Sprite ring = ChoSiren.UI.VisualA1101.RingSprite ?? BattleFxArt1021.Ring;
            w.ringTrack = VisualBattle1101.NewImage("RingTrack", w.pulse, ring, new Color(1f, 1f, 1f, .16f));
            VisualBattle1101.Place(w.ringTrack.rectTransform, Vector2.zero, Vector2.one * size);
            w.ringFill = VisualBattle1101.NewImage("RingFill", w.pulse, ring, color);
            w.ringFill.type = Image.Type.Filled; w.ringFill.fillMethod = Image.FillMethod.Radial360;
            w.ringFill.fillOrigin = (int)Image.Origin360.Top; w.ringFill.fillClockwise = false; w.ringFill.fillAmount = 1f;
            VisualBattle1101.Place(w.ringFill.rectTransform, Vector2.zero, Vector2.one * size);
            Image icon = VisualBattle1101.NewImage("Threat", w.pulse, symbol, Color.white);
            VisualBattle1101.Place(icon.rectTransform, Vector2.zero, Vector2.one * size * .62f);
            w.art = icon.rectTransform;
            if (!string.IsNullOrEmpty(word) && font != null)
            {
                var tag = new GameObject("Tag", typeof(RectTransform)).GetComponent<RectTransform>();
                tag.SetParent(root, false);
                Vector2 plate = new Vector2(size * .92f, size * .36f);
                VisualBattle1101.Place(tag, new Vector2(0f, -size * .66f), plate);
                tag.localEulerAngles = new Vector3(0f, 0f, -5f);
                P5Letters1023.Plate(tag, "Shadow", new Vector2(4f, -4f), plate, Brand1025.Ink, -16f);
                P5Letters1023.Plate(tag, "Plate", Vector2.zero, plate, color, -16f);
                Text t = VisualBattle1101.NewText("Word", tag, font, word, Mathf.RoundToInt(size * .26f), Color.white);
                VisualBattle1101.Place(t.rectTransform, Vector2.zero, plate);
            }
            w.Follow();
            w.Pose();
            return w;
        }

        public void End() { ending = true; }

        void Follow()
        {
            if (anchor == null) return;
            root.anchoredPosition = anchor();
        }

        void Update()
        {
            if (paused != null && paused()) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            clock += dt;
            float left = remaining != null ? remaining() : 0f;
            if (left <= 0f) ending = true;
            if (ending) fade -= dt / .25f;
            if (fade <= 0f) { Destroy(gameObject); return; }
            Follow();
            if (!ending) ringFill.fillAmount = Mathf.MoveTowards(ringFill.fillAmount, Mathf.Clamp01(left), dt * 1.5f);
            Pose();
        }

        void Pose()
        {
            bool calm = BattlePresentationOptions.ReduceMotion;
            float enter = calm ? 1f : VisualBattle1101.OutBack(clock / .28f);
            float beat = calm ? 1f : 1f + .06f * Mathf.Max(0f, Mathf.Sin(clock * 7.5f));
            pulse.localScale = Vector3.one * enter * beat;
            float glow = .35f + .3f * (.5f + .5f * Mathf.Sin(clock * 7.5f));
            halo.color = new Color(tint.r, tint.g, tint.b, glow);
            art.localEulerAngles = new Vector3(0f, 0f, calm ? 0f : Mathf.Sin(clock * 15f) * 4f * Mathf.Clamp01(1f - clock / 1.2f));
            GetComponent<CanvasGroup>().alpha = Mathf.Clamp01(fade);
        }
    }

    /// <summary>Screen-edge warning light: a flash when a threat starts, then a slow throb while it lasts.</summary>
    public sealed class EdgeWarning1101 : MonoBehaviour
    {
        Image frame;
        Func<bool> paused;
        float flash, hold, holdTarget, clock;
        Color tint = Color.red;

        public static EdgeWarning1101 Create(RectTransform host, Func<bool> isPaused)
        {
            var go = new GameObject("EdgeWarning1101", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(host, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var w = go.AddComponent<EdgeWarning1101>();
            w.frame = go.GetComponent<Image>();
            w.frame.sprite = VisualBattle1101.EdgeSprite; w.frame.type = Image.Type.Sliced;
            w.frame.pixelsPerUnitMultiplier = 1f; // ≈ 56 canvas px of light on each edge
            w.frame.raycastTarget = false; w.frame.color = Color.clear;
            w.paused = isPaused;
            return w;
        }

        public void Flash(Color color, float strength = 1f) { tint = color; flash = Mathf.Max(flash, strength); }
        public void Hold(bool on, Color color) { if (on) tint = color; holdTarget = on ? 1f : 0f; }
        public float Level => frame != null ? frame.color.a : 0f;

        void Update()
        {
            if (paused != null && paused()) { frame.color = new Color(tint.r, tint.g, tint.b, 0f); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            clock += dt;
            flash = Mathf.MoveTowards(flash, 0f, dt / .55f);
            hold = Mathf.MoveTowards(hold, holdTarget, dt / .35f);
            float throb = BattlePresentationOptions.ReduceMotion ? .16f : .1f + .12f * (.5f + .5f * Mathf.Sin(clock * 5.2f));
            float a = Mathf.Max(flash * .7f, hold * throb);
            frame.color = new Color(tint.r, tint.g, tint.b, a);
        }
    }
}
