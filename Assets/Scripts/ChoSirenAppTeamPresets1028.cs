using ChoSiren.Panels;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        /// <summary>
        /// Team preset tabs. 2026-11-07 (团队.psd): with <paramref name="chips1107"/> they sit top-left as the PSD's teal /
        /// dark chips at (<paramref name="left"/>, <paramref name="top"/>), the active one dark text on teal.
        /// </summary>
        private void BuildTeamPresetControls1028(Transform root, float left = 80f, float top = 1520f, bool chips1107 = false)
        {
            int visible = model.VisibleTeamPresetCount;
            bool canAdd = visible < GameModel.TeamPresetCount;
            int items = visible + (canAdd ? 1 : 0);
            const float gap = 8f;
            float width = chips1107 ? 132f : Mathf.Clamp((620f - gap * (items - 1)) / items, 92f, 132f);
            float height = chips1107 ? 54f : 64f;
            float x = left;
            for (int i = 0; i < visible; i++)
            {
                int slot = i;
                var preset = model.Save.TeamPresets[i];
                bool active = i == model.Save.ActiveTeamPreset;
                GameObject button = BoardButton038("TeamPreset-" + i, root,
                    new Rect(x, top, width, height), () =>
                {
                    if (active) { Toast("正在使用这支队伍"); return; }
                    if (model.SelectTeamPreset(slot, out string message)) ShowScreen("team");
                    else Toast(message);
                });
                Text label = BoardText038(button.transform, "Label", preset.Name,
                    23, chips1107 ? (active ? (Color)Brand1025.Ink : White) : active ? Cyan : White, 8, 2, width - 16, height - 4, TextAnchor.MiddleCenter);
                if (chips1107)
                {
                    Image face = button.GetComponent<Image>();
                    face.sprite = Resources.Load<Sprite>("Art/Ui1107/" + (active ? "team-tab-on" : "team-tab-off"));
                    face.color = Color.white;
                    label.fontStyle = FontStyle.Bold;
                }
                else SirenPageChrome.Tab(button, active);
                PanelKit.EnableBestFit(label, 16);
                label.supportRichText = false;
                if (active && visible > 1)
                {
                    // Delete sits only on the active team, so a stray tap cannot remove another one.
                    GameObject delete = BoardButton038("TeamPresetDelete-" + i, button.transform,
                        new Rect(width - 34, -6, 34, 34), () => OpenDeleteTeamDialog1028(slot));
                    BoardText038(delete.transform, "Cross", "×", 24, White, 0, 0, 34, 34, TextAnchor.MiddleCenter);
                }
                x += width + gap;
            }
            if (!canAdd) return;
            GameObject add = BoardButton038("TeamPresetAdd", root, new Rect(x, top, width, height), () =>
            {
                if (model.AddTeamPreset(out string message)) ShowScreen("team");
                Toast(message);
            });
            BoardText038(add.transform, "Label", "+", 34, chips1107 ? White : Cyan, 0, 0, width, height, TextAnchor.MiddleCenter);
            if (chips1107)
            {
                Image face = add.GetComponent<Image>();
                face.sprite = Resources.Load<Sprite>("Art/Ui1107/team-tab-off");
                face.color = Color.white;
            }
            else SirenPageChrome.Tab(add, false);
        }

        /// <summary>
        /// 2026-11-08 (少爷: "队伍搞下拉选择吧"): the 团队 page shows one chip with the current team; tapping it drops down every
        /// team (current one solid teal, × to delete it when there is more than one) and 「＋ 新建队伍」 while there is room.
        /// </summary>
        private void BuildTeamPresetPicker1108(Transform root, float left, float top)
        {
            const float width = 210f, height = 54f;
            var active = model.Save.TeamPresets[model.Save.ActiveTeamPreset];
            GameObject picker = BoardButton038("TeamPresetPicker", root, new Rect(left, top, width, height),
                () => ToggleTeamPresetMenu1108(root, new Rect(left, top, width, height)));
            Image face = picker.GetComponent<Image>();
            face.sprite = Resources.Load<Sprite>("Art/Ui1107/team-tab-on");
            face.color = Color.white;
            Text label = BoardText038(picker.transform, "Label", active.Name, 23, Brand1025.Ink, 14, 2, width - 58, height - 4, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.supportRichText = false;
            PanelKit.EnableBestFit(label, 16);
            BoardText038(picker.transform, "Arrow", "▼", 20, Brand1025.Ink, width - 46, 2, 30, height - 4, TextAnchor.MiddleCenter);
        }

        private void ToggleTeamPresetMenu1108(Transform root, Rect anchor)
        {
            Transform existing = root.Find("TeamPresetMenu");
            if (existing != null)
            {
                Destroy(existing.gameObject);
                Transform oldBlocker = root.Find("TeamPresetBlocker");
                if (oldBlocker != null) Destroy(oldBlocker.gameObject);
                return;
            }
            int visible = model.VisibleTeamPresetCount;
            bool canAdd = visible < GameModel.TeamPresetCount;
            GameObject blocker = NewImage("TeamPresetBlocker", root, null, new Color(0f, 0f, 0f, .16f));
            RectTransform blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = blockerRect.anchorMax = blockerRect.pivot = new Vector2(.5f, .5f);
            blockerRect.anchoredPosition = Vector2.zero;
            blockerRect.sizeDelta = new Vector2(12000f, 12000f);
            blocker.GetComponent<Image>().raycastTarget = true;
            GameObject menu = NewImage("TeamPresetMenu", root, null, new Color32(16, 10, 34, 246));
            void Close() { Destroy(menu); Destroy(blocker); }
            blocker.AddComponent<Button>().onClick.AddListener(Close);
            const float rowH = 58f, pad = 8f;
            float width = anchor.width + 40f;
            int rows = visible + (canAdd ? 1 : 0);
            PlaceTop(menu.GetComponent<RectTransform>(), anchor.x, anchor.yMax + 6f, width, pad * 2f + rowH * rows);
            menu.GetComponent<Image>().raycastTarget = true;
            Outline rim = menu.AddComponent<Outline>();
            rim.effectColor = Brand1025.Teal; rim.effectDistance = new Vector2(2f, -2f);
            for (int i = 0; i < visible; i++)
            {
                int slot = i;
                bool on = i == model.Save.ActiveTeamPreset;
                GameObject row = BoardButton038("TeamPreset-" + i, menu.transform, new Rect(pad, pad + rowH * i, width - pad * 2f, rowH - 6f), () =>
                {
                    Close();
                    if (on) return;
                    if (model.SelectTeamPreset(slot, out string message)) ShowScreen("team");
                    else Toast(message);
                });
                row.GetComponent<Image>().color = on ? (Color)Brand1025.Teal : new Color(1f, 1f, 1f, .06f);
                Text name = BoardText038(row.transform, "Label", model.Save.TeamPresets[i].Name, 22, on ? (Color)Brand1025.Ink : Color.white,
                    10, 0, width - pad * 2f - (on && visible > 1 ? 60f : 20f), rowH - 6f, TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;
                name.supportRichText = false;
                PanelKit.EnableBestFit(name, 16);
                if (on && visible > 1)
                {
                    // Delete sits only on the current team, so a stray tap cannot remove another one.
                    GameObject delete = BoardButton038("TeamPresetDelete-" + i, row.transform,
                        new Rect(width - pad * 2f - 48f, (rowH - 6f - 40f) * .5f, 40f, 40f), () => { Close(); OpenDeleteTeamDialog1028(slot); });
                    BoardText038(delete.transform, "Cross", "×", 28, Brand1025.Ink, 0, 0, 40, 40, TextAnchor.MiddleCenter);
                }
            }
            if (canAdd)
            {
                GameObject add = BoardButton038("TeamPresetAdd", menu.transform, new Rect(pad, pad + rowH * visible, width - pad * 2f, rowH - 6f), () =>
                {
                    Close();
                    if (model.AddTeamPreset(out string message)) ShowScreen("team");
                    Toast(message);
                });
                add.GetComponent<Image>().color = new Color(1f, 1f, 1f, .06f);
                BoardText038(add.transform, "Label", "＋ 新建队伍", 22, Brand1025.Teal, 0, 0, width - pad * 2f, rowH - 6f, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            }
            menu.AddComponent<DropdownPop1108>();
        }

        private void OpenDeleteTeamDialog1028(int slot)
        {
            string name = model.Save.TeamPresets[slot].Name;
            var frame = ModalPolish038Chrome.Begin(safeRoot, "DeleteTeamDialog", "删除队伍",
                $"删除「{name}」？该队伍的编队记录会一起移除，其他队伍不受影响。", null, 480f);
            var confirm = ModalPolish038Chrome.ActionButton(frame.Body, "DeleteTeam", "删除", 26, () =>
            {
                if (!model.DeleteTeamPreset(slot, out string message)) { Toast(message); Destroy(frame.Overlay); return; }
                Destroy(frame.Overlay);
                ShowScreen("team");
                Toast(message);
            }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(confirm.GetComponent<RectTransform>(), 0, 42, 420, 72);
            var cancel = ModalPolish038Chrome.ActionButton(frame.Body, "Cancel", "取消", 24,
                () => Destroy(frame.Overlay), ModalPolish038ButtonStyle.Ghost);
            PlaceTop(cancel.GetComponent<RectTransform>(), 0, 134, 420, 64);
        }

        private void OpenTeamNameDialog1028(int slot)
        {
            var frame = ModalPolish038Chrome.Begin(safeRoot, "TeamNameDialog", "队伍改名", "最多8个字", null, 520f);
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            GameObject box = ModalPolish038Chrome.RowPlate(frame.Body, "TeamNameInput");
            PlaceTop(box.GetComponent<RectTransform>(), 0, 10, width, 76);
            box.GetComponent<Image>().raycastTarget = true;
            Text text = ModalPolish038Chrome.Text(box.transform, "Value", "", 28, White, FontStyle.Normal, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform, 16, 0, -16, 0);
            InputField input = box.AddComponent<InputField>();
            input.targetGraphic = box.GetComponent<Image>();
            input.textComponent = text;
            input.characterLimit = 16;
            input.lineType = InputField.LineType.SingleLine;
            input.text = model.Save.TeamPresets[slot].Name;
            var confirm = ModalPolish038Chrome.ActionButton(frame.Body, "Confirm", "保存", 26, () =>
            {
                if (!model.RenameTeamPreset(slot, input.text, out string message)) { Toast(message); return; }
                Destroy(frame.Overlay); ShowScreen("team");
            }, ModalPolish038ButtonStyle.Primary);
            PlaceTop(confirm.GetComponent<RectTransform>(), 0, 110, width, 72);
            var cancel = ModalPolish038Chrome.ActionButton(frame.Body, "Cancel", "取消", 23,
                () => Destroy(frame.Overlay), ModalPolish038ButtonStyle.Ghost);
            PlaceTop(cancel.GetComponent<RectTransform>(), 0, 196, width, 58);
            input.ActivateInputField();
        }
    }
}
