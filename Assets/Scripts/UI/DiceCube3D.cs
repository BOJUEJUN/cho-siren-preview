using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Owner pick B2 (2026-10-20), smoothed per the owner's review: each die is tossed on its own
    /// (no collisions), all dice share one timing — one high toss toward the camera, one landing,
    /// one small hop — and tumble about a tilted 3D axis whose spin eases out exactly onto the
    /// model's face. The view tilts to top-down while it settles so the last frame matches the
    /// flat die art. Six UI images form the cube.
    /// </summary>
    public sealed class DiceCube3D
    {
        // Face axes in die-local "math" space (x right, y away, z up): +x,-x,+y,-y,+z,-z.
        private static readonly Vector3[] Axes =
            { Vector3.right, Vector3.left, new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
        private static readonly Vector3[] Tangents =
            { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(0, 1, 0) };
        private static readonly int[] Opposite = { 1, 0, 3, 2, 5, 4 };
        private const float SampleRate = 120f;

        public const float TossDuration = 1.05f;
        private const float FirstLanding = .56f, SecondLanding = .78f, TossHeight = 230f;
        private const int UpAxis = 4; // +z: the final pose is the identity, value on top, art upright

        /// <summary>Values for each axis so that axis <paramref name="up"/> shows <paramref name="value"/>.</summary>
        public static int[] Labels(int up, int value)
        {
            var labels = new int[6];
            labels[up] = value; labels[Opposite[up]] = 7 - value;
            var rest = new List<int>();
            for (int n = 1; n <= 6; n++) if (n != value && n != 7 - value) rest.Add(n);
            var others = new List<int>();
            foreach (int i in new[] { 0, 2, 4 }) if (i != up && i != Opposite[up]) others.Add(i);
            labels[others[0]] = rest[0]; labels[Opposite[others[0]]] = 7 - rest[0];
            int second = rest.Find(n => n != rest[0] && n != 7 - rest[0]);
            labels[others[1]] = second; labels[Opposite[others[1]]] = 7 - second;
            return labels;
        }

        private readonly RectTransform rig;
        private readonly Image[] faces = new Image[6];
        private readonly Image shadow, burst, landStar;
        private readonly CelRingGraphic landRing;
        private bool running;
        private Vector3 spinAxis, drift;
        private float spinAngle, size;
        public bool Built => rig != null;
        public event Action<Vector2> Landed;

        private static Sprite softDot;
        private static Sprite burstArt;

        public DiceCube3D(RectTransform parent)
        {
            shadow = NewImage("DieShadow", parent, SoftDot(), new Color(0, 0, 0, 0));
            burst = NewImage("DieBurst", parent, BurstArt(), new Color(1, 1, 1, 0));
            burst.preserveAspect = true;
            // 2026-10-21 research §5.7: a flat hard-edged dust ring + a 2-frame white star replace the sticker burst.
            var ringGo = new GameObject("DieDustRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(CelRingGraphic));
            ringGo.transform.SetParent(parent, false);
            landRing = ringGo.GetComponent<CelRingGraphic>();
            landRing.color = new Color(.75f, .95f, 1f, 0f); landRing.raycastTarget = false; landRing.Gaps = 4;
            var rr = landRing.rectTransform; rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(.5f, .5f); rr.sizeDelta = Vector2.one;
            rr.localScale = new Vector3(1f, .38f, 1f);
            landStar = NewImage("DieLandStar", parent, CelShapes.Star4, new Color(1, 1, 1, 0));
            if (BattleFxArt1021.Additive != null) landStar.material = BattleFxArt1021.Additive;
            rig = new GameObject("DieCube", typeof(RectTransform)).GetComponent<RectTransform>();
            rig.SetParent(parent, false);
            rig.anchorMin = rig.anchorMax = new Vector2(.5f, .5f); rig.sizeDelta = Vector2.zero;
            for (int i = 0; i < 6; i++) faces[i] = NewImage("Face-" + i, rig, null, Color.white);
            SetVisible(false);
        }

        public void Begin(int seed, float dieSize, int value, IReadOnlyList<Sprite> faceSprites)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            size = dieSize;
            labels = Labels(UpAxis, value);
            SetFaces(faceSprites);
            // A tilted axis (mostly horizontal) so the die rolls end over end, never just spins
            // flat toward the viewer.
            float yaw = R() * Mathf.PI * 2f;
            spinAxis = new Vector3(Mathf.Cos(yaw), Mathf.Sin(yaw), (R() - .5f) * .7f).normalized;
            spinAngle = 540f + R() * 270f;
            drift = new Vector3((R() - .5f) * 40f, -70f - R() * 40f, 0f);
            landed = false;
            running = true;
            SetVisible(true);
        }

        private int[] labels;

        /// <summary>
        /// (Re)reads the face art. The battle's opening roll is requested before the PSD layout swaps in the
        /// crystal faces, so the presentation refreshes them on the toss's first visible frame (2026-10-24).
        /// </summary>
        public void SetFaces(IReadOnlyList<Sprite> faceSprites)
        {
            if (labels == null) return;
            for (int i = 0; i < 6; i++)
            {
                faces[i].sprite = faceSprites != null && labels[i] - 1 < faceSprites.Count ? Inner(faceSprites[labels[i] - 1]) : null;
                faces[i].rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        public float Duration => running ? TossDuration : 0f;
        private bool landed;

        /// <summary>Poses the cube at time t; returns the screen offset (relative to the slot) and scale.</summary>
        public void Apply(float t, RectTransform motionRoot, Vector2 origin, Vector3 baseScale)
        {
            if (!running) return;
            float u = Mathf.Clamp01(t / TossDuration);
            float r = size / 2f;
            // Height: toss → land → one small hop → rest. Parabolas meet at zero, so no jolts.
            float height;
            if (u < FirstLanding) { float s1 = u / FirstLanding; height = 30f * (1f - s1) + TossHeight * 4f * s1 * (1f - s1); }
            else if (u < SecondLanding) { float s2 = (u - FirstLanding) / (SecondLanding - FirstLanding); height = TossHeight * .14f * 4f * s2 * (1f - s2); }
            else height = 0f;
            float travel = 1f - Mathf.Pow(1f - Mathf.Clamp01(u / SecondLanding), 2f);
            Vector3 world = new Vector3(drift.x * (1f - travel), drift.y * (1f - travel), r + height);
            // Spin eases out to rest exactly on the final face.
            float spin = Mathf.Clamp01(u / .9f);
            float remaining = Mathf.Pow(1f - spin, 3f);
            Quaternion q = Quaternion.AngleAxis(spinAngle * remaining, spinAxis);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.74f, 1f, u));
            float elevation = Mathf.Lerp(55f, 90f, settle) * Mathf.Deg2Rad;
            float sa = Mathf.Sin(elevation), ca = Mathf.Cos(elevation);
            float sy = world.y * sa + (world.z - r) * ca;
            float depth = -world.y * ca + (world.z - r) * sa;
            float scale = Mathf.Max(.6f, 1f + depth / 620f);
            // A short squash on each landing.
            float squash = Mathf.Exp(-Mathf.Pow((u - FirstLanding) / .035f, 2f)) * .12f + Mathf.Exp(-Mathf.Pow((u - SecondLanding) / .03f, 2f)) * .05f;
            motionRoot.anchoredPosition = origin + new Vector2(world.x, sy);
            motionRoot.localScale = Vector3.Scale(baseScale, new Vector3(scale * (1f + squash), scale * (1f - squash), 1f));
            motionRoot.localRotation = Quaternion.identity;

            var light = new Vector3(-.35f, .55f, -.75f).normalized;
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = ToUi(q * Axes[i], sa, ca), tan = ToUi(q * Tangents[i], sa, ca);
                bool front = n.z < -1e-3f;
                faces[i].enabled = front && faces[i].sprite != null;
                if (!front) continue;
                RectTransform rt = faces[i].rectTransform;
                rt.localPosition = n * r;
                rt.localRotation = Quaternion.LookRotation(-n, tan);
                float shade = Mathf.Lerp(.62f, 1f, Mathf.Clamp01(Vector3.Dot(n, light)));
                faces[i].color = new Color(shade, shade, shade, 1f);
            }

            // Shadow on the table under the die (in the scaled rig's local units).
            float groundY = world.y * sa - sy;
            shadow.rectTransform.anchoredPosition = new Vector2(0f, groundY - r * .55f * (1f - settle)) / scale;
            shadow.rectTransform.sizeDelta = new Vector2(size * 1.15f, size * .5f) * (1f + height / 260f) / scale;
            shadow.color = new Color(0, 0, 0, Mathf.Clamp(.5f - height / 520f, .08f, .5f) * (1f - settle));

            // One landing burst + shake when the toss lands.
            if (!landed && u >= FirstLanding)
            {
                landed = true;
                burstStart = FirstLanding * TossDuration;
                Landed?.Invoke(new Vector2(world.x, sy));
            }
            if (burstStart >= 0f)
            {
                float b = Mathf.Clamp01((t - burstStart) / .38f);
                burst.rectTransform.anchoredPosition = new Vector2(0f, groundY) / scale;
                burst.rectTransform.sizeDelta = Vector2.one * size * Mathf.Lerp(.8f, 2.6f, 1f - (1f - b) * (1f - b)) / scale;
                burst.rectTransform.localRotation = Quaternion.Euler(0, 0, b * 24f);
                burst.color = new Color(1, 1, 1, 0f);
                float since = t - burstStart;
                float ring = Mathf.Clamp01(since / .14f);
                landRing.rectTransform.anchoredPosition = new Vector2(0f, groundY) / scale;
                landRing.SetShape(size * Mathf.Lerp(.35f, 1.15f, 1f - (1f - ring) * (1f - ring)) / scale,
                    size * Mathf.Lerp(.16f, .01f, ring) / scale, 0f);
                var rc = landRing.color; rc.a = ring >= 1f ? 0f : 1f - ring * ring; landRing.color = rc;
                landStar.rectTransform.anchoredPosition = new Vector2(0f, groundY + size * .25f) / scale;
                landStar.rectTransform.sizeDelta = Vector2.one * size * .9f / scale;
                landStar.color = new Color(1, 1, 1, since < .035f ? 1f : 0f);
                if (b >= 1f) burstStart = -1f;
            }
        }

        private float burstStart = -1f;

        // Math (x right, y away, z up) → camera (x right, y up, z toward viewer) → Unity UI (z into screen).
        private static Vector3 ToUi(Vector3 v, float sa, float ca) =>
            new Vector3(v.x, v.y * sa + v.z * ca, -(-v.y * ca + v.z * sa));

        public void SetVisible(bool visible)
        {
            rig.gameObject.SetActive(visible);
            shadow.gameObject.SetActive(visible);
            burst.gameObject.SetActive(visible);
            landRing.gameObject.SetActive(visible);
            landStar.gameObject.SetActive(visible);
            if (!visible)
            {
                burstStart = -1f; burst.color = new Color(1, 1, 1, 0);
                var rc = landRing.color; rc.a = 0f; landRing.color = rc; landStar.color = new Color(1, 1, 1, 0);
            }
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        // 2026-10-23 review #42 (骰子动画有 Bug): each face art is a whole die tile with its own dark rim, so a
        // tumbling cube showed two or three framed tiles and read as several dice. The cube uses the inner face only.
        private static readonly Dictionary<Sprite, Sprite> innerFaces = new Dictionary<Sprite, Sprite>();

        private static Sprite Inner(Sprite art)
        {
            if (art == null || art.texture == null) return art;
            if (innerFaces.TryGetValue(art, out Sprite inner) && inner != null) return inner;
            Rect r = art.textureRect;
            float inset = Mathf.Round(Mathf.Min(r.width, r.height) * .1f);
            inner = Sprite.Create(art.texture, new Rect(r.x + inset, r.y + inset, r.width - 2f * inset, r.height - 2f * inset),
                new Vector2(.5f, .5f), art.pixelsPerUnit);
            inner.hideFlags = HideFlags.DontSave;
            return innerFaces[art] = inner;
        }

        private static Sprite SoftDot()
        {
            if (softDot != null) return softDot;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude / (n / 2f);
                byte a = (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px); tex.Apply();
            softDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            softDot.hideFlags = HideFlags.DontSave;
            return softDot;
        }

        private static Sprite BurstArt()
        {
            if (burstArt != null) return burstArt;
            Texture2D tex = Resources.Load<Texture2D>("UI/DiceFx/dice-land-burst");
            if (tex == null) return null;
            burstArt = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f);
            return burstArt;
        }
    }
}
