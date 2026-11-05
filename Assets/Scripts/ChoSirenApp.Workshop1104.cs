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
    /// 2026-11-04 创意工坊, photo-editor layout (用户: "简单易用…角色上半身…参考开始演出的光碟滑动选择参数，没选的时候收起来，
    /// 滑动时伸出来…参考手机 P 图软件…调节部位要有丝滑的镜头特写…想看身后"). The member's upper body fills the page; a thin
    /// top bar (返回 · 创意工坊 · 成员名 · 保存) and one row of round tools sit over her. Picking a tool eases the camera to
    /// the part it changes (face for 滤镜/亮度/音高/我的声音, bust for 换装/应援色, full figure for 背景/律动/转身) and raises the
    /// vinyl dial from the bottom edge; turning it sets the value shown in a bubble, and it sinks back when left alone. 转身
    /// turns her through the AI-drawn angle sheets (Art/Workshop1104/Turn/&lt;member&gt;-q45/side/back, right side mirrored).
    /// Every change is saved per member (GameSave.WorkshopLooks1104). The lobby card keeps its scene name AlbumProduction.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        public const string DressLabUrl1104 = "https://bojuejun.github.io/cho-siren-preview/dress-lab/";

        internal static readonly (string Name, Color Tint)[] WorkshopFilters1104 =
        {
            ("原色", Color.white), ("暖光", new Color(1f, .9f, .8f)), ("冷调", new Color(.84f, .93f, 1f)),
            ("霓虹", new Color(1f, .82f, 1f)), ("复古", new Color(.96f, .86f, .72f)), ("夜色", new Color(.74f, .76f, .98f)),
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
            ("dress", "sparkle", "AI换装", 1), ("filter", "star", "滤镜", 0), ("bright", "bolt", "亮度", 0),
            ("pitch", "mic", "音高", 0), ("voice", "music", "我的声音", 0), ("bg", "flag", "背景", 2),
            ("accent", "heart", "应援色", 1), ("bob", "refresh", "律动", 2), ("turn", "exchange", "转身", 2),
        };

        private GameObject workshopRoot1104;
        private WorkshopStage1104 workshopStage1104;
        /// <summary>-1 = the 初始角色 (the workshop's own starter idol, 2026-11-04 user loop); otherwise a member index.</summary>
        private int workshopMember1104 = -1, workshopScript1104;
        public const string StarterId1104 = "starter";
        public const string StarterName1104 = "初始角色";
        private WorkshopLook1104 workshopLook1104;
        private string workshopTool1104 = "filter";

        private void OpenWorkshop1104()
        {
            CloseModal();
            CloseWorkshop1104();
            if (workshopMember1104 >= 0 && !model.IsUnlocked(workshopMember1104)) workshopMember1104 = -1;
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

            // Thin top bar.
            Image bar = NewImage("WorkshopTopBar", root, null, new Color32(10, 6, 20, 170)).GetComponent<Image>();
            bar.rectTransform.anchorMin = new Vector2(0f, 1f); bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.pivot = new Vector2(.5f, 1f);
            bar.rectTransform.offsetMin = new Vector2(0f, -74f); bar.rectTransform.offsetMax = Vector2.zero;
            WorkshopTextButton1104(bar.transform, "WorkshopBack", "‹ 返回", 20, new Vector2(0f, .5f), new Vector2(12f, 0f), CloseWorkshop1104);
            Text title = VisualA1101.Label(bar.transform, "WorkshopTitle", $"创意工坊 · {subjectName}", 22, VisualA1101.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(190f, 0f); title.rectTransform.offsetMax = new Vector2(-190f, 0f);
            WorkshopTextButton1104(bar.transform, "WorkshopPrev", "‹", 30, new Vector2(.5f, .5f), new Vector2(-180f, 0f), () => StepWorkshopMember1104(-1));
            WorkshopTextButton1104(bar.transform, "WorkshopNext", "›", 30, new Vector2(.5f, .5f), new Vector2(180f, 0f), () => StepWorkshopMember1104(1));
            WorkshopTextButton1104(bar.transform, "WorkshopSave", "保存", 20, new Vector2(1f, .5f), new Vector2(-12f, 0f), () =>
            {
                model.SaveWorkshopLook1104(workshopLook1104);
                VisualA1101.Success();
                Toast($"已保存「{subjectName}」的造型");
            });

            BuildWorkshopTools1104(root);
            OpenWorkshopTool1104(workshopTool1104, false);
        }

        private void CloseWorkshop1104()
        {
            if (workshopRoot1104 != null) Destroy(workshopRoot1104);
            workshopRoot1104 = null;
            workshopStage1104 = null;
            WorkshopRecorder1104.Cancel();
        }

        private string WorkshopSubjectId1104 => workshopMember1104 < 0 ? StarterId1104 : GameModel.Members[workshopMember1104].Id;
        private string WorkshopSubjectName1104 => workshopMember1104 < 0 ? StarterName1104 : GameModel.Members[workshopMember1104].Name;

        /// <summary>Cycles 初始角色 → every unlocked member → 初始角色.</summary>
        private void StepWorkshopMember1104(int step)
        {
            int count = GameModel.Members.Length + 1, slot = workshopMember1104 + 1;
            for (int i = 0; i < count; i++)
            {
                slot = (slot + step + count) % count;
                if (slot == 0 || model.IsUnlocked(slot - 1)) break;
            }
            workshopMember1104 = slot - 1;
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

        private void BuildWorkshopTools1104(RectTransform root)
        {
            // A soft dark gradient keeps the white tool captions readable over any background.
            Image shade = NewImage("WorkshopToolShade", root, VisualA1101.Glow, new Color(0f, 0f, 0f, .55f)).GetComponent<Image>();
            shade.raycastTarget = false;
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1f, 0f);
            shade.rectTransform.pivot = new Vector2(.5f, 0f);
            shade.rectTransform.offsetMin = new Vector2(-200f, -60f); shade.rectTransform.offsetMax = new Vector2(200f, 260f);

            var row = new GameObject("WorkshopTools", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            row.transform.SetParent(root, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.offsetMin = new Vector2(0f, 64f); rect.offsetMax = new Vector2(0f, 64f + 112f);
            row.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(row.transform, false);
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, .5f);
            const float cell = 92f;
            content.sizeDelta = new Vector2(16f + cell * WorkshopTools1104.Length, 0f);
            var scroll = row.GetComponent<ScrollRect>();
            scroll.content = content; scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < WorkshopTools1104.Length; i++)
            {
                var tool = WorkshopTools1104[i];
                var go = new GameObject("WorkshopTool-" + tool.Id, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(content, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, .5f); r.pivot = new Vector2(0f, .5f);
                r.anchoredPosition = new Vector2(10f + i * cell, 0f); r.sizeDelta = new Vector2(cell - 6f, 108f);
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
                Image disc = NewImage("Disc", go.transform, DotSprite(), new Color32(18, 12, 34, 220)).GetComponent<Image>();
                PlaceTop(disc.rectTransform, (cell - 6f - 64f) * .5f, 4f, 64f, 64f);
                Image ring = VisualA1101.NewImage("Ring", disc.transform, VisualA1101.RingSprite, new Color(1f, 1f, 1f, .4f));
                VisualA1101.Stretch(ring.rectTransform);
                RectTransform glyph = VisualA1101.NeonIcon(disc.transform, "Icon", tool.Icon, VisualA1101.Paper, 32f, 0f);
                VisualA1101.Center(glyph, 32f);
                Text caption = VisualA1101.Label(go.transform, "Caption", tool.Name, 15, VisualA1101.Paper, TextAnchor.MiddleCenter);
                PlaceTop(caption.rectTransform, 0f, 72f, cell - 6f, 26f);
                Button button = go.GetComponent<Button>(); button.transition = Selectable.Transition.None;
                string id = tool.Id;
                button.onClick.AddListener(() => { VisualA1101.Click(); OpenWorkshopTool1104(id); });
                go.AddComponent<VisualA1101Motion>().Press();
            }
            Text group = VisualA1101.Label(root, "WorkshopGroups", "造型 · 声线 · 舞台 · 动作", 14, VisualA1101.Muted, TextAnchor.MiddleCenter);
            group.rectTransform.anchorMin = Vector2.zero; group.rectTransform.anchorMax = new Vector2(1f, 0f);
            group.rectTransform.pivot = new Vector2(.5f, 0f);
            group.rectTransform.offsetMin = new Vector2(0f, 30f); group.rectTransform.offsetMax = new Vector2(0f, 60f);
        }

        /// <summary>Selects a tool: the selected one wears the solid teal disc (selected-only rule), the camera moves to
        /// its part, and value tools raise the dial.</summary>
        private void OpenWorkshopTool1104(string id) => OpenWorkshopTool1104(id, true);

        private void OpenWorkshopTool1104(string id, bool raise)
        {
            if (workshopRoot1104 == null) return;
            workshopTool1104 = id;
            foreach (var tool in WorkshopTools1104)
            {
                Transform t = workshopRoot1104.transform.Find("WorkshopTools/Content/WorkshopTool-" + tool.Id);
                if (t == null) continue;
                bool on = tool.Id == id;
                t.Find("Disc").GetComponent<Image>().color = on ? (Color)Brand1025.Teal : new Color32(18, 12, 34, 220);
                Image glyph = VisualA1101.Glyph((RectTransform)t.Find("Disc/Icon"));
                if (glyph != null) glyph.color = on ? (Color)Brand1025.Ink : VisualA1101.Paper;
                if (on) workshopStage1104.Shot = tool.Shot;
            }
            WorkshopLook1104 look = workshopLook1104;
            string subjectId = WorkshopSubjectId1104, subjectName = WorkshopSubjectName1104;
            workshopStage1104.TurnMode = id == "turn";
            switch (id)
            {
                case "dress":
                    workshopStage1104.Dial(null);
                    if (raise)
                    {
                        Application.OpenURL(DressLabUrl1104 + "?member=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(subjectId));
                        Toast($"已打开 AI 换装：{subjectName}（只换衣服，不换脸）");
                    }
                    break;
                case "filter":
                    workshopStage1104.Dial(new WorkshopDial1104("滤镜", WorkshopFilters1104.Length, () => look.Filter, v => look.Filter = v,
                        v => WorkshopFilters1104[v].Name), raise);
                    break;
                case "bright":
                    workshopStage1104.Dial(new WorkshopDial1104("亮度", 60, 130, 5, () => look.Brightness, v => look.Brightness = v, v => v + "%"), raise);
                    break;
                case "pitch":
                    workshopStage1104.Dial(new WorkshopDial1104("音高", -30, 30, 2, () => look.Pitch, v => look.Pitch = v,
                        v => v > 0 ? "+" + v : v.ToString()) { Released = PlayWorkshopVoice1104 }, raise);
                    break;
                case "voice":
                    workshopStage1104.Dial(null);
                    if (raise) OpenVoiceRecorder1104();
                    break;
                case "bg":
                    workshopStage1104.Dial(new WorkshopDial1104("背景", WorkshopBackgrounds1104.Length, () => look.Background, v => look.Background = v,
                        v => WorkshopBackgrounds1104[v].Name), raise);
                    break;
                case "accent":
                    workshopStage1104.Dial(new WorkshopDial1104("应援色", WorkshopAccents1104.Length, () => look.Accent, v => look.Accent = v,
                        v => WorkshopAccents1104[v].Name) { Swatch = v => WorkshopAccents1104[v].Glow }, raise);
                    break;
                case "bob":
                    workshopStage1104.Dial(new WorkshopDial1104("律动", 0, 100, 5, () => look.Bob, v => look.Bob = v, v => v.ToString()), raise);
                    break;
                case "turn":
                    workshopStage1104.Dial(null);
                    if (raise) Toast(workshopStage1104.HasTurnViews ? "左右拖动她转身" : $"{subjectName}的多角度立绘还在制作中");
                    break;
            }
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
    /// the camera that eases between face / bust / full shots, and the value dial — the same half record as 开始演出's
    /// stage picker (VinylStageWheel1102): it slides in from the right edge while a value tool is in use, the values sit
    /// on its rim as the stage map's torn-sticker nodes (teal rim = selected), and it slides back to a sliver when left alone.
    /// </summary>
    public sealed class WorkshopStage1104 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float WheelSize = 1124f, NodeSize = 112f, IdleSeconds = 2f, NodeRadius = 348f;
        private RectTransform root, figureRect, wheelRect;
        private Image figure, background, glow;
        private Func<WorkshopLook1104> look;
        private Sprite front, dot;
        private Sprite[] turn; // 0 front, 1 left 45, 2 left 90, 3 back, 4 right 90 (mirrored), 5 right 45 (mirrored)
        private static readonly bool[] Mirrored = { false, false, false, false, true, true };
        private Rect face;
        private WorkshopDial1104 dial;
        private VinylStageWheel1102 wheel;
        private Image[] nodeGlows;
        private Text dialTitle;
        private float zoom = -1f, panY, slide, turnPos, turnVel, lastTouch = -10f;
        private bool turning;
        public int Shot = 1;
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
        private Material videoMaterial;

        /// <param name="idleVideo">Optional stacked-alpha loop under StreamingAssets (left colour, right alpha), the same
        /// frame as <paramref name="still"/>; the still shows until the first frame and whenever she is turned.</param>
        public void Build(RectTransform page, string subjectId, Sprite still, Rect faceRect, string idleVideo,
            Func<WorkshopLook1104> source, Sprite rounded, Sprite dotSprite)
        {
            root = page; look = source; dot = dotSprite;
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

            glow = MakeImage("WorkshopGlow", page, VisualA1101.Glow, Color.white);
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(.5f, .5f);
            figure = MakeImage("WorkshopFigure", page, front, Color.white);
            figureRect = figure.rectTransform;
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
            wheel = null; nodeGlows = null;
            lastTouch = show && value != null ? Time.unscaledTime : -10f;
            dialTitle.text = value != null ? value.Name : string.Empty;
            if (value == null) return;

            Image disc = MakeImage("Disc", wheelRect, Resources.Load<Sprite>("Art/LevelMap1102/vinyl"), Color.white);
            disc.preserveAspect = true;
            var dr = disc.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(.5f, .5f); dr.sizeDelta = new Vector2(WheelSize, WheelSize);
            int count = value.Steps + 1;
            var nodes = new RectTransform[count];
            var buttons = new Button[count];
            nodeGlows = new Image[count];
            for (int i = 0; i < count; i++)
            {
                int v = value.Min + i * value.Step, stage = i + 1;
                var go = new GameObject("DialNode-" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(wheelRect, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f); r.sizeDelta = new Vector2(NodeSize, NodeSize);
                Image paper = go.GetComponent<Image>();
                paper.sprite = VisualA1101.TornSprite; paper.color = new Color32(190, 165, 222, 255);
                Outline rim = go.AddComponent<Outline>();
                rim.effectColor = new Color32(137, 115, 177, 230); rim.effectDistance = new Vector2(1.5f, -1.5f);
                Image selected = MakeImage("SelectedRim", go.transform, VisualA1101.TornSprite, Brand1025.Teal);
                Place(selected.rectTransform, -6f, -6f, NodeSize + 12f, NodeSize + 12f);
                selected.transform.SetAsFirstSibling();
                nodeGlows[i] = selected;
                Color? swatch = value.Swatch?.Invoke(v);
                Image core = MakeImage("Core", go.transform, dot, swatch ?? new Color32(14, 13, 29, 255));
                Place(core.rectTransform, 8f, 8f, NodeSize - 16f, NodeSize - 16f);
                Image nick = MakeImage("PaperNick", go.transform, PunkUi.SlantFill, new Color32(238, 224, 255, 240));
                Place(nick.rectTransform, NodeSize - 34f, 8f, 26f, 6f);
                nick.rectTransform.localEulerAngles = new Vector3(0f, 0f, -28f);
                if (swatch == null)
                {
                    string text = value.Format(v);
                    Text label = VisualA1101.Label(go.transform, "Label", text, text.Length > 3 ? 21 : 26, VisualA1101.Paper, TextAnchor.MiddleCenter);
                    label.fontStyle = FontStyle.Bold;
                    Place(label.rectTransform, 6f, 0f, NodeSize - 12f, NodeSize);
                }
                Button button = go.GetComponent<Button>();
                button.transition = Selectable.Transition.None; button.targetGraphic = paper;
                button.onClick.AddListener(() => Select(stage));
                nodes[i] = r; buttons[i] = button;
            }
            wheel = wheelRect.gameObject.AddComponent<VinylStageWheel1102>();
            int current = Mathf.Clamp((value.Get() - value.Min) / value.Step, 0, count - 1) + 1;
            wheel.Configure(dr, nodes, buttons, current, Select, s => s >= 1 && s <= count);
            Highlight(current);
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
            for (int i = 0; i < nodeGlows.Length; i++) nodeGlows[i].enabled = i == stage - 1;
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
            var bg = ChoSirenApp.WorkshopBackgrounds1104[Mathf.Clamp(l.Background, 0, ChoSirenApp.WorkshopBackgrounds1104.Length - 1)];
            background.sprite = Resources.Load<Sprite>(bg.Path) ?? Resources.Load<Sprite>("Art/LobbyBackground");
            Color accent = ChoSirenApp.WorkshopAccents1104[Mathf.Clamp(l.Accent, 0, ChoSirenApp.WorkshopAccents1104.Length - 1)].Glow;
            glow.color = new Color(accent.r, accent.g, accent.b, .45f);
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
                bool showVideo = ready && view == 0;
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
            float top = page.y * .5f - 84f, bottom = -page.y * .5f + bottomBand, stageH = top - bottom;
            float faceH = face.height * art.y, faceCy = face.center.y * art.y;
            float shotH = Shot == 0 ? faceH * 4.2f : Shot == 1 ? faceH * 6.6f : art.y * 1.02f;
            float focus = Shot == 2 ? art.y * .5f : Shot == 1 ? faceCy - faceH * 1.9f : faceCy - faceH * .55f;
            float targetZoom = stageH / shotH * (l != null ? Mathf.Clamp(l.Scale, 85, 115) / 100f : 1f);
            float ease = 1f - Mathf.Exp(-dt * 6.5f);
            if (zoom < 0f) zoom = targetZoom;
            zoom = Mathf.Lerp(zoom, targetZoom, ease);
            float drawH = art.y * zoom, drawW = art.x * zoom;
            float targetPan = (bottom + stageH * .5f) - (focus - art.y * .5f) * zoom;
            panY = Mathf.Lerp(panY, targetPan, ease);
            float shiftX = -page.x * .17f * r;
            float t = Time.unscaledTime, bob = l != null ? l.Bob / 100f : .5f;
            figureRect.sizeDelta = new Vector2(drawW, drawH);
            figureRect.anchoredPosition = new Vector2(shiftX, panY + 5f * bob * Mathf.Sin(t * 1.6f));
            figureRect.localScale = new Vector3(flip * (l != null && l.Mirror ? -1f : 1f) * (1f + .008f * bob * Mathf.Sin(t * 2.2f)), 1f, 1f);
            figureRect.localRotation = Quaternion.Euler(0f, 0f, 1f * bob * Mathf.Sin(t * 1.1f));
            glow.rectTransform.sizeDelta = new Vector2(Mathf.Min(drawW, page.x * 1.6f), Mathf.Min(drawH * .8f, page.y * 1.4f));
            glow.rectTransform.anchoredPosition = new Vector2(shiftX, bottom + stageH * .5f);

            // Background pushes in a little with the camera, so a close-up reads as the camera moving.
            float closeness = Mathf.InverseLerp(stageH / (art.y * 1.02f), stageH / (faceH * 4.2f), zoom);
            background.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, closeness);
            float dim = Mathf.Lerp(.68f, .5f, closeness);
            background.color = new Color(dim, dim, dim + .03f, 1f);
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
