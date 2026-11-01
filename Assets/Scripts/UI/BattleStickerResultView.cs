using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren.UI
{
    /// <summary>Vertical concert screens. Output MVP is derived only from the recorded battle events.</summary>
    public sealed class BattleStickerResultView : MonoBehaviour
    {
        public sealed class Contribution
        {
            public BattleUnit Unit;
            public long Damage, Healing;
            public int Defeats;
        }

        private readonly List<GameObject> screens = new List<GameObject>();
        public int MvpUnitId { get; private set; } = -1;
        private bool defeatMood;

        public static List<Contribution> ReadContributions(BattleSimulator battle)
        {
            var list = new List<Contribution>();
            if (battle == null) return list;
            foreach (BattleUnit unit in battle.Units)
                if (unit.Side == BattleSide.Player) list.Add(new Contribution { Unit = unit });
            var lookup = list.ToDictionary(item => item.Unit.Id);
            foreach (BattleEvent entry in battle.Log)
            {
                if (!lookup.TryGetValue(entry.ActorId, out Contribution actor)) continue;
                BattleUnit target = battle.FindUnit(entry.TargetId);
                if (target == null) continue;
                if (entry.Kind == BattleEventKind.Damage && target.Side == BattleSide.Enemy)
                {
                    // Both current combat paths record capped HP and shield loss. Amount can
                    // include legacy overkill, so it is deliberately never used as a fallback.
                    long applied = (long)entry.HpLostAmount + entry.AbsorbedAmount;
                    actor.Damage += Math.Max(0L, applied);
                }
                else if (entry.Kind == BattleEventKind.Heal && target.Side == BattleSide.Player)
                    actor.Healing += Math.Max(0, entry.Amount);
                else if (entry.Kind == BattleEventKind.Defeated && target.Side == BattleSide.Enemy)
                    actor.Defeats++;
            }
            return list;
        }

        public string Bind(BattleSimulator battle, Font font, Func<BattleUnit, Sprite> art, float heroFraction = .58f, float rowTop = -1f)
        {
            foreach (GameObject screen in screens)
                if (screen != null) { screen.SetActive(false); Destroy(screen); }
            screens.Clear();
            defeatMood = battle != null && battle.Outcome == BattleOutcome.Defeat; // muted band on a loss
            var contributions = ReadContributions(battle);
            Contribution best = contributions.OrderByDescending(item => item.Damage).FirstOrDefault();
            MvpUnitId = best != null && best.Damage > 0 ? best.Unit.Id : -1;
            var ordered = contributions.OrderByDescending(item => item.Unit.Id == MvpUnitId).Take(4).ToList();
            var root = (RectTransform)transform;
            float width = root.rect.width > 1f ? root.rect.width : 720f;
            float height = root.rect.height > 1f ? root.rect.height : 1536f;
            // 2026-10-21 owner pick R2, replicated 1:1 from the option page: the MVP's half-body
            // covers the top 58% of the screen, a red band slanted -8° crosses it at 52–64% with
            // [MVP] name damage, and the rest of the team sits at 73% as round avatars.
            if (ordered.Count > 0) BuildHero(ordered[0], ordered[0].Unit.Id == MvpUnitId, font, width, height, heroFraction);
            int others = ordered.Count - 1;
            const float column = 200f, gap = 30f;
            float rowLeft = (width - (others * column + Mathf.Max(0, others - 1) * gap)) * .5f;
            for (int i = 1; i < ordered.Count; i++)
                BuildMember(ordered[i], font, rowLeft + (i - 1) * (column + gap), rowTop >= 0f ? rowTop : height * .73f, column);
            var ticker = new StringBuilder();
            ticker.Append(battle.Stage.Name).Append("  ·  ");
            if (MvpUnitId >= 0) ticker.Append("输出MVP ").Append(best.Unit.Definition.Name).Append("  ·  ");
            foreach (Contribution item in contributions)
                ticker.Append(item.Unit.Definition.Name).Append(" 输出 ").Append(item.Damage.ToString("N0"))
                    .Append(" / 治疗 ").Append(item.Healing.ToString("N0")).Append("    ");
            if (Application.isPlaying && isActiveAndEnabled && !ChoSiren.BattlePresentationOptions.ReduceMotion)
            {
                if (entrance != null) StopCoroutine(entrance);
                entrance = StartCoroutine(Entrance1023(width));
            }
            return ticker.ToString();
        }

        private Coroutine entrance;

        /// <summary>
        /// 2026-10-23 review #39 (结算界面需要设计): the R2 layout the owner picked stays; it now enters like a P5
        /// results card — the MVP slides in from the right, the red band sweeps across, the MVP line stamps
        /// down, then the rest of the team pops in one by one.
        /// </summary>
        private System.Collections.IEnumerator Entrance1023(float width)
        {
            var items = new List<RectTransform>(); var homes = new List<Vector2>(); var groups = new List<CanvasGroup>();
            foreach (GameObject screen in screens)
            {
                if (screen == null) continue;
                var r = (RectTransform)screen.transform; items.Add(r); homes.Add(r.anchoredPosition);
                var g = UnityNull.Real(screen.GetComponent<CanvasGroup>()) ?? screen.AddComponent<CanvasGroup>(); g.blocksRaycasts = false; groups.Add(g);
            }
            float t = 0f; const float Duration = 1.1f;
            int member = 0;
            while (t < Duration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, .05f);
                member = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    RectTransform r = items[i]; if (r == null) continue; string n = r.name;
                    if (i == 0 && n.StartsWith("ResultMember-", StringComparison.Ordinal))
                    {
                        float k = Ease(t / .35f);
                        r.anchoredPosition = homes[i] + new Vector2((1f - k) * width * .35f, 0f); groups[i].alpha = k;
                    }
                    else if (n == "MvpBand") r.localScale = new Vector3(Ease((t - .15f) / .22f), 1f, 1f);
                    else if (n == "MvpLine")
                    {
                        float k = Mathf.Clamp01((t - .32f) / .12f);
                        r.localScale = Vector3.one * (k <= 0f ? 0f : Mathf.Lerp(1.6f, 1f, Ease(k))); groups[i].alpha = k > 0f ? 1f : 0f;
                    }
                    else
                    {
                        float k = Mathf.Clamp01((t - .5f - member * .09f) / .14f); member++;
                        r.localScale = Vector3.one * (k <= 0f ? 0f : Mathf.Lerp(1.35f, 1f, Ease(k))); groups[i].alpha = k > 0f ? 1f : 0f;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                items[i].anchoredPosition = homes[i]; items[i].localScale = Vector3.one; groups[i].alpha = 1f;
            }
            entrance = null;
        }

        private static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        private void BuildHero(Contribution item, bool mvp, Font font, float width, float screenHeight, float heroFraction)
        {
            float heroHeight = screenHeight * heroFraction;
            var hero = NewRect("ResultMember-" + item.Unit.Id, transform);
            Place(hero, 0, 0, width, heroHeight);
            hero.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var portrait = NewRect("ResultPortrait", hero).gameObject.AddComponent<UnityEngine.UI.Image>();
            portrait.raycastTarget = false;
            // Members without a result avatar (夕璃 …) show their member art instead of an empty stage.
            Sprite avatar = Resources.Load<Sprite>("Art/Members/Avatars/avatar-" + item.Unit.Definition.Id)
                ?? ChoSiren.Panels.PanelKit.MemberSpriteOrNull(item.Unit.Definition.Id, false);
            if (avatar != null)
            {
                // 2026-11-01 avatar-face: half-body framed on the annotated face.
                portrait.sprite = avatar;
                PortraitFace1101.Apply(portrait, hero, item.Unit.Definition.Id, PortraitFace1101.Framing.Half);
            }
            else portrait.color = Color.clear;
            if (avatar != null) portrait.color = item.Unit.Alive ? Color.white : new Color(.7f, .65f, .8f, 1f);
            screens.Add(hero.gameObject);
            // .band{left:-10%;right:-10%;top:52%;height:12%;transform:rotate(-8deg)}
            var band = NewRect("MvpBand", transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            band.color = defeatMood ? new Color32(74, 58, 104, 255) : Brand1025.Teal; band.raycastTarget = false;
            Place(band.rectTransform, -width * .1f, heroHeight - screenHeight * .06f, width * 1.2f, screenHeight * .12f);
            band.rectTransform.pivot = new Vector2(.5f, .5f);
            band.rectTransform.anchoredPosition = new Vector2(width * .5f, -heroHeight);
            band.rectTransform.localEulerAngles = new Vector3(0, 0, 8f);
            // Ink on the brand-teal band (white vanishes on it); white on the purple defeat band.
            Color bandInk = defeatMood ? Color.white : (Color)Brand1025.Ink;
            screens.Add(band.gameObject);
            // .mvp{top:53%;left:8%;font-size:16px(×3);rotate(-8deg)} b{white plate} em{margin-left}
            var line = NewRect("MvpLine", transform);
            Place(line, width * .08f, heroHeight, width * .9f, 64f);
            line.pivot = new Vector2(0f, .5f);
            line.anchoredPosition = new Vector2(width * .08f, -(heroHeight + (width * .5f - width * .08f) * Mathf.Tan(8f * Mathf.Deg2Rad) - 6f));
            line.localEulerAngles = new Vector3(0, 0, 8f);
            var tagPlate = NewRect("MvpTag", line).gameObject.AddComponent<UnityEngine.UI.Image>();
            tagPlate.color = Color.white; tagPlate.raycastTarget = false;
            var tag = Text(tagPlate.transform, "MvpLabel", mvp ? "MVP" : "TOP", font, 46, new Rect(0f, 0f, 10f, 56f), new Color32(0, 0, 0, 255));
            tag.resizeTextForBestFit = false; tag.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tagWidth = tag.preferredWidth + 24f;
            Place(tagPlate.rectTransform, 0f, 4f, tagWidth, 56f);
            Place(tag.rectTransform, 0f, 0f, tagWidth, 56f);
            var name = Text(line, "Name", item.Unit.Definition.Name, font, 48, new Rect(tagWidth + 18f, 0f, 10f, 64f), bandInk);
            name.resizeTextForBestFit = false; name.horizontalOverflow = HorizontalWrapMode.Overflow; name.alignment = TextAnchor.MiddleLeft;
            float nameWidth = name.preferredWidth;
            Place(name.rectTransform, tagWidth + 18f, 0f, nameWidth + 4f, 64f);
            var output = Text(line, "Output", item.Damage.ToString("N0"), font, 48, new Rect(tagWidth + 18f + nameWidth + 18f, 0f, 300f, 64f), bandInk);
            output.resizeTextForBestFit = false; output.horizontalOverflow = HorizontalWrapMode.Overflow; output.alignment = TextAnchor.MiddleLeft;
            screens.Add(line.gameObject);
        }

        private void BuildMember(Contribution item, Font font, float left, float top, float width)
        {
            // .av{width:46px(×3);border-radius:50%;background:center 15%/cover;border:2px(×3) solid pink}
            const float avatarSize = 128f, border = 6f;
            var cell = NewRect("ResultMember-" + item.Unit.Id, transform);
            Place(cell, left, top, width, 204f);
            var ring = NewRect("Ring", cell).gameObject.AddComponent<UnityEngine.UI.Image>();
            ring.sprite = Circle(); ring.color = new Color32(255, 95, 180, 255); ring.raycastTarget = false;
            Place(ring.rectTransform, (width - avatarSize) * .5f, 0f, avatarSize, avatarSize);
            var mask = NewRect("Mask", ring.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            mask.sprite = Circle(); mask.raycastTarget = false;
            var maskRect = mask.rectTransform; maskRect.anchorMin = Vector2.zero; maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = new Vector2(border, border); maskRect.offsetMax = new Vector2(-border, -border);
            mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = true;
            mask.color = new Color32(28, 20, 48, 255);
            var face = NewRect("ResultPortrait", mask.transform).gameObject.AddComponent<UnityEngine.UI.Image>();
            face.raycastTarget = false;
            // Only four members have a result avatar; the rest fall back to their member art instead of a blank white tile.
            Sprite avatar = Resources.Load<Sprite>("Art/Members/Avatars/avatar-" + item.Unit.Definition.Id)
                ?? ChoSiren.Panels.PanelKit.MemberSpriteOrNull(item.Unit.Definition.Id, false);
            if (avatar == null) face.enabled = false;
            if (avatar != null)
            {
                // 2026-11-01 avatar-face: head framed on the annotated face.
                face.sprite = avatar;
                PortraitFace1101.Apply(face, maskRect, item.Unit.Definition.Id, PortraitFace1101.Framing.Head);
            }
            face.color = item.Unit.Alive ? Color.white : new Color(.6f, .55f, .7f, 1f);
            Text(cell, "Name", item.Unit.Definition.Name, font, 32, new Rect(0f, avatarSize + 2f, width, 40f), Color.white);
            Text(cell, "Output", item.Damage.ToString("N0"), font, 27, new Rect(0f, avatarSize + 42f, width, 32f), SirenStickerUi.Cyan);
            screens.Add(cell.gameObject);
        }

        private static Sprite circle;
        private static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2f + .5f, y - n / 2f + .5f).magnitude;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(n / 2f - d));
            }
            tex.SetPixels32(px); tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
            circle.hideFlags = HideFlags.DontSave;
            return circle;
        }

        private void BuildScreen(Contribution item, Sprite art, Font font, float left, float width, float height, bool mvp)
        {
            var screen = NewRect("ResultMember-" + item.Unit.Id, transform);
            Place(screen, left, 0, width, height);
            var plate = screen.gameObject.AddComponent<UnityEngine.UI.Image>();
            plate.color = SirenStickerUi.Ink;
            plate.raycastTarget = false;
            SirenStickerUi.SkinPanel(screen.gameObject);
            var viewport = NewRect("PortraitCrop", screen);
            Place(viewport, 7, 7, Mathf.Max(1f, width - 14f), Mathf.Max(1f, height - 14f));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var backdrop = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = mvp ? new Color32(61, 24, 116, 255) : new Color32(28, 20, 48, 255);
            backdrop.raycastTarget = false;
            // 2026-10-20 (owner): half-body bust above the name plate, not the full figure.
            var bustArea = NewRect("BustArea", viewport);
            Place(bustArea, 0, 0, Mathf.Max(1f, width - 14f), Mathf.Max(1f, height - 14f - 80f));
            var portrait = NewRect("ResultPortrait", bustArea).gameObject.AddComponent<UnityEngine.UI.Image>();
            portrait.sprite = art;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            // Head-and-shoulder avatar art (already framed on the face) when the member has one.
            Sprite avatar = Resources.Load<Sprite>("Art/Members/Avatars/avatar-" + item.Unit.Definition.Id);
            if (avatar != null)
            {
                // 2026-11-01 avatar-face: bust framed on the annotated face.
                portrait.sprite = avatar;
                PortraitFace1101.Apply(portrait, bustArea, item.Unit.Definition.Id, PortraitFace1101.Framing.Bust);
            }
            else if (art != null) ChoSiren.Panels.PanelKit.FrameBustPortrait(portrait, bustArea, item.Unit.Definition.Id);
            portrait.color = item.Unit.Alive ? Color.white : new Color(.7f, .65f, .8f, 1f);
            var labelPlate = NewRect("NamePlate", viewport).gameObject.AddComponent<UnityEngine.UI.Image>();
            Place(labelPlate.rectTransform, 0, height - 94f, width, 92f);
            labelPlate.color = new Color32(6, 3, 14, 240);
            labelPlate.raycastTarget = false;
            Text(viewport, "Name", item.Unit.Definition.Name, font, mvp ? 22 : 17,
                new Rect(3f, height - 88f, width - 20f, 32f), Color.white);
            Text(viewport, "Output", item.Damage.ToString("N0"), font, mvp ? 20 : 16,
                new Rect(3f, height - 56f, width - 20f, 26f), SirenStickerUi.Cyan);
            Text(viewport, "OutputLabel", "实际输出", font, 12,
                new Rect(3f, height - 32f, width - 20f, 18f), SirenStickerUi.Muted);
            if (mvp)
            {
                var stamp = NewRect("MvpStamp", viewport).gameObject.AddComponent<UnityEngine.UI.Image>();
                Place(stamp.rectTransform, 7, 9, width - 28f, 38);
                stamp.sprite = SirenStickerUi.TabSprite;
                stamp.color = SirenStickerUi.Violet;
                stamp.raycastTarget = false;
                Text(stamp.transform, "MvpLabel", "输出 MVP", font, 19,
                    new Rect(0, 0, width - 28f, 38), Color.white);
            }
            screens.Add(screen.gameObject);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        private static UnityEngine.UI.Text Text(Transform parent, string name, string value, Font font,
            int size, Rect bounds, Color color)
        {
            var rect = NewRect(name, parent);
            Place(rect, bounds.x, bounds.y, bounds.width, bounds.height);
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            // Overflow, never best-fit: a line taller than its box would otherwise vanish.
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }

    /// <summary>Parallelogram fill (CSS skewX) for P5 buttons.</summary>
    public sealed class SkewedPlate1021 : UnityEngine.UI.MaskableGraphic
    {
        public float SkewDegrees = -10f;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float s = Mathf.Tan(-SkewDegrees * Mathf.Deg2Rad) * r.height * .5f;
            Color32 c = color;
            vh.AddVert(new Vector2(r.xMin - s, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + s, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax + s, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector2(r.xMax - s, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3);
        }
    }
}
