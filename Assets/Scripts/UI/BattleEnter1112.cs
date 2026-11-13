using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>Ground fractures, lights up, then the enemy rises. Layout still owns the anchor.</summary>
    public sealed class BattleEnter1112 : MonoBehaviour
    {
        public const float RevealAt = .48f, Duration = 1.05f;
        private CanvasGroup group;
        private BattleGroundCrack1113 crack;
        private float age = -1f;
        private Func<bool> paused;
        public bool IsShowing => age >= 0f;

        public static void Show(RectTransform anchor, bool on, Func<bool> isPaused = null)
        {
            if (anchor == null) return;
            bool wasOn = anchor.gameObject.activeSelf;
            var enter = anchor.GetComponent<BattleEnter1112>();
            anchor.gameObject.SetActive(on);
            if (!on || wasOn && enter != null || !Application.isPlaying || BattlePresentationOptions.ReduceMotion) return;
            if (enter == null) enter = anchor.gameObject.AddComponent<BattleEnter1112>();
            enter.paused = isPaused;
            enter.Play();
        }

        private void Play()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (crack == null)
            {
                var go = new GameObject("GroundFracture1113", typeof(RectTransform), typeof(CanvasRenderer));
                go.transform.SetParent(transform.parent, false);
                crack = go.AddComponent<BattleGroundCrack1113>();
                crack.raycastTarget = false;
            }
            crack.gameObject.SetActive(true);
            age = 0f;
            Advance(0f);
        }

        private void Update() { if (paused?.Invoke() != true) Advance(Mathf.Min(Time.unscaledDeltaTime, .05f)); }

        public void Advance(float seconds)
        {
            if (age < 0f) return;
            age = Mathf.Min(Duration, age + Mathf.Max(0f, seconds));
            var anchor = (RectTransform)transform;
            if (crack != null)
            {
                var rt = crack.rectTransform;
                rt.position = anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMin + 8f));
                rt.sizeDelta = new Vector2(Mathf.Clamp(anchor.rect.width * .9f, 150f, 380f), 110f);
                rt.SetSiblingIndex(anchor.GetSiblingIndex());
                crack.Progress = age / Duration;
            }
            float t = Mathf.Clamp01((age - RevealAt) / (Duration - RevealAt));
            if (group != null) { group.alpha = Mathf.SmoothStep(0f, 1f, t); group.blocksRaycasts = t > .5f; }
            float s = Mathf.Lerp(.9f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            transform.localScale = new Vector3(s, s, 1f);
            if (age >= Duration)
            {
                age = -1f;
                if (crack != null) crack.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
            if (crack != null) crack.gameObject.SetActive(false);
            transform.localScale = Vector3.one;
            age = -1f;
        }
        private void OnDestroy() { if (crack != null) Destroy(crack.gameObject); }
    }

    /// <summary>Ink fracture with a white-violet inner seam; no opaque rectangular flash.</summary>
    public sealed class BattleGroundCrack1113 : MaskableGraphic
    {
        private float progress;
        public float Progress { set { progress = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float grow = Mathf.Clamp01(progress / .42f);
            float light = Mathf.Clamp01((progress - .25f) / .24f);
            float fade = 1f - Mathf.Clamp01((progress - .72f) / .28f);
            Vector2 size = rectTransform.rect.size;
            Seam(vh, size, grow, light, fade, new[] { Vector2.zero, new Vector2(-.1f,.04f), new Vector2(-.22f,-.02f), new Vector2(-.34f,.05f), new Vector2(-.49f,0f) }, 1f);
            Seam(vh, size, grow, light, fade, new[] { Vector2.zero, new Vector2(.11f,-.04f), new Vector2(.23f,.025f), new Vector2(.35f,-.02f), new Vector2(.49f,.035f) }, 1f);
            Seam(vh, size, grow, light, fade, new[] { new Vector2(-.18f,0f), new Vector2(-.23f,.18f), new Vector2(-.33f,.3f) }, .7f);
            Seam(vh, size, grow, light, fade, new[] { new Vector2(.14f,-.02f), new Vector2(.21f,-.2f), new Vector2(.34f,-.38f) }, .65f);
            Seam(vh, size, grow, light, fade, new[] { new Vector2(.33f,-.02f), new Vector2(.38f,.12f), new Vector2(.44f,.18f) }, .45f);
        }
        private static void Seam(VertexHelper vh, Vector2 size, float grow, float light, float fade, Vector2[] points, float strength)
        {
            for (int i = 1; i < points.Length; i++)
            {
                float t = Mathf.Clamp01(grow * (points.Length - 1) - (i - 1));
                if (t <= 0f) break;
                Vector2 a = Vector2.Scale(points[i - 1], size), b = Vector2.Scale(Vector2.Lerp(points[i - 1], points[i], t), size);
                float taper = (1f - .65f * i / (points.Length - 1)) * strength * fade;
                Line(vh, a, b, 10f * taper, new Color(.035f, .01f, .08f, fade));
                Line(vh, a, b, (2.5f + light * 2f) * taper, new Color(.64f, .28f, 1f, fade));
                Line(vh, a, b, 1.5f * light * taper, new Color(.96f, .88f, 1f, light * fade));
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            if (width <= 0f) return;
            Vector2 delta = (b - a).normalized, n = new Vector2(-delta.y, delta.x) * width / 2f;
            int start = vh.currentVertCount;
            foreach (Vector2 p in new[] { a - n, a + n, b + n, b - n }) vh.AddVert(p, c, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
