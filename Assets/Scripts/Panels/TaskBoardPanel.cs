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
        private const float RowHeight = 132f;
        private const float RowGap = 12f;

        private sealed class RowView
        {
            public TaskView Task;
            public Image ProgressFill;
            public Text ProgressText;
            public GameObject ClaimButton;
            public Text ClaimLabel;
            public Image RowBackground;
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
        private GameObject claimAllButton;
        private Text claimAllLabel;
        private Text summaryText;
        private GameObject checkInButton;
        private Text checkInLabel;
        private RectTransform listContent;
        private Text emptyText;
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
            kit.BuildBackdrop(transform);

            GameObject header = kit.NewHeader(transform, "活跃奖励", "任务面板", Close);
            claimAllButton = kit.NewButton("ClaimAll", header.transform, "一键领取", 15, PanelKit.Pink,
                PanelKit.White, ClaimAll, 18);
            PanelKit.PlaceTop(claimAllButton.GetComponent<RectTransform>(), 540, 27, 162, 56);
            claimAllLabel = PanelKit.LabelOf(claimAllButton);

            tabDaily = kit.NewButton("TabDaily", transform, "每日任务", 17, PanelKit.ButtonDark, PanelKit.White,
                () => SelectCadence(TaskCadence.Daily), 18);
            PanelKit.PlaceTop(tabDaily.GetComponent<RectTransform>(), 20, 126, 335, 56);
            tabWeekly = kit.NewButton("TabWeekly", transform, "每周任务", 17, PanelKit.ButtonDark, PanelKit.White,
                () => SelectCadence(TaskCadence.Weekly), 18);
            PanelKit.PlaceTop(tabWeekly.GetComponent<RectTransform>(), 365, 126, 335, 56);

            summaryText = kit.NewPlacedText(transform, string.Empty, 13, PanelKit.Muted, 24, 192, 470, 34,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            checkInButton = kit.NewButton("TaskCheckIn", transform, string.Empty, 13, PanelKit.Pink,
                PanelKit.White, CheckIn, 14);
            PanelKit.PlaceTop(checkInButton.GetComponent<RectTransform>(), 510, 190, 190, 38);
            checkInLabel = PanelKit.LabelOf(checkInButton);

            BuildList();

            hintText = kit.NewPlacedText(transform, "完成演出、战斗与签约会自动推进任务进度", 12,
                new Color32(220, 206, 239, 255), 24, 1486, 672, 30, TextAnchor.MiddleCenter, FontStyle.Bold);

            Refresh();
        }

        private void BuildList()
        {
            RectTransform viewport = kit.NewRect("TaskList", transform);
            PanelKit.PlaceTop(viewport, 20, 224, 680, 1250);
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            listContent = kit.NewRect("Content", viewport);
            listContent.anchorMin = new Vector2(0, 1);
            listContent.anchorMax = new Vector2(1, 1);
            listContent.pivot = new Vector2(0.5f, 1);
            listContent.anchoredPosition = Vector2.zero;
            listContent.sizeDelta = new Vector2(0, 0);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = listContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            emptyText = kit.NewPlacedText(viewport, "本周期暂无任务", 17, PanelKit.Muted, 0, 120, 680, 40,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            emptyText.gameObject.SetActive(false);
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
            emptyText.gameObject.SetActive(count == 0);
            listContent.sizeDelta = new Vector2(0, Mathf.Max(0, count * (RowHeight + RowGap)));
            for (int index = 0; index < count; index++) rows.Add(BuildRow(tasks[index], index));
        }

        private RowView BuildRow(TaskView task, int index)
        {
            TaskDefinition definition = task.Definition;
            GameObject row = kit.NewPanel("Task-" + definition.Id, listContent, PanelKit.Glass, 22);
            PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0, index * (RowHeight + RowGap), 680, RowHeight);
            kit.AddOutline(row, new Color32(166, 112, 255, 110), 1.5f);

            kit.NewPlacedText(row.transform, definition.Title, 18, PanelKit.White, 24, 16, 400, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            kit.NewPlacedText(row.transform, definition.Cadence == TaskCadence.Weekly ? "每周" : "每日", 12,
                new Color32(255, 173, 226, 255), 430, 20, 60, 22, TextAnchor.MiddleLeft, FontStyle.Bold);

            Image fill = kit.NewBar("Progress", row.transform, 24, 58, 330, 14,
                new Color32(66, 54, 117, 255), PanelKit.Cyan, 7);
            Text progress = kit.NewPlacedText(row.transform, string.Empty, 13, PanelKit.White, 362, 50, 120, 30,
                TextAnchor.MiddleLeft, FontStyle.Bold);

            GameObject reward = kit.NewPanel("Reward", row.transform, new Color32(43, 32, 93, 230), 14);
            PanelKit.PlaceTop(reward.GetComponent<RectTransform>(), 24, 86, 250, 36);
            string currency = definition.Reward != null ? definition.Reward.Currency : string.Empty;
            int amount = definition.Reward != null ? definition.Reward.Amount : 0;
            Sprite icon = PanelKit.CurrencyIcon(currency);
            Image iconImage = kit.NewImage("Icon", reward.transform, icon ?? kit.RoundedSprite(6),
                icon != null ? Color.white : PanelKit.CurrencyColor(currency));
            PanelKit.PlaceTop(iconImage.rectTransform, 8, 6, 24, 24);
            iconImage.preserveAspect = true;
            kit.NewPlacedText(reward.transform, $"{PanelKit.CurrencyName(currency)} ×{amount:N0}", 14,
                PanelKit.White, 40, 0, 200, 36, TextAnchor.MiddleLeft, FontStyle.Bold);

            string taskId = definition.Id;
            GameObject claim = kit.NewButton("Claim-" + taskId, row.transform, "领取", 17, PanelKit.Pink,
                PanelKit.White, () => Claim(taskId), 18);
            PanelKit.PlaceTop(claim.GetComponent<RectTransform>(), 532, 38, 124, 56);

            RowView view = new RowView
            {
                Task = task,
                ProgressFill = fill,
                ProgressText = progress,
                ClaimButton = claim,
                ClaimLabel = PanelKit.LabelOf(claim),
                RowBackground = row.GetComponent<Image>(),
            };
            ApplyRowState(view);
            return view;
        }

        private static void ApplyRowState(RowView view)
        {
            TaskView task = view.Task;
            int target = Mathf.Max(1, task.Definition.Target);
            int progress = Mathf.Clamp(task.Progress, 0, target);
            view.ProgressFill.fillAmount = progress / (float)target;
            view.ProgressText.text = $"{progress}/{target}";

            if (task.Claimed)
            {
                view.ClaimLabel.text = "已领取";
                view.ClaimLabel.color = new Color32(196, 190, 220, 255);
                PanelKit.SetButtonState(view.ClaimButton, false, new Color32(74, 70, 104, 235));
                view.RowBackground.color = new Color32(22, 19, 58, 215);
                view.ProgressFill.color = new Color32(140, 132, 180, 255);
            }
            else if (task.Claimable)
            {
                view.ClaimLabel.text = "领取";
                view.ClaimLabel.color = PanelKit.White;
                PanelKit.SetButtonState(view.ClaimButton, true, PanelKit.Pink);
                view.RowBackground.color = new Color32(46, 27, 96, 248);
                view.ProgressFill.color = PanelKit.Pink;
            }
            else
            {
                view.ClaimLabel.text = "进行中";
                view.ClaimLabel.color = new Color32(214, 206, 236, 255);
                PanelKit.SetButtonState(view.ClaimButton, false, new Color32(88, 66, 138, 235));
                view.RowBackground.color = PanelKit.Glass;
                view.ProgressFill.color = PanelKit.Cyan;
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
            PanelKit.SetButtonState(tabDaily, true, daily ? new Color32(116, 43, 177, 252) : PanelKit.ButtonDark);
            PanelKit.SetButtonState(tabWeekly, true, daily ? PanelKit.ButtonDark : new Color32(116, 43, 177, 252));
            PanelKit.LabelOf(tabDaily).color = daily ? PanelKit.White : PanelKit.Muted;
            PanelKit.LabelOf(tabWeekly).color = daily ? PanelKit.Muted : PanelKit.White;

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
            claimAllLabel.text = claimable > 0 ? $"一键领取 ({claimable})" : "一键领取";
            PanelKit.SetButtonState(claimAllButton, claimable > 0, claimable > 0 ? PanelKit.Pink : PanelKit.Disabled);

            bool canCheckIn = !model.HasCheckedInToday;
            checkInLabel.text = canCheckIn ? $"签到 · 连续 {model.Save.CheckInDay} 天" : "今日已签到";
            PanelKit.SetButtonState(checkInButton, canCheckIn,
                canCheckIn ? new Color32(120, 62, 190, 250) : PanelKit.Disabled);
        }

        // ------------------------------------------------------------------ lifecycle

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (hintText != null) hintText.text = message;
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
