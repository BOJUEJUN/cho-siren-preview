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
        private void BuildTeamPresetControls1028(Transform root, float left = 80f, float top = 1520f, bool chips1107 = false,
            float stripWidth = 0f)
        {
            int visible = model.VisibleTeamPresetCount;
            bool canAdd = visible < GameModel.TeamPresetCount;
            int items = visible + (canAdd ? 1 : 0);
            const float gap = 8f;
            float width = chips1107 ? 132f : Mathf.Clamp((620f - gap * (items - 1)) / items, 92f, 132f);
            float height = chips1107 ? 54f : 64f;
            float x = left;
            RectTransform strip = null, viewport = null;
            if (chips1107 && stripWidth > 0f && items * (width + gap) - gap > stripWidth)
            {
                // 2026-11-08 (少爷: "队伍加多了会有UI遮挡"): more chips than fit left of the pig / banner scroll sideways in
                // their own clipped strip (a slimmer chip so the next one peeks out), instead of running under the banner.
                width = 112f;
                const float pad = 10f;
                var view = NewImage("TeamPresetStrip", root, null, Color.clear);
                viewport = view.GetComponent<RectTransform>();
                PlaceTop(viewport, left, top - pad, stripWidth, height + pad * 2f);
                view.GetComponent<Image>().raycastTarget = true;
                view.AddComponent<RectMask2D>().softness = new Vector2Int(14, 0);
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(viewport, false);
                strip = (RectTransform)content.transform;
                float contentWidth = items * (width + gap) - gap;
                PlaceTop(strip, 0f, 0f, contentWidth, height + pad * 2f);
                var scroll = view.AddComponent<ScrollRect>();
                scroll.content = strip; scroll.viewport = viewport;
                scroll.horizontal = true; scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                scroll.scrollSensitivity = 18f;
                float activeX = model.Save.ActiveTeamPreset * (width + gap);
                float target = Mathf.Clamp(activeX - (stripWidth - width) * .5f, 0f, Mathf.Max(0f, contentWidth - stripWidth));
                strip.anchoredPosition = new Vector2(-target, 0f);
                root = strip; x = 0f; top = pad;
            }
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
