using System;
using System.IO;
using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-04 创意工坊 (用户: "专辑制作改成创意工坊…高强度自定义，但页面要简单易用，一个角色占大地方其他是参数修改…
    /// 自定义声音：录音念我们给的文稿、训练成专属声线"). A full page: the chosen member stands large in the upper stage and
    /// every change shows on her at once; the lower panel has four tabs of simple controls.
    ///   造型 — AI 换装 (opens the web 造型工坊 demo for her), colour filter, brightness.
    ///   声线 — pitch and volume (applied wherever her lines play in the game), a preview line, and 我的声音: with consent,
    ///          read three scripts into the microphone; the samples stay on this device until a voice-cloning service is
    ///          connected (it needs a server holding the API key, so 生成专属声线 says so instead of uploading anything).
    ///   舞台 — background and 应援色 (the glow behind her).
    ///   动作 — idle sway, size, facing.
    /// Every change is saved per member (GameSave.WorkshopLooks1104). The lobby card keeps its scene name AlbumProduction.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        public const string DressLabUrl1104 = "https://bojuejun.github.io/cho-siren-preview/dress-lab/";

        private static readonly (string Name, Color Tint)[] WorkshopFilters1104 =
        {
            ("原色", Color.white), ("暖光", new Color(1f, .9f, .8f)), ("冷调", new Color(.84f, .93f, 1f)),
            ("霓虹", new Color(1f, .82f, 1f)), ("复古", new Color(.96f, .86f, .72f)), ("夜色", new Color(.74f, .76f, .98f)),
        };
        private static readonly (string Name, string Path)[] WorkshopBackgrounds1104 =
        {
            ("创作室", "Art/Workshop1104/bg-studio"), ("霓虹街", "Art/Workshop1104/bg-street"),
            ("巨蛋", "Art/Workshop1104/bg-dome"), ("大厅", "Art/LobbyBackground"),
        };
        private static readonly (string Name, Color Glow)[] WorkshopAccents1104 =
        {
            ("青", new Color32(28, 242, 236, 255)), ("粉", new Color32(255, 95, 210, 255)), ("金", new Color32(255, 214, 107, 255)),
            ("紫", new Color32(156, 104, 255, 255)), ("红", new Color32(255, 80, 96, 255)), ("白", new Color32(240, 236, 255, 255)),
        };
        private static readonly string[] WorkshopTabs1104 = { "造型", "声线", "舞台", "动作" };
        private static readonly string[] VoiceScripts1104 =
        {
            "大家好，我是今天的主唱，请多关照！",
            "下一首歌，送给一直支持我们的你。",
            "三、二、一——一起喊出来吧！",
        };

        private GameObject workshopRoot1104;
        private RectTransform workshopPanel1104;
        private Image workshopFigure1104, workshopBackground1104, workshopGlow1104;
        private int workshopMember1104 = -1, workshopTab1104, workshopScript1104;
        private WorkshopLook1104 workshopLook1104;

        private void OpenWorkshop1104()
        {
            CloseModal();
            CloseWorkshop1104();
            if (workshopMember1104 < 0 || !model.IsUnlocked(workshopMember1104))
                workshopMember1104 = model.Save.Team.Count > 0 ? model.Save.Team[0] : 0;
            MemberDefinition member = GameModel.Members[workshopMember1104];
            workshopLook1104 = model.WorkshopLook1104(member.Id);

            workshopRoot1104 = NewImage("WorkshopPage1104", safeRoot, null, new Color32(8, 4, 16, 255));
            var root = (RectTransform)workshopRoot1104.transform;
            Stretch(root);
            workshopRoot1104.GetComponent<Image>().raycastTarget = true;
            float width = root.rect.width > 0 ? root.rect.width : 720f;

            // Stage: background, accent glow, the member herself.
            workshopBackground1104 = NewImage("WorkshopBackground", root, null, Color.white).GetComponent<Image>();
            Stretch(workshopBackground1104.rectTransform);
            workshopBackground1104.preserveAspect = false;
            workshopBackground1104.raycastTarget = false;
            RectTransform stage = VisualA1101.Rect("WorkshopStage", root);
            stage.anchorMin = new Vector2(0f, .4f); stage.anchorMax = Vector2.one;
            stage.offsetMin = new Vector2(0f, 0f); stage.offsetMax = new Vector2(0f, -96f);
            workshopGlow1104 = VisualA1101.NewImage("WorkshopGlow", stage, VisualA1101.Glow, Color.white);
            workshopGlow1104.rectTransform.anchorMin = new Vector2(.1f, .05f); workshopGlow1104.rectTransform.anchorMax = new Vector2(.9f, .95f);
            workshopGlow1104.rectTransform.offsetMin = workshopGlow1104.rectTransform.offsetMax = Vector2.zero;
            workshopFigure1104 = NewImage("WorkshopFigure", stage, Resources.Load<Sprite>(member.ResourcePath), Color.white).GetComponent<Image>();
            workshopFigure1104.preserveAspect = true;
            workshopFigure1104.raycastTarget = false;
            var figureRect = workshopFigure1104.rectTransform;
            figureRect.anchorMin = new Vector2(.02f, 0f); figureRect.anchorMax = new Vector2(.98f, 1f);
            figureRect.offsetMin = figureRect.offsetMax = Vector2.zero;
            figureRect.pivot = new Vector2(.5f, 0f);
            workshopFigure1104.gameObject.AddComponent<WorkshopFigureMotion1104>().Bind(() => workshopLook1104);

            // Header: back, title, member switch.
            GameObject back = ModalPolish038Chrome.ActionButton(root, "WorkshopBack", "返回", 18, CloseWorkshop1104,
                ModalPolish038ButtonStyle.Secondary);
            PlaceTop(back.GetComponent<RectTransform>(), 16f, 18f, 110f, 54f);
            Text title = VisualA1101.Label(root, "WorkshopTitle", "创意工坊", 30, VisualA1101.Paper, TextAnchor.MiddleCenter);
            VisualA1101.Place(title.rectTransform, 140f, 16f, width - 280f, 58f);
            WorkshopArrow1104(root, "WorkshopPrev", "‹", 16f, -1);
            WorkshopArrow1104(root, "WorkshopNext", "›", width - 72f, 1);
            Text name = VisualA1101.Label(root, "WorkshopMemberName", $"{member.Name} · {member.Career}", 20,
                VisualA1101.Paper, TextAnchor.MiddleCenter);
            VisualA1101.Place(name.rectTransform, 80f, 80f, width - 160f, 30f);

            // Panel: tabs and controls.
            Image panel = NewImage("WorkshopPanel", root, RoundedSprite(18), new Color32(14, 9, 28, 240)).GetComponent<Image>();
            panel.type = Image.Type.Sliced;
            workshopPanel1104 = panel.rectTransform;
            workshopPanel1104.anchorMin = Vector2.zero; workshopPanel1104.anchorMax = new Vector2(1f, .4f);
            workshopPanel1104.offsetMin = new Vector2(10f, 10f); workshopPanel1104.offsetMax = new Vector2(-10f, 0f);
            ApplyWorkshopLook1104();
            ShowWorkshopTab1104(workshopTab1104);
        }

        private void CloseWorkshop1104()
        {
            if (workshopRoot1104 != null) Destroy(workshopRoot1104);
            workshopRoot1104 = null;
            WorkshopRecorder1104.Cancel();
        }

        private void WorkshopArrow1104(Transform parent, string objectName, string glyph, float x, int step)
        {
            GameObject arrow = ModalPolish038Chrome.ActionButton(parent, objectName, glyph, 30, () =>
            {
                int count = GameModel.Members.Length, next = workshopMember1104;
                for (int i = 0; i < count; i++)
                {
                    next = (next + step + count) % count;
                    if (model.IsUnlocked(next)) break;
                }
                workshopMember1104 = next;
                VisualA1101.Click();
                OpenWorkshop1104();
            }, ModalPolish038ButtonStyle.Secondary);
            PlaceTop(arrow.GetComponent<RectTransform>(), x, 76f, 56f, 40f);
        }

        private void ApplyWorkshopLook1104()
        {
            if (workshopFigure1104 == null || workshopLook1104 == null) return;
            WorkshopLook1104 look = workshopLook1104;
            Color tint = WorkshopFilters1104[Mathf.Clamp(look.Filter, 0, WorkshopFilters1104.Length - 1)].Tint;
            float bright = look.Brightness / 100f;
            workshopFigure1104.color = new Color(Mathf.Min(1f, tint.r * bright), Mathf.Min(1f, tint.g * bright), Mathf.Min(1f, tint.b * bright), 1f);
            var bg = WorkshopBackgrounds1104[Mathf.Clamp(look.Background, 0, WorkshopBackgrounds1104.Length - 1)];
            workshopBackground1104.sprite = Resources.Load<Sprite>(bg.Path) ?? Resources.Load<Sprite>("Art/LobbyBackground");
            workshopBackground1104.color = new Color(.7f, .7f, .7f, 1f);
            Color glow = WorkshopAccents1104[Mathf.Clamp(look.Accent, 0, WorkshopAccents1104.Length - 1)].Glow;
            workshopGlow1104.color = new Color(glow.r, glow.g, glow.b, .45f);
        }

        private void SaveWorkshopLook1104()
        {
            model.SaveWorkshopLook1104(workshopLook1104);
            ApplyWorkshopLook1104();
        }

        // ------------------------------------------------------------------ panel

        private void ShowWorkshopTab1104(int tab)
        {
            workshopTab1104 = tab;
            for (int i = workshopPanel1104.childCount - 1; i >= 0; i--) Destroy(workshopPanel1104.GetChild(i).gameObject);
            float width = workshopPanel1104.rect.width > 0 ? workshopPanel1104.rect.width : 700f;
            float inner = width - 40f;
            float tabW = inner / WorkshopTabs1104.Length;
            for (int i = 0; i < WorkshopTabs1104.Length; i++)
            {
                int captured = i;
                WorkshopChip1104(workshopPanel1104, "WorkshopTab-" + i, WorkshopTabs1104[i], 20f + i * tabW, 18f, tabW - 10f, 60f, 22,
                    i == tab, () => { VisualA1101.Click(); ShowWorkshopTab1104(captured); });
            }
            float y = 100f;
            switch (tab)
            {
                case 0: y = LookTab1104(inner, y); break;
                case 1: y = VoiceTab1104(inner, y); break;
                case 2: y = StageTab1104(inner, y); break;
                default: y = MotionTab1104(inner, y); break;
            }
            GameObject reset = ModalPolish038Chrome.ActionButton(workshopPanel1104, "WorkshopReset", "恢复默认", 16, () =>
            {
                bool consent = workshopLook1104.VoiceConsent; int samples = workshopLook1104.SamplesRecorded;
                workshopLook1104 = new WorkshopLook1104 { MemberId = workshopLook1104.MemberId, VoiceConsent = consent, SamplesRecorded = samples };
                SaveWorkshopLook1104();
                ShowWorkshopTab1104(workshopTab1104);
                Toast("已恢复默认");
            }, ModalPolish038ButtonStyle.Secondary);
            reset.GetComponent<RectTransform>().anchorMin = reset.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
            reset.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            reset.GetComponent<RectTransform>().anchoredPosition = new Vector2(-20f, 16f);
            reset.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 46f);
        }

        private float LookTab1104(float inner, float y)
        {
            GameObject dress = ModalPolish038Chrome.ActionButton(workshopPanel1104, "WorkshopDress", "AI 换装 · 说一句话换衣服", 20, () =>
            {
                MemberDefinition member = GameModel.Members[workshopMember1104];
                Application.OpenURL(DressLabUrl1104 + "?member=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(member.Id));
                Toast($"已打开 AI 换装：{member.Name}（只换衣服，不换脸）");
            }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(dress.GetComponent<RectTransform>(), 20f, y, inner, 72f);
            y += 92f;
            y = WorkshopChips1104("滤镜", WorkshopFilters1104.Length, i => WorkshopFilters1104[i].Name, workshopLook1104.Filter,
                v => workshopLook1104.Filter = v, inner, y);
            y = WorkshopSlider1104("亮度", 60, 130, workshopLook1104.Brightness, v => workshopLook1104.Brightness = v, inner, y, "%");
            return y;
        }

        private float VoiceTab1104(float inner, float y)
        {
            y = WorkshopSlider1104("音高", -30, 30, workshopLook1104.Pitch, v => workshopLook1104.Pitch = v, inner, y, string.Empty);
            y = WorkshopSlider1104("音量", 30, 100, workshopLook1104.Volume, v => workshopLook1104.Volume = v, inner, y, "%");
            MemberDefinition member = GameModel.Members[workshopMember1104];
            string clip = DialogueVoice1102.FirstLineOf(member.Id, out string line);
            GameObject listen = ModalPolish038Chrome.ActionButton(workshopPanel1104, "WorkshopListen", clip != null ? "试听她的声音" : "她还没有录好的台词", 17, () =>
            {
                if (clip == null || gameAudio == null) { Toast("这位成员还没有录好的台词"); return; }
                gameAudio.PlayVoice(clip, "workshop");
                if (!string.IsNullOrEmpty(line)) Toast(line);
            }, ModalPolish038ButtonStyle.Secondary);
            PlaceTop(listen.GetComponent<RectTransform>(), 20f, y, inner * .5f - 6f, 64f);
            GameObject mine = ModalPolish038Chrome.ActionButton(workshopPanel1104, "WorkshopMyVoice", "录制我的声音", 17,
                OpenVoiceRecorder1104, ModalPolish038ButtonStyle.Primary);
            PlaceTop(mine.GetComponent<RectTransform>(), 20f + inner * .5f + 6f, y, inner * .5f - 6f, 64f);
            y += 80f;
            Text note = VisualA1101.Label(workshopPanel1104, "WorkshopVoiceNote",
                $"已录 {workshopLook1104.SamplesRecorded}/3 段 · 录满 3 段可生成专属声线", 16, VisualA1101.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(note.rectTransform, 20f, y, inner, 24f);
            return y + 30f;
        }

        private float StageTab1104(float inner, float y)
        {
            y = WorkshopChips1104("背景", WorkshopBackgrounds1104.Length, i => WorkshopBackgrounds1104[i].Name, workshopLook1104.Background,
                v => workshopLook1104.Background = v, inner, y);
            y = WorkshopChips1104("应援色", WorkshopAccents1104.Length, i => WorkshopAccents1104[i].Name, workshopLook1104.Accent,
                v => workshopLook1104.Accent = v, inner, y);
            return y;
        }

        private float MotionTab1104(float inner, float y)
        {
            y = WorkshopSlider1104("律动", 0, 100, workshopLook1104.Bob, v => workshopLook1104.Bob = v, inner, y, string.Empty);
            y = WorkshopSlider1104("大小", 85, 115, workshopLook1104.Scale, v => workshopLook1104.Scale = v, inner, y, "%");
            y = WorkshopChips1104("朝向", 2, i => i == 0 ? "正向" : "镜像", workshopLook1104.Mirror ? 1 : 0,
                v => workshopLook1104.Mirror = v == 1, inner, y);
            return y;
        }

        // ------------------------------------------------------------------ controls

        private float WorkshopChips1104(string label, int count, Func<int, string> names, int selected, Action<int> pick, float inner, float y)
        {
            Text tag = VisualA1101.Label(workshopPanel1104, "Label-" + label, label, 19, VisualA1101.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(tag.rectTransform, 20f, y, 84f, 56f);
            float area = inner - 84f, chipW = area / count;
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                WorkshopChip1104(workshopPanel1104, $"Chip-{label}-{i}", names(i), 104f + i * chipW, y, chipW - 8f, 56f, 18,
                    i == selected, () =>
                    {
                        VisualA1101.Click();
                        pick(captured);
                        SaveWorkshopLook1104();
                        ShowWorkshopTab1104(workshopTab1104);
                    });
            }
            return y + 74f;
        }

        /// <summary>Solid teal only when selected (the game-wide selected-state rule); otherwise a dark face.</summary>
        private void WorkshopChip1104(Transform parent, string objectName, string text, float x, float y, float w, float h, int size,
            bool selected, Action click)
        {
            Image face = NewImage(objectName, parent, RoundedSprite(12), selected ? (Color)Brand1025.Teal : new Color32(30, 22, 52, 255)).GetComponent<Image>();
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;
            PlaceTop(face.rectTransform, x, y, w, h);
            Text caption = VisualA1101.Label(face.transform, "Text", text, size, selected ? (Color)Brand1025.Ink : VisualA1101.Paper, TextAnchor.MiddleCenter);
            VisualA1101.Stretch(caption.rectTransform);
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => click());
            face.gameObject.AddComponent<VisualA1101Motion>().Press();
        }

        private float WorkshopSlider1104(string label, int min, int max, int value, Action<int> set, float inner, float y, string unit)
        {
            Text tag = VisualA1101.Label(workshopPanel1104, "Label-" + label, label, 19, VisualA1101.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(tag.rectTransform, 20f, y, 84f, 56f);
            Text number = VisualA1101.Number(workshopPanel1104, "Value-" + label, value + unit, 19, VisualA1101.Teal, TextAnchor.MiddleRight);
            VisualA1101.Place(number.rectTransform, inner - 60f, y, 80f, 56f);
            var sliderGo = new GameObject("Slider-" + label, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(workshopPanel1104, false);
            PlaceTop((RectTransform)sliderGo.transform, 104f, y + 16f, inner - 184f, 24f);
            Image track = NewImage("Track", sliderGo.transform, RoundedSprite(8), new Color32(40, 28, 70, 255)).GetComponent<Image>();
            Stretch(track.rectTransform);
            RectTransform fillArea = SliderArea038("FillArea", sliderGo.transform);
            Image fill = NewImage("Fill", fillArea, RoundedSprite(8), Brand1025.Teal).GetComponent<Image>();
            Stretch(fill.rectTransform);
            RectTransform handleArea = SliderArea038("HandleArea", sliderGo.transform);
            Image handle = NewImage("Handle", handleArea, DotSprite(), White).GetComponent<Image>();
            handle.rectTransform.sizeDelta = new Vector2(42f, 42f);
            handle.raycastTarget = true;
            Slider slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle; slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true; slider.value = value;
            slider.onValueChanged.AddListener(v =>
            {
                number.text = (int)v + unit;
                set((int)v);
                ApplyWorkshopLook1104();
            });
            var trigger = sliderGo.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var up = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            up.callback.AddListener(_ => model.SaveWorkshopLook1104(workshopLook1104));
            trigger.triggers.Add(up);
            return y + 74f;
        }

        // ------------------------------------------------------------------ 我的声音

        private void OpenVoiceRecorder1104()
        {
            MemberDefinition member = GameModel.Members[workshopMember1104];
            RectTransform body = ProgressionModal("VoiceRecorderModal", "录制我的声音", null, 620f);
            var frame = new ModalPolish038Frame
            {
                Overlay = modalObject, Body = body, Panel = (RectTransform)body.parent,
                Width = 620f, Height = 620f / ModalPolish038Chrome.PanelAspect,
            };
            float width = BodyWidth1101(frame), y = 0f;
            Text intro = VisualA1101.Label(body, "RecorderIntro", $"读下面 3 段文稿，用你的声音为「{member.Name}」生成专属声线", 15,
                VisualA1101.Muted, TextAnchor.MiddleLeft);
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            VisualA1101.Place(intro.rectTransform, 0f, y, width, 44f);
            y += 52f;

            bool consent = workshopLook1104.VoiceConsent;
            WorkshopChip1104(body, "RecorderConsent", consent ? "✓ 我确认是本人的声音，同意用于生成声线" : "□ 我确认是本人的声音，同意用于生成声线",
                0f, y, width, 48f, 15, consent, () =>
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
            bool has = WorkshopRecorder1104.HasSample(member.Id, index);
            float third = (width - 24f) / 3f;
            GameObject rec = ModalPolish038Chrome.ActionButton(body, "RecorderRecord", recording ? "■ 停止" : "● 录音", 18, () =>
            {
                if (!supported) { Toast("网页版暂不支持录音，请在 App 里录制"); return; }
                if (!workshopLook1104.VoiceConsent) { Toast("请先确认是本人的声音"); return; }
                if (WorkshopRecorder1104.Recording)
                {
                    if (WorkshopRecorder1104.Stop(member.Id, index, out string error))
                    {
                        workshopLook1104.SamplesRecorded = WorkshopRecorder1104.CountSamples(member.Id, VoiceScripts1104.Length);
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
                if (!WorkshopRecorder1104.Play(member.Id, index, this)) Toast("这一段还没有录音");
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

            int done = WorkshopRecorder1104.CountSamples(member.Id, VoiceScripts1104.Length);
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
    }

    /// <summary>The workshop figure's idle sway, size and facing, read live from the look being edited.</summary>
    public sealed class WorkshopFigureMotion1104 : MonoBehaviour
    {
        private Func<WorkshopLook1104> look;
        public void Bind(Func<WorkshopLook1104> source) => look = source;

        private void LateUpdate()
        {
            WorkshopLook1104 l = look?.Invoke();
            if (l == null) return;
            float t = Time.unscaledTime, amount = l.Bob / 100f;
            float scale = l.Scale / 100f * (1f + .012f * amount * Mathf.Sin(t * 2.2f));
            transform.localScale = new Vector3(l.Mirror ? -scale : scale, scale, 1f);
            ((RectTransform)transform).anchoredPosition = new Vector2(0f, 10f * amount * Mathf.Sin(t * 1.6f));
            transform.localRotation = Quaternion.Euler(0f, 0f, 1.4f * amount * Mathf.Sin(t * 1.1f));
        }
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
