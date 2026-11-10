using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// 2026-11-10 (少爷: 鼠标跟随的动效跟点击效果怎么没了，弄回来): a game cursor (fox-ear arrow), the web page's neon-shard mouse trail
    /// (2026-10-19) and a click burst, now inside the game so the PC / Mac apps have them too. Mouse only: phones and
    /// touch screens never see it, and 减弱动画 turns the shards off (the cursor stays).
    /// </summary>
    public sealed class PointerTrail1110 : MonoBehaviour
    {
        private static readonly Color32[] Colors =
        {
            new Color32(255, 46, 147, 255), new Color32(63, 240, 255, 255), new Color32(255, 255, 255, 255),
            new Color32(255, 46, 147, 255), new Color32(181, 140, 255, 255),
        };
        private static readonly Color32 StarColor = new Color32(255, 233, 168, 255);
        private const int MaxShards = 160;

        private sealed class Shard
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Spin, Life;
        }

        private RectTransform layer;
        private readonly List<Shard> live = new List<Shard>(MaxShards);
        private readonly Stack<Shard> pool = new Stack<Shard>();
        private Sprite shardSprite, starSprite;
        private Vector2 last;
        private bool hasLast;
        private float distance;

        public static PointerTrail1110 Install()
        {
            if (Application.isMobilePlatform) return null;
            var root = new GameObject("PointerTrail1110", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(root);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            return root.AddComponent<PointerTrail1110>();
        }

        private void Awake()
        {
            layer = (RectTransform)transform;
            shardSprite = MakeShard();
            starSprite = MakeStar();
            // 2026-11-10 (少爷选 6 号, 32 px; 64 px was twice too big; 少爷: 逐帧画). Frames share one tip position:
            // press-in (squash, ears back), a twinkling spark while held, and a release bounce with two hearts drifting off.
            rest = Frame("rest");
            pressIn = new[] { Frame("press1"), Frame("press2") };
            hold = new[] { Frame("hold1"), Frame("hold2") };
            release = new[] { Frame("release1"), Frame("release2"), Frame("release3"), Frame("release4") };
            SetCursor(rest);
        }

        private static readonly Vector2 Hotspot = new Vector2(3.5f, 3.5f);
        private const float FrameSeconds = .06f, HoldFrameSeconds = .18f;
        private Texture2D rest, cursorShown;
        private Texture2D[] pressIn, hold, release;
        private Texture2D[] playing;
        private float playStart;
        private bool held;

        private static Texture2D Frame(string name) => Resources.Load<Texture2D>("Art/Cursor1110/pointer-" + name);

        private void SetCursor(Texture2D texture)
        {
            if (texture == null || texture == cursorShown) return;
            cursorShown = texture;
            Cursor.SetCursor(texture, Hotspot, CursorMode.Auto);
        }

        private void UpdateCursorFrame()
        {
            float now = Time.unscaledTime;
            if (Input.GetMouseButtonDown(0)) { held = true; playing = pressIn; playStart = now; }
            else if (Input.GetMouseButtonUp(0)) { held = false; playing = release; playStart = now; }
            if (playing != null)
            {
                int index = Mathf.FloorToInt((now - playStart) / FrameSeconds);
                if (index < playing.Length) { SetCursor(playing[index]); return; }
                playing = null;
                playStart = now;
            }
            if (held) SetCursor(hold[Mathf.FloorToInt((now - playStart) / HoldFrameSeconds) % hold.Length]);
            else SetCursor(rest);
        }

        private void Update()
        {
            UpdateCursorFrame();
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f) * 60f;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Shard s = live[i];
                s.Life -= .028f * dt;
                if (s.Life <= 0f) { Recycle(i); continue; }
                s.Rect.anchoredPosition += s.Velocity * dt;
                float drag = Mathf.Pow(.93f, dt);
                s.Velocity *= drag;
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.Rect.localEulerAngles.z + s.Spin * dt);
                Color c = s.Image.color; c.a = Mathf.Min(1f, s.Life * 1.4f); s.Image.color = c;
            }

            if (!Input.mousePresent || Input.touchCount > 0 || BattlePresentationOptions.ReduceMotion) { hasLast = false; return; }
            Vector2 p = Input.mousePosition;
            bool inside = p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;
            if (!inside) { hasLast = false; return; }
            if (Input.GetMouseButtonDown(0)) Burst(p);
            Vector2 d = hasLast ? p - last : Vector2.zero;
            last = p; hasLast = true;
            distance += d.magnitude;
            if (distance < 11f * UiScale) return;
            distance = 0f;
            float len = Mathf.Max(d.magnitude, 1f);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                bool star = Random.value < .18f;
                // Web coordinates grow downward; here y grows upward, so the perpendicular flips sign.
                Vector2 v = new Vector2(-d.y / len, d.x / len) * side * (1f + Random.value * 1.5f) - d * .04f + new Vector2(0f, .2f);
                Spawn(p, v * UiScale, star, star ? 4f + Random.value * 3f : 5f + Random.value * 6f, 1f);
            }
        }

        private void Burst(Vector2 p)
        {
            for (int k = 0; k < 12; k++)
            {
                float angle = k / 12f * Mathf.PI * 2f + Random.value * .4f;
                Vector2 v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (3.2f + Random.value * 2.4f);
                bool star = k % 4 == 0;
                Spawn(p, v * UiScale, star, star ? 6f + Random.value * 3f : 7f + Random.value * 6f, .9f);
            }
        }

        private float UiScale => Mathf.Max(1f, Screen.height / 1100f);

        private void Spawn(Vector2 screen, Vector2 velocity, bool star, float size, float life)
        {
            if (live.Count >= MaxShards) Recycle(0);
            Shard s = pool.Count > 0 ? pool.Pop() : Create();
            s.Rect.gameObject.SetActive(true);
            s.Image.sprite = star ? starSprite : shardSprite;
            Color32 c = star ? StarColor : Colors[Random.Range(0, Colors.Length)];
            s.Image.color = c;
            float px = size * 2.6f * UiScale;
            s.Rect.sizeDelta = new Vector2(px, px);
            s.Rect.anchoredPosition = screen;
            s.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            s.Velocity = velocity;
            s.Spin = (Random.value - .5f) * 16f;
            s.Life = life;
            live.Add(s);
        }

        private Shard Create()
        {
            var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(layer, false);
            var shard = new Shard { Rect = (RectTransform)go.transform, Image = go.GetComponent<Image>() };
            shard.Rect.anchorMin = shard.Rect.anchorMax = Vector2.zero;
            shard.Image.raycastTarget = false;
            return shard;
        }

        private void Recycle(int index)
        {
            Shard s = live[index];
            live.RemoveAt(index);
            s.Rect.gameObject.SetActive(false);
            pool.Push(s);
        }

        // A slim triangle shard: white fill (tinted per shard) inside a dark ink rim, like the web canvas.
        private static Sprite MakeShard()
        {
            const int n = 48;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Vector2 a = new Vector2(.05f, .27f) * n, b = new Vector2(.98f, .47f) * n, c = new Vector2(.32f, .80f) * n;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float inside = Mathf.Min(Edge(a, b, p), Mathf.Min(Edge(b, c, p), Edge(c, a, p)));
                byte alpha = (byte)(Mathf.Clamp01(inside + .5f) * 255f);
                bool rim = inside < 2.2f;
                byte v = rim ? (byte)26 : (byte)255;
                px[y * n + x] = new Color32(v, rim ? (byte)11 : v, rim ? (byte)51 : v, alpha);
            }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        }

        private static Sprite MakeStar()
        {
            const int n = 48;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            Vector2 center = new Vector2(n, n) * .5f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 d = new Vector2(x + .5f, y + .5f) - center;
                float r = d.magnitude / (n * .5f);
                float angle = Mathf.Atan2(d.y, d.x);
                // Four-point star (the web's 8-vertex star) with a soft glow halo.
                float lobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 6f);
                float edge = .3f + .7f * lobe;
                float body = Mathf.Clamp01((edge - r) * 14f);
                float glow = Mathf.Clamp01(1f - r) * .35f;
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(body, glow) * 255f));
            }
            t.SetPixels32(px); t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        }

        // Signed distance (in pixels) of p from edge a→b, positive on the inner side of a counter-clockwise triangle.
        private static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 e = b - a;
            return (e.x * (p.y - a.y) - e.y * (p.x - a.x)) / e.magnitude;
        }
    }
}
