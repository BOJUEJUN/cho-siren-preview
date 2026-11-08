using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    /// <summary>Task board access the panel needs; GameModel implements it, tests can fake it.</summary>
    public interface ITaskBoardService
    {
        /// <param name="cadence">"daily" or "weekly" (see <see cref="TaskCadence"/>).</param>
        List<TaskView> TaskViews(string cadence);

        bool TryClaimTask(string id, out string message);

        int ClaimableTaskCount { get; }
    }

    /// <summary>
    /// 每日 / 每周 task board: tabs, one row per task with progress bar, reward chip and a
    /// Claim-&lt;taskId&gt; button, plus 一键领取. Opened with
    /// TaskBoardPanel.Open(safeRoot, model, service, onBack, toast).
    /// </summary>
    public sealed class TaskBoardPanel : MonoBehaviour
    {
        // 2026-11-07 (任务.psd): the shared dark-top / light-bottom page gradient instead of the neon city stage.
        private const string StageBackgroundResource = "Art/Ui1107/page-bg-board";
        // 2026-10-25 readability pass (少爷: "一大推信息文字太乱了"): one line of info and one action per row.
        private const float RowHeight = 118f;
        private const float RowGap = 12f;
        private const float ListTop = 272f;
        private const float ListBottom = 52f;
        private const float ProgressX = 24f;
        private const float ProgressY = 58f;
        private const float ProgressWidth = 330f;
        private const float ProgressHeight = 14f;
        private static readonly Color RowGlass = new Color32(16, 8, 38, 140);

        private sealed class RowView
        {
            public TaskView Task;
            public Image ProgressFill;
            public Text ProgressText;
            public GameObject ClaimButton;
            public Button Action;
            public Text ActionText;
            public RectTransform ActionIcon;
            public RectTransform Medal;
            public VisualA1101Motion Pulse;
            public Image RowBackground;
            public Image Stripe;
            public Text Title;
            public Text Reward;
            public CanvasGroup Group;
            public bool CanGo;
        }

        private readonly List<RowView> rows = new List<RowView>();

        private PanelKit kit;
        private GameModel model;
        private ITaskBoardService service;
        private Action onBack;
        private Action<string> onMessage;
        // Takes the player to where an unfinished task is done (keyed by its trigger).
        private Action<string> onGo;
        private bool closing;
        private string cadence = TaskCadence.Daily;

        private CleanUi1025.Segment tabs;
        private Image summaryFill;
        private GameObject claimAllButton;
        private Button claimAll;
        private Text claimAllText;
        private Text summaryText;
        private GameObject checkInButton;
        private Button checkIn;
        private Text checkInText;
        private RectTransform listViewport;
        private RectTransform listContent;
        private GameObject emptyState;
        private RectTransform checkInIcon;

        public static TaskBoardPanel Open(Transform host, GameModel gameModel, ITaskBoardService taskService,
            Action back = null, Action<string> message = null, Action<string> go = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameModel == null) throw new ArgumentNullException(nameof(gameModel));
            if (taskService == null) throw new ArgumentNullException(nameof(taskService));

            TaskBoardPanel existing = host.GetComponentInChildren<TaskBoardPanel>(true);
            if (existing != null) Destroy(existing.gameObject);

            GameObject panelObject = PanelKit.CreateOverlayRoot("TaskBoardPanel", host);
            TaskBoardPanel panel = panelObject.AddComponent<TaskBoardPanel>();
            panel.model = gameModel;
            panel.service = taskService;
            panel.onBack = back;
            panel.onMessage = message;
            panel.onGo = go;
            panel.Build();
            return panel;
        }

        public string Cadence => cadence;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("TaskBoard");
            model.Changed += HandleModelChanged;
            ChoSiren.UI.RewardGain1101.Bind(model, transform, kit.Font);
            BuildStageBackdrop();

            SecondaryTasks038Chrome.Header(kit, transform, string.Empty, "任务", "Back", Close);
            claimAll = CleanUi1025.Pill(kit, transform, "ClaimAll", "一键领取", 24, ClaimAll, out claimAllText);
            claimAllButton = claimAll.gameObject;
            PanelKit.PlaceTop(claimAllButton.GetComponent<RectTransform>(), 512, 32, 188, 56);
            // visual-a 1101c: the footer sentence lives behind 「?」 now.
            GameObject help = VisualA1101.HelpButton(transform, "TaskHelp", transform, 50f,
                ("mic", "完成演出、战斗与签到会自动计入任务进度"),
                ("clock", "每日任务每天刷新，每周任务每周一刷新"),
                ("gift", "完成后点「领取」，或一键领取全部"));
            VisualA1101.Place(help.GetComponent<RectTransform>(), 446, 35, 50, 50);
            tabs = CleanUi1025.Segmented(kit, transform, "CadenceTabs", "每日", "每周", 26,
                () => SelectCadence(TaskCadence.Daily), () => SelectCadence(TaskCadence.Weekly));
            PanelKit.PlaceTop(tabs.Track.rectTransform, 20, 128, 680, 64);

            // Today's share as a ring and two numbers (was 「今日完成 1/6」 + a bar).
            summaryFill = VisualA1101.Ring(transform, "SummaryRing", 46, 0f, Brand1025.Teal);
            VisualA1101.Place((RectTransform)summaryFill.transform.parent, 24, 206, 46, 46);
            summaryText = VisualA1101.Number(transform, "Summary", string.Empty, 26, VisualA1101.Paper, TextAnchor.MiddleLeft);
            VisualA1101.Place(summaryText.rectTransform, 82, 206, 160, 46);
            checkIn = CleanUi1025.Pill(kit, transform, "CheckInTaskChip", string.Empty, 22, CheckIn, out checkInText);
            checkInButton = checkIn.gameObject;
            PanelKit.PlaceTop(checkInButton.GetComponent<RectTransform>(), 500, 204, 200, 50);
            checkInIcon = PillIcon(checkIn, checkInText, "calendar", 34f);

            BuildList();
            Refresh();
        }

        /// <summary>A neon icon at the left of a CleanUi1025 pill; its label keeps the rest.</summary>
        private static RectTransform PillIcon(Button pill, Text label, string icon, float size)
        {
            RectTransform glyph = VisualA1101.NeonIcon(pill.transform, "ActionIcon", icon, Brand1025.Ink, size, 0f);
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(0f, .5f);
            glyph.anchoredPosition = new Vector2(16f, 0f);
            RectTransform rect = label.rectTransform;
            rect.offsetMin = new Vector2(16f + size, rect.offsetMin.y);
            return glyph;
        }

        private static void SetPillIcon(RectTransform glyph, string icon, bool brand, bool interactable)
        {
            if (glyph == null) return;
            Image image = VisualA1101.Glyph(glyph);
            if (image == null) return;
            image.sprite = VisualA1101.Icon(icon);
            image.color = brand ? (Color)Brand1025.Ink : interactable ? VisualA1101.Teal : VisualA1101.Muted;
        }

        private static (string icon, string verb) TriggerArt(string trigger)
        {
            switch (trigger)
            {
                case TaskTriggers.Login: return ("login", "登录");
                case TaskTriggers.CheckIn: return ("calendar", "签到");
                case TaskTriggers.Perform: return ("mic", "演出");
                case TaskTriggers.BattleWin: return ("dice", "胜利");
                case TaskTriggers.GachaPull: return ("gacha", "签约");
                case TaskTriggers.Train: return ("dumbbell", "训练");
                case TaskTriggers.SpendStamina: return ("heart", "体力");
                case TaskTriggers.ClaimIdle: return ("coin", "收益");
                case TaskTriggers.ReadStory: return ("book", "剧情");
                default: return ("star", null);
            }
        }

        private void BuildStageBackdrop()
        {
            Sprite stageSprite = Resources.Load<Sprite>(StageBackgroundResource);
            if (stageSprite == null)
            {
                Texture2D stageTexture = Resources.Load<Texture2D>(StageBackgroundResource);
                if (stageTexture != null)
                {
                    stageSprite = kit.NewSprite(stageTexture,
                        new Rect(0f, 0f, stageTexture.width, stageTexture.height),
                        Vector2.one * 0.5f, Vector4.zero);
                }
            }

            if (stageSprite == null)
            {
                kit.BuildBackdrop(transform);
                return;
            }

            Image background = kit.NewImage("TaskBoardStageBackground", transform, stageSprite, Color.white);
            ChoSiren.UI.PageAmbient.Set(gameObject, stageSprite, .3f);
            PanelKit.Stretch(background.rectTransform);
            background.raycastTarget = true;
            background.type = Image.Type.Simple;

            AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = stageSprite.rect.width / Mathf.Max(1f, stageSprite.rect.height);

            // Keep the generated stage visible while gently damping its brightest floor lights behind text.
            // Darken the stage art so the black cards read as one calm surface.
            Image shade = kit.NewImage("TaskBoardStageShade", transform, null, new Color32(11, 8, 18, 40));
            PanelKit.Stretch(shade.rectTransform);
            // The PSD's pig peeks in from the bottom-left corner.
            Image pig = kit.NewImage("TaskBoardPig", transform, Resources.Load<Sprite>("Art/Ui1107/task-pig"), Color.white);
            pig.preserveAspect = true;
            pig.raycastTarget = false;
            RectTransform pigRect = pig.rectTransform;
            pigRect.anchorMin = pigRect.anchorMax = pigRect.pivot = Vector2.zero;
            pigRect.anchoredPosition = new Vector2(-40f, 0f);
            pigRect.sizeDelta = new Vector2(150f, 157f);
        }

        private void BuildList()
        {
            listViewport = kit.NewRect("TaskList", transform);
            listViewport.anchorMin = Vector2.zero;
            listViewport.anchorMax = Vector2.one;
            listViewport.pivot = new Vector2(0.5f, 0.5f);
            listViewport.offsetMin = new Vector2(20f, ListBottom);
            listViewport.offsetMax = new Vector2(-20f, -ListTop);
            Image viewportImage = listViewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
            listViewport.gameObject.AddComponent<RectMask2D>();

            listContent = kit.NewRect("Content", listViewport);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.anchoredPosition = Vector2.zero;
            listContent.sizeDelta = new Vector2(0, 0);

            ScrollRect scroll = listViewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = listContent;
            scroll.viewport = listViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            emptyState = ModalPolish038Chrome.StateBlock(listViewport, "TaskEmpty",
                ModalPolish038StateKind.Empty, "暂无任务", null);
            PanelKit.PlaceCentered(emptyState.GetComponent<RectTransform>(), 560f, 120f);
            SirenStickerUi.SkinPanel(emptyState);
            emptyState.SetActive(false);
        }

        private void RebuildRows(List<TaskView> tasks)
        {
            // Same task set (typical after a claim): update in place so Claim-<id> objects stay stable.
            if (tasks != null && tasks.Count == rows.Count && rows.Count > 0)
            {
                bool sameOrder = true;
                for (int index = 0; index < rows.Count; index++)
                {
                    if (rows[index].Task.Definition.Id == tasks[index].Definition.Id) continue;
                    sameOrder = false;
                    break;
                }

                if (sameOrder)
                {
                    for (int index = 0; index < rows.Count; index++)
                    {
                        rows[index].Task = tasks[index];
                        ApplyRowState(rows[index]);
                    }

                    return;
                }
            }

            for (int index = listContent.childCount - 1; index >= 0; index--)
            {
                GameObject stale = listContent.GetChild(index).gameObject;
                stale.SetActive(false);
                Destroy(stale);
            }

            rows.Clear();

            int count = tasks == null ? 0 : tasks.Count;
            if (emptyState != null) emptyState.SetActive(count == 0);
            listContent.sizeDelta = new Vector2(0, ListContentHeight(count));
            for (int index = 0; index < count; index++) rows.Add(BuildRow(tasks[index], index));
        }

        public static float ListContentHeight(int count)
        {
            int normalizedCount = Mathf.Max(0, count);
            if (normalizedCount == 0) return 0f;
            return normalizedCount * RowHeight + (normalizedCount - 1) * RowGap;
        }

        private RowView BuildRow(TaskView task, int index)
        {
            // visual-a 1101c: one row = a medallion that says what (icon) inside a ring that says how far, a 2-character
            // verb, the count, the reward badge and one icon button.
            TaskDefinition definition = task.Definition;
            Image card = CleanUi1025.RowPanel(kit, listContent, "Task-" + definition.Id, out Image stripe);
            PanelKit.PlaceTop(card.rectTransform, 0, index * (RowHeight + RowGap), 680, RowHeight);
            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();

            (string icon, string verb) = TriggerArt(definition.Trigger);
            Image fill = VisualA1101.Ring(card.transform, "ProgressRing", 96f, 0f, Brand1025.Teal);
            VisualA1101.Place((RectTransform)fill.transform.parent, 22, 11, 96, 96);
            RectTransform medal = VisualA1101.Medallion(card.transform, "TaskMedal", icon, VisualA1101.Teal, 78f);
            VisualA1101.Place(medal, 31, 20, 78, 78);

            Text title = VisualA1101.Label(card.transform, "Title", verb ?? definition.Title, 30, CleanUi1025.Text, TextAnchor.MiddleLeft);
            VisualA1101.Place(title.rectTransform, 138, 16, 170, 44);
            Text progress = VisualA1101.Number(card.transform, "Count", string.Empty, 24, CleanUi1025.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(progress.rectTransform, 138, 62, 170, 38);

            // the reward sits on its own, between the task and the button
            string currency = definition.Reward != null ? definition.Reward.Currency : string.Empty;
            int amount = definition.Reward != null ? definition.Reward.Amount : 0;
            Sprite rewardSprite = ChoSiren.UI.RewardItemVisuals.SpriteFor(currency); // reward-ui 1101: the HUD badge set
            Image iconImage = kit.NewImage("RewardIcon", card.transform,
                rewardSprite ?? ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Gem),
                rewardSprite != null && !ChoSiren.UI.RewardItemVisuals.IsCodeSymbol(rewardSprite) ? Color.white : PanelKit.CurrencyColor(currency));
            PanelKit.PlaceTop(iconImage.rectTransform, 318, 30, 58, 58);
            iconImage.preserveAspect = true; iconImage.raycastTarget = false;
            Text rewardText = VisualA1101.Number(card.transform, "Reward", $"×{amount:N0}",
                26, currency == CurrencyIds.Diamond ? (Color)CleanUi1025.Cyan : CleanUi1025.Gold, TextAnchor.MiddleLeft);
            VisualA1101.Place(rewardText.rectTransform, 380, 36, 130, 46);

            string taskId = definition.Id;
            string trigger = definition.Trigger;
            RowView view = null;
            Button action = CleanUi1025.Pill(kit, card.transform, "Claim-" + taskId, "领取", 24, () =>
            {
                if (view == null) return;
                if (view.Task.Claimable) Claim(taskId);
                else if (view.CanGo) Go(trigger);
                else VisualA1101.Deny();
            }, out Text actionText);
            PanelKit.PlaceTop(action.GetComponent<RectTransform>(), 524, 31, 140, 56);

            view = new RowView
            {
                Task = task,
                ProgressFill = fill,
                ProgressText = progress,
                ClaimButton = action.gameObject,
                Action = action,
                ActionText = actionText,
                ActionIcon = PillIcon(action, actionText, "gift", 34f),
                Medal = medal,
                RowBackground = card,
                Stripe = stripe,
                Title = title,
                Reward = rewardText,
                Group = group,
                CanGo = onGo != null && HasDestination(trigger),
            };
            ApplyRowState(view);
            card.gameObject.AddComponent<VisualA1101Motion>().Pop(Mathf.Min(.35f, index * .05f));
            return view;
        }

        /// <summary>Triggers that have a place to go and do them.</summary>
        private static bool HasDestination(string trigger) =>
            trigger == TaskTriggers.Perform || trigger == TaskTriggers.BattleWin ||
            trigger == TaskTriggers.SpendStamina || trigger == TaskTriggers.Train ||
            trigger == TaskTriggers.ClaimIdle || trigger == TaskTriggers.GachaPull ||
            trigger == TaskTriggers.ReadStory || trigger == TaskTriggers.CheckIn;

        private static void ApplyRowState(RowView view)
        {
            TaskView task = view.Task;
            int target = Mathf.Max(1, task.Definition.Target);
            int progress = Mathf.Clamp(task.Progress, 0, target);
            view.ProgressFill.fillAmount = progress / (float)target;
            view.ProgressText.text = $"{progress}/{target}";
            Color accent;

            if (task.Claimed)
            {
                // done: the whole row steps back
                CleanUi1025.StylePill(view.Action, view.ActionText, CleanUi1025.Weight.Quiet, "已领", false);
                SetPillIcon(view.ActionIcon, "check", false, false);
                view.RowBackground.color = CleanUi1025.RowDone;
                view.Stripe.color = new Color(1f, 1f, 1f, .15f);
                accent = VisualA1101.Muted;
                view.Group.alpha = .55f;
            }
            else if (task.Claimable)
            {
                // the one thing to do: green button, green edge
                CleanUi1025.StylePill(view.Action, view.ActionText, CleanUi1025.Weight.Primary, "领取", true);
                SetPillIcon(view.ActionIcon, "gift", true, true);
                view.RowBackground.color = CleanUi1025.Row;
                view.Stripe.color = Brand1025.Teal;
                accent = Brand1025.Teal;
                view.Group.alpha = 1f;
            }
            else
            {
                if (view.CanGo) CleanUi1025.StylePill(view.Action, view.ActionText, CleanUi1025.Weight.Outline, "前往", true);
                else CleanUi1025.StylePill(view.Action, view.ActionText, CleanUi1025.Weight.Quiet, "未达成", false);
                SetPillIcon(view.ActionIcon, view.CanGo ? "arrow" : "clock", false, view.CanGo);
                view.RowBackground.color = CleanUi1025.Row;
                view.Stripe.color = new Color(1f, 1f, 1f, .22f);
                accent = VisualA1101.Lavender;
                view.Group.alpha = 1f;
            }
            view.ProgressFill.color = task.Claimable ? (Color)Brand1025.Teal : task.Claimed ? VisualA1101.Muted : VisualA1101.Violet;
            view.ProgressText.color = task.Claimable ? (Color)Brand1025.Teal : VisualA1101.Muted;
            if (view.Medal != null)
            {
                Image rim = view.Medal.Find("MedallionRim")?.GetComponent<Image>();
                if (rim != null) rim.color = accent;
                VisualA1101.Tint((RectTransform)view.Medal.Find("MedallionIcon"), accent);
            }
            // the claimable button breathes; nothing else moves
            if (task.Claimable && view.Pulse == null) view.Pulse = view.ClaimButton.AddComponent<VisualA1101Motion>().Pulse();
            else if (!task.Claimable && view.Pulse != null)
            {
                Destroy(view.Pulse);
                view.Pulse = null;
                view.ClaimButton.transform.localScale = Vector3.one;
            }
        }

        // ------------------------------------------------------------------ actions

        private void SelectCadence(string value)
        {
            if (cadence == value) return;
            cadence = value;
            // visual-a 1101c: the lit tab says it; no 「已切换到…」 toast on top.
            Refresh();
        }

        private void Claim(string taskId)
        {
            // reward-ui 1101: the grant itself raises the shared 获得奖励 presentation; the message becomes its caption.
            bool succeeded = service.TryClaimTask(taskId, out string message);
            if (succeeded) kit.PlaySuccess();
            Notify(string.IsNullOrEmpty(message) ? (succeeded ? "奖励已领取" : "领取失败") : message);
            Refresh();
        }

        private void Go(string trigger)
        {
            Action<string> go = onGo;
            Close();
            go?.Invoke(trigger);
        }

        private void ClaimAll()
        {
            List<TaskView> daily = service.TaskViews(TaskCadence.Daily) ?? new List<TaskView>();
            List<TaskView> weekly = service.TaskViews(TaskCadence.Weekly) ?? new List<TaskView>();
            int claimed = 0;
            string lastMessage = string.Empty;
            for (int pass = 0; pass < 2; pass++)
            {
                List<TaskView> list = pass == 0 ? daily : weekly;
                for (int index = 0; index < list.Count; index++)
                {
                    if (!list[index].Claimable) continue;
                    if (service.TryClaimTask(list[index].Definition.Id, out string message)) claimed++;
                    else lastMessage = message;
                }
            }

            if (claimed > 0)
            {
                kit.PlaySuccess();
                Notify($"已一键领取 {claimed} 项任务奖励");
            }
            else
            {
                Notify(string.IsNullOrEmpty(lastMessage) ? "当前没有可领取的任务" : lastMessage);
            }

            Refresh();
        }

        private void CheckIn()
        {
            bool succeeded = model.CheckIn(out string message);
            if (succeeded) kit.PlaySuccess();
            Notify(string.IsNullOrEmpty(message) ? (succeeded ? "签到成功" : "今天已经签到") : message);
            Refresh();
        }

        private void HandleModelChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (kit == null) return;
            bool daily = cadence == TaskCadence.Daily;
            tabs.Select(daily);

            // what can be claimed first, then what is in progress, finished last
            List<TaskView> tasks = new List<TaskView>(service.TaskViews(cadence) ?? new List<TaskView>());
            tasks = tasks.Select((t, i) => (t, i))
                .OrderBy(p => p.t.Claimable ? 0 : p.t.Claimed ? 2 : 1).ThenBy(p => p.i).Select(p => p.t).ToList();
            RebuildRows(tasks);

            int completed = 0;
            for (int index = 0; index < tasks.Count; index++)
            {
                if (tasks[index].Completed) completed++;
            }

            summaryText.text = tasks.Count == 0 ? string.Empty : $"{completed}/{tasks.Count}";
            summaryFill.fillAmount = tasks.Count == 0 ? 0f : completed / (float)tasks.Count;

            int claimable = service.ClaimableTaskCount;
            // only offered when there is something to collect
            claimAllButton.SetActive(claimable > 0);
            CleanUi1025.StylePill(claimAll, claimAllText, CleanUi1025.Weight.Primary, $"一键领取 {claimable}", claimable > 0);

            bool canCheckIn = !model.HasCheckedInToday;
            CleanUi1025.StylePill(checkIn, checkInText, canCheckIn ? CleanUi1025.Weight.Primary : CleanUi1025.Weight.Quiet,
                canCheckIn ? "签到" : $"{model.Save.CheckInDay} 天", canCheckIn);
            SetPillIcon(checkInIcon, canCheckIn ? "calendar" : "check", canCheckIn, canCheckIn);
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            // Keep the footer as stable guidance. Mirroring the same transient message here
            // and in the app toast produces two identical notices on the task board.
            onMessage?.Invoke(message);
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            Action callback = onBack;
            gameObject.SetActive(false);
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            if (model != null) model.Changed -= HandleModelChanged;
            kit?.Dispose();
        }
    }
}
