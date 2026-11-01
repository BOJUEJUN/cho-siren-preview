using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.UI
{
    /// <summary>
    /// Sword + attack readout on a member card. It shows the attack the member actually hits with
    /// (base attack × the share of the dice buff she carries). When a hand lands, the value holds
    /// until that member's shard arrives, then counts up, punches and turns gold with a "+N"
    /// pop — so the player sees exactly who the dice boosted and by how much.
    /// </summary>
    public sealed class BattleAttackBadge : MonoBehaviour
    {
        private static readonly Color Plain = Color.white;
        private static readonly Color Boosted = new Color(1f, .84f, .3f);
        private Text value, gain;
        private RectTransform punchRoot;
        private float shown, target, punch, gainAge = 99f, holdUntil;
        private bool held, initialized, boosted;
        private System.Func<bool> paused;

        public float Shown => shown;
        public bool Held => held;

        public static BattleAttackBadge Create(Transform parent, Font font, System.Func<bool> isPaused)
        {
            var root = new GameObject("AttackBadge", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var badge = root.AddComponent<BattleAttackBadge>();
            badge.paused = isPaused;
            badge.Build(font);
            return badge;
        }

        private void Build(Font font)
        {
            var punchGo = new GameObject("Punch", typeof(RectTransform));
            punchGo.transform.SetParent(transform, false);
            punchRoot = (RectTransform)punchGo.transform;
            punchRoot.anchorMin = Vector2.zero; punchRoot.anchorMax = Vector2.one;
            punchRoot.offsetMin = punchRoot.offsetMax = Vector2.zero;
            punchRoot.pivot = new Vector2(0f, .5f);

            var plate = new GameObject("Plate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            plate.transform.SetParent(punchRoot, false);
            var plateImage = plate.GetComponent<Image>();
            plateImage.color = new Color(.04f, .02f, .1f, .72f);
            plateImage.raycastTarget = false;
            Stretch((RectTransform)plate.transform);

            var sword = new GameObject("Sword", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleSwordGlyph));
            sword.transform.SetParent(punchRoot, false);
            var swordRect = (RectTransform)sword.transform;
            swordRect.anchorMin = new Vector2(0f, 0f); swordRect.anchorMax = new Vector2(0f, 1f);
            swordRect.pivot = new Vector2(0f, .5f);
            swordRect.sizeDelta = new Vector2(22f, -4f);
            swordRect.anchoredPosition = new Vector2(3f, 0f);
            sword.GetComponent<BattleSwordGlyph>().raycastTarget = false;

            value = NewText("Value", punchRoot, font, 17, TextAnchor.MiddleLeft);
            var valueRect = value.rectTransform;
            valueRect.anchorMin = Vector2.zero; valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = new Vector2(26f, 0f); valueRect.offsetMax = Vector2.zero;

            gain = NewText("Gain", transform, font, 18, TextAnchor.MiddleLeft);
            var gainRect = gain.rectTransform;
            gainRect.anchorMin = gainRect.anchorMax = new Vector2(1f, .5f);
            gainRect.pivot = new Vector2(0f, .5f);
            gainRect.sizeDelta = new Vector2(80f, 28f);
            gain.color = new Color(Boosted.r, Boosted.g, Boosted.b, 0f);
        }

        private static Text NewText(string name, Transform parent, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>The attack she currently hits with; boosted when the dice buff reaches her.</summary>
        public void SetTarget(int attack, bool isBoosted)
        {
            target = attack;
            boosted = isBoosted;
            if (!initialized) { shown = attack; initialized = true; }
        }

        /// <summary>A new hand: keep showing the old value until this member's shard lands.</summary>
        public void Hold() { held = true; holdUntil = Time.unscaledTime + 1.6f; }

        /// <summary>The shard arrived: count up to the new value with a "+N" pop.</summary>
        public void Release()
        {
            if (!held) return;
            held = false;
            int delta = Mathf.RoundToInt(target - shown);
            if (delta > 0)
            {
                gain.text = "+" + delta;
                gainAge = 0f;
                punch = 1f;
            }
        }

        private void Update()
        {
            if (value == null || (paused != null && paused())) return;
            float dt = Time.unscaledDeltaTime;
            // A shard that never arrives (skipped animation) must not freeze the readout.
            if (held && Time.unscaledTime > holdUntil) Release();
            if (!held)
            {
                // Rises quickly enough to read as a hit, falls back quietly when a hand ends.
                float rate = Mathf.Max(30f, Mathf.Abs(target - shown) * 4f);
                shown = Mathf.MoveTowards(shown, target, rate * dt);
            }
            value.text = Mathf.RoundToInt(shown).ToString();
            value.color = Color.Lerp(Plain, Boosted, boosted ? 1f : 0f);
            punch = Mathf.MoveTowards(punch, 0f, dt / .35f);
            float p = Mathf.Sin(punch * Mathf.PI) * punch;
            punchRoot.localScale = Vector3.one * (1f + .3f * p);
            gainAge += dt / 1.1f;
            Color c = gain.color;
            c.a = gainAge < 1f ? Mathf.Clamp01((1f - gainAge) * 2.5f) : 0f;
            gain.color = c;
            gain.rectTransform.anchoredPosition = new Vector2(4f, Mathf.Min(1f, gainAge) * 18f);
        }
    }

    /// <summary>A small upright sword: tapered blade, cross guard, grip and pommel.</summary>
    public sealed class BattleSwordGlyph : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float cx = r.center.x, w = r.width, h = r.height;
            Color32 blade = new Color32(236, 244, 255, 255), gold = new Color32(255, 204, 82, 255);
            float top = r.yMax, guardY = r.yMin + h * .3f;
            // Blade: a long tapered quad with a point.
            Tri(mesh, new Vector2(cx, top), new Vector2(cx - w * .13f, top - h * .14f), new Vector2(cx + w * .13f, top - h * .14f), blade);
            Quad(mesh, cx - w * .13f, cx + w * .13f, guardY, top - h * .14f, blade);
            // Guard, grip and pommel.
            Quad(mesh, cx - w * .42f, cx + w * .42f, guardY - h * .06f, guardY + h * .02f, gold);
            Quad(mesh, cx - w * .08f, cx + w * .08f, r.yMin + h * .1f, guardY - h * .06f, gold);
            Quad(mesh, cx - w * .14f, cx + w * .14f, r.yMin, r.yMin + h * .1f, gold);
        }

        private static void Quad(VertexHelper mesh, float x0, float x1, float y0, float y1, Color32 c)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            mesh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            mesh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            mesh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
            mesh.AddTriangle(i + 2, i + 3, i);
        }

        private static void Tri(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
