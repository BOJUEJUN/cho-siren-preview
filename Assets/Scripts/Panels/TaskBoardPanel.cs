using System;
using System.Collections.Generic;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

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
        private const string StageBackgroundResource = "Art/TaskAI/task-board-bg-ai-v1-20260903";
        private const float RowHeight = 132f;
        private const float RowGap = 12f;
        private const float ListTop = 238f;
        private const float ListBottom = 52f;
        private const float ProgressX = 24f;
        private const float ProgressY = 58f;
        private const float ProgressWidth = 330f;
        private const float ProgressHeight = 14f;
        private static readonly Color RowGlass = new Color32(15, 14, 54, 108);

        private sealed class RowView
        {
            public TaskView Task;
            public Image ProgressFill;
            public Image ProgressMarker;
            public Text ProgressText;
            public GameObject ClaimButton;
            public Text ClaimLabel;
            public Image RowBackground;
            public Image Accent;
            public Image ClaimGlow;
            public Outline Edge;
        }

        private readonly List<RowView> rows = new List<RowView>();

        private PanelKit kit;
        private GameModel model;
        private ITaskBoardService service;
        private Action onBack;
        private Action<string> onMessage;
        private bool closing;
        private string cadence = TaskCadence.Daily;

        private GameObject tabDaily;
        private GameObject tabWeekly;
        private Image tabDailyGlyph;
        private Image tabWeeklyGlyph;
        private GameObject claimAllButton;
        private Text summaryText;
        private GameObject checkInButton;
        private RectTransform listViewport;
        private RectTransform listContent;
        private GameObject emptyState;
        private Text hintText;

        public static TaskBoardPanel Open(Transform host, GameModel gameModel, ITaskBoardService taskService,
            Action back = null, Action<string> message = null)
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
            panel.Build();
            return panel;
        }

        public string Cadence => cadence;

        // ------------------------------------------------------------------ build

        private void Build()
        {
            kit = new PanelKit("TaskBoard");
            model.Changed += HandleModelChanged;
            BuildStageBackdrop();

            GameObject header = SecondaryTasks038Chrome.Header(kit, transform, "活跃奖励", "任务面板",
                "Back", Close);
            claimAllButton = SecondaryTasks038Chrome.Button(header.transform, "ClaimAll", "一键领取",
                15, ClaimAll, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(claimAllButton.GetComponent<RectTransform>(), 540, 27, 162, 56);

            tabDaily = SecondaryTasks038Chrome.Button(transform, "TabDaily", "每日任务", 17,
                () => SelectCadence(TaskCadence.Daily), ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(tabDaily.GetComponent<RectTransform>(), 20, 126, 335, 56);
            tabDailyGlyph = AddTabGlyph(tabDaily);
            tabWeekly = SecondaryTasks038Chrome.Button(transform, "TabWeekly", "每周任务", 17,
                () => SelectCadence(TaskCadence.Weekly), ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(tabWeekly.GetComponent<RectTransform>(), 365, 126, 335, 56);
            tabWeeklyGlyph = AddTabGlyph(tabWeekly);

            summaryText = kit.NewPlacedText(transform, string.Empty, 13, PanelKit.Muted, 24, 192, 470, 34,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(summaryText, 10);

            checkInButton = SecondaryTasks038Chrome.Button(transform, "CheckInTaskChip", string.Empty,
                13, CheckIn, ModalPolish038ButtonStyle.Ghost);
            PanelKit.PlaceTop(checkInButton.GetComponent<RectTransform>(), 510, 190, 190, 38);

            BuildList();

            hintText = kit.NewPlacedText(transform, "完成演出、战斗与签约会自动推进任务进度", 12,
                new Color32(220, 206, 239, 255), 24, 0, 672, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 12f);
            hintRect.sizeDelta = new Vector2(-48f, 30f);

            Refresh();
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
            PanelKit.Stretch(background.rectTransform);
            background.raycastTarget = true;
            background.type = Image.Type.Simple;

            AspectRatioFitter cover = background.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            cover.aspectRatio = stageSprite.rect.width / Mathf.Max(1f, stageSprite.rect.height);

            // Keep the generated stage visible while gently damping its brightest floor lights behind text.
            Image shade = kit.NewImage("TaskBoardStageShade", transform, null, new Color32(3, 6, 27, 54));
            PanelKit.Stretch(shade.rectTransform);
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
                ModalPolish038StateKind.Empty, "本周期暂无任务",
                "周期刷新后会开放新的任务与奖励");
            PanelKit.PlaceCentered(emptyState.GetComponent<RectTransform>(), 560f, 120f);
            emptyState.SetActive(false);
        }

        /// <summary>Spark marker inside the tab body so it scales with the press feedback.</summary>
        private Image AddTabGlyph(GameObject tab)
        {
            Transform body = tab.transform.Find("Body");
            if (body == null) return null;
            Image glyph = kit.NewImage("TabGlyph", body,
                ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Spark),
                SecondaryTasks038Chrome.EdgeSelected);
            PanelKit.PlaceTop(glyph.rectTransform, 14f, 16f, 24f, 24f);
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            return glyph;
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
            TaskDefinition definition = task.Definition;
            GameObject row = SecondaryTasks038Chrome.Plate(kit, listContent, "Task-" + definition.Id,
                RowGlass, new Color32(104, 224, 255, 70));
            PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0, index * (RowHeight + RowGap), 680, RowHeight);
            Image accent = ReferencePunkFx.Slash(row.transform, "Accent", 13f, 66f, 108f, 4f, 78f,
                PanelKit.Cyan);

            Text title = kit.NewPlacedText(row.transform, definition.Title, 18, PanelKit.White,
                30, 16, 386, 30, TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(title, 13);

            GameObject cadence = SecondaryTasks038Chrome.Plate(kit, row.transform, "Cadence",
                new Color32(35, 24, 80, 170), new Color32(166, 112, 255, 70), true);
            PanelKit.PlaceTop(cadence.GetComponent<RectTransform>(), 424, 17, 80, 26);
            kit.NewPlacedText(cadence.transform, definition.Cadence == TaskCadence.Weekly ? "每周" : "每日",
                12, new Color32(255, 173, 226, 255), 0, 0, 80, 26,
                TextAnchor.MiddleCenter, FontStyle.Bold);

            Image fill = SecondaryTasks038Chrome.Progress(kit, row.transform, "Progress",
                ProgressX, ProgressY, ProgressWidth, ProgressHeight,
                new Color32(30, 26, 74, 190), PanelKit.Cyan, out Image marker);
            Text progress = kit.NewPlacedText(row.transform, string.Empty, 13, PanelKit.White, 362, 50, 132, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(progress, 10);

            GameObject reward = SecondaryTasks038Chrome.Plate(kit, row.transform, "Reward",
                new Color32(30, 25, 76, 150), new Color32(255, 205, 96, 60), true);
            PanelKit.PlaceTop(reward.GetComponent<RectTransform>(), 24, 86, 250, 36);
            string currency = definition.Reward != null ? definition.Reward.Currency : string.Empty;
            int amount = definition.Reward != null ? definition.Reward.Amount : 0;
            Sprite icon = PanelKit.CurrencyIcon(currency);
            Image iconImage = kit.NewImage("Icon", reward.transform,
                icon ?? ReferencePunkFx.StatGlyph(ReferencePunkFx.StatGlyphKind.Gem),
                icon != null ? Color.white : PanelKit.CurrencyColor(currency));
            PanelKit.PlaceTop(iconImage.rectTransform, 10, 6, 24, 24);
            iconImage.preserveAspect = true;
            Text rewardText = kit.NewPlacedText(reward.transform,
                $"{PanelKit.CurrencyName(currency)} ×{amount:N0}", 14, PanelKit.White,
                42, 0, 200, 36, TextAnchor.MiddleLeft, FontStyle.Bold);
            PanelKit.EnableBestFit(rewardText, 10);

            Image glow = kit.NewImage("ClaimGlow", row.transform, kit.RadialSprite(),
                new Color32(255, 82, 194, 0));
            PanelKit.PlaceTop(glow.rectTransform, 500, 16, 190, 100);
            glow.raycastTarget = false;

            string taskId = definition.Id;
            GameObject claim = SecondaryTasks038Chrome.Button(row.transform, "Claim-" + taskId, "领取",
                17, () => Claim(taskId), ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(claim.GetComponent<RectTransform>(), 532, 38, 124, 56);

            RowView view = new RowView
            {
                Task = task,
                ProgressFill = fill,
                ProgressMarker = marker,
                ProgressText = progress,
                ClaimButton = claim,
                ClaimLabel = SecondaryTasks038Chrome.LabelOf(claim),
                RowBackground = row.GetComponent<Image>(),
                Accent = accent,
                ClaimGlow = glow,
                Edge = row.GetComponent<Outline>(),
            };
            ApplyRowState(view);
            return view;
        }

        private static void ApplyRowState(RowView view)
        {
            TaskView task = view.Task;
            int target = Mathf.Max(1, task.Definition.Target);
            int progress = Mathf.Clamp(task.Progress, 0, target);
            float amount = progress / (float)target;
            view.ProgressFill.fillAmount = amount;
            view.ProgressText.text = $"{progress}/{target}";

            if (task.Claimed)
            {
                SecondaryTasks038Chrome.SetState(view.ClaimButton, false,
                    SecondaryTasks038Chrome.EdgeDisabled, SecondaryTasks038Chrome.InsetDisabled,
                    new Color32(196, 190, 220, 255), "已领取");
                view.RowBackground.color = new Color32(13, 12, 43, 88);
                view.ProgressFill.color = new Color32(140, 132, 180, 255);
                view.Accent.color = new Color32(126, 119, 164, 180);
                if (view.Edge != null) view.Edge.effectColor = new Color32(126, 119, 164, 50);
                if (view.ClaimGlow != null) view.ClaimGlow.color = new Color32(255, 82, 194, 0);
                SecondaryTasks038Chrome.SetProgressMarker(view.ProgressMarker,
                    ProgressX, ProgressY, ProgressWidth, ProgressHeight, amount,
                    new Color32(140, 132, 180, 255));
            }
            else if (task.Claimable)
            {
                SecondaryTasks038Chrome.SetState(view.ClaimButton, true,
                    ModalPolish038Chrome.AccentPink, new Color32(44, 17, 56, 250),
                    PanelKit.White, "领取");
                view.RowBackground.color = new Color32(39, 21, 88, 132);
                view.ProgressFill.color = PanelKit.Pink;
                view.Accent.color = PanelKit.Pink;
                if (view.Edge != null) view.Edge.effectColor = new Color32(255, 138, 226, 110);
                if (view.ClaimGlow != null) view.ClaimGlow.color = new Color32(255, 82, 194, 44);
                SecondaryTasks038Chrome.SetProgressMarker(view.ProgressMarker,
                    ProgressX, ProgressY, ProgressWidth, ProgressHeight, amount, PanelKit.Pink);
            }
            else
            {
                SecondaryTasks038Chrome.SetState(view.ClaimButton, false,
                    SecondaryTasks038Chrome.EdgeViolet, SecondaryTasks038Chrome.InsetViolet,
                    new Color32(214, 206, 236, 255), "进行中", .62f);
                view.RowBackground.color = RowGlass;
                view.ProgressFill.color = PanelKit.Cyan;
                view.Accent.color = PanelKit.Cyan;
                if (view.Edge != null) view.Edge.effectColor = new Color32(104, 224, 255, 70);
                if (view.ClaimGlow != null) view.ClaimGlow.color = new Color32(255, 82, 194, 0);
                SecondaryTasks038Chrome.SetProgressMarker(view.ProgressMarker,
                    ProgressX, ProgressY, ProgressWidth, ProgressHeight, amount, PanelKit.Cyan);
            }
        }

        // ------------------------------------------------------------------ actions

        private void SelectCadence(string value)
        {
            if (cadence == value) return;
            cadence = value;
            Refresh();
            Notify(value == TaskCadence.Weekly ? "已切换到每周任务" : "已切换到每日任务");
        }

        private void Claim(string taskId)
        {
            bool succeeded = service.TryClaimTask(taskId, out string message);
            if (succeeded) kit.PlaySuccess();
            Notify(string.IsNullOrEmpty(message) ? (succeeded ? "奖励已领取" : "领取失败") : message);
            Refresh();
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
            SecondaryTasks038Chrome.SetState(tabDaily, true,
                daily ? SecondaryTasks038Chrome.EdgeSelected : SecondaryTasks038Chrome.EdgeIdle,
                daily ? SecondaryTasks038Chrome.InsetSelected : SecondaryTasks038Chrome.InsetIdle,
                daily ? PanelKit.White : SecondaryTasks038Chrome.LabelIdle);
            SecondaryTasks038Chrome.SetState(tabWeekly, true,
                !daily ? SecondaryTasks038Chrome.EdgeSelected : SecondaryTasks038Chrome.EdgeIdle,
                !daily ? SecondaryTasks038Chrome.InsetSelected : SecondaryTasks038Chrome.InsetIdle,
                !daily ? PanelKit.White : SecondaryTasks038Chrome.LabelIdle);
            if (tabDailyGlyph != null) tabDailyGlyph.gameObject.SetActive(daily);
            if (tabWeeklyGlyph != null) tabWeeklyGlyph.gameObject.SetActive(!daily);

            List<TaskView> tasks = service.TaskViews(cadence) ?? new List<TaskView>();
            RebuildRows(tasks);

            int completed = 0;
            int claimedCount = 0;
            for (int index = 0; index < tasks.Count; index++)
            {
                if (tasks[index].Completed) completed++;
                if (tasks[index].Claimed) claimedCount++;
            }

            summaryText.text = tasks.Count == 0
                ? string.Empty
                : $"{(daily ? "今日" : "本周")}完成 {completed}/{tasks.Count} · 已领取 {claimedCount}";

            int claimable = service.ClaimableTaskCount;
            SecondaryTasks038Chrome.SetState(claimAllButton, claimable > 0,
                claimable > 0 ? ModalPolish038Chrome.AccentPink : SecondaryTasks038Chrome.EdgeDisabled,
                claimable > 0 ? new Color32(44, 17, 56, 250) : SecondaryTasks038Chrome.InsetDisabled,
                claimable > 0 ? PanelKit.White : SecondaryTasks038Chrome.LabelIdle,
                claimable > 0 ? $"一键领取 ({claimable})" : "一键领取", .58f);

            bool canCheckIn = !model.HasCheckedInToday;
            SecondaryTasks038Chrome.SetState(checkInButton, canCheckIn,
                canCheckIn ? SecondaryTasks038Chrome.EdgeGold : SecondaryTasks038Chrome.EdgeDisabled,
                canCheckIn ? SecondaryTasks038Chrome.InsetGold : SecondaryTasks038Chrome.InsetDisabled,
                canCheckIn ? PanelKit.White : SecondaryTasks038Chrome.LabelIdle,
                canCheckIn ? $"签到任务 · 连续 {model.Save.CheckInDay} 天" : "签到任务 · 已完成", .6f);
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
