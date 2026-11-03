using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-03 (用户: 左上角三个信息文字看起来非常不好; picked option C): the plaque art only has thin ruled rows
    /// and was stretched 7% taller; the old texts sat above and past them, so the name floated over the frame and 战力
    /// ran into the waveform. The art keeps its own aspect, the name and 战力 sit on its rows, and the team level is a
    /// small badge on the avatar ring.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        const float PlaqueW = 282f, PlaqueAspect = 528f / 1536f;
        static readonly Color ProfileGold = new Color32(255, 214, 107, 255);

        // Art fractions → plaque pixels (art fitted to the 282px width, centred in the 104px band).
        static float AX(float f) => f * PlaqueW;
        static float AY(float f) => (104f - PlaqueW * PlaqueAspect) * .5f + f * PlaqueW * PlaqueAspect;

        /// <summary>Returns true when the plaque is laid out (call again after the generic header pass).</summary>
        private bool LayoutProfile1103(Transform profile, Transform visual)
        {
            if (visual != null)
                PlaceTop((RectTransform)visual, 0f, AY(0f), PlaqueW, PlaqueW * PlaqueAspect);
            Text name = profile.Find("PlayerName")?.GetComponent<Text>();
            Text level = profile.Find("TeamAverageLevel")?.GetComponent<Text>();
            Text power = teamLevelText;
            if (name == null || level == null || power == null) return true;
            foreach (Text t in new[] { name, level, power })
            {
                t.supportRichText = true;
                t.resizeTextForBestFit = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.alignment = TextAnchor.LowerLeft;
            }
            // Avatar: centred on the art's ring (fitted from the art's inner edge: centre 362,282, radius ~130 of
            // 1536x528) and drawn under the plaque, so the art's own anti-aliased ring hides the stencil mask edge.
            float cx = AX(362f / 1536f), cy = AY(282f / 528f), d = 2f * 132f / 1536f * PlaqueW;
            Transform avatarMask = profile.Find("AvatarMask");
            if (avatarMask != null)
            {
                PlaceTop((RectTransform)avatarMask, cx - d * .5f, cy - d * .5f, d, d);
                Image shape = avatarMask.GetComponent<Image>();
                if (shape != null) { shape.sprite = CircleSprite1103(); shape.type = Image.Type.Simple; }
                if (visual != null) visual.SetSiblingIndex(avatarMask.GetSiblingIndex() + 1);
            }
            // The tap target for the avatar picker follows the circle (it lives on the bar, above the plaque).
            Transform avatarHit = profile.parent != null ? profile.parent.Find("AvatarHit") : null;
            if (avatarHit != null)
            {
                Vector2 origin = ((RectTransform)profile).anchoredPosition;
                PlaceTop((RectTransform)avatarHit, origin.x + cx - d * .5f - 4f, -origin.y + cy - d * .5f - 4f, d + 8f, d + 8f);
            }
            float x = AX(.395f);
            // Lowered (用户: 经纪人跟上面的 UI 重叠了): the name clears the top bracket and triangles.
            Row(name, x, AY(.575f), 16, Color.white);
            Row(power, x, AY(.765f), 15, Color.white);

            Transform pill = profile.Find("LevelPill1103");
            if (pill == null)
            {
                var go = new GameObject("LevelPill1103", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(profile, false);
                pill = go.transform;
                go.GetComponent<Image>().raycastTarget = false;
            }
            var image = pill.GetComponent<Image>();
            image.sprite = RoundedSprite(12); image.type = Image.Type.Sliced; image.color = Brand1025.Teal;
            PlaceTop((RectTransform)pill, cx - 22f, cy + d * .5f - 9f, 44f, 18f);
            pill.SetAsLastSibling();
            // The text stays a direct child of Profile (UpdateTopBar finds it there), drawn over the pill.
            PlaceTop(level.rectTransform, cx - 22f, cy + d * .5f - 9f, 44f, 18f);
            level.transform.SetAsLastSibling();
            level.alignment = TextAnchor.MiddleCenter;
            level.fontSize = 12;
            level.color = Brand1025.Ink;
            level.fontStyle = FontStyle.Bold;
            FormatProfile1103();
            return true;
        }

        static Sprite circle1103;

        /// <summary>A 256px anti-aliased disc (the old 64px rounded square read as a squarish, jagged circle).</summary>
        static Sprite CircleSprite1103()
        {
            if (circle1103 != null) return circle1103;
            const int n = 256;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            var pixels = new Color32[n * n];
            float r = n * .5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dist = Mathf.Sqrt((x + .5f - r) * (x + .5f - r) + (y + .5f - r) * (y + .5f - r));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - dist) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circle1103 = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            circle1103.hideFlags = HideFlags.DontSave;
            return circle1103;
        }

        /// <summary>Places a text so its baseline sits on a ruled line of the plaque.</summary>
        private static void Row(Text text, float x, float lineY, int size, Color color)
        {
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            float h = size * 1.5f;
            PlaceTop(text.rectTransform, x, lineY - h + size * .28f, 150f, h);
            text.transform.SetAsLastSibling();
        }

        private void FormatProfile1103()
        {
            if (teamLevelText == null || model == null) return;
            if (model.Save.Team.Count == 0) { teamLevelText.text = "尚未编队"; return; }
            teamLevelText.text = $"战力 <color=#{ColorUtility.ToHtmlStringRGB(ProfileGold)}>{model.TeamPower:N0}</color>";
        }
    }
}
