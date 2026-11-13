using System.Linq;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-12 七色功能骰 entry (同事: "主界面多做一个 UI 入口，进去选骰子；界面先随意一点，先把功能做好"): a die button on the
    /// homepage's right column under the eye (the corner the colleague circled), shown once 第三章 is open. The page lists
    /// the seven colours with count, rarity and team skill; tap a colour to carry it, tap a carried die to put it back.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private GameObject psdRelicEntry1111;

        private void BuildRelicEntry1111(Transform root, float scale, float layoutWidth, float layoutHeight)
        {
            if (root == null) return;
            bool locked = !model.RelicDiceUnlocked;
            const float x = 3540f, y = 2110f, w = 440f, h = 430f;
            GameObject button = NewImage("RelicDiceEntry1111", root, null, Color.clear);
            PlaceTop(button.GetComponent<RectTransform>(),
                x * scale - PsdRightInset((x + w * .5f) / layoutWidth), y * scale + PsdSafeShift((y + h * .5f) / layoutHeight),
                w * scale, h * scale);
            var hit = button.GetComponent<Image>();
            hit.raycastTarget = true;
            var face = NewImage("RelicDiceFace", button.transform, Resources.Load<Sprite>(RelicDice1111.SpritePath(RelicDice1111.Chaos, 5)),
                Color.white);
            face.GetComponent<Image>().preserveAspect = true; face.GetComponent<Image>().raycastTarget = false;
            PlaceTop(face.GetComponent<RectTransform>(), 14, 0, w * scale - 28, w * scale - 28);
            Text label = NewText("RelicDiceLabel", button.transform, "功能骰", 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            PlaceTop(label.rectTransform, -10, w * scale - 30, w * scale + 20, 24);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .9f);
            if (locked)
            {
                // Before 第三章: the die is dimmed with a lock badge and a "2-20 解锁" caption under the name.
                face.GetComponent<Image>().color = new Color(.62f, .62f, .7f, 1f);
                GameObject badge = NewImage("RelicLockBadge", button.transform, Resources.Load<Sprite>("Art/VisualA1101/icon-lock"), Color.white);
                badge.GetComponent<Image>().preserveAspect = true; badge.GetComponent<Image>().raycastTarget = false;
                PlaceTop(badge.GetComponent<RectTransform>(), w * scale - 34, 0, 26, 26);
                Text unlock = NewText("RelicUnlockCaption", button.transform, "2-20 解锁", 11, new Color32(255, 214, 102, 255),
                    FontStyle.Bold, TextAnchor.MiddleCenter);
                PlaceTop(unlock.rectTransform, -10, w * scale - 8, w * scale + 20, 16);
                unlock.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .9f);
            }
            var b = button.AddComponent<Button>();
            b.targetGraphic = hit; b.transition = Selectable.Transition.None;
            b.onClick.AddListener(OpenRelicDice1111);
            button.AddComponent<LobbyHotspotFeedback>().Configure(LobbyHotspotFeedback.VisualKind.Settings);
            psdMotion?.RegisterUi(button);
            psdRelicEntry1111 = button;
        }

        private void OpenRelicDice1111()
        {
            CloseModal();
            model.EnsureRelicStarter();
            ModalPolish038Frame frame = ModalPolish038Chrome.Begin(safeRoot, "RelicDiceModal1111", "功能骰", "第三章",
                CloseModal, 640f, closeName: "Close");
            modalObject = frame.Overlay;
            RectTransform body = frame.Body;
            float width = BodyWidth1101(frame);
            Text tip = NewText("RelicTip", body, $"带进第三章战斗，每颗一场用一次，每回合最多用 {RelicDice1111.UsesPerRound} 次。输了每颗有一半概率碎掉。",
                15, new Color32(171, 181, 213, 255), FontStyle.Normal, TextAnchor.UpperLeft);
            tip.horizontalOverflow = HorizontalWrapMode.Wrap; tip.verticalOverflow = VerticalWrapMode.Overflow;
            PlaceTop(tip.rectTransform, 0, 0, width, 44);
            float top = 0f;
            if (!model.RelicDiceUnlocked)
            {
                // 2026-11-12 (少爷: 开放但要提示怎么解锁): a banner says what unlocks the dice; picking them already works.
                top = 52f;
                GameObject banner = NewImage("RelicLockBanner", body, null, new Color32(255, 196, 64, 38));
                PlaceTop(banner.GetComponent<RectTransform>(), 0, 46, width, 46);
                GameObject lockIcon = NewImage("RelicLockIcon", banner.transform, Resources.Load<Sprite>("Art/VisualA1101/icon-lock"), Color.white);
                lockIcon.GetComponent<Image>().preserveAspect = true; lockIcon.GetComponent<Image>().raycastTarget = false;
                PlaceTop(lockIcon.GetComponent<RectTransform>(), 10, 9, 28, 28);
                Text hint = NewText("RelicLockHint", banner.transform, "未解锁：" + GameModel.RelicUnlockHint1112 + "。现在可以先挑好。",
                    14, new Color32(255, 214, 102, 255), FontStyle.Bold, TextAnchor.MiddleLeft);
                hint.horizontalOverflow = HorizontalWrapMode.Wrap; hint.verticalOverflow = VerticalWrapMode.Overflow;
                PlaceTop(hint.rectTransform, 46, 0, width - 54, 46);
            }

            var carried = model.CarriedRelics;
            float slotW = (width - 24) / RelicDice1111.CarrySlots;
            for (int slot = 0; slot < RelicDice1111.CarrySlots; slot++)
            {
                int captured = slot;
                string id = slot < carried.Count ? carried[slot] : null;
                string caption = id == null ? "空位" : RelicDice1111.ColorName(id) + "·" + RelicDice1111.Name(id);
                GameObject chip = NewModalPunkButton("RelicCarry-" + slot, body, caption, 17,
                    () => { if (model.UncarryRelic(captured)) OpenRelicDice1111(); }, id == null ? Muted : Cyan);
                PlaceTop(chip.GetComponent<RectTransform>(), slot * (slotW + 12), 50 + top, slotW, 54);
                if (id != null) TintRelic1111(chip.transform, id, 8, 11, 32);
            }

            float y = 120f + top;
            float rowH = Mathf.Min(64f, (frame.Height * .62f - y) / RelicDice1111.All.Length);
            foreach (string id in RelicDice1111.All)
            {
                int owned = model.RelicCount(id);
                int left = owned - carried.Count(x => x == id);
                string captured = id;
                GameObject row = NewModalPunkButton("RelicRow-" + id, body, string.Empty, 15, () =>
                {
                    if (!model.CarryRelic(captured, out string message)) ShowToast1111(message);
                    else OpenRelicDice1111();
                }, owned > 0 ? Cyan : Muted);
                PlaceTop(row.GetComponent<RectTransform>(), 0, y, width, rowH - 6);
                TintRelic1111(row.transform, id, 8, (rowH - 6 - 36) * .5f, 36);
                Text name = NewText("RelicName", row.transform,
                    $"{RelicDice1111.ColorName(id)}·{RelicDice1111.Name(id)}  {RelicDice1111.Rarity(id)}   ×{owned}" + (owned > 0 && left <= 0 ? "（已带）" : string.Empty),
                    16, owned > 0 ? Color.white : (Color)Muted, FontStyle.Bold, TextAnchor.UpperLeft);
                name.verticalOverflow = VerticalWrapMode.Overflow;
                PlaceTop(name.rectTransform, 54, 3, width - 64, 22);
                Text effect = NewText("RelicEffect", row.transform, RelicDice1111.Effect(id), 12,
                    new Color32(171, 181, 213, 255), FontStyle.Normal, TextAnchor.UpperLeft);
                effect.horizontalOverflow = HorizontalWrapMode.Wrap; effect.verticalOverflow = VerticalWrapMode.Overflow;
                PlaceTop(effect.rectTransform, 54, 26, width - 64, rowH - 30);
                y += rowH;
            }
        }

        private void TintRelic1111(Transform parent, string id, float x, float y, float size)
        {
            GameObject die = NewImage("RelicDie", parent, Resources.Load<Sprite>(RelicDice1111.SpritePath(id, 6)), Color.white);
            die.GetComponent<Image>().preserveAspect = true; die.GetComponent<Image>().raycastTarget = false;
            PlaceTop(die.GetComponent<RectTransform>(), x, y, size, size);
        }

        private void ShowToast1111(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Toast(message);
        }
    }
}
