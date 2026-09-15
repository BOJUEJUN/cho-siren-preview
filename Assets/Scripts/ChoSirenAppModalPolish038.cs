using System;
using ChoSiren.Panels;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 0.3.8 secondary-UI polish: settings, generic info modals (producer profile, inbox,
    /// stage income, reset confirmation) and the deep-talk result rebuilt on the approved
    /// illustrated crystal backdrop instead of the rejected giant rounded purple panels.
    /// All object names used by tests are preserved (SettingsModal / InfoModal / DeepTalkModal,
    /// Setting-*, Toggle, Reset, Done, Primary, Close, DeepTalkDialogue,
    /// DeepTalkAffectionResult, BackToProfile) and every action/confirmation keeps its exact
    /// behavior — only the presentation changes. ChoSirenApp.cs delegates here; see
    /// /tmp/cho-parallel-modals-integration.patch.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private void OpenSettings038()
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "SettingsModal",
                "游戏设置", "系统", CloseModal, 560f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);

            SettingsRow038(body, "音乐", model.Save.MusicEnabled ? "已开启" : "已关闭", 0f, () =>
            {
                model.ToggleMusic();
                ApplyMusicRouting();
                OpenSettings038();
            });
            SettingsRow038(body, "音效", model.Save.SfxEnabled ? "已开启" : "已关闭", 78f, () =>
            {
                model.ToggleSfx();
                gameAudio.ApplySettings();
                OpenSettings038();
            });
            SettingsRow038(body, "画质", model.Save.QualityLevel == 1 ? "高清" : "流畅", 156f, () =>
            {
                model.ToggleQuality();
                OpenSettings038();
            });

            ModalPolish038Chrome.Divider(body, "SettingsDivider", 4f, 242f, width - 8f);
            Text storage = ModalPolish038Chrome.Text(body, "SettingsStorageLabel", "存档管理", 15,
                ModalPolish038Chrome.TextMuted, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(storage.rectTransform, 4f, 254f, width - 8f, 28f);

            GameObject reset = ModalPolish038Chrome.ActionButton(body, "Reset", "清除本机存档", 17,
                () => OpenInfoModal038("确认清除本机存档？",
                    "这会永久删除本机的培养、资源和通关记录。\n\n成员回到 1 级，关卡从 1-1 重新开始。\n\n关闭此窗口即可取消。",
                    "确认清除并重新开始", () =>
                    {
                        model.Reset();
                        ApplyMusicRouting();
                        ShowScreen("lobby");
                        Toast("已重新开始：成员 1 级，仅开放 1-1");
                    }), ModalPolish038ButtonStyle.Danger);
            PanelKit.PlaceTop(reset.GetComponent<RectTransform>(), 0f, 292f, width, 56f);

            GameObject done = ModalPolish038Chrome.ActionButton(body, "Done", "完成", 20,
                CloseModal, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(done.GetComponent<RectTransform>(), (width - 260f) * .5f, 372f, 260f, 58f);

            Text hint = ModalPolish038Chrome.Text(body, "SettingsHint", "设置与进度保存在本机存档中", 13,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.MiddleCenter);
            PanelKit.PlaceTop(hint.rectTransform, 0f, 452f, width, 26f);
        }

        private void SettingsRow038(Transform parent, string title, string value, float y,
            UnityEngine.Events.UnityAction action)
        {
            GameObject row = ModalPolish038Chrome.RowPlate(parent, $"Setting-{title}");
            float rowWidth = 560f * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            PanelKit.PlaceTop(row.GetComponent<RectTransform>(), 0f, y, rowWidth, 64f);
            Text label = ModalPolish038Chrome.Text(row.transform, "SettingName", title, 18,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(label.rectTransform, 20f, 10f, 150f, 44f);
            GameObject toggle = ModalPolish038Chrome.ActionButton(row.transform, "Toggle", value, 16,
                action, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(toggle.GetComponent<RectTransform>(), rowWidth - 172f, 10f, 152f, 44f);
        }

        private void OpenInfoModal038(string title, string body, string primaryLabel,
            UnityEngine.Events.UnityAction primaryAction)
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "InfoModal",
                title, "事务", CloseModal, 520f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform content = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            float height = frame.Height * (ModalPolish038Chrome.BodyBottom - ModalPolish038Chrome.BodyTop);

            Text bodyText = ModalPolish038Chrome.Text(content, "InfoBody", body, 17,
                ModalPolish038Chrome.TextMuted, FontStyle.Normal, TextAnchor.UpperLeft);
            PanelKit.PlaceTop(bodyText.rectTransform, 4f, 8f, width - 8f, height - 150f);
            bodyText.lineSpacing = 1.22f;

            ModalPolish038Chrome.Divider(content, "InfoDivider", 8f, height - 122f, width - 16f);
            UnityEngine.Events.UnityAction confirmed = () =>
            {
                CloseModal();
                primaryAction?.Invoke();
            };
            GameObject primary = ModalPolish038Chrome.ActionButton(content, "Primary",
                primaryLabel, 18, confirmed, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(primary.GetComponent<RectTransform>(), (width - 280f) * .5f,
                height - 96f, 280f, 58f);
        }

        /// <summary>v0.3.4 深入交流结果：立绘 + 羁绊文案 + 好感变化，返回原档案。</summary>
        private void OpenDeepTalkResult038(int memberIndex, int teamSlot, string dialogue,
            int affectionBefore, int affectionAfter)
        {
            MemberDefinition member = GameModel.Members[memberIndex];
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "DeepTalkModal",
                "深入交流", "羁绊时刻", CloseModal, 560f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = frame.Width * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);
            float height = frame.Height * (ModalPolish038Chrome.BodyBottom - ModalPolish038Chrome.BodyTop);

            ModalPolish038Chrome.PortraitSlot(body, "PortraitFrame",
                Resources.Load<Sprite>(member.ResourcePath), member.Id, 0f, 4f, 148f, 196f, White);

            Text name = ModalPolish038Chrome.Text(body, "DeepTalkName", member.Name, 27,
                ModalPolish038Chrome.TextWhite, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(name.rectTransform, 168f, 8f, width - 176f, 42f);
            PanelKit.EnableBestFit(name, 18);
            Text meta = ModalPolish038Chrome.Text(body, "DeepTalkMeta",
                $"{MemberRace(member, memberIndex)} · {member.Career}", 15,
                ModalPolish038Chrome.AccentPink, FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(meta.rectTransform, 168f, 54f, width - 176f, 28f);
            PanelKit.EnableBestFit(meta, 12);

            Text affectionGain = ModalPolish038Chrome.Text(body, "DeepTalkAffectionResult",
                $"好感度 {affectionBefore} → {affectionAfter}（+{affectionAfter - affectionBefore}） · 明日可再来",
                15, new Color32(111, 255, 194, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
            PanelKit.PlaceTop(affectionGain.rectTransform, 168f, 96f, width - 176f, 56f);
            PanelKit.EnableBestFit(affectionGain, 12);

            ModalPolish038Chrome.Divider(body, "DeepTalkDivider", 0f, 216f, width);
            Text dialogueText = ModalPolish038Chrome.Text(body, "DeepTalkDialogue", dialogue, 16,
                ModalPolish038Chrome.TextWhite, FontStyle.Normal, TextAnchor.UpperLeft);
            PanelKit.PlaceTop(dialogueText.rectTransform, 4f, 230f, width - 8f, height - 330f);
            dialogueText.lineSpacing = 1.25f;

            GameObject back = ModalPolish038Chrome.ActionButton(body, "BackToProfile", "返回档案", 18,
                () => OpenTeamMember(memberIndex, teamSlot), ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(back.GetComponent<RectTransform>(), (width - 280f) * .5f,
                height - 72f, 280f, 56f);
        }
    }
}
