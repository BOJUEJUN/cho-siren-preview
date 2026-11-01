using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using ChoSiren.Systems.Tactics;
using ChoSiren.Systems.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        private int equipmentMember = -1;
        private int equipmentPage;
        private string equipmentCategory = "全部";
        private bool equipmentOwnedOnly;
        private int equipmentSlotFilter = -1;
        private int lastEquipmentMember = -1;
        private int lastEquipmentAccessory = -1;
        private bool equipmentRevealSelection;
        private Action memberProfileReturn;

        /// <summary>
        /// 0.3.8 crystal modal scaffold: dim overlay + approved illustrated backdrop +
        /// centered title + CloseProgression. Returns the safe Body rect for content.
        /// Overlay name keeps the test-facing id (CurrencyModal, pickers, confirmations).
        /// </summary>
        private RectTransform ProgressionModal(string name, string title, string eyebrow, float width)
        {
            CloseModal();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, name, title, eyebrow,
                CloseModal, width, closeName: "CloseProgression");
            modalObject = frame.Overlay;
            return frame.Body;
        }

        private static float ModalBodyWidth(float panelWidth)
            => panelWidth * (ModalPolish038Chrome.BodyRight - ModalPolish038Chrome.BodyLeft);

        private void OpenTeamSlotPicker(int slot, int page = 0) => OpenOwnedMemberPicker(slot, page, false);
        private void OpenOwnedMemberPicker(int slot, int page, bool equipmentSelection)
            => OpenMemberSelection(equipmentSelection ? "equipment" : "team", slot, page);
        private void OpenCaptainPicker() => OpenMemberSelection("captain", 0, 0);
        /// <summary>Member picker for every entry (team slot / captain / replacement / accessory owner).
        /// 2026-11-01: a near-full-screen two-column card grid that scrolls — see ChoSirenAppMemberPicker1101.cs.
        /// <paramref name="page"/> is kept for existing callers; the grid has no pages.</summary>
        private void OpenMemberSelection(string purpose, int slot, int page, bool ownedOnly = true, string query = "")
            => OpenMemberSelection1101(purpose, slot, ownedOnly, query);
        private string MemberDeploymentLabel(int member)
        {
            if (!model.IsUnlocked(member)) return "未签约";
            int slot = model.Save.Team.IndexOf(member);
            return slot == 0 ? "队长" : slot > 0 ? "出战中" : "待命";
        }
        // 训练/展示专项：队长特性文案抽到 MemberCaptainTraits，本处仅保留原调用点，
        // 文案与效果不变，便于成员档案分组展示与独立测试共用同一份真实配置。
        private static string CaptainEffectCopy(string race) => MemberCaptainTraits.Describe(race);
        private void ConfirmCaptain(int member)
        {
            if (!model.IsUnlocked(member)) { Toast("请先签约该成员"); return; }
            if (model.IsInTeam(member)) { ApplyCaptain(member); return; }
            // visual-a 1101c: old captain's face → new captain's face under a crown; the sentence is behind 「?」.
            MemberDefinition next = GameModel.Members[member];
            MemberDefinition current = model.Save.Team.Count > 0 ? GameModel.Members[model.Save.Team[0]] : null;
            OpenConfirm1101(new Confirm1101
            {
                Name = "CaptainConfirmation",
                CloseName = "CloseProgression",
                Title = "任命队长",
                Icon = "crown",
                Accent = UI.VisualA1101.Gold,
                Faces = new[] { current, next },
                Note = next.Name,
                NoteColor = UI.VisualA1101.Paper,
                PrimaryName = "ConfirmCaptain",
                PrimaryLabel = "任命",
                PrimaryIcon = "crown",
                PrimaryKeepsModal = true,
                Primary = () => ApplyCaptain(member),
                CancelName = "CancelCaptain",
                CancelLabel = "返回",
                Cancel = OpenCaptainPicker,
                Help = new[]
                {
                    ("crown", $"{next.Name}将替换{(current != null ? current.Name : "空位")}的队长位置"),
                    ("team", "原成员转为待命，等级和装备保留；其他队员不变"),
                },
            });
        }
        private void ApplyCaptain(int member)
        {
            model.AppointCaptain(member, out string message);
            ShowScreen("team"); Toast(message);
        }
        private void OpenTeamReplacement(int member)
            => OpenMemberSelection("replacement", member, 0);
        // visual-a 1101c: badge + balance + picture strips, see ChoSirenApp.VisualA1101.cs.
        private void OpenCurrency(string currency) => OpenCurrency1101(currency);
        private void OpenCurrencyConfirmation(string currency, EconomyExchangeQuote quote)
            => OpenCurrencyConfirmation1101(currency, quote);
        private void BuildPersonalEquipment() => BuildInventory();

        private void CycleEquipmentMember(int direction)
        {
            var owned = model.Save.UnlockedMembers;
            equipmentMember = owned[(owned.IndexOf(equipmentMember) + direction + owned.Count) % owned.Count];
            selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(equipmentMember));
            equipmentRevealSelection = true;
            ShowScreen("accessory");
        }

        private void RefreshEquipmentCollection()
        {
            ScrollRect oldScroll = contentRoot.GetComponentInChildren<ScrollRect>();
            float position = oldScroll != null ? oldScroll.verticalNormalizedPosition : 1f;
            ShowScreen("accessory");
            Canvas.ForceUpdateCanvases();
            ScrollRect newScroll = contentRoot.GetComponentInChildren<ScrollRect>();
            if (newScroll != null) newScroll.verticalNormalizedPosition = Mathf.Clamp01(position);
        }
        private Text FlowText(Transform parent, string name, string value, int size, float x, float y, float width, float height, Color? color = null)
        {
            Text text = NewPlacedText(parent, value, size, color ?? White, x, y, width, height, TextAnchor.MiddleLeft);
            text.name = name;
            return text;
        }
        private GameObject FlowButton(Transform parent, string name, string value, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            GameObject button = NewButton(name, parent, value, 18, Purple, White, action);
            PlaceTop(button.GetComponent<RectTransform>(), x, y, width, height);
            return button;
        }
    }
}
