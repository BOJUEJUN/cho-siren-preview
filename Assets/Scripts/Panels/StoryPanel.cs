using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ChoSiren.Systems.Story;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// Visual-novel presenter for <see cref="StoryRunner"/>: background, three portrait slots,
    /// typewriter dialogue box, centred choices, 自动 / 跳过 / 记录. Audio directives (bgm/sfx) are
    /// forwarded to <c>onAudio(command, assetId)</c>; <c>onFinished</c> fires once when the script ends.
    /// </summary>
    public sealed class StoryPanel : MonoBehaviour
    {
        private const float CharSeconds = 0.03f;
        private const float AutoBaseDelay = 1.2f;
        private const float AutoPerCharDelay = 0.045f;

        private sealed class SlotView
        {
            public string Position;
            public string CharacterId;
            public GameObject Root;
            public CanvasGroup Group;
            public Image Portrait;
            public GameObject Silhouette;
            public Text SilhouetteName;
        }

        private static readonly string[] SlotOrder = { "left", "center", "right" };

        private readonly List<SlotView> slots = new List<SlotView>();
        private readonly List<GameObject> choiceButtons = new List<GameObject>();
        private readonly List<string> history = new List<string>();

        private PanelKit kit;
        private GameModel model;
        private StoryRunner runner;
        private Action onFinished;
        private Action<string, string> onAudio;
        private Func<string, string> nameResolver;
        private Action onBack;
        private Action<string> onMessage;

        private bool closing;
        private bool finishedReported;
        private bool ended;
        private bool autoMode;
        private bool typing;
        private string fullLine = string.Empty;
        private Coroutine typeRoutine;
        private Coroutine autoRoutine;
        private Sprite backgroundFallback;

        private Image background;
        private Text backgroundId;
        private Text titleText;
        private GameObject dialogueBox;
        private GameObject nameTag;
        private Text nameText;
        private Text bodyText;
        private Text continueHint;
        private RectTransform choiceLayer;
        private GameObject autoButton;
        private GameObject skipButton;
        private GameObject historyOverlay;
        private Text historyText;
        private RectTransform historyContent;

        public static StoryPanel Open(Transform host, GameModel gameModel, StoryRunner storyRunner,
            Action finished, Action<string, string> audio = null, Func<string, string> characterName = null,
            Action back = null, Action<string> message = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (storyRunner == null) throw new ArgumentNullException(nameof(storyRunner));

            StoryPanel existing = host.GetComponentInChildren<StoryPanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = PanelKit.CreateOverlayRoot("StoryPanel", host);
            StoryPanel panel = panelObject.AddComponent<StoryPanel>();
            panel.model = gameModel;
            panel.runner = storyRunner;
            panel.onFinished = finished;
            panel.onAudio = audio;
            panel.nameResolver = characterName;
            panel.onBack = back;
            panel.onMessage = message;
            panel.Build();
            panel.Begin();
            return panel;
        }

        public bool AutoMode => autoMode;
        public bool IsTyping => typing;
        public bool Ended => ended;
        public IReadOnlyList<string> History => history;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("Story");
            backgroundFallback = kit.CreateGradientSprite("StoryBackdrop",
                new Color32(12, 10, 48, 255), new Color32(46, 18, 96, 255), new Color32(8, 20, 60, 255));

            background = kit.NewImage("Background", transform, backgroundFallback, PanelKit.White);
            PanelKit.Stretch(background.rectTransform);
            background.raycastTarget = true;
            background.preserveAspect = false;

            Image vignette = kit.NewImage("Vignette", transform, kit.RadialSprite(), new Color32(255, 78, 212, 40));
            PanelKit.PlaceTop(vignette.rectTransform, -100, 500, 920, 900);

            backgroundId = kit.NewPlacedText(transform, string.Empty, 12, new Color32(200, 190, 230, 200), 16, 78, 400, 22,
                TextAnchor.MiddleLeft);

            BuildTopBar();
            BuildSlots();
            BuildDialogue();
            BuildChoices();
            BuildHistory();
        }

        private void BuildTopBar()
        {
            Image bar = kit.NewImage("TopBar", transform, null, new Color32(5, 10, 38, 170));
            PanelKit.PlaceTop(bar.rectTransform, 0, 0, 720, 76);

            GameObject back = kit.NewButton("Back", bar.transform, "返回", 15, PanelKit.ButtonDark, PanelKit.White,
                Leave, 16);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), 18, 14, 86, 48);

            titleText = kit.NewPlacedText(bar.transform, runner.Script.Title, 15, PanelKit.White, 116, 14, 260, 48,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            autoButton = kit.NewButton("AutoPlay", bar.transform, "自动", 15, PanelKit.ButtonDark, PanelKit.White,
                ToggleAuto, 16);
            PanelKit.PlaceTop(autoButton.GetComponent<RectTransform>(), 392, 14, 92, 48);
            skipButton = kit.NewButton("Skip", bar.transform, "跳过", 15, PanelKit.ButtonDark, PanelKit.White,
                SkipToDecision, 16);
            PanelKit.PlaceTop(skipButton.GetComponent<RectTransform>(), 496, 14, 92, 48);
            GameObject historyButton = kit.NewButton("History", bar.transform, "记录", 15, PanelKit.ButtonDark,
                PanelKit.White, ShowHistory, 16);
            PanelKit.PlaceTop(historyButton.GetComponent<RectTransform>(), 600, 14, 102, 48);
        }

        private void BuildSlots()
        {
            float[] lefts = { -30f, 200f, 430f };
            for (int index = 0; index < SlotOrder.Length; index++)
            {
                RectTransform root = kit.NewRect("Slot-" + SlotOrder[index], transform);
                PanelKit.PlaceTop(root, lefts[index], 150, 320, 900);
                CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;

                Image portrait = kit.NewImage("Portrait", root, null, PanelKit.White);
                PanelKit.Stretch(portrait.rectTransform);
                portrait.preserveAspect = true;

                GameObject silhouette = kit.NewPanel("Silhouette", root, new Color32(20, 16, 58, 215), 32);
                PanelKit.PlaceTop(silhouette.GetComponent<RectTransform>(), 60, 160, 200, 640);
                kit.AddOutline(silhouette, new Color32(166, 112, 255, 120), 2);
                Image head = kit.NewImage("Head", silhouette.transform, kit.RadialSprite(), new Color32(120, 90, 200, 180));
                PanelKit.PlaceTop(head.rectTransform, 40, 50, 120, 120);
                Text silhouetteName = kit.NewPlacedText(silhouette.transform, string.Empty, 18, PanelKit.White, 10, 300,
                    180, 40, TextAnchor.MiddleCenter, FontStyle.Bold);
                kit.NewPlacedText(silhouette.transform, "立绘待补", 12, PanelKit.Muted, 10, 344, 180, 24,
                    TextAnchor.MiddleCenter);

                SlotView slot = new SlotView
                {
                    Position = SlotOrder[index],
                    Root = root.gameObject,
                    Group = group,
                    Portrait = portrait,
                    Silhouette = silhouette,
                    SilhouetteName = silhouetteName,
                };
                root.gameObject.SetActive(false);
                slots.Add(slot);
            }
        }

        private void BuildDialogue()
        {
            dialogueBox = kit.NewButton("DialogueBox", transform, string.Empty, 12, new Color32(10, 11, 42, 236),
                PanelKit.White, DialogueClicked, 28);
            PanelKit.PlaceTop(dialogueBox.GetComponent<RectTransform>(), 20, 1050, 680, 372);
            kit.AddOutline(dialogueBox, new Color32(166, 112, 255, 160), 2);
            PanelKit.LabelOf(dialogueBox).gameObject.SetActive(false);

            nameTag = kit.NewPanel("NameTag", dialogueBox.transform, PanelKit.Pink, 16);
            PanelKit.PlaceTop(nameTag.GetComponent<RectTransform>(), 28, -24, 240, 50);
            nameText = kit.NewPlacedText(nameTag.transform, string.Empty, 19, PanelKit.White, 12, 0, 216, 50,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            bodyText = kit.NewPlacedText(dialogueBox.transform, string.Empty, 21, PanelKit.White, 36, 48, 608, 268,
                TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.3f;

            continueHint = kit.NewPlacedText(dialogueBox.transform, "▼", 18, new Color32(255, 173, 226, 255), 600, 318,
                48, 36, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void BuildChoices()
        {
            choiceLayer = kit.NewRect("Choices", transform);
            PanelKit.Stretch(choiceLayer);
            choiceLayer.gameObject.SetActive(false);
        }

        private void BuildHistory()
        {
            Image overlay = kit.NewImage("HistoryPanel", transform, null, new Color32(3, 4, 23, 236));
            PanelKit.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            historyOverlay = overlay.gameObject;

            kit.NewPlacedText(overlay.transform, "已读台词", 24, PanelKit.White, 40, 40, 640, 44,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            RectTransform viewport = kit.NewRect("HistoryList", overlay.transform);
            PanelKit.PlaceTop(viewport, 40, 100, 640, 1280);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            historyContent = kit.NewRect("Content", viewport);
            historyContent.anchorMin = new Vector2(0, 1);
            historyContent.anchorMax = new Vector2(1, 1);
            historyContent.pivot = new Vector2(0.5f, 1);
            historyContent.anchoredPosition = Vector2.zero;
            historyText = kit.NewText("Text", historyContent, string.Empty, 17, PanelKit.White, FontStyle.Normal,
                TextAnchor.UpperLeft);
            historyText.lineSpacing = 1.35f;
            historyText.verticalOverflow = VerticalWrapMode.Overflow;
            PanelKit.Stretch(historyText.rectTransform, 12, 0, -12, 0);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = historyContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            GameObject close = kit.NewButton("HistoryClose", overlay.transform, "关闭", 19, PanelKit.Pink,
                PanelKit.White, HideHistory, 22);
            PanelKit.PlaceTop(close.GetComponent<RectTransform>(), 200, 1410, 320, 66);
            historyOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ flow

        private void Begin()
        {
            StoryFrame frame = runner.Current ?? runner.Start();
            Present(frame);
        }

        private void Present(StoryFrame frame)
        {
            ApplyDirectives(frame, true);
            if (frame.IsEnd)
            {
                PresentEnd();
                return;
            }

            if (frame.IsChoice)
            {
                PresentChoice(frame.Blocking);
                return;
            }

            PresentSay(frame.Blocking);
        }

        private void ApplyDirectives(StoryFrame frame, bool playSound)
        {
            for (int index = 0; index < frame.Directives.Count; index++)
            {
                StoryLine line = frame.Directives[index];
                switch (line.Command)
                {
                    case StoryCommand.Background:
                        SetBackground(line.Subject);
                        break;
                    case StoryCommand.Music:
                        onAudio?.Invoke(StoryCommand.Music, line.Subject);
                        break;
                    case StoryCommand.Sound:
                        if (playSound) onAudio?.Invoke(StoryCommand.Sound, line.Subject);
                        break;
                    case StoryCommand.Show:
                        ShowCharacter(line.Subject, line.Expression, line.Position);
                        break;
                    case StoryCommand.Hide:
                        HideCharacter(line.Subject);
                        break;
                }
            }
        }

        private void PresentSay(StoryLine line)
        {
            HideChoices();
            dialogueBox.SetActive(true);
            bool narrator = string.IsNullOrEmpty(line.Subject);
            nameTag.SetActive(!narrator);
            string speaker = narrator ? string.Empty : ResolveName(line.Subject);
            nameText.text = speaker;

            if (!narrator)
            {
                if (FindSlotByCharacter(line.Subject) == null && !string.IsNullOrEmpty(line.Position))
                    ShowCharacter(line.Subject, line.Expression, line.Position);
                else if (!string.IsNullOrEmpty(line.Expression))
                    RefreshPortrait(FindSlotByCharacter(line.Subject), line.Expression);
            }

            FocusSpeaker(narrator ? null : line.Subject);
            history.Add(narrator ? line.Text : $"{speaker}：{line.Text}");
            StartTyping(line.Text);
        }

        private void PresentChoice(StoryLine line)
        {
            StopTyping(true);
            nameTag.SetActive(false);
            bodyText.text = string.IsNullOrEmpty(line.Text) ? "请做出选择" : line.Text;
            continueHint.gameObject.SetActive(false);
            FocusSpeaker(null);
            history.Add("【选择】" + (string.IsNullOrEmpty(line.Text) ? string.Empty : line.Text));

            HideChoices();
            choiceLayer.gameObject.SetActive(true);
            int count = line.Choices.Count;
            const float height = 68f;
            const float gap = 16f;
            float total = count * height + (count - 1) * gap;
            float top = 600f - total * 0.5f;
            for (int index = 0; index < count; index++)
            {
                int captured = index;
                GameObject button = kit.NewButton("Choice-" + index, choiceLayer, line.Choices[index].Text, 18,
                    new Color32(46, 27, 96, 248), PanelKit.White, () => Choose(captured), 22);
                PanelKit.PlaceTop(button.GetComponent<RectTransform>(), 100, top + index * (height + gap), 520, height);
                kit.AddOutline(button, new Color32(255, 126, 226, 200), 2);
                choiceButtons.Add(button);
            }
        }

        private void PresentEnd()
        {
            StopTyping(true);
            HideChoices();
            ended = true;
            nameTag.SetActive(false);
            bodyText.text = "—— 本段剧情结束 ——\n\n点击对话框返回";
            continueHint.gameObject.SetActive(true);
            FocusSpeaker(null);
            history.Add("—— 剧情结束 ——");
            ReportFinished();
        }

        private void ReportFinished()
        {
            if (finishedReported) return;
            finishedReported = true;
            try
            {
                onFinished?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Choose(int index)
        {
            if (closing || runner.Current == null || !runner.Current.IsChoice) return;
            List<StoryChoice> options = runner.Current.Blocking.Choices;
            if (index < 0 || index >= options.Count) return;
            history.Add("　→ " + options[index].Text);
            StoryFrame frame = runner.Choose(index);
            HideChoices();
            Present(frame);
        }

        private void DialogueClicked()
        {
            if (closing) return;
            if (ended)
            {
                Close();
                return;
            }

            if (runner.Current == null || runner.Current.IsChoice) return;
            if (typing)
            {
                StopTyping(true);
                return;
            }

            AdvanceLine();
        }

        private void AdvanceLine()
        {
            if (closing || runner.Finished || runner.Current == null || runner.Current.IsChoice) return;
            StoryFrame frame = runner.Advance();
            Present(frame);
        }

        private void SkipToDecision()
        {
            if (closing || ended) return;
            StoryFrame frame = runner.Current;
            if (frame == null) return;
            int guard = 0;
            while (!frame.IsChoice && !frame.IsEnd && guard++ < 512)
            {
                frame = runner.Advance();
                if (frame.IsChoice || frame.IsEnd) break;
                // Skipped lines still update the stage (bg/show/hide) and land in 记录; sfx are muted.
                ApplyDirectives(frame, false);
                string speaker = string.IsNullOrEmpty(frame.Blocking.Subject) ? string.Empty : ResolveName(frame.Blocking.Subject);
                history.Add(string.IsNullOrEmpty(speaker) ? frame.Blocking.Text : $"{speaker}：{frame.Blocking.Text}");
            }

            Present(frame);
            Notify(frame.IsChoice ? "已跳到选择支" : "已跳到本段结尾");
        }

        private void ToggleAuto()
        {
            autoMode = !autoMode;
            PanelKit.LabelOf(autoButton).text = autoMode ? "自动：开" : "自动";
            PanelKit.SetButtonState(autoButton, true, autoMode ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            if (autoMode && !typing) ScheduleAuto();
            else if (!autoMode) CancelAuto();
            Notify(autoMode ? "已开启自动播放" : "已关闭自动播放");
        }

        private void ScheduleAuto()
        {
            CancelAuto();
            if (!autoMode || ended || runner.Current == null || runner.Current.IsChoice) return;
            autoRoutine = StartCoroutine(AutoAdvance(fullLine.Length));
        }

        private void CancelAuto()
        {
            if (autoRoutine == null) return;
            StopCoroutine(autoRoutine);
            autoRoutine = null;
        }

        private IEnumerator AutoAdvance(int length)
        {
            yield return new WaitForSecondsRealtime(AutoBaseDelay + length * AutoPerCharDelay);
            autoRoutine = null;
            if (autoMode && !typing && !ended) AdvanceLine();
        }

        // ------------------------------------------------------------------ typewriter

        private void StartTyping(string text)
        {
            CancelAuto();
            if (typeRoutine != null) StopCoroutine(typeRoutine);
            fullLine = text ?? string.Empty;
            typing = true;
            continueHint.gameObject.SetActive(false);
            bodyText.text = string.Empty;
            typeRoutine = StartCoroutine(TypeLine());
        }

        private IEnumerator TypeLine()
        {
            int shown = 0;
            float elapsed = 0f;
            while (shown < fullLine.Length)
            {
                elapsed += Time.unscaledDeltaTime;
                int target = Mathf.Min(fullLine.Length, Mathf.FloorToInt(elapsed / CharSeconds));
                if (target != shown)
                {
                    shown = target;
                    bodyText.text = fullLine.Substring(0, shown);
                }

                yield return null;
            }

            typeRoutine = null;
            FinishTyping();
        }

        private void StopTyping(bool showFull)
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            if (typing && showFull) FinishTyping();
            else typing = false;
        }

        private void FinishTyping()
        {
            typing = false;
            bodyText.text = fullLine;
            continueHint.gameObject.SetActive(!ended && runner.Current != null && !runner.Current.IsChoice);
            if (autoMode) ScheduleAuto();
        }

        private void Update()
        {
            if (continueHint == null || !continueHint.gameObject.activeSelf) return;
            Color color = continueHint.color;
            color.a = 0.45f + Mathf.Sin(Time.unscaledTime * 4f) * 0.4f;
            continueHint.color = color;
        }

        // ------------------------------------------------------------------ stage

        private void SetBackground(string assetId)
        {
            Sprite sprite = string.IsNullOrEmpty(assetId) ? null : Resources.Load<Sprite>("Art/Story/" + assetId);
            if (sprite != null)
            {
                background.sprite = sprite;
                background.color = PanelKit.White;
                backgroundId.text = string.Empty;
                return;
            }

            background.sprite = backgroundFallback;
            background.color = PanelKit.White;
            backgroundId.text = string.IsNullOrEmpty(assetId) ? string.Empty : "背景资源缺失";
        }

        private void ShowCharacter(string characterId, string expression, string position)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            SlotView existing = FindSlotByCharacter(characterId);
            string wanted = NormalizePosition(position, existing != null ? existing.Position : "center");
            if (existing != null && existing.Position != wanted) ClearSlot(existing);

            SlotView slot = FindSlotByPosition(wanted);
            if (slot == null) return;
            slot.CharacterId = characterId;
            slot.Root.SetActive(true);
            slot.Group.alpha = 1f;
            RefreshPortrait(slot, expression);
        }

        private void RefreshPortrait(SlotView slot, string expression)
        {
            if (slot == null || string.IsNullOrEmpty(slot.CharacterId)) return;
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(expression))
                sprite = Resources.Load<Sprite>("Art/Story/Portraits/" + slot.CharacterId + "-" + expression);
            if (sprite == null) sprite = Resources.Load<Sprite>("Art/Story/Portraits/" + slot.CharacterId);
            if (sprite == null) sprite = PanelKit.MemberSpriteOrNull(slot.CharacterId, false);

            slot.Portrait.sprite = sprite;
            slot.Portrait.enabled = sprite != null;
            slot.Silhouette.SetActive(sprite == null);
            slot.SilhouetteName.text = ResolveName(slot.CharacterId);
        }

        private void HideCharacter(string characterId)
        {
            SlotView slot = FindSlotByCharacter(characterId);
            if (slot != null) ClearSlot(slot);
        }

        private static void ClearSlot(SlotView slot)
        {
            slot.CharacterId = null;
            slot.Portrait.sprite = null;
            slot.Root.SetActive(false);
        }

        private void FocusSpeaker(string characterId)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                SlotView slot = slots[index];
                if (!slot.Root.activeSelf) continue;
                bool speaking = string.IsNullOrEmpty(characterId) || slot.CharacterId == characterId;
                slot.Group.alpha = speaking ? 1f : 0.55f;
                slot.Root.transform.localScale = speaking ? Vector3.one : new Vector3(0.96f, 0.96f, 1f);
                if (speaking && !string.IsNullOrEmpty(characterId)) slot.Root.transform.SetAsLastSibling();
            }

            // Keep the dialogue box, choices and history above any re-ordered portrait.
            dialogueBox.transform.SetAsLastSibling();
            choiceLayer.SetAsLastSibling();
            historyOverlay.transform.SetAsLastSibling();
        }

        private SlotView FindSlotByCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            for (int index = 0; index < slots.Count; index++)
                if (slots[index].CharacterId == characterId) return slots[index];
            return null;
        }

        private SlotView FindSlotByPosition(string position)
        {
            for (int index = 0; index < slots.Count; index++)
                if (slots[index].Position == position) return slots[index];
            return null;
        }

        private static string NormalizePosition(string position, string fallback)
        {
            if (string.IsNullOrEmpty(position)) return fallback;
            string lowered = position.Trim().ToLowerInvariant();
            for (int index = 0; index < SlotOrder.Length; index++)
                if (SlotOrder[index] == lowered) return lowered;
            return fallback;
        }

        private string ResolveName(string characterId)
        {
            string custom = nameResolver?.Invoke(characterId);
            return string.IsNullOrEmpty(custom) ? PanelKit.MemberNameOrId(characterId) : custom;
        }

        private void HideChoices()
        {
            for (int index = 0; index < choiceButtons.Count; index++)
            {
                choiceButtons[index].SetActive(false);
                Destroy(choiceButtons[index]);
            }

            choiceButtons.Clear();
            choiceLayer.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ history

        private void ShowHistory()
        {
            var builder = new StringBuilder();
            if (history.Count == 0) builder.Append("尚无已读台词");
            for (int index = 0; index < history.Count; index++)
            {
                if (index > 0) builder.Append('\n');
                builder.Append(history[index]);
            }

            historyText.text = builder.ToString();
            float preferred = historyText.preferredHeight + 24f;
            historyContent.sizeDelta = new Vector2(0, Mathf.Max(preferred, 100f));
            historyOverlay.SetActive(true);
            historyOverlay.transform.SetAsLastSibling();
        }

        private void HideHistory()
        {
            historyOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            onMessage?.Invoke(message);
        }

        private void Leave()
        {
            if (ended)
            {
                Close();
                return;
            }

            ClosePanel("已退出剧情，进度不会保存");
        }

        public void Close()
        {
            ClosePanel(null);
        }

        private void ClosePanel(string message)
        {
            if (closing) return;
            closing = true;
            Action callback = onBack;
            Action<string> notify = onMessage;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
            if (!string.IsNullOrEmpty(message)) notify?.Invoke(message);
        }

        private void OnDestroy()
        {
            closing = true;
            StopAllCoroutines();
            kit?.Dispose();
        }
    }
}
