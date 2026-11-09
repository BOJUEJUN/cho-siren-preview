using System;
using System.IO;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 创意工坊 photo-editor layout: stable upper-body preview, the game's shared 返回 control, a horizontally scrollable
    /// tool strip, and compact parameter drawers with explicit return/collapse. Sliders and preview cards change the real
    /// per-member values; there is no giant record over the character. 转身 keeps the AI-drawn angle sheets
    /// (Art/Workshop1104/Turn/&lt;member&gt;-q45/side/back, right side mirrored), with an optional full-figure framing control.
    /// Every change is saved per member (GameSave.WorkshopLooks1104). The lobby card keeps its scene name AlbumProduction.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        internal static readonly (string Name, Color Tint)[] WorkshopFilters1104 =
        {
            ("原色", Color.white), ("暖光", new Color(1f, .9f, .8f)), ("冷调", new Color(.84f, .93f, 1f)),
            ("霓虹", new Color(1f, .82f, 1f)), ("复古", new Color(.96f, .86f, .72f)), ("夜色", new Color(.74f, .76f, .98f)),
        };
        /// <summary>
        /// 2026-11-09 炫彩 (少爷: "只有换装之类的参数…类似炫彩皮肤"): colour variants of her outfit. Hue is the target
        /// hue (0–1) the outfit's colourful parts take; strength 0 keeps the original.
        /// </summary>
        internal static readonly (string Name, float Hue, float Strength, float Sat, float Val, Color Swatch)[] WorkshopChromas1109 =
        {
            ("原色", 0f, 0f, 1f, 1f, new Color32(240, 236, 248, 255)),
            ("冰蓝", .56f, .92f, 1.05f, 1.05f, new Color32(96, 200, 255, 255)),
            ("樱粉", .92f, .88f, .9f, 1.08f, new Color32(255, 150, 200, 255)),
            ("暗夜", .76f, .9f, 1.1f, .82f, new Color32(132, 84, 220, 255)),
            ("鎏金", .12f, .85f, 1.1f, 1.08f, new Color32(255, 204, 88, 255)),
            ("翡翠", .40f, .88f, 1f, 1f, new Color32(70, 214, 150, 255)),
            ("赤焰", .99f, .9f, 1.15f, 1f, new Color32(240, 70, 80, 255)),
        };

        internal static readonly (string Name, string Path)[] WorkshopBackgrounds1104 =
        {
            ("创作室", "Art/Workshop1104/bg-studio"), ("霓虹街", "Art/Workshop1104/bg-street"),
            ("巨蛋", "Art/Workshop1104/bg-dome"), ("大厅", "Art/LobbyBackground"),
        };
        internal static readonly (string Name, Color Glow)[] WorkshopAccents1104 =
        {
            ("青", new Color32(28, 242, 236, 255)), ("粉", new Color32(255, 95, 210, 255)), ("金", new Color32(255, 214, 107, 255)),
            ("紫", new Color32(156, 104, 255, 255)), ("红", new Color32(255, 80, 96, 255)), ("白", new Color32(240, 236, 255, 255)),
        };
        private static readonly string[] VoiceScripts1104 =
        {
            "大家好，我是今天的主唱，请多关照！",
            "下一首歌，送给一直支持我们的你。",
            "三、二、一——一起喊出来吧！",
        };

        /// <summary>Tool row: id, icon, caption, camera shot (0 face, 1 bust, 2 full figure).</summary>
        private static readonly (string Id, string Icon, string Name, int Shot)[] WorkshopTools1104 =
        {
            ("dress", "sparkle", "换装", 1), ("filter", "star", "滤镜", 1), ("bright", "bolt", "亮度", 1),
            ("pitch", "mic", "音高", 1), ("voice", "music", "录声音", 1), ("bg", "flag", "背景", 1),
            ("accent", "heart", "应援色", 1), ("bob", "refresh", "律动", 1), ("turn", "exchange", "转身", 2),
            ("music", "music", "作曲", 1),
            ("chroma", "drop", "炫彩", 2),
        };

        private GameObject workshopRoot1104;
        private WorkshopStage1104 workshopStage1104;
        /// <summary>-1 = the 初始角色 (the workshop's own starter idol, 2026-11-04 user loop); otherwise a member index.</summary>
        private int workshopMember1104 = -1, workshopScript1104;
        public const string StarterId1104 = "starter";
        public const string StarterName1104 = "初始角色";
        private WorkshopLook1104 workshopLook1104;
        private string workshopTool1104 = string.Empty;
        private GameObject workshopParameter1105;
        private int workshopFraming1105 = 2; // 11-09: one full-length view (the outfit is the point); no 半身/全身
        private const float WorkshopToolBand1105 = 118f;

        private void OpenWorkshop1104()
        {
            CloseModal();
            CloseWorkshop1104();
            // 2026-11-09 (少爷: "创意工坊只有我们自己的角色，去掉这个初始角色"): open on a signed member (the captain first);
            // the starter idol is only a fallback when nobody is signed yet.
            if (workshopMember1104 < 0 || !model.IsUnlocked(workshopMember1104)) workshopMember1104 = FirstWorkshopMember1109();
            string subjectId = WorkshopSubjectId1104, subjectName = WorkshopSubjectName1104;
            workshopLook1104 = model.WorkshopLook1104(subjectId);

            workshopRoot1104 = NewImage("WorkshopPage1104", safeRoot, null, new Color32(8, 4, 16, 255));
            var root = (RectTransform)workshopRoot1104.transform;
            Stretch(root);
            workshopRoot1104.GetComponent<Image>().raycastTarget = true;
            workshopRoot1104.AddComponent<RectMask2D>();

            workshopStage1104 = workshopRoot1104.AddComponent<WorkshopStage1104>();
            if (workshopMember1104 < 0)
                workshopStage1104.Build(root, StarterId1104, Resources.Load<Sprite>("Art/Workshop1104/starter-still"),
                    WorkshopStage1104.StarterFace, "Workshop/starter-idle.mp4", () => workshopLook1104, RoundedSprite(18), DotSprite());
            else
            {
                MemberDefinition member = GameModel.Members[workshopMember1104];
                Sprite still = Resources.Load<Sprite>(member.ResourcePath);
                workshopStage1104.Build(root, member.Id, still, still != null ? PortraitFace1101.FaceFor(member.Id, still) : new Rect(.4f, .78f, .2f, .12f),
                    null, () => workshopLook1104, RoundedSprite(18), DotSprite());
            }
            workshopStage1104.Changed = () => model.SaveWorkshopLook1104(workshopLook1104);
            if (aiDressResult1104 != null && aiDressResultSubject1105 == subjectId) workshopStage1104.SetOverride(aiDressResult1104);

            BuildWorkshopHeader1105(root, subjectName);
            BuildWorkshopTools1104(root);
            OpenWorkshopTool1104(workshopTool1104, false);
        }

        private void CloseWorkshop1104()
        {
            if (workshopRoot1104 != null) Destroy(workshopRoot1104);
            workshopRoot1104 = null;
            workshopStage1104 = null;
            aiReveal1105 = null;
            workshopParameter1105 = null;
            aiDressPanel1104 = null;
            CloseWorkshopMusic1105();
            WorkshopRecorder1104.Cancel();
        }

        private string WorkshopSubjectId1104 => workshopMember1104 < 0 ? StarterId1104 : GameModel.Members[workshopMember1104].Id;
        private string WorkshopSubjectName1104 => workshopMember1104 < 0 ? StarterName1104 : GameModel.Members[workshopMember1104].Name;

        private int FirstWorkshopMember1109()
        {
            if (model.Save.Team.Count > 0 && model.Save.Team[0] >= 0 && model.IsUnlocked(model.Save.Team[0])) return model.Save.Team[0];
            for (int i = 0; i < GameModel.Members.Length; i++) if (model.IsUnlocked(i)) return i;
            return -1;
        }

        /// <summary>Cycles through the signed members only (2026-11-09: no 初始角色).</summary>
        private void StepWorkshopMember1104(int step)
        {
            if (aiDressBusy1104) { Toast("生成中，完成后再切换成员"); return; }
            int count = GameModel.Members.Length, slot = Mathf.Max(0, workshopMember1104);
            for (int i = 0; i < count; i++)
            {
                slot = (slot + step + count) % count;
                if (model.IsUnlocked(slot)) break;
            }
            if (!model.IsUnlocked(slot)) return;
            workshopMember1104 = slot;
            OpenWorkshop1104();
        }

        private void WorkshopTextButton1104(Transform parent, string objectName, string text, int size, Vector2 anchor, Vector2 offset, Action click)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(anchor.x, .5f);
            rect.anchoredPosition = offset; rect.sizeDelta = new Vector2(text.Length <= 1 ? 52f : 110f, 60f);
            Image hit = go.GetComponent<Image>(); hit.color = new Color(0f, 0f, 0f, .001f);
            Text label = VisualA1101.Label(go.transform, "Text", text, size, text == "保存" ? (Color)Brand1025.Teal : VisualA1101.Paper, TextAnchor.MiddleCenter);
            VisualA1101.Stretch(label.rectTransform);
            Button button = go.GetComponent<Button>(); button.transition = Selectable.Transition.None; button.targetGraphic = hit;
            button.onClick.AddListener(() => { VisualA1101.Click(); click(); });
            go.AddComponent<VisualA1101Motion>().Press();
        }

        /// <summary>
        /// 2026-11-05 poster header: 返回 · slanted 保存 ticket, the ransom-note 创意工坊 title and the member tag with ◀ ▶.
        /// </summary>
        private void BuildWorkshopHeader1105(RectTransform root, string subjectName)
        {
            var top = new Vector2(0f, 1f);
            GameObject back = NewButton("WorkshopBack", root, "‹ 返回", 23, PanelKit.ButtonDark, White, CloseWorkshop1104);
            WorkshopPoster1105.Corner((RectTransform)back.transform, top, 18f, -18f, 132f, 54f);

            Button save = WorkshopPoster1105.Ticket(root, "WorkshopSave", "保存", null, 20, () =>
            {
                model.SaveWorkshopLook1104(workshopLook1104);
                VisualA1101.Success();
                Toast($"已保存「{subjectName}」的造型");
            });
            WorkshopPoster1105.Corner((RectTransform)save.transform, Vector2.one, -20f, -18f, 112f, 46f);

            // Ransom-note title: four torn letter blocks, each tilted its own way.
            var title = new GameObject("WorkshopTitle", typeof(RectTransform)).GetComponent<RectTransform>();
            title.SetParent(root, false);
            WorkshopPoster1105.Corner(title, top, 22f, -84f, 330f, 96f, 8f);
            (string ch, Color face, Color ink, float size, float tilt, float dy)[] letters =
            {
                ("创", WorkshopPoster1105.Paper, WorkshopPoster1105.Ink, 50f, 4f, 0f),
                ("意", WorkshopPoster1105.Ink, WorkshopPoster1105.Teal, 58f, -6f, -6f),
                ("工", WorkshopPoster1105.Teal, WorkshopPoster1105.Ink, 44f, 6f, 4f),
                ("坊", WorkshopPoster1105.Paper, WorkshopPoster1105.Hot, 54f, -3f, 2f),
            };
            float x = 0f;
            foreach (var l in letters)
            {
                float box = l.size + 18f;
                bool dark = l.face == WorkshopPoster1105.Ink;
                // A dark letter is a paper sticker with an ink face inset (a child draws over its parent).
                Image plate = WorkshopPoster1105.Block(title, "Letter-" + l.ch, WorkshopPoster1105.Jag(), dark ? WorkshopPoster1105.Paper : l.face);
                WorkshopPoster1105.Corner(plate.rectTransform, new Vector2(0f, .5f), x, l.dy, box, box, l.tilt);
                if (dark)
                {
                    Image inner = WorkshopPoster1105.Block(plate.transform, "Face", WorkshopPoster1105.Jag(), WorkshopPoster1105.Ink);
                    WorkshopPoster1105.Fill(inner.rectTransform, 4f, 4f, -4f, -4f);
                }
                Text t = WorkshopPoster1105.Word(plate.transform, "Text", l.ch, Mathf.RoundToInt(l.size), l.ink);
                VisualA1101.Stretch(t.rectTransform);
                x += box + 4f;
            }

            // Member tag: hot pink slanted plate; ◀ ▶ step through 初始角色 and the unlocked members.
            Image tag = WorkshopPoster1105.Block(root, "WorkshopMemberTag", WorkshopPoster1105.Slant(.3f), WorkshopPoster1105.Hot);
            WorkshopPoster1105.Corner(tag.rectTransform, top, 30f, -196f, 290f, 52f, 8f);
            WorkshopPoster1105.HardShadow(tag, WorkshopPoster1105.Ink, 5f, -5f);
            Text name = WorkshopPoster1105.Word(tag.transform, "Name", subjectName, 22, WorkshopPoster1105.Paper);
            VisualA1101.Stretch(name.rectTransform);
            WorkshopTextButton1104(tag.transform, "WorkshopPrev", "◀", 22, new Vector2(0f, .5f), new Vector2(10f, 0f), () => StepWorkshopMember1104(-1));
            WorkshopTextButton1104(tag.transform, "WorkshopNext", "▶", 22, new Vector2(1f, .5f), new Vector2(-10f, 0f), () => StepWorkshopMember1104(1));

            if (true) return; // 2026-11-09: no 半身 / 全身 toggle — one full-length view
#pragma warning disable CS0162
            var framing = new GameObject("WorkshopFraming", typeof(RectTransform)).GetComponent<RectTransform>();
            framing.SetParent(root, false);
            WorkshopPoster1105.Corner(framing, Vector2.one, -18f, -196f, 224f, 48f);
            for (int i = 0; i < 2; i++)
            {
                int shot = i == 0 ? 1 : 2;
                GameObject frame = NewButton("Frame-" + shot, framing, i == 0 ? "半身" : "全身", 20,
                    PanelKit.ButtonDark, White, () => SetWorkshopFraming1105(shot));
                WorkshopPoster1105.Corner((RectTransform)frame.transform, new Vector2(0f, 1f), i * 114f, 0f, 106f, 46f);
                SirenPageChrome.Tab(frame, shot == workshopFraming1105);
            }
        }

        /// <summary>
        /// 2026-11-07 public release: 作曲 only works with the private test code (the Suno service refuses everyone
        /// else), so public builds leave that tool out instead of showing a button that always says "未开通".
        /// 2026-11-08 (少爷: "创意工坊…功能太多了要简化"): the bar keeps only what players come here for — AI 换装, 背景 and
        /// 转身 (with 半身/全身 above). 滤镜 / 亮度 / 音高 / 录声音 / 应援色 / 律动 stay in code but are no longer shown.
        /// </summary>
        internal static readonly string[] CoreWorkshopTools1108 = { "dress", "chroma" }; // 11-09: 背景 / 转身 removed

        /// <summary>The tools shown in the bar (作曲 only with the private test code).</summary>
        internal static (string Id, string Icon, string Name, int Shot)[] ShownWorkshopTools1107 =>
            System.Array.FindAll(WorkshopTools1104, t => System.Array.IndexOf(CoreWorkshopTools1108, t.Id) >= 0
                || t.Id == "music" && WorkshopMusic1105.TestBuild);

        private static readonly Color WorkshopToolFace1109 = new Color32(22, 16, 38, 255);

        /// <summary>Photo-editor tools stay in one horizontally scrollable strip below the live preview.</summary>
        private void BuildWorkshopTools1104(RectTransform root)
        {
            Image bar = WorkshopPoster1105.Block(root, "WorkshopTools", null, new Color32(10, 7, 22, 252));
            bar.raycastTarget = true;
            var rect = bar.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(0f, WorkshopToolBand1105);
            Image edge = WorkshopPoster1105.Block(bar.transform, "TopEdge", null, WorkshopPoster1105.Teal);
            edge.rectTransform.anchorMin = new Vector2(0f, 1f); edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = new Vector2(0f, -2f); edge.rectTransform.offsetMax = Vector2.zero;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(bar.transform, false); WorkshopPoster1105.Fill(viewport, 10f, 5f, -10f, -5f);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, .5f);
            var tools = ShownWorkshopTools1107;
            // 11-09: two or three tools share the full bar as wide plates (icon + caption side by side), centred.
            // The bar is ~700 units wide on the 720-wide canvas; content stretches across it and each tool takes an equal share.
            content.anchorMax = Vector2.one; content.sizeDelta = Vector2.zero;
            var scroll = bar.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f; scroll.decelerationRate = .08f;
            for (int i = 0; i < tools.Length; i++)
            {
                var tool = tools[i];
                var go = new GameObject("WorkshopTool-" + tool.Id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(content, false);
                var toolRect = (RectTransform)go.transform;
                toolRect.anchorMin = new Vector2(i / (float)tools.Length, .5f); toolRect.anchorMax = new Vector2((i + 1f) / tools.Length, .5f);
                toolRect.pivot = new Vector2(.5f, .5f); toolRect.anchoredPosition = Vector2.zero;
                toolRect.offsetMin = new Vector2(14f, -38f); toolRect.offsetMax = new Vector2(-14f, 38f);
                Image hit = go.GetComponent<Image>(); hit.color = new Color(0f, 0f, 0f, .001f);
                // Teal frame under a dark face (main-action style); the selected tool fills solid teal.
                Image rim = WorkshopPoster1105.Block(go.transform, "Rim", WorkshopPoster1105.Slant(.16f), WorkshopPoster1105.Teal);
                WorkshopPoster1105.Fill(rim.rectTransform, 0f, 0f, 0f, 0f);
                Image plate = WorkshopPoster1105.Block(go.transform, "Disc", WorkshopPoster1105.Slant(.16f), WorkshopToolFace1109);
                WorkshopPoster1105.Fill(plate.rectTransform, 3f, 3f, -3f, -3f);
                Image icon = WorkshopPoster1105.Block(go.transform, "Icon", VisualB1101.Icon(tool.Icon), WorkshopPoster1105.Teal);
                WorkshopPoster1105.Corner(icon.rectTransform, new Vector2(.5f, .5f), -36f, 0f, 30f, 30f); icon.preserveAspect = true;
                Text caption = WorkshopPoster1105.Word(go.transform, "Caption", tool.Name, 24, WorkshopPoster1105.Teal);
                WorkshopPoster1105.Corner(caption.rectTransform, new Vector2(.5f, .5f), 20f, 0f, 90f, 40f);
                caption.fontStyle = FontStyle.Bold;
                string id = tool.Id;
                var button = go.GetComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { VisualA1101.Click(); OpenWorkshopTool1104(id); });
            }
        }

        private void SetWorkshopFraming1105(int shot)
        {
            workshopFraming1105 = shot;
            if (workshopStage1104 != null) workshopStage1104.Shot = shot;
            RefreshWorkshopFraming1105();
        }

        private void RefreshWorkshopFraming1105()
        {
            if (workshopRoot1104 == null) return;
            Transform framing = workshopRoot1104.transform.Find("WorkshopFraming");
            if (framing == null) return;
            int shot = workshopStage1104 != null ? workshopStage1104.Shot : workshopFraming1105;
            foreach (Transform child in framing) SirenPageChrome.Tab(child.gameObject, child.name == "Frame-" + shot);
        }

        private void RefreshWorkshopTools1105()
        {
            if (workshopRoot1104 == null) return;
            for (int i = 0; i < WorkshopTools1104.Length; i++)
            {
                var tool = WorkshopTools1104[i];
                Transform t = workshopRoot1104.transform.Find("WorkshopTools/Viewport/Content/WorkshopTool-" + tool.Id);
                if (t == null) continue;
                bool on = tool.Id == workshopTool1104;
                t.Find("Disc").GetComponent<Image>().color = on ? WorkshopPoster1105.Teal : WorkshopToolFace1109;
                t.Find("Icon").GetComponent<Image>().color = on ? WorkshopPoster1105.Ink : WorkshopPoster1105.Teal;
                t.Find("Caption").GetComponent<Text>().color = on ? WorkshopPoster1105.Ink : WorkshopPoster1105.Teal;
            }
        }

        /// <summary>Only closes the parameter workspace; the page-level 返回 still leaves the workshop.</summary>
        private void CollapseWorkshopEditor1105()
        {
            CloseWorkshopParameter1105(); CloseAiDress1104(); CloseWorkshopMusic1105();
            workshopTool1104 = string.Empty;
            if (workshopStage1104 != null)
            {
                workshopStage1104.Dial(null); workshopStage1104.TurnMode = false;
                workshopStage1104.BottomBand = WorkshopToolBand1105; workshopStage1104.Shot = workshopFraming1105;
            }
            RefreshWorkshopFraming1105();
            RefreshWorkshopTools1105();
        }

        private void CloseWorkshopParameter1105()
        {
            if (workshopParameter1105 != null)
            {
                var motion = workshopParameter1105.GetComponent<WorkshopDrawerMotion1105>();
                if (motion != null) motion.Close(); else Destroy(workshopParameter1105);
            }
            workshopParameter1105 = null;
        }

        private RectTransform WorkshopEditorPanel1105(string name, string title, float height)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(workshopRoot1104.transform, false);
            panel.anchorMin = Vector2.zero; panel.anchorMax = new Vector2(1f, 0f); panel.pivot = new Vector2(.5f, 0f);
            panel.offsetMin = new Vector2(0f, WorkshopToolBand1105); panel.offsetMax = new Vector2(0f, WorkshopToolBand1105 + height);
            Image face = panel.GetComponent<Image>(); face.color = new Color32(12, 8, 26, 252); face.raycastTarget = true;
            Image edge = WorkshopPoster1105.Block(panel, "PaperEdge", WorkshopPoster1105.Jag(), WorkshopPoster1105.Paper);
            edge.rectTransform.anchorMin = new Vector2(0f, 1f); edge.rectTransform.anchorMax = Vector2.one;
            edge.rectTransform.offsetMin = new Vector2(-12f, -6f); edge.rectTransform.offsetMax = new Vector2(12f, 3f);
            GameObject back = NewButton("ParameterBack", panel, "‹ 工具", 20, PanelKit.ButtonDark, White, CollapseWorkshopEditor1105);
            WorkshopPoster1105.Corner((RectTransform)back.transform, new Vector2(0f, 1f), 18f, -18f, 112f, 46f);
            Text label = WorkshopPoster1105.Word(panel, "ParameterTitle", title, 25, WorkshopPoster1105.Paper, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(label.rectTransform, new Vector2(0f, 1f), 150f, -18f, 310f, 46f);
            GameObject close = NewButton("ParameterCollapse", panel, "收起 ⌄", 20, PanelKit.ButtonDark, White, CollapseWorkshopEditor1105);
            WorkshopPoster1105.Corner((RectTransform)close.transform, Vector2.one, -18f, -18f, 118f, 46f);
            panel.gameObject.AddComponent<WorkshopDrawerMotion1105>();
            if (workshopStage1104 != null) workshopStage1104.BottomBand = WorkshopToolBand1105 + height + 14f;
            return panel;
        }

        private void OpenWorkshopTool1104(string id) => OpenWorkshopTool1104(id, true);

        private void OpenWorkshopTool1104(string id, bool toggle)
        {
            if (workshopRoot1104 == null) return;
            if (string.IsNullOrEmpty(id) || (toggle && workshopTool1104 == id)) { CollapseWorkshopEditor1105(); return; }
            CloseWorkshopParameter1105(); CloseAiDress1104(); CloseWorkshopMusic1105();
            workshopTool1104 = id;
            workshopStage1104.TurnMode = id == "turn";
            workshopStage1104.Shot = id == "turn" ? 2 : workshopFraming1105;
            RefreshWorkshopFraming1105();
            RefreshWorkshopTools1105();
            WorkshopLook1104 look = workshopLook1104;
            switch (id)
            {
                case "dress": workshopStage1104.Dial(null); OpenAiDress1104(); break;
                case "music": workshopStage1104.Dial(null); OpenWorkshopMusic1105(); break;
                case "filter":
                    OpenWorkshopPresets1105(new WorkshopDial1104("滤镜", WorkshopFilters1104.Length, () => look.Filter, v => look.Filter = v,
                        v => WorkshopFilters1104[v].Name), "filter"); break;
                case "bright":
                    OpenWorkshopSlider1105(new WorkshopDial1104("亮度", 60, 130, 5, () => look.Brightness, v => look.Brightness = v, v => v + "%"), 100); break;
                case "pitch":
                    OpenWorkshopSlider1105(new WorkshopDial1104("音高", -30, 30, 2, () => look.Pitch, v => look.Pitch = v,
                        v => v > 0 ? "+" + v : v.ToString()) { Released = PlayWorkshopVoice1104 }, 0); break;
                case "voice": workshopStage1104.Dial(null); OpenWorkshopVoicePanel1105(); break;
                case "bg":
                    OpenWorkshopPresets1105(new WorkshopDial1104("背景", WorkshopBackgrounds1104.Length, () => look.Background, v => look.Background = v,
                        v => WorkshopBackgrounds1104[v].Name), "bg"); break;
                case "accent":
                    OpenWorkshopPresets1105(new WorkshopDial1104("应援色", WorkshopAccents1104.Length, () => look.Accent, v => look.Accent = v,
                        v => WorkshopAccents1104[v].Name) { Swatch = v => WorkshopAccents1104[v].Glow }, "accent"); break;
                case "bob":
                    OpenWorkshopSlider1105(new WorkshopDial1104("律动", 0, 100, 5, () => look.Bob, v => look.Bob = v, v => v + "%"), 50); break;
                case "turn": workshopStage1104.Dial(null); OpenWorkshopTurnPanel1105(); break;
                case "chroma":
                    OpenWorkshopPresets1105(new WorkshopDial1104("炫彩", WorkshopChromas1109.Length, () => look.Chroma, v => look.Chroma = v,
                        v => WorkshopChromas1109[v].Name) { Swatch = v => WorkshopChromas1109[v].Swatch }, "chroma"); break;
            }
        }

        private void OpenWorkshopPresets1105(WorkshopDial1104 value, string kind)
        {
            workshopStage1104.Dial(value, false);
            var panel = WorkshopEditorPanel1105("WorkshopParameterPanel", value.Name, 280f);
            workshopParameter1105 = panel.gameObject;
            var viewport = new GameObject("PresetViewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(panel, false);
            WorkshopPoster1105.Fill(viewport, 18f, 20f, -18f, -84f);
            var content = new GameObject("PresetContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, .5f);
            int count = value.Steps + 1;
            content.sizeDelta = new Vector2(count * 128f, 0f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>(); viewportHit.color = new Color(0f, 0f, 0f, .001f);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28f;
            Sprite portrait = workshopMember1104 < 0 ? Resources.Load<Sprite>("Art/Workshop1104/starter-still")
                : PlayerAvatar1102.Member(WorkshopSubjectId1104);
            var faces = new Image[count]; var labels = new Text[count];
            void Refresh()
            {
                for (int j = 0; j < count; j++)
                {
                    bool on = value.Get() == value.Min + j * value.Step;
                    faces[j].color = on ? WorkshopPoster1105.Teal : new Color32(46, 38, 62, 255);
                    labels[j].color = on ? WorkshopPoster1105.Ink : WorkshopPoster1105.Paper;
                }
            }
            for (int i = 0; i < count; i++)
            {
                int v = value.Min + i * value.Step;
                Image face = WorkshopPoster1105.Block(content, "Preset-" + v, WorkshopPoster1105.Slant(.08f), Color.clear);
                face.raycastTarget = true;
                WorkshopPoster1105.Corner(face.rectTransform, new Vector2(0f, .5f), 4f + i * 128f, 0f, 116f, 164f);
                faces[i] = face;
                Sprite preview = kind == "bg" ? Resources.Load<Sprite>(WorkshopBackgrounds1104[v].Path) : portrait;
                Color tint = kind == "accent" ? WorkshopAccents1104[v].Glow : kind == "filter" ? WorkshopFilters1104[v].Tint : Color.white;
                Image thumb = WorkshopPoster1105.Block(face.transform, "Preview", kind == "accent" ? DotSprite() : preview, tint);
                thumb.preserveAspect = true;
                WorkshopPoster1105.Corner(thumb.rectTransform, new Vector2(.5f, 1f), 0f, -8f, 98f, 116f);
                if (kind == "chroma")
                {
                    // Her portrait in that colour variant, with the variant's swatch as a stripe under it.
                    var chroma = WorkshopChromas1109[v];
                    Shader shader = Resources.Load<Shader>("Shaders/UIChroma1109");
                    if (shader != null)
                    {
                        var mat = new Material(shader) { name = "ChromaPreview-" + v };
                        mat.SetFloat("_Hue", chroma.Hue); mat.SetFloat("_Strength", chroma.Strength);
                        mat.SetFloat("_SatBoost", chroma.Sat); mat.SetFloat("_ValGain", chroma.Val);
                        thumb.material = mat;
                        thumb.gameObject.AddComponent<OwnedMaterial1109>().Material = mat;
                    }
                    Image stripe = WorkshopPoster1105.Block(face.transform, "Swatch", null, chroma.Swatch);
                    WorkshopPoster1105.Corner(stripe.rectTransform, new Vector2(.5f, 0f), 0f, 38f, 84f, 6f);
                }
                Text caption = WorkshopPoster1105.Word(face.transform, "Label", value.Format(v), 20, White);
                WorkshopPoster1105.Corner(caption.rectTransform, new Vector2(.5f, 0f), 0f, 5f, 110f, 32f);
                labels[i] = caption;
                Button button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face; button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => { VisualA1101.Click(); workshopStage1104.SetEditorValue1105(v, true); Refresh(); });
            }
            Refresh();
        }

        private void OpenWorkshopSlider1105(WorkshopDial1104 value, int defaultValue)
        {
            workshopStage1104.Dial(value, false);
            var panel = WorkshopEditorPanel1105("WorkshopParameterPanel", value.Name, value.Released == null ? 264f : 312f);
            workshopParameter1105 = panel.gameObject;
            Text number = WorkshopPoster1105.Word(panel, "ParameterValue", value.Format(value.Get()), 32, WorkshopPoster1105.Teal);
            WorkshopPoster1105.Corner(number.rectTransform, new Vector2(.5f, 1f), 0f, -86f, 220f, 46f);
            var sliderRoot = new GameObject("ParameterSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            sliderRoot.transform.SetParent(panel, false);
            WorkshopPoster1105.Corner((RectTransform)sliderRoot.transform, new Vector2(.5f, 1f), 0f, -138f, 536f, 54f);
            sliderRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            Image track = WorkshopPoster1105.Block(sliderRoot.transform, "Track", null, new Color32(69, 60, 88, 255));
            track.rectTransform.anchorMin = new Vector2(0f, .5f); track.rectTransform.anchorMax = new Vector2(1f, .5f);
            track.rectTransform.offsetMin = new Vector2(0f, -3f); track.rectTransform.offsetMax = new Vector2(0f, 3f);
            Image fill = WorkshopPoster1105.Block(track.transform, "Fill", null, WorkshopPoster1105.Teal);
            WorkshopPoster1105.Fill(fill.rectTransform, 0f, 0f, 0f, 0f);
            Image handle = WorkshopPoster1105.Block(sliderRoot.transform, "Handle", WorkshopPoster1105.Jag(), WorkshopPoster1105.Paper);
            handle.rectTransform.sizeDelta = new Vector2(26f, 36f);
            var slider = sliderRoot.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = value.Steps; slider.wholeNumbers = true;
            slider.SetValueWithoutNotify((value.Get() - value.Min) / value.Step);
            void Set(int v, bool released)
            {
                workshopStage1104.SetEditorValue1105(v, released);
                number.text = value.Format(value.Get());
                slider.SetValueWithoutNotify((value.Get() - value.Min) / value.Step);
            }
            slider.onValueChanged.AddListener(v => Set(value.Min + Mathf.RoundToInt(v) * value.Step, false));
            sliderRoot.AddComponent<WorkshopSliderRelease1105>().Released = () => value.Released?.Invoke();
            GameObject minus = NewButton("ParameterMinus", panel, "−", 25, PanelKit.ButtonDark, White, () => Set(value.Get() - value.Step, true));
            WorkshopPoster1105.Corner((RectTransform)minus.transform, new Vector2(0f, 1f), 18f, -142f, 54f, 48f);
            GameObject plus = NewButton("ParameterPlus", panel, "+", 25, PanelKit.ButtonDark, White, () => Set(value.Get() + value.Step, true));
            WorkshopPoster1105.Corner((RectTransform)plus.transform, Vector2.one, -18f, -142f, 54f, 48f);
            WorkshopTextButton1104(panel, "ParameterReset", "重置", 19, new Vector2(.5f, 0f), new Vector2(0f, 32f), () => Set(defaultValue, true));
            if (value.Released != null)
            {
                GameObject play = NewButton("ParameterPreview", panel, "▶ 试听", 20, PanelKit.ButtonDark, White, () => value.Released());
                WorkshopPoster1105.Corner((RectTransform)play.transform, new Vector2(.5f, 0f), 0f, 64f, 160f, 48f);
            }
        }

        private void OpenWorkshopTurnPanel1105()
        {
            var panel = WorkshopEditorPanel1105("WorkshopParameterPanel", "转身", 260f);
            workshopParameter1105 = panel.gameObject;
            bool ready = workshopStage1104.HasTurnViews;
            Text hint = WorkshopPoster1105.Word(panel, "TurnHint", ready ? "左右拖动角色，查看不同角度" : "这位成员的多角度立绘还在制作中", 20, VisualA1101.Muted);
            WorkshopPoster1105.Corner(hint.rectTransform, new Vector2(.5f, 1f), 0f, -86f, 660f, 40f);
            string[] names = { "正面", "侧面", "背面" }; int[] views = { 0, 2, 3 };
            for (int i = 0; i < names.Length; i++)
            {
                int view = views[i];
                GameObject button = NewButton("TurnView-" + view, panel, names[i], 21, PanelKit.ButtonDark, White, () => workshopStage1104.TurnTo(view));
                WorkshopPoster1105.Corner((RectTransform)button.transform, new Vector2(0f, 0f), 80f + i * 190f, 42f, 172f, 54f);
                button.GetComponent<Button>().interactable = ready || view == 0;
            }
        }

        private void OpenWorkshopVoicePanel1105()
        {
            var panel = WorkshopEditorPanel1105("WorkshopParameterPanel", "录声音", 244f);
            workshopParameter1105 = panel.gameObject;
            Text hint = WorkshopPoster1105.Word(panel, "VoiceHint", $"已录 {WorkshopRecorder1104.CountSamples(WorkshopSubjectId1104, 3)}/3 段 · 录音保存在本机", 20, VisualA1101.Muted);
            WorkshopPoster1105.Corner(hint.rectTransform, new Vector2(.5f, 1f), 0f, -88f, 660f, 38f);
            GameObject open = NewButton("OpenVoiceRecorder", panel, "打开录音", 22, PanelKit.ButtonDark, White, OpenVoiceRecorder1104);
            WorkshopPoster1105.Corner((RectTransform)open.transform, new Vector2(.5f, 0f), 0f, 30f, 300f, 58f);
        }

        // ------------------------------------------------------------------ AI 换装 (in game)

        private GameObject aiDressPanel1104;
        private string aiDressTag1104 = WorkshopAi1104.StyleTags[0], aiDressPrompt1104 = string.Empty, aiDressStatus1104 = string.Empty;
        private bool aiDressBusy1104;
        private Texture2D aiDressResult1104;
        private string aiDressResultSubject1105, aiDressResultStyle1105;
        private Sprite aiDressSource1105;

        private void CloseAiDress1104()
        {
            if (aiDressPanel1104 != null)
            {
                var motion = aiDressPanel1104.GetComponent<WorkshopDrawerMotion1105>();
                if (motion != null) motion.Close(); else Destroy(aiDressPanel1104);
            }
            aiDressPanel1104 = null;
            if (workshopStage1104 != null) workshopStage1104.BottomBand = WorkshopToolBand1105;
        }

        /// <summary>
        /// 2026-11-05 poster AI 换装 panel along the bottom: a slanted ink band holding torn style stickers (selected =
        /// solid teal), the description strip and the 生成 ticket. A finished outfit gets a full-page reveal first.
        /// </summary>
        private void OpenAiDress1104()
        {
            CloseAiDress1104();
            if (workshopRoot1104 == null) return;
            var panel = WorkshopEditorPanel1105("AiDressPanel", "换装", 440f);
            aiDressPanel1104 = panel.gameObject;
            Text note = WorkshopPoster1105.Word(panel, "Note", $"只换衣服不换脸 · 完成后消耗 {GameModel.AiDressDiamondCost1105} 星钻", 17, VisualA1101.Muted, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(note.rectTransform, new Vector2(0f, 1f), 28f, -80f, 664f, 30f);

            // Torn style stickers, 4 x 2, each tilted a little differently.
            string[] tags = WorkshopAi1104.StyleTags;
            float[] tilts = { -4f, 3f, -2f, 5f, 2f, -5f, 4f, -3f };
            float cw = 150f, ch = 52f, gap = 14f, left = (720f - 4 * cw - 3 * gap) * .5f;
            for (int i = 0; i < tags.Length; i++)
            {
                string tag = tags[i];
                bool on = tag == aiDressTag1104;
                Color face = on ? (Color)Brand1025.Teal : (i % 3 == 1 ? WorkshopPoster1105.Ink : WorkshopPoster1105.Paper);
                Color ink = on ? WorkshopPoster1105.Ink : (i % 3 == 1 ? WorkshopPoster1105.Paper : (i % 2 == 0 ? WorkshopPoster1105.Ink : WorkshopPoster1105.Hot));
                var go = new GameObject("Tag-" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(panel, false);
                var r = (RectTransform)go.transform;
                WorkshopPoster1105.Corner(r, Vector2.zero, left + (i % 4) * (cw + gap), 266f - (i / 4) * (ch + 14f), cw, ch, tilts[i]);
                bool dark = i % 3 == 1 && !on;
                Image sticker = go.GetComponent<Image>(); sticker.sprite = WorkshopPoster1105.Jag(); sticker.color = dark ? WorkshopPoster1105.Paper : face;
                if (dark)
                {
                    Image inner = WorkshopPoster1105.Block(go.transform, "Face", WorkshopPoster1105.Jag(), WorkshopPoster1105.Ink);
                    WorkshopPoster1105.Fill(inner.rectTransform, 3f, 3f, -3f, -3f);
                }
                if (on) WorkshopPoster1105.HardShadow(sticker, WorkshopPoster1105.Paper, 5f, -5f);
                Text label = WorkshopPoster1105.Word(go.transform, "Text", tag, on ? 21 : 19, ink);
                VisualA1101.Stretch(label.rectTransform);
                Button b = go.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = sticker;
                b.onClick.AddListener(() => { VisualA1101.Click(); aiDressTag1104 = tag; OpenAiDress1104(); });
                go.AddComponent<VisualA1101Motion>().Press();
            }

            Image box = WorkshopPoster1105.Block(panel, "Describe", WorkshopPoster1105.Slant(.18f), new Color(1f, 1f, 1f, .08f));
            box.raycastTarget = true;
            WorkshopPoster1105.Corner(box.rectTransform, Vector2.zero, 26f, 144f, 668f, 46f);
            Text value = VisualA1101.Label(box.transform, "Value", string.Empty, 17, VisualA1101.Paper, TextAnchor.MiddleLeft);
            value.supportRichText = false;
            Stretch(value.rectTransform, 20, 0, -14, 0);
            Text hint = VisualA1101.Label(box.transform, "Placeholder", "补充描述（选填）：比如 白色蕾丝、星星发饰…", 15, VisualA1101.Muted, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 20, 0, -14, 0);
            InputField input = box.gameObject.AddComponent<InputField>();
            input.targetGraphic = box; input.textComponent = value; input.placeholder = hint;
            input.characterLimit = 40; input.lineType = InputField.LineType.SingleLine;
            input.text = aiDressPrompt1104;
            input.onValueChanged.AddListener(v => aiDressPrompt1104 = v);

            bool hasResult = aiDressResult1104 != null && aiDressResultSubject1105 == WorkshopSubjectId1104;
            bool pending = WorkshopAi1104.HasPending(WorkshopSubjectId1104);
            Button go1 = WorkshopPoster1105.Ticket(panel, "AiGenerate", aiDressBusy1104 ? "生成中…" : pending ? "继续上次生成" : hasResult ? "再来一套" : "生成新造型",
                aiDressBusy1104 ? null : $"{GameModel.AiDressDiamondCost1105} 星钻", 24, StartAiDress1104);
            WorkshopPoster1105.Corner((RectTransform)go1.transform, Vector2.zero, 30f, 22f, hasResult ? 400f : 560f, 60f);
            go1.interactable = !aiDressBusy1104;
            if (hasResult)
            {
                Button keep = WorkshopPoster1105.Ticket(panel, "AiSave", "存进衣橱", null, 20, SaveAiDress1105,
                    WorkshopPoster1105.Ink, WorkshopPoster1105.Paper, WorkshopPoster1105.Ink);
                WorkshopPoster1105.Corner((RectTransform)keep.transform, Vector2.zero, 446f, 22f, 170f, 56f);
                WorkshopTextButton1104(panel, "AiUndo", "换回", 18, Vector2.zero, new Vector2(626f, 50f), UndoAiDress1105);
                RectTransform undo = (RectTransform)panel.Find("AiUndo");
                undo.anchoredPosition = new Vector2(640f, 50f); undo.sizeDelta = new Vector2(68f, 60f);
            }
            string status = aiDressBusy1104 ? aiDressStatus1104 : !WorkshopAi1104.Connected ? WorkshopAi1104.PendingNotice : aiDressStatus1104;
            Text line = WorkshopPoster1105.Word(panel, "Status", status, 14, VisualA1101.Muted, TextAnchor.MiddleLeft);
            WorkshopPoster1105.Corner(line.rectTransform, Vector2.zero, 28f, 102f, 664f, 30f);
        }

        private void SaveAiDress1105()
        {
            if (aiDressResult1104 == null) return;
            WorkshopAi1104.SaveToWardrobe(string.IsNullOrEmpty(aiDressResultSubject1105) ? WorkshopSubjectId1104 : aiDressResultSubject1105, aiDressResult1104);
            VisualA1101.Success(); Toast("已存进衣橱（本机）");
        }

        private void UndoAiDress1105()
        {
            aiDressResult1104 = null; aiDressResultSubject1105 = null; aiDressSource1105 = null; workshopStage1104?.SetOverride(null);
            CloseAiReveal1105();
            if (aiDressPanel1104 != null) OpenAiDress1104();
        }

        // ------------------------------------------------------------------ 生成完成：全屏亮相

        private GameObject aiReveal1105;

        private void CloseAiReveal1105()
        {
            if (aiReveal1105 != null) Destroy(aiReveal1105);
            aiReveal1105 = null;
        }

        /// <summary>
        /// 2026-11-05 a finished outfit appears like a finishing move: hot pink poster page, a big NEW LOOK!, the new look
        /// large on the right and the old one greyed on the left, then 存进衣橱 / 再来一套 / 换回原装 (先看看 keeps it on stage).
        /// </summary>
        private void ShowAiReveal1105(Texture2D result)
        {
            CloseAiReveal1105();
            if (workshopRoot1104 == null || result == null) return;
            var root = (RectTransform)workshopRoot1104.transform;
            var page = new GameObject("AiReveal1105", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            page.SetParent(root, false); WorkshopPoster1105.Fill(page, 0f, 0f, 0f, 0f);
            aiReveal1105 = page.gameObject;
            Image bg = page.GetComponent<Image>(); bg.color = WorkshopPoster1105.Hot; bg.raycastTarget = true;
            Image stripes = WorkshopPoster1105.Block(page, "Halftone", WorkshopPoster1105.Dots(), new Color(0f, 0f, 0f, .16f));
            stripes.type = Image.Type.Tiled; WorkshopPoster1105.Fill(stripes.rectTransform, 0f, 0f, 0f, 0f);
            Image inkBand = WorkshopPoster1105.Block(page, "BandInk", null, WorkshopPoster1105.Ink);
            WorkshopPoster1105.At(inkBand.rectTransform, 0f, 120f, 1800f, 560f, -14f);
            Image paperBand = WorkshopPoster1105.Block(page, "BandPaper", null, WorkshopPoster1105.Paper);
            WorkshopPoster1105.At(paperBand.rectTransform, 0f, -230f, 1800f, 26f, -14f);

            Sprite before = aiDressSource1105 ?? (workshopMember1104 < 0 ? Resources.Load<Sprite>("Art/Workshop1104/starter-still")
                : Resources.Load<Sprite>(GameModel.Members[workshopMember1104].ResourcePath));
            float pageH = Mathf.Max(1200f, root.rect.height);
            if (before != null)
            {
                Image old = WorkshopPoster1105.Block(page, "Before", before, new Color(.55f, .55f, .6f, .75f));
                old.preserveAspect = true;
                WorkshopPoster1105.Corner(old.rectTransform, new Vector2(0f, .5f), -40f, -20f, 300f, pageH * .42f, -6f);
                Text oldTag = WorkshopPoster1105.Word(page, "BeforeTag", "换装前", 18, WorkshopPoster1105.Ink);
                Image oldPlate = WorkshopPoster1105.Block(page, "BeforePlate", null, WorkshopPoster1105.Paper);
                WorkshopPoster1105.Corner(oldPlate.rectTransform, new Vector2(0f, .5f), 30f, -pageH * .22f - 20f, 96f, 34f, -6f);
                oldTag.transform.SetParent(oldPlate.transform, false); VisualA1101.Stretch(oldTag.rectTransform);
            }
            var tex = result;
            Image now = WorkshopPoster1105.Block(page, "After", Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f), Color.white);
            now.preserveAspect = true;
            WorkshopPoster1105.Corner(now.rectTransform, new Vector2(1f, .5f), 30f, 10f, 520f, pageH * .68f, 3f);
            WorkshopPoster1105.HardShadow(now, WorkshopPoster1105.Paper, -10f, 0f);

            Text big = WorkshopPoster1105.Word(page, "NewLook", "NEW LOOK!", 92, WorkshopPoster1105.Paper);
            big.fontStyle = FontStyle.BoldAndItalic;
            WorkshopPoster1105.Corner(big.rectTransform, new Vector2(0f, 1f), 24f, -120f, 640f, 110f, 10f);
            WorkshopPoster1105.HardShadow(big, WorkshopPoster1105.Ink, 7f, -7f);
            Image tagPlate = WorkshopPoster1105.Block(page, "StylePlate", WorkshopPoster1105.Jag(), WorkshopPoster1105.Paper);
            WorkshopPoster1105.Corner(tagPlate.rectTransform, new Vector2(0f, 1f), 30f, -250f, 200f, 60f, -5f);
            WorkshopPoster1105.HardShadow(tagPlate, WorkshopPoster1105.Ink, 6f, -6f);
            Text style = WorkshopPoster1105.Word(tagPlate.transform, "Text", aiDressResultStyle1105 ?? aiDressTag1104, 26, WorkshopPoster1105.Ink);
            VisualA1101.Stretch(style.rectTransform);

            WorkshopTextButton1104(page, "AiRevealPeek", "先看看 ›", 20, Vector2.one, new Vector2(-16f, -40f), CloseAiReveal1105);
            Button keep = WorkshopPoster1105.Ticket(page, "AiRevealSave", "存进衣橱", null, 26, () => { SaveAiDress1105(); CloseAiReveal1105(); });
            WorkshopPoster1105.Corner((RectTransform)keep.transform, Vector2.zero, 28f, 120f, 330f, 66f);
            Button again = WorkshopPoster1105.Ticket(page, "AiRevealAgain", "再来一套", $"{GameModel.AiDressDiamondCost1105} 星钻", 20,
                () => { CloseAiReveal1105(); StartAiDress1104(); }, WorkshopPoster1105.Ink, WorkshopPoster1105.Paper, WorkshopPoster1105.Ink);
            WorkshopPoster1105.Corner((RectTransform)again.transform, new Vector2(1f, 0f), -24f, 40f, 300f, 58f);
            WorkshopTextButton1104(page, "AiRevealUndo", "换回原装", 18, Vector2.zero, new Vector2(34f, 60f), UndoAiDress1105);
            page.gameObject.AddComponent<VisualA1101Motion>().Pop(0f);
        }

        private void StartAiDress1104()
        {
            if (aiDressBusy1104) return;
            if (!WorkshopAi1104.Connected) { VisualA1101.Deny(); Toast(WorkshopAi1104.PendingNotice); return; }
            if (!model.CanPayAiDress1105) { VisualA1101.Deny(); Toast($"星钻不足，每次需要 {GameModel.AiDressDiamondCost1105} 星钻"); return; }
            string prompt = WorkshopAi1104.PromptFor(aiDressTag1104, aiDressPrompt1104);
            Sprite still = workshopMember1104 < 0 ? Resources.Load<Sprite>("Art/Workshop1104/starter-still")
                : Resources.Load<Sprite>(GameModel.Members[workshopMember1104].ResourcePath);
            string generatingSubject = WorkshopSubjectId1104, generatingStyle = aiDressTag1104;
            aiDressBusy1104 = true; aiDressStatus1104 = "准备中…";
            OpenAiDress1104();
            StartCoroutine(WorkshopAi1104.Generate(generatingSubject, still, prompt, text =>
            {
                aiDressStatus1104 = text;
                Text line = aiDressPanel1104 != null ? aiDressPanel1104.transform.Find("Status")?.GetComponent<Text>() : null;
                if (line != null) line.text = text;
            }, (texture, error) =>
            {
                aiDressBusy1104 = false;
                if (texture != null)
                {
                    aiDressResult1104 = texture;
                    aiDressResultSubject1105 = generatingSubject; aiDressResultStyle1105 = generatingStyle; aiDressSource1105 = still;
                    bool paid = model.PayAiDress1105();
                    aiDressStatus1104 = (paid ? $"生成完成 · 消耗 {GameModel.AiDressDiamondCost1105} 星钻" : "生成完成")
                        + (WorkshopAi1104.RemainingToday >= 0 ? $" · 今天还能换 {WorkshopAi1104.RemainingToday} 次" : string.Empty);
                    VisualA1101.Success();
                    if (workshopRoot1104 != null && WorkshopSubjectId1104 == generatingSubject)
                    {
                        workshopStage1104?.SetOverride(texture);
                        ShowAiReveal1105(texture);
                    }
                }
                else { aiDressStatus1104 = error; Toast(error); }
                if (aiDressPanel1104 != null) OpenAiDress1104();
            }));
        }

        private void PlayWorkshopVoice1104()
        {
            string clip = DialogueVoice1102.FirstLineOf(WorkshopSubjectId1104, out _);
            model.SaveWorkshopLook1104(workshopLook1104);
            if (clip != null && gameAudio != null) gameAudio.PlayVoice(clip, "workshop");
        }

        // ------------------------------------------------------------------ 我的声音

        private void OpenVoiceRecorder1104()
        {
            string subjectId = WorkshopSubjectId1104, subjectName = WorkshopSubjectName1104;
            RectTransform body = ProgressionModal("VoiceRecorderModal", "录制我的声音", null, 620f);
            var frame = new ModalPolish038Frame
            {
                Overlay = modalObject, Body = body, Panel = (RectTransform)body.parent,
                Width = 620f, Height = 620f / ModalPolish038Chrome.PanelAspect,
            };
            float width = BodyWidth1101(frame), y = 0f;
            Text intro = VisualA1101.Label(body, "RecorderIntro", $"读下面 3 段文稿，用你的声音为「{subjectName}」生成专属声线", 15,
                VisualA1101.Muted, TextAnchor.MiddleLeft);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            VisualA1101.Place(intro.rectTransform, 0f, y, width, 44f);
            y += 52f;

            bool consent = workshopLook1104.VoiceConsent;
            RecorderChip1104(body, "RecorderConsent", consent ? "✓ 我确认是本人的声音，同意用于生成声线" : "□ 我确认是本人的声音，同意用于生成声线",
                0f, y, width, 48f, consent, () =>
                {
                    workshopLook1104.VoiceConsent = !workshopLook1104.VoiceConsent;
                    model.SaveWorkshopLook1104(workshopLook1104);
                    OpenVoiceRecorder1104();
                });
            y += 60f;

            int index = Mathf.Clamp(workshopScript1104, 0, VoiceScripts1104.Length - 1);
            GameObject card = ModalPolish038Chrome.RowPlate(body, "RecorderScript");
            PanelKit.PlaceTop(card.GetComponent<RectTransform>(), 0f, y, width, 120f);
            Text number = VisualA1101.Label(card.transform, "Index", $"第 {index + 1}/3 段", 14, VisualA1101.Teal, TextAnchor.MiddleLeft);
            VisualA1101.Place(number.rectTransform, 18f, 10f, 160f, 24f);
            Text script = VisualA1101.Label(card.transform, "Text", "「" + VoiceScripts1104[index] + "」", 22, VisualA1101.Paper, TextAnchor.MiddleCenter);
            script.horizontalOverflow = HorizontalWrapMode.Wrap;
            VisualA1101.Place(script.rectTransform, 18f, 36f, width - 36f, 76f);
            y += 132f;

            bool supported = WorkshopRecorder1104.Supported;
            bool recording = WorkshopRecorder1104.Recording;
            bool has = WorkshopRecorder1104.HasSample(subjectId, index);
            float third = (width - 24f) / 3f;
            GameObject rec = ModalPolish038Chrome.ActionButton(body, "RecorderRecord", recording ? "■ 停止" : "● 录音", 18, () =>
            {
                if (!supported) { Toast("网页版暂不支持录音，请在 App 里录制"); return; }
                if (!workshopLook1104.VoiceConsent) { Toast("请先确认是本人的声音"); return; }
                if (WorkshopRecorder1104.Recording)
                {
                    if (WorkshopRecorder1104.Stop(subjectId, index, out string error))
                    {
                        workshopLook1104.SamplesRecorded = WorkshopRecorder1104.CountSamples(subjectId, VoiceScripts1104.Length);
                        model.SaveWorkshopLook1104(workshopLook1104);
                        Toast($"第 {index + 1} 段已保存在本机");
                        if (index < VoiceScripts1104.Length - 1) workshopScript1104 = index + 1;
                    }
                    else Toast(error);
                }
                else if (!WorkshopRecorder1104.Start(out string error)) Toast(error);
                OpenVoiceRecorder1104();
            }, recording ? ModalPolish038ButtonStyle.Secondary : ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(rec.GetComponent<RectTransform>(), 0f, y, third, 56f);
            GameObject play = ModalPolish038Chrome.ActionButton(body, "RecorderPlay", "▶ 回放", 18, () =>
            {
                if (!WorkshopRecorder1104.Play(subjectId, index, this)) Toast("这一段还没有录音");
            }, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(play.GetComponent<RectTransform>(), third + 12f, y, third, 56f);
            ModalPolish038Chrome.SetActionEnabled(play, has);
            GameObject next = ModalPolish038Chrome.ActionButton(body, "RecorderNext", "下一段 ›", 18, () =>
            {
                workshopScript1104 = (index + 1) % VoiceScripts1104.Length;
                OpenVoiceRecorder1104();
            }, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(next.GetComponent<RectTransform>(), (third + 12f) * 2f, y, third, 56f);
            y += 70f;

            int done = WorkshopRecorder1104.CountSamples(subjectId, VoiceScripts1104.Length);
            Text progress = VisualA1101.Label(body, "RecorderProgress", supported ? $"已录 {done}/3 段 · 录音只保存在本机" : "网页版暂不支持录音，请用 App 录制",
                14, VisualA1101.Muted, TextAnchor.MiddleCenter);
            VisualA1101.Place(progress.rectTransform, 0f, y, width, 24f);
            y += 34f;
            GameObject train = ModalPolish038Chrome.ActionButton(body, "RecorderTrain", "生成专属声线", 19, () =>
                Toast("声线训练服务接入中：开通后，录好的 3 段会上传训练，完成后在这里一键换上"),
                done >= VoiceScripts1104.Length ? ModalPolish038ButtonStyle.Primary : ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(train.GetComponent<RectTransform>(), 0f, y, width, 56f);
            ModalPolish038Chrome.SetActionEnabled(train, done >= VoiceScripts1104.Length);
            ModalPolish038Chrome.FitHeight(frame, y + 60f);
        }

        private void RecorderChip1104(Transform parent, string objectName, string text, float x, float y, float w, float h, bool selected, Action click)
        {
            Image face = NewImage(objectName, parent, RoundedSprite(12), selected ? (Color)Brand1025.Teal : new Color32(30, 22, 52, 255)).GetComponent<Image>();
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;
            PlaceTop(face.rectTransform, x, y, w, h);
            Text caption = VisualA1101.Label(face.transform, "Text", text, 15, selected ? (Color)Brand1025.Ink : VisualA1101.Paper, TextAnchor.MiddleCenter);
            VisualA1101.Stretch(caption.rectTransform);
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => click());
        }
    }

    /// <summary>A value the vinyl dial edits: an integer range with a step, or a list of named options.</summary>
    /// <summary>Destroys a runtime material with the graphic that uses it (炫彩 previews, 2026-11-09).</summary>
    public sealed class OwnedMaterial1109 : MonoBehaviour
    {
        public Material Material;
        private void OnDestroy() { if (Material != null) Destroy(Material); }
    }

    public sealed class WorkshopDial1104
    {
        public readonly string Name;
        public readonly int Min, Max, Step;
        public readonly Func<int> Get;
        public readonly Action<int> Set;
        public readonly Func<int, string> Format;
        public Action Released;
        /// <summary>Optional colour per value: the rim node shows the colour instead of text (应援色).</summary>
        public Func<int, Color?> Swatch;

        public WorkshopDial1104(string name, int min, int max, int step, Func<int> get, Action<int> set, Func<int, string> format)
        { Name = name; Min = min; Max = max; Step = Math.Max(1, step); Get = get; Set = set; Format = format; }

        public WorkshopDial1104(string name, int options, Func<int> get, Action<int> set, Func<int, string> format)
            : this(name, 0, options - 1, 1, get, set, format) { }

        public int Steps => (Max - Min) / Step;
    }

    /// <summary>
    /// The workshop stage: background, accent glow, the member (her still or idle loop, or an angle sheet while turning),
    /// and the camera that eases between half-body and full shots. Value bindings remain here while their actual controls
    /// live in the photo-editor drawer, leaving the stage clear for preview and turning gestures.
    /// </summary>
    public sealed class WorkshopStage1104 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float WheelSize = 1124f, NodeSize = 100f, IdleSeconds = 2f, NodeRadius = 348f;
        private RectTransform root, figureRect, wheelRect;
        private Image figure, background, glow;
        private Func<WorkshopLook1104> look;
        private Sprite front, dot, roundedSprite;
        private Sprite[] turn; // 0 front, 1 left 45, 2 left 90, 3 back, 4 right 90 (mirrored), 5 right 45 (mirrored)
        private static readonly bool[] Mirrored = { false, false, false, false, true, true };
        private Rect face;
        private WorkshopDial1104 dial;
        private VinylStageWheel1102 wheel;
        private Image[] nodeGlows, nodeFaces;
        private Text[] nodeLabels;
        private Text dialTitle;
        private float zoom = -1f, panY, slide, turnPos, turnVel, lastTouch = -10f;
        private bool turning;
        public int Shot = 2;
        /// <summary>2026-11-05 poster layout: space kept clear above (title) and below (AI panel) the full-length figure.</summary>
        public float TopBand = 250f, BottomBand = 118f;
        public bool TurnMode;
        public Action Changed;
        public bool HasTurnViews => turn != null;
        public int View => turn == null ? 0 : ((Mathf.RoundToInt(turnPos) % 6) + 6) % 6;
        /// <summary>0 tucked away … 1 fully out.</summary>
        public float Raise => slide;

        /// <summary>初始角色's face in her 432x768 loop (normalised, y up), measured on the first frame.</summary>
        public static readonly Rect StarterFace = new Rect(180f / 432f, (768f - 145f) / 768f, 60f / 432f, 58f / 768f);

        private RawImage video;
        private UnityEngine.Video.VideoPlayer player;
        private RenderTexture videoTexture;
        private Material videoMaterial, chromaMaterial;

        /// <param name="idleVideo">Optional stacked-alpha loop under StreamingAssets (left colour, right alpha), the same
        /// frame as <paramref name="still"/>; the still shows until the first frame and whenever she is turned.</param>
        public void Build(RectTransform page, string subjectId, Sprite still, Rect faceRect, string idleVideo,
            Func<WorkshopLook1104> source, Sprite rounded, Sprite dotSprite)
        {
            root = page; look = source; dot = dotSprite; roundedSprite = rounded;
            front = still;
            Sprite q45 = Resources.Load<Sprite>($"Art/Workshop1104/Turn/{subjectId}-q45");
            Sprite side = Resources.Load<Sprite>($"Art/Workshop1104/Turn/{subjectId}-side");
            Sprite back = Resources.Load<Sprite>($"Art/Workshop1104/Turn/{subjectId}-back");
            if (front != null && q45 != null && side != null && back != null) turn = new[] { front, q45, side, back, side, q45 };
            face = faceRect;

            background = MakeImage("WorkshopBackground", page, null, Color.white);
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = new Vector2(-40f, -40f); background.rectTransform.offsetMax = new Vector2(40f, 40f);
            // The whole stage takes turning drags (the dial and tools sit above it and keep their own input).
            background.raycastTarget = true;
            background.gameObject.AddComponent<WorkshopDragRelay1104>().Target = this;
            WorkshopPoster1105.Backdrop(page);

            glow = MakeImage("WorkshopGlow", page, VisualA1101.Glow, Color.white);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(.5f, .5f);
            figure = MakeImage("WorkshopFigure", page, front, Color.white);
            figureRect = figure.rectTransform;
            Shader chromaShader = Resources.Load<Shader>("Shaders/UIChroma1109");
            if (chromaShader != null) { chromaMaterial = new Material(chromaShader) { name = "WorkshopChroma1109" }; figure.material = chromaMaterial; }
            figureRect.anchorMin = figureRect.anchorMax = new Vector2(.5f, .5f);
            figureRect.pivot = new Vector2(.5f, .5f);
            if (!string.IsNullOrEmpty(idleVideo)) BuildVideo(idleVideo);

            // Dial container: the record's centre sits past the right edge; only its left arc and rim nodes show.
            wheelRect = new GameObject("WorkshopDial", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
            wheelRect.SetParent(page, false);
            // Top-left pivot: VinylStageWheel1102 lays its rim nodes out from the record's top-left corner.
            wheelRect.anchorMin = wheelRect.anchorMax = new Vector2(1f, 0f); wheelRect.pivot = new Vector2(0f, 1f);
            wheelRect.sizeDelta = new Vector2(WheelSize, WheelSize);
            Image hit = wheelRect.GetComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;
            wheelRect.gameObject.AddComponent<WorkshopDialTouch1104>().Touched = () => lastTouch = Time.unscaledTime;
            dialTitle = VisualA1101.Label(page, "WorkshopDialTitle", string.Empty, 18, Brand1025.Teal, TextAnchor.MiddleRight);
            dialTitle.rectTransform.anchorMin = dialTitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            dialTitle.rectTransform.pivot = new Vector2(1f, .5f);
            dialTitle.rectTransform.sizeDelta = new Vector2(200f, 30f);
            wheelRect.gameObject.SetActive(false);
            dialTitle.gameObject.SetActive(false);
            Apply();
        }

        private void BuildVideo(string file)
        {
            Shader shader = Resources.Load<Shader>("Shaders/UIStackedAlpha");
            if (shader == null) return;
            videoMaterial = new Material(shader) { name = "WorkshopIdleStackedAlpha" };
            var go = new GameObject("WorkshopFigureVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(figureRect, false);
            video = go.GetComponent<RawImage>();
            video.material = videoMaterial; video.raycastTarget = false; video.enabled = false;
            var r = video.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            player = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            player.playOnAwake = false; player.isLooping = true; player.skipOnDrop = true; player.waitForFirstFrame = true;
            player.source = UnityEngine.Video.VideoSource.Url;
            player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            string url = Application.streamingAssetsPath.TrimEnd('/') + "/" + file;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!url.Contains("://")) url = new Uri(Path.GetFullPath(url)).AbsoluteUri;
#endif
            player.url = url;
            player.prepareCompleted += p =>
            {
                videoTexture = new RenderTexture(Mathf.Max(2, (int)p.width), Mathf.Max(2, (int)p.height), 0, RenderTextureFormat.ARGB32);
                videoTexture.Create();
                p.targetTexture = videoTexture;
                video.texture = videoTexture;
                videoMaterial.SetFloat("_AlphaFromSrgb", QualitySettings.activeColorSpace == ColorSpace.Linear && videoTexture.sRGB ? 1f : 0f);
                p.Play();
            };
            player.errorReceived += (p, message) => Debug.LogWarning("CHO_SIREN_WORKSHOP_IDLE clip failed: " + message);
            player.Prepare();
        }

        private void OnDestroy()
        {
            if (player != null) player.Stop();
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
            if (videoMaterial != null) Destroy(videoMaterial);
            if (chromaMaterial != null) Destroy(chromaMaterial);
        }

        /// <summary>2026-11-09 炫彩: the chosen colour variant on her still and her idle loop.</summary>
        private void ApplyChroma(int index)
        {
            var c = ChoSirenApp.WorkshopChromas1109[Mathf.Clamp(index, 0, ChoSirenApp.WorkshopChromas1109.Length - 1)];
            foreach (Material m in new[] { chromaMaterial, videoMaterial })
            {
                if (m == null) continue;
                m.SetFloat("_Hue", c.Hue); m.SetFloat("_Strength", c.Strength);
                m.SetFloat("_SatBoost", c.Sat); m.SetFloat("_ValGain", c.Val);
            }
        }

        private static Image MakeImage(string objectName, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        /// <summary>Shows the dial for a value (null tucks it away completely). show=false leaves only its rim at the edge.</summary>
        public void Dial(WorkshopDial1104 value, bool show = true)
        {
            dial = value;
            for (int i = wheelRect.childCount - 1; i >= 0; i--) Destroy(wheelRect.GetChild(i).gameObject);
            if (wheel != null) Destroy(wheel);
            wheel = null; nodeGlows = null; nodeFaces = null; nodeLabels = null;
            lastTouch = show && value != null ? Time.unscaledTime : -10f;
            dialTitle.text = value != null ? value.Name : string.Empty;
            if (value == null) return;

            // Values are edited in the compact drawer, never on a giant half-disc over the character.
            wheelRect.gameObject.SetActive(false);
            dialTitle.gameObject.SetActive(false);
        }

        /// <summary>Continuously applies a photo-editor control; optional release previews audio once, not on every drag tick.</summary>
        public void SetEditorValue1105(int value, bool released)
        {
            if (dial == null) return;
            value = Mathf.Clamp(dial.Min + Mathf.RoundToInt((value - dial.Min) / (float)dial.Step) * dial.Step, dial.Min, dial.Max);
            if (value != dial.Get()) { dial.Set(value); Apply(); Changed?.Invoke(); }
            if (released) dial.Released?.Invoke();
        }

        private static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        }

        private void Select(int stage)
        {
            if (dial == null) return;
            lastTouch = Time.unscaledTime;
            int value = Mathf.Clamp(dial.Min + (stage - 1) * dial.Step, dial.Min, dial.Max);
            bool changed = value != dial.Get();
            dial.Set(value);
            wheel?.ShowStage(stage);
            Highlight(stage);
            Apply();
            if (changed) { dial.Released?.Invoke(); Changed?.Invoke(); }
        }

        private void Highlight(int stage)
        {
            if (nodeGlows == null) return;
            for (int i = 0; i < nodeGlows.Length; i++)
            {
                bool on = i == stage - 1;
                nodeGlows[i].enabled = on;
                if (nodeFaces[i] != null) nodeFaces[i].color = on ? (Color)Brand1025.Teal : new Color32(18, 12, 34, 230);
                if (nodeLabels[i] != null) nodeLabels[i].color = on ? (Color)Brand1025.Ink : VisualA1101.Paper;
            }
        }

        /// <summary>Tests and the stage UI: set the dial's value directly (it slides out to show it).</summary>
        public void SetDialValue(int value)
        {
            if (dial == null) return;
            Select(Mathf.Clamp((value - dial.Min) / dial.Step, 0, dial.Steps) + 1);
        }

        public void Apply()
        {
            WorkshopLook1104 l = look?.Invoke();
            if (l == null || figure == null) return;
            Color tint = ChoSirenApp.WorkshopFilters1104[Mathf.Clamp(l.Filter, 0, ChoSirenApp.WorkshopFilters1104.Length - 1)].Tint;
            float bright = l.Brightness / 100f;
            figure.color = new Color(Mathf.Min(1f, tint.r * bright), Mathf.Min(1f, tint.g * bright), Mathf.Min(1f, tint.b * bright), 1f);
            if (video != null) video.color = figure.color;
            var bg = ChoSirenApp.WorkshopBackgrounds1104[0]; // 11-09: 背景 removed — one stage backdrop
            background.sprite = Resources.Load<Sprite>(bg.Path) ?? Resources.Load<Sprite>("Art/LobbyBackground");
            Color accent = ChoSirenApp.WorkshopAccents1104[Mathf.Clamp(l.Accent, 0, ChoSirenApp.WorkshopAccents1104.Length - 1)].Glow;
            glow.color = new Color(accent.r, accent.g, accent.b, .45f);
            ApplyChroma(l.Chroma);
        }

        // ------------------------------------------------------------------ 转身

        public void OnBeginDrag(PointerEventData e) { turning = TurnMode && turn != null; }
        public void OnDrag(PointerEventData e)
        {
            if (!turning) return;
            float d = -e.delta.x / 110f;
            turnPos += d; turnVel = d;
        }
        public void OnEndDrag(PointerEventData e) { turning = false; }

        private Sprite authoredFront;
        private GameObject aiBadge;

        /// <summary>Shows an AI outfit in place of her front view (null restores her authored still / idle loop).</summary>
        public void SetOverride(Texture2D texture)
        {
            if (authoredFront == null) authoredFront = front;
            if (aiBadge != null) Destroy(aiBadge);
            if (texture == null) { front = authoredFront; if (turn != null) turn[0] = front; if (video != null) video.gameObject.SetActive(true); return; }
            front = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            if (turn != null) turn[0] = front;
            if (video != null) video.gameObject.SetActive(false);
            Image badge = MakeImage("AiBadge", root, roundedSprite, new Color32(12, 8, 26, 210));
            badge.type = Image.Type.Sliced;
            var br = badge.rectTransform; br.anchorMin = br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 1f);
            br.anchorMin = br.anchorMax = br.pivot = Vector2.one;
            br.anchoredPosition = new Vector2(-20f, -148f); br.sizeDelta = new Vector2(110f, 32f);
            Text label = VisualA1101.Label(badge.transform, "Text", "AI 生成", 14, Brand1025.Teal, TextAnchor.MiddleCenter);
            VisualA1101.Stretch(label.rectTransform);
            aiBadge = badge.gameObject;
        }

        /// <summary>Tests: turn to a view index (0 front … 3 back).</summary>
        public void TurnTo(int view) { turnPos = view; turnVel = 0f; }

        private void LateUpdate()
        {
            if (root == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            Vector2 page = root.rect.size;
            if (page.x <= 0f || page.y <= 0f) return;

            // Turning coasts and settles on the nearest sheet; leaving 转身 turns her back to the front.
            if (!TurnMode) turnPos = Mathf.MoveTowards(turnPos, Mathf.Round(turnPos / 6f) * 6f, dt * 9f);
            else if (!turning)
            {
                turnPos += turnVel; turnVel *= .85f;
                if (Mathf.Abs(turnVel) < .01f) turnPos = Mathf.MoveTowards(turnPos, Mathf.Round(turnPos), dt * 6f);
            }
            int view = View;
            Sprite want = turn != null ? turn[view] : front;
            if (want != null && figure.sprite != want) figure.sprite = want;
            if (video != null)
            {
                bool ready = player != null && player.isPrepared && player.isPlaying && (player.frame > 0 || player.time > .05);
                bool showVideo = ready && view == 0 && video.gameObject.activeSelf;
                video.enabled = showVideo;
                Color c = figure.color; c.a = showVideo ? 0f : 1f; figure.color = c;
            }
            float flip = turn != null && Mirrored[view] ? -1f : 1f;

            // Dial: out while a value tool is in use and touched recently; otherwise only the rim stays at the edge.
            bool using_ = dial != null && (Time.unscaledTime - lastTouch < IdleSeconds || (wheel != null && wheel.IsDragging));
            if (wheel != null && wheel.IsDragging) lastTouch = Time.unscaledTime;
            slide = Mathf.MoveTowards(slide, using_ ? 1f : 0f, dt * 4.5f);
            float r = slide * slide * (3f - 2f * slide);
            // Out: the record's centre 138px past the right edge (as on the stage map); tucked: a 64px sliver of rim (tap it or the tool to bring it back).
            float bottomBand = 186f, half = WheelSize * .5f;
            float centreX = Mathf.Lerp(dial == null ? half + 40f : half - 64f, 138f, r);
            float centreY = bottomBand + (page.y - bottomBand - 84f) * .5f;
            wheelRect.anchoredPosition = new Vector2(centreX - half, centreY + half);
            dialTitle.rectTransform.anchoredPosition = new Vector2(-24f, centreY + NodeRadius + 70f);
            dialTitle.color = new Color(Brand1025.Teal.r / 255f, Brand1025.Teal.g / 255f, Brand1025.Teal.b / 255f, r);

            // Camera: fit the shot (face / bust / full figure) into the stage between the top bar and the tools,
            // sliding her left while the dial is out so the record never covers her.
            WorkshopLook1104 l = look?.Invoke();
            Vector2 art = want != null ? want.rect.size : new Vector2(1000f, 2400f);
            float top = page.y * .5f - TopBand, bottom = -page.y * .5f + BottomBand, stageH = top - bottom;
            float faceH = face.height * art.y, faceCy = face.center.y * art.y;
            float shotH = Shot == 0 ? faceH * 4.2f : Shot == 1 ? art.y * .43f : art.y * 1.02f;
            float focus = Shot == 2 ? art.y * .5f : Shot == 1 ? art.y * .78f : faceCy - faceH * .55f;
            float targetZoom = stageH / shotH * (l != null ? Mathf.Clamp(l.Scale, 85, 115) / 100f : 1f);
            float ease = 1f - Mathf.Exp(-dt * 6.5f);
            if (zoom < 0f) zoom = targetZoom;
            zoom = Mathf.Lerp(zoom, targetZoom, ease);
            float drawH = art.y * zoom, drawW = art.x * zoom;
            float targetPan = (bottom + stageH * .5f) - (focus - art.y * .5f) * zoom;
            panY = Mathf.Lerp(panY, targetPan, ease);
            // The editor leaves the preview centered; opening controls only adjusts its available height.
            float shiftX = 0f;
            float t = Time.unscaledTime, bob = l != null ? l.Bob / 100f : .5f;
            figureRect.sizeDelta = new Vector2(drawW, drawH);
            figureRect.anchoredPosition = new Vector2(shiftX, panY + 5f * bob * Mathf.Sin(t * 1.6f));
            figureRect.localScale = new Vector3(flip * (l != null && l.Mirror ? -1f : 1f) * (1f + .008f * bob * Mathf.Sin(t * 2.2f)), 1f, 1f);
            figureRect.localRotation = Quaternion.Euler(0f, 0f, -2f + 1f * bob * Mathf.Sin(t * 1.1f));
            glow.rectTransform.sizeDelta = new Vector2(Mathf.Min(drawW, page.x * 1.6f), Mathf.Min(drawH * .8f, page.y * 1.4f));
            glow.rectTransform.anchoredPosition = new Vector2(shiftX, bottom + stageH * .5f);

            // Background pushes in a little with the camera, so a close-up reads as the camera moving.
            float closeness = Mathf.InverseLerp(stageH / (art.y * 1.02f), stageH / (faceH * 4.2f), zoom);
            background.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, closeness);
            float dim = Mathf.Lerp(.5f, .4f, closeness);
            background.color = new Color(dim, dim, dim + .03f, 1f);
        }
    }

    /// <summary>Short drawer movement; closing immediately releases raycasts while it slides away.</summary>
    public sealed class WorkshopDrawerMotion1105 : MonoBehaviour
    {
        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 target;
        private float age;
        private bool closing;
        private void Awake()
        {
            rect = (RectTransform)transform; target = rect.anchoredPosition;
            group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
        }
        public void Close() { if (closing) return; closing = true; age = 0f; group.blocksRaycasts = false; }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / .2f), ease = 1f - Mathf.Pow(1f - t, 3f);
            float reveal = closing ? 1f - ease : ease;
            rect.anchoredPosition = target + Vector2.down * (1f - reveal) * 90f;
            group.alpha = reveal;
            if (closing && t >= 1f) Destroy(gameObject);
        }
    }

    public sealed class WorkshopSliderRelease1105 : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        public Action Released;
        private bool pending;
        public void OnPointerDown(PointerEventData e) => pending = true;
        public void OnPointerUp(PointerEventData e) => Finish();
        public void OnEndDrag(PointerEventData e) => Finish();
        private void Finish() { if (!pending) return; pending = false; Released?.Invoke(); }
    }

    /// <summary>Slides the right-hand tool menu off the edge while the value dial is out (2026-11-05 poster layout).</summary>
    public sealed class WorkshopMenuSlide1105 : MonoBehaviour
    {
        public WorkshopStage1104 Stage;
        private RectTransform rect;
        private CanvasGroup group;
        private float home = float.NaN;

        private void LateUpdate()
        {
            if (Stage == null) return;
            if (rect == null) { rect = (RectTransform)transform; group = gameObject.AddComponent<CanvasGroup>(); }
            if (float.IsNaN(home)) home = rect.anchoredPosition.x;
            float r = Stage.Raise, e = r * r * (3f - 2f * r);
            rect.anchoredPosition = new Vector2(home + 260f * e, rect.anchoredPosition.y);
            group.alpha = 1f - e;
            group.blocksRaycasts = e < .5f;
        }
    }

    /// <summary>Marks the dial as in use on any touch (drags are handled by the record itself).</summary>
    public sealed class WorkshopDialTouch1104 : MonoBehaviour, IPointerDownHandler
    {
        public Action Touched;
        public void OnPointerDown(PointerEventData e) => Touched?.Invoke();
    }

    /// <summary>Forwards drags on the stage background to the stage (Unity only drag-routes to the hit object).</summary>
    public sealed class WorkshopDragRelay1104 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public WorkshopStage1104 Target;
        public void OnBeginDrag(PointerEventData e) => Target?.OnBeginDrag(e);
        public void OnDrag(PointerEventData e) => Target?.OnDrag(e);
        public void OnEndDrag(PointerEventData e) => Target?.OnEndDrag(e);
    }

    /// <summary>Microphone voice samples for 我的声音 (native only; WebGL has no Microphone API). Saved as 16-bit mono WAV
    /// under persistentDataPath/voice-samples/&lt;member&gt;/&lt;n&gt;.wav and never uploaded by this build.</summary>
    public static class WorkshopRecorder1104
    {
        private const int Rate = 44100, MaxSeconds = 15;
        private static AudioClip clip;
        private static string device;

        public static bool Supported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return Microphone.devices != null && Microphone.devices.Length > 0;
#endif
            }
        }

        public static bool Recording => clip != null;

        private static string PathOf(string memberId, int index) =>
            Path.Combine(Application.persistentDataPath, "voice-samples", memberId ?? "member", index + ".wav");

        public static bool HasSample(string memberId, int index) => File.Exists(PathOf(memberId, index));

        public static int CountSamples(string memberId, int count)
        {
            int n = 0;
            for (int i = 0; i < count; i++) if (HasSample(memberId, i)) n++;
            return n;
        }

        public static bool Start(out string error)
        {
            error = string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
            error = "网页版暂不支持录音，请在 App 里录制";
            return false;
#else
            if (!Supported) { error = "没有找到麦克风"; return false; }
            device = Microphone.devices[0];
            clip = Microphone.Start(device, false, MaxSeconds, Rate);
            if (clip == null) { error = "麦克风没有打开，请在系统设置里允许录音"; return false; }
            return true;
#endif
        }

        public static void Cancel()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (clip != null) Microphone.End(device);
#endif
            clip = null;
        }

        public static bool Stop(string memberId, int index, out string error)
        {
            error = string.Empty;
#if UNITY_WEBGL && !UNITY_EDITOR
            error = "网页版暂不支持录音";
            return false;
#else
            if (clip == null) { error = "还没有开始录音"; return false; }
            int samples = Microphone.GetPosition(device);
            Microphone.End(device);
            AudioClip recorded = clip;
            clip = null;
            if (samples <= Rate / 2) { error = "录音太短了，再读一遍吧"; return false; }
            var data = new float[samples * recorded.channels];
            recorded.GetData(data, 0);
            string path = PathOf(memberId, index);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Wav(data, recorded.channels, Rate));
            return true;
#endif
        }

        public static bool Play(string memberId, int index, MonoBehaviour host)
        {
            string path = PathOf(memberId, index);
            if (!File.Exists(path) || host == null) return false;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length <= 44) return false;
            int count = (bytes.Length - 44) / 2;
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = BitConverter.ToInt16(bytes, 44 + i * 2) / 32768f;
            AudioClip sample = AudioClip.Create("voice-sample", count, 1, Rate, false);
            sample.SetData(data, 0);
            AudioSource source = host.GetComponent<AudioSource>();
            if (source == null) source = host.gameObject.AddComponent<AudioSource>();
            source.PlayOneShot(sample);
            return true;
        }

        private static byte[] Wav(float[] data, int channels, int rate)
        {
            int mono = data.Length / Math.Max(1, channels);
            var bytes = new byte[44 + mono * 2];
            void Write(int at, string text) { for (int i = 0; i < 4; i++) bytes[at + i] = (byte)text[i]; }
            void Int(int at, int v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
            void Short(int at, short v) => BitConverter.GetBytes(v).CopyTo(bytes, at);
            Write(0, "RIFF"); Int(4, 36 + mono * 2); Write(8, "WAVE"); Write(12, "fmt ");
            Int(16, 16); Short(20, 1); Short(22, 1); Int(24, rate); Int(28, rate * 2); Short(32, 2); Short(34, 16);
            Write(36, "data"); Int(40, mono * 2);
            for (int i = 0; i < mono; i++)
            {
                float s = 0f;
                for (int c = 0; c < channels; c++) s += data[i * channels + c];
                s = Mathf.Clamp(s / channels, -1f, 1f);
                Short(44 + i * 2, (short)(s * 32767f));
            }
            return bytes;
        }
    }
}
