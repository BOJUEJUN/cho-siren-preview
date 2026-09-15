using System;
using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    public sealed partial class ChoSirenApp
    {
        // Equipment member selector state: which deployment bucket the grid shows.
        private static readonly string[] EquipmentPolish038StateLabels = { "全部", "出战中", "待命", "未签约" };

        /// <summary>
        /// Crystalline equipment member selector replacing the generic rounded progression
        /// panel: angular shard card, portrait thumbnails, visible names, ownership/state
        /// filters, search and paging. Selection semantics are unchanged — picking an
        /// unlocked member only retargets the equipment board, it never edits the team.
        /// </summary>
        private void OpenEquipmentMemberSelector(int page = 0, bool ownedOnly = true, string query = "", int stateFilter = 0)
        {
            CloseModal();
            stateFilter = Mathf.Clamp(stateFilter, 0, EquipmentPolish038StateLabels.Length - 1);
            GameObject overlay = NewImage("EquipmentPolish038Selector", safeRoot, null, new Color32(3, 4, 20, 224));
            Stretch(overlay.GetComponent<RectTransform>());
            overlay.GetComponent<Image>().raycastTarget = true;
            modalObject = overlay;

            GameObject card = NewImage("EquipmentPolish038Panel", overlay.transform,
                EquipmentPolish038Sprites.AngularPanel(), White);
            Image cardImage = card.GetComponent<Image>();
            cardImage.type = Image.Type.Sliced;
            cardImage.raycastTarget = true;
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(.5f, .5f);
            cardRect.sizeDelta = new Vector2(640, 880);

            GameObject accent = NewImage("EquipmentPolish038TitleShard", card.transform,
                EquipmentPolish038Sprites.Chip(), Cyan);
            PlaceTop(accent.GetComponent<RectTransform>(), 28, 30, 16, 16);
            accent.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, 0, 45f);
            accent.GetComponent<Image>().raycastTarget = false;
            NewPlacedText(card.transform, "选择饰品管理角色", 25, White, 54, 22, 370, 42, TextAnchor.MiddleLeft).name = "Title";
            NewPlacedText(card.transform, $"已拥有 {model.Save.UnlockedMembers.Count}/{GameModel.Members.Length}", 14, Muted,
                424, 30, 132, 30, TextAnchor.MiddleRight).name = "OwnedCount";
            EquipmentPolish038Button(card.transform, "EquipmentPolish038Close", "×",
                566, 20, 52, 52, CloseModal, 24);
            NewPlacedText(card.transform, "仅切换管理对象，不改变编队或转移装备。未签约角色只能查看。", 14, Muted,
                30, 70, 584, 40, TextAnchor.MiddleLeft).name = "Help";

            bool owned = ownedOnly;
            string q = query;
            int state = stateFilter;
            GameObject ownedChip = EquipmentPolish038Button(card.transform, "EquipmentPolish038Owned",
                ownedOnly ? "已拥有 ✓" : "全部成员", 28, 124, 160, 48,
                () => OpenEquipmentMemberSelector(0, !owned, q, state), 16);
            ownedChip.GetComponentInChildren<Text>().color = ownedOnly ? Cyan : White;
            EquipmentPolish038Button(card.transform, "EquipmentPolish038State",
                "状态：" + EquipmentPolish038StateLabels[stateFilter], 196, 124, 168, 48,
                () => OpenEquipmentMemberSelector(0, owned, q, (state + 1) % EquipmentPolish038StateLabels.Length), 15);

            GameObject search = NewImage("EquipmentPolish038Search", card.transform,
                EquipmentPolish038Sprites.Chip(), White);
            PlaceTop(search.GetComponent<RectTransform>(), 372, 124, 240, 48);
            Image searchImage = search.GetComponent<Image>();
            searchImage.type = Image.Type.Sliced;
            searchImage.raycastTarget = true;
            Text placeholder = NewPlacedText(search.transform, "搜索角色 · 回车", 14, Muted, 14, 0, 212, 46, TextAnchor.MiddleLeft);
            placeholder.name = "Placeholder";
            Text value = NewPlacedText(search.transform, query, 15, White, 14, 0, 212, 46, TextAnchor.MiddleLeft);
            value.name = "Value";
            InputField input = search.AddComponent<InputField>();
            input.targetGraphic = searchImage;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.characterLimit = 12;
            input.text = query;
            input.onEndEdit.AddListener(text =>
            {
                string normalized = (text ?? "").Trim();
                if (normalized != q) OpenEquipmentMemberSelector(0, owned, normalized, state);
            });

            MemberRosterPage roster = MemberRosterPagination.Build(GameModel.Members.Length, page, index =>
            {
                MemberDefinition candidate = GameModel.Members[index];
                if (!MemberRosterVisibility.MatchesRosterFilter(model.IsUnlocked(index), candidate.Name,
                        candidate.Career, candidate.Race, ownedOnly, string.Empty, string.Empty, query))
                    return false;
                if (stateFilter == 1) return model.IsInTeam(index);
                if (stateFilter == 2) return model.IsUnlocked(index) && !model.IsInTeam(index);
                if (stateFilter == 3) return !model.IsUnlocked(index);
                return true;
            }, 8, index => model.IsUnlocked(index) ? 0 : 1);
            page = roster.PageIndex;

            for (int slot = 0; slot < roster.VisibleCount; slot++)
            {
                int member = roster.SourceIndexAt(slot);
                MemberDefinition m = GameModel.Members[member];
                bool unlocked = model.IsUnlocked(member);
                GameObject cell = EquipmentPolish038Button(card.transform, "EquipmentPolish038Member-" + member, string.Empty,
                    28 + (slot % 2) * 294, 186 + (slot / 2) * 158, 284, 150, () =>
                    {
                        if (!unlocked)
                        {
                            OpenMember(member);
                            memberProfileReturn = () => OpenEquipmentMemberSelector(page, ownedOnly, query, stateFilter);
                            return;
                        }
                        equipmentMember = member;
                        selectedAccessoryIndex = Math.Max(0, model.EquippedAccessoryFor(member));
                        equipmentRevealSelection = true;
                        ShowScreen("accessory");
                    });
                cell.GetComponent<Image>().sprite = EquipmentPolish038Sprites.MemberCard();

                GameObject thumbFrame = NewImage("EquipmentPolish038ThumbFrame", cell.transform, null, Color.clear);
                PlaceTop(thumbFrame.GetComponent<RectTransform>(), 10, 12, 76, 104);
                thumbFrame.GetComponent<Image>().raycastTarget = false;
                thumbFrame.AddComponent<RectMask2D>();
                GameObject thumb = NewImage("Thumb", thumbFrame.transform,
                    unlocked ? Resources.Load<Sprite>(m.ResourcePath) : LockedSilhouetteSprite(),
                    unlocked ? White : LockedSilhouetteTint);
                thumb.GetComponent<Image>().preserveAspect = true;
                thumb.GetComponent<Image>().raycastTarget = false;
                if (unlocked)
                    PanelKit.FrameBustPortrait(thumb.GetComponent<Image>(), thumbFrame.GetComponent<RectTransform>(), m.Id);
                else
                    Stretch(thumb.GetComponent<RectTransform>(), 0, 0, 0, 0);
                GameObject thumbRing = NewImage("EquipmentPolish038ThumbRing", thumbFrame.transform,
                    EquipmentPolish038Sprites.ThumbRing(), White);
                Stretch(thumbRing.GetComponent<RectTransform>(), 0, 0, 0, 0);
                thumbRing.GetComponent<Image>().raycastTarget = false;

                NewPlacedText(cell.transform, unlocked ? m.Name : MemberRosterVisibility.LockedName, 19,
                    unlocked ? White : Muted, 96, 10, 182, 30, TextAnchor.MiddleLeft).name = "Name";
                NewPlacedText(cell.transform, unlocked ? $"{m.Career} · Lv.{model.LevelOf(member)}" : MemberRosterVisibility.LockedCareer,
                    13, unlocked ? Pink : Muted, 96, 42, 182, 22, TextAnchor.MiddleLeft).name = "Sub";
                NewPlacedText(cell.transform, unlocked ? $"战力 {model.PowerOf(member):N0}" : "签约后查看形象与属性",
                    14, unlocked ? Cyan : Muted, 96, 66, 182, 22, TextAnchor.MiddleLeft).name = "Power";
                int teamSlot = model.Save.Team.IndexOf(member);
                NewPlacedText(cell.transform, MemberDeploymentLabel(member), 13,
                    !unlocked ? Muted : teamSlot == 0 ? new Color32(255, 205, 96, 255) : teamSlot > 0 ? Cyan : White,
                    96, 94, 150, 24, TextAnchor.MiddleLeft).name = "State";
                if (member == equipmentMember)
                {
                    GameObject badge = NewImage("EquipmentPolish038Current", cell.transform,
                        EquipmentPolish038Sprites.Chip(), new Color32(46, 16, 44, 255));
                    PlaceTop(badge.GetComponent<RectTransform>(), 208, 10, 66, 28);
                    Image badgeImage = badge.GetComponent<Image>();
                    badgeImage.type = Image.Type.Sliced;
                    badgeImage.raycastTarget = false;
                    NewPlacedText(badge.transform, "管理中", 12, Pink, 0, 0, 66, 28, TextAnchor.MiddleCenter).name = "Label";
                }
            }
            if (roster.IsEmpty)
                NewPlacedText(card.transform, "没有符合条件的角色，可调整筛选或搜索。", 18, Muted,
                    40, 400, 560, 60, TextAnchor.MiddleCenter).name = "Empty";

            EquipmentPolish038Button(card.transform, "EquipmentPolish038Prev", "上一页", 28, 820, 160, 48,
                () => OpenEquipmentMemberSelector(page - 1, owned, q, state), 17)
                .GetComponent<Button>().interactable = roster.HasPrevious;
            NewPlacedText(card.transform, $"{(roster.PageCount == 0 ? 0 : page + 1)}/{roster.PageCount}", 20, White,
                240, 820, 160, 48, TextAnchor.MiddleCenter).name = "Page";
            EquipmentPolish038Button(card.transform, "EquipmentPolish038Next", "下一页", 452, 820, 160, 48,
                () => OpenEquipmentMemberSelector(page + 1, owned, q, state), 17)
                .GetComponent<Button>().interactable = roster.HasNext;
        }

        /// <summary>Angular chip button matching the crystalline selector styling.</summary>
        private GameObject EquipmentPolish038Button(Transform parent, string name, string label,
            float x, float y, float width, float height, UnityEngine.Events.UnityAction action, int fontSize = 16)
        {
            GameObject result = NewImage(name, parent, EquipmentPolish038Sprites.Chip(), White);
            PlaceTop(result.GetComponent<RectTransform>(), x, y, width, height);
            Image image = result.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = result.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                action?.Invoke();
                ResumeMediaAfterUserGesture();
            });
            Text text = NewText("Label", result.transform, label, fontSize, White, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6, 4, -6, -4);
            text.raycastTarget = false;
            AttachLobbyHotspotFeedback(result, LobbyHotspotFeedback.VisualKind.Entry);
            return result;
        }
    }
}
