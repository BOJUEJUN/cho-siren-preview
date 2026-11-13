using System.Collections.Generic;
using System.IO;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-12 创意工坊 · 衣橱 (少爷: "你选一个直接放到游戏里面…功能要完整，不是 demo"; "换完装在哪里看也不知道"; 炫彩不要了).
    /// The bottom of the workshop is her wardrobe: 原装 plus every outfit made for her (newest first). Tap a card to try it on
    /// the stage, 穿上 to wear it across the game (成员详情, 大招立绘, 剧情…; the homepage idols stay as drawn), 删除 to throw
    /// it away, and 设计新造型 to make another — a whole outfit or just one part (上衣 / 下装 / 鞋子 / 发型 / 配饰) on top of
    /// the look she is trying on. Every paid result goes straight into the wardrobe.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        internal static readonly string[] DressParts1112 = { "整套", "上衣", "下装", "鞋子", "发型", "配饰" };
        private string aiDressPart1112 = "整套";
        /// <summary>The outfit on the stage right now (file name; empty = her original look).</summary>
        private string wardrobePreview1112 = string.Empty;
        private GameObject wardrobeBand1112;
        private readonly Dictionary<string, Texture2D> wardrobeThumbs1112 = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Sprite> wardrobeThumbSprites1112 = new Dictionary<string, Sprite>();

        private Sprite WardrobeThumbSprite1112(string memberId, string file)
        {
            string key = memberId + "/" + file;
            if (wardrobeThumbSprites1112.TryGetValue(key, out Sprite cached) && cached != null) return cached;
            Texture2D thumb = WardrobeThumb1112(memberId, file);
            if (thumb == null) return null;
            Sprite sprite = Sprite.Create(thumb, new Rect(0, 0, thumb.width, thumb.height), new Vector2(.5f, .5f), 100f);
            wardrobeThumbSprites1112[key] = sprite;
            return sprite;
        }

        private const float WardrobeBand1112 = 300f;

        /// <summary>What the model is asked for: a whole outfit, or one part changed on top of the current look.</summary>
        internal static string DressPrompt1112(string part, string tag, string extra)
        {
            if (string.IsNullOrEmpty(part) || part == DressParts1112[0]) return WorkshopAi1104.PromptFor(tag, extra);
            string wish = string.IsNullOrWhiteSpace(extra) ? $"换成{tag}风格的新{part}" : $"换成{tag}风格的新{part}，{extra.Trim()}";
            return $"只改她的{part}：{wish}。其他衣服、发型、脸、表情、姿势和背景都保持原样，不要改动";
        }

        private void BuildWardrobe1112(RectTransform root)
        {
            if (wardrobeBand1112 != null) Destroy(wardrobeBand1112);
            string memberId = WorkshopSubjectId1104;
            Image band = WorkshopPoster1105.Block(root, "WardrobeBand1112", null, new Color32(10, 7, 22, 252));
            band.raycastTarget = true;
            wardrobeBand1112 = band.gameObject;
            // An open drawer stays on top of the wardrobe it covers.
            if (aiDressPanel1104 != null) band.transform.SetSiblingIndex(aiDressPanel1104.transform.GetSiblingIndex());
            else if (workshopParameter1105 != null) band.transform.SetSiblingIndex(workshopParameter1105.transform.GetSiblingIndex());
            RectTransform rect = band.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(0f, WardrobeBand1112);
            Image edge = WorkshopPoster1105.Block(band.transform, "TopEdge", WorkshopPoster1105.Jag(), WorkshopPoster1105.Paper);
            edge.rectTransform.anchorMin = new Vector2(0f, 1f); edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = new Vector2(-12f, -6f); edge.rectTransform.offsetMax = new Vector2(12f, 3f);

            var outfits = model.Wardrobe1112(memberId);
            string worn = model.WornOutfitFile1112(memberId) ?? string.Empty;
            Text title = WorkshopPoster1105.Word(band.transform, "WardrobeTitle", "衣橱", 26, WorkshopPoster1105.Paper, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(title.rectTransform, new Vector2(0f, 1f), 24f, -16f, 80f, 40f);
            title.fontStyle = FontStyle.BoldAndItalic;
            Text count = WorkshopPoster1105.Word(band.transform, "WardrobeCount",
                $"{outfits.Count}/{GameModel.WardrobeCapacity1112} 套 · 点一下试穿", 16, VisualA1101.Muted, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(count.rectTransform, new Vector2(0f, 1f), 102f, -18f, 330f, 38f);

            // Cards: 原装 first, then her outfits; a horizontal strip that scrolls when it is long.
            var viewport = new GameObject("WardrobeViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(band.transform, false);
            WorkshopPoster1105.Fill(viewport, 16f, 92f, -16f, -62f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            var content = new GameObject("WardrobeContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, .5f);
            const float cardW = 104f, gap = 12f;
            int cards = outfits.Count + 1;
            content.sizeDelta = new Vector2(cards * (cardW + gap) + gap, 0f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;

            Sprite original = WorkshopOriginalStill1112();
            WardrobeCard1112(content, 0, cardW, "原装", original, string.Empty, worn.Length == 0);
            for (int i = 0; i < outfits.Count; i++)
            {
                var outfit = outfits[i];
                Sprite sprite = WardrobeThumbSprite1112(memberId, outfit.File);
                string label = string.IsNullOrEmpty(outfit.Part) || outfit.Part == DressParts1112[0] ? outfit.Name : outfit.Name + "·" + outfit.Part;
                WardrobeCard1112(content, i + 1, cardW, label, sprite, outfit.File, outfit.File == worn);
            }

            // Actions for the outfit on the stage, and the way to make a new one.
            bool previewWorn = wardrobePreview1112 == worn;
            Button wear = WorkshopPoster1105.Ticket(band.transform, "WardrobeWear", previewWorn ? "穿着中" : "穿上", null, 22, WearPreview1112,
                WorkshopPoster1105.Teal, previewWorn ? WorkshopPoster1105.Teal : (Color)WorkshopPoster1105.Ink, previewWorn ? WorkshopPoster1105.Ink : WorkshopPoster1105.Teal);
            WorkshopPoster1105.Corner((RectTransform)wear.transform, Vector2.zero, 20f, 20f, 160f, 58f);
            wear.interactable = !previewWorn;
            if (wardrobePreview1112.Length > 0)
            {
                GameObject delete = NewButton("WardrobeDelete", band.transform, "删除", 18, PanelKit.ButtonDark, White, ConfirmDeleteOutfit1112);
                WorkshopPoster1105.Corner((RectTransform)delete.transform, Vector2.zero, 192f, 26f, 92f, 48f);
            }
            Button design = WorkshopPoster1105.Ticket(band.transform, "WardrobeDesign", "设计新造型",
                $"{GameModel.AiDressDiamondCost1105} 星钻", 22, () => OpenWorkshopTool1104("dress", false));
            WorkshopPoster1105.Corner((RectTransform)design.transform, new Vector2(1f, 0f), -20f, 20f, 380f, 58f);
            if (workshopStage1104 != null && string.IsNullOrEmpty(workshopTool1104)) workshopStage1104.BottomBand = WardrobeBand1112;
        }

        private void WardrobeCard1112(RectTransform content, int index, float width, string label, Sprite picture, string file, bool worn)
        {
            bool previewing = wardrobePreview1112 == file;
            var go = new GameObject("WardrobeCard-" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(content, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, .5f);
            r.anchoredPosition = new Vector2(12f + index * (width + 12f), 0f); r.sizeDelta = new Vector2(width, 0f);
            Image frame = go.GetComponent<Image>();
            frame.sprite = WorkshopPoster1105.Slant(.06f);
            frame.color = previewing ? (Color)Brand1025.Teal : worn ? new Color32(255, 214, 102, 255) : new Color32(58, 44, 92, 255);
            Image face = WorkshopPoster1105.Block(go.transform, "Face", WorkshopPoster1105.Slant(.06f), new Color32(30, 20, 52, 255));
            WorkshopPoster1105.Fill(face.rectTransform, 3f, 3f, -3f, -3f);
            if (picture != null)
            {
                Image pic = WorkshopPoster1105.Block(go.transform, "Picture", picture, Color.white);
                pic.preserveAspect = true;
                WorkshopPoster1105.Fill(pic.rectTransform, 6f, 26f, -6f, -6f);
            }
            Image strip = WorkshopPoster1105.Block(go.transform, "LabelStrip", null, new Color(0f, 0f, 0f, .72f));
            strip.rectTransform.anchorMin = Vector2.zero; strip.rectTransform.anchorMax = new Vector2(1f, 0f);
            strip.rectTransform.offsetMin = new Vector2(3f, 3f); strip.rectTransform.offsetMax = new Vector2(-3f, 28f);
            Text caption = WorkshopPoster1105.Word(strip.transform, "Label", label, 14, WorkshopPoster1105.Paper);
            VisualA1101.Stretch(caption.rectTransform);
            caption.supportRichText = false;
            PanelKit.EnableBestFit(caption, 10);
            if (worn)
            {
                Image badge = WorkshopPoster1105.Block(go.transform, "WornBadge", null, new Color32(255, 214, 102, 255));
                WorkshopPoster1105.Corner(badge.rectTransform, new Vector2(0f, 1f), 0f, 0f, 62f, 24f);
                Text t = WorkshopPoster1105.Word(badge.transform, "Text", "穿着中", 13, WorkshopPoster1105.Ink);
                VisualA1101.Stretch(t.rectTransform);
            }
            Button button = go.GetComponent<Button>(); button.targetGraphic = frame; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { VisualA1101.Click(); PreviewOutfit1112(file); });
            go.AddComponent<VisualA1101Motion>().Press();
        }

        private Sprite WorkshopOriginalStill1112() => workshopMember1104 < 0 ? Resources.Load<Sprite>("Art/Workshop1104/starter-still")
            : Resources.Load<Sprite>(GameModel.Members[workshopMember1104].ResourcePath);

        /// <summary>A small copy of an outfit for its card (made once, kept next to the outfit).</summary>
        private Texture2D WardrobeThumb1112(string memberId, string file)
        {
            string key = memberId + "/" + file;
            if (wardrobeThumbs1112.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            string path = WorkshopAi1104.OutfitPath1112(memberId, file);
            string thumbPath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "thumbs", file);
            Texture2D thumb = File.Exists(thumbPath) ? WorkshopAi1104.Load(thumbPath) : null;
            if (thumb == null)
            {
                Texture2D full = WorkshopAi1104.Load(path);
                if (full == null) return null;
                int h = Mathf.Min(320, full.height), w = Mathf.Max(1, Mathf.RoundToInt(full.width * (h / (float)full.height)));
                thumb = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = full.GetPixelBilinear((x + .5f) / w, (y + .5f) / h);
                thumb.SetPixels32(px); thumb.Apply(false, false);
                Destroy(full);
                try { Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)); File.WriteAllBytes(thumbPath, thumb.EncodeToPNG()); }
                catch (System.Exception error) { Debug.LogWarning("[Wardrobe] thumbnail not saved: " + error.Message); }
            }
            thumb.name = "WardrobeThumb";
            wardrobeThumbs1112[key] = thumb;
            return thumb;
        }

        /// <summary>Tries an outfit on the stage (empty = her original look) without wearing it yet.</summary>
        private void PreviewOutfit1112(string file)
        {
            if (aiDressBusy1104) { Toast("生成中，完成后再试穿"); return; }
            wardrobePreview1112 = file ?? string.Empty;
            ShowPreviewOnStage1112();
            if (workshopRoot1104 != null) BuildWardrobe1112((RectTransform)workshopRoot1104.transform);
        }

        private void ShowPreviewOnStage1112()
        {
            if (workshopStage1104 == null) return;
            if (wardrobePreview1112.Length == 0) { workshopStage1104.SetOverride(null); return; }
            Texture2D texture = WorkshopAi1104.Load(WorkshopAi1104.OutfitPath1112(WorkshopSubjectId1104, wardrobePreview1112));
            workshopStage1104.SetOverride(texture);
        }

        private void WearPreview1112()
        {
            string memberId = WorkshopSubjectId1104;
            model.WearOutfit1112(memberId, wardrobePreview1112);
            VisualA1101.Success();
            Toast(wardrobePreview1112.Length == 0 ? $"{WorkshopSubjectName1104} 换回原装了"
                : $"{WorkshopSubjectName1104} 穿上了新造型 · 成员页、战斗大招和剧情都会显示");
            if (workshopRoot1104 != null) BuildWardrobe1112((RectTransform)workshopRoot1104.transform);
        }

        private void ConfirmDeleteOutfit1112()
        {
            string file = wardrobePreview1112;
            if (string.IsNullOrEmpty(file)) return;
            string memberId = WorkshopSubjectId1104;
            var frame = ModalPolish038Chrome.Begin(safeRoot, "DeleteOutfitDialog", "删除造型", "删除后找不回来，穿着的话会换回原装。", null, 480f);
            var confirm = ModalPolish038Chrome.ActionButton(frame.Body, "DeleteOutfit", "删除", 26, () =>
            {
                model.DeleteOutfit1112(memberId, file);
                wardrobeThumbs1112.Remove(memberId + "/" + file);
                wardrobeThumbSprites1112.Remove(memberId + "/" + file);
                Destroy(frame.Overlay);
                wardrobePreview1112 = model.WornOutfitFile1112(memberId) ?? string.Empty;
                ShowPreviewOnStage1112();
                if (workshopRoot1104 != null) BuildWardrobe1112((RectTransform)workshopRoot1104.transform);
                Toast("已删除");
            }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(confirm.GetComponent<RectTransform>(), 0, 42, 420, 72);
            var cancel = ModalPolish038Chrome.ActionButton(frame.Body, "Cancel", "取消", 24, () => Destroy(frame.Overlay), ModalPolish038ButtonStyle.Ghost);
            PlaceTop(cancel.GetComponent<RectTransform>(), 0, 134, 420, 64);
        }

        /// <summary>
        /// A finished (and paid) outfit: straight into her wardrobe with its label, on the stage, then the reveal page.
        /// Also used by tests with a ready-made picture.
        /// </summary>
        private void FinishAiDress1112(string memberId, string style, string part, Texture2D texture)
        {
            string path = WorkshopAi1104.SaveToWardrobe(memberId, texture);
            model.NoteOutfit1112(memberId, path, style, part);
            aiDressResult1104 = texture;
            aiDressResultSubject1105 = memberId; aiDressResultStyle1105 = part == DressParts1112[0] ? style : style + "·" + part;
            if (workshopRoot1104 == null || WorkshopSubjectId1104 != memberId) return;
            wardrobePreview1112 = Path.GetFileName(path);
            workshopStage1104?.SetOverride(texture);
            CollapseWorkshopEditor1105(); // back to the stage and her wardrobe (rebuilds it with the new card)
            ShowAiReveal1105(texture);
        }

        private void ReleaseWardrobeThumbs1112()
        {
            foreach (var sprite in wardrobeThumbSprites1112.Values) if (sprite != null) Destroy(sprite);
            wardrobeThumbSprites1112.Clear();
            foreach (var thumb in wardrobeThumbs1112.Values) if (thumb != null) Destroy(thumb);
            wardrobeThumbs1112.Clear();
        }
    }
}
