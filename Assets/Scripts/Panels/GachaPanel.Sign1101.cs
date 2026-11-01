using System.Collections.Generic;
using ChoSiren.Systems.Presentation;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-01 (visual-b, 赵金 1101c): the signing confirm / signed cards rebuilt around the
    /// candidate. A large half-body portrait (cropped through <see cref="PortraitFace1101"/>) fills the
    /// right of the card; the left column is name + race badge + role chip + battle-position chip and
    /// the diamond price; the bottom band shows the wallet as "300 → 180" and two icon buttons. The old
    /// explanatory sentences live behind the 「?」.
    /// </summary>
    public sealed partial class GachaPanel
    {
        private const float SignCardW = 620f, SignCardH = 860f;

        private static readonly string[] SignHelpLines =
        {
            "签约后成员进入「成员」页，可编队、练习",
            "每 7 天一期，按预估月薪扣星光币",
            "线上：报价低、风险高；线下：报价高、更稳",
        };

        private void ShowSignConfirm1101(int memberIndex, int cost)
        {
            CloseSignModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            ModalPolish038Frame frame = kit.NewModal038(transform, "SignConfirmOverlay",
                "签约", null, CloseSignModal, SignCardW, "SignModalClose", false);
            frame.Panel.gameObject.name = "SignConfirmCard";
            signModal = frame.Overlay;
            RectTransform card = SignCardFrame1101(frame);

            SignHero1101(card, member, memberIndex, out float columnBottom);
            int balance = model.Save.Diamonds;
            bool affordable = balance >= cost;

            // Price: big diamond + number, the one thing this card is about.
            RectTransform price = VisualB1101.Rect("SignPrice", card);
            PanelKit.PlaceTop(price, 40f, columnBottom, 220f, 70f);
            Image gem = VisualB1101.Img("SignCostIcon", price, PanelKit.CurrencyIcon("diamond"), Color.white);
            gem.preserveAspect = true;
            PanelKit.PlaceTop(gem.rectTransform, 0f, 6f, 58f, 58f);
            Text costText = VisualB1101.Label(price, "SignConfirmCost", cost.ToString("N0"), 54,
                affordable ? VisualB1101.Teal : VisualB1101.Danger, 66f, 0f, 160f, 70f, TextAnchor.MiddleLeft, 2f);
            VisualB1101.Pop(price, .14f, .7f);

            // Wallet before → after (or the shortfall), and the running salary.
            const float bandY = 640f;
            Image band = VisualB1101.Img("SignWalletBand", card, PunkUi.SlantFill, new Color32(22, 12, 42, 235));
            PanelKit.PlaceTop(band.rectTransform, 30f, bandY - 8f, SignCardW - 60f, 60f);
            RectTransform wallet;
            if (affordable)
                wallet = VisualB1101.Delta(card, "SignConfirmBalance", PanelKit.CurrencyIcon("diamond"), balance,
                    balance - cost, 48f, bandY, 44f, VisualB1101.Teal, 26);
            else
            {
                wallet = VisualB1101.Rect("SignConfirmBalance", card);
                PanelKit.PlaceTop(wallet, 48f, bandY, 300f, 44f);
                Image have = VisualB1101.Img("Currency", wallet, PanelKit.CurrencyIcon("diamond"), new Color(1f, 1f, 1f, .7f));
                have.preserveAspect = true;
                PanelKit.PlaceTop(have.rectTransform, 0f, 4f, 36f, 36f);
                Text own = VisualB1101.Label(wallet, "From", balance.ToString("N0"), 26, VisualB1101.Danger, 42f, 0f, 120f, 44f);
                own.rectTransform.sizeDelta = new Vector2(own.preferredWidth + 2f, 44f);
                VisualB1101.Chip(wallet, "SignShortfall", "lock", "差 " + (cost - balance).ToString("N0"),
                    VisualB1101.Danger, 52f + own.preferredWidth, 4f, 36f, out _);
            }
            VisualB1101.Pop(wallet, .22f, .9f);
            RectTransform salary = VisualB1101.Chip(card, "SignSalary", "coin",
                model.EstimatedMonthlySalary(memberIndex).ToString("N0") + "/期", VisualB1101.Gold,
                0f, bandY + 4f, 36f, out _);
            salary.anchoredPosition = new Vector2(SignCardW - 48f - salary.sizeDelta.x, salary.anchoredPosition.y);

            // Actions: icon + one short word each.
            const float buttonY = 740f;
            GameObject cancel = kit.NewModal038Button(card, "SignCancel", "再想想", 22,
                CloseSignModal, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(cancel.GetComponent<RectTransform>(), 40f, buttonY, 220f, 76f);
            GameObject confirm = kit.NewModal038Button(card, "SignConfirm", affordable ? "签约" : "星钻不足", 26,
                () => ConfirmSign(memberIndex, cost), ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(confirm.GetComponent<RectTransform>(), 280f, buttonY, SignCardW - 320f, 76f);
            ModalPolish038Chrome.SetActionEnabled(confirm, affordable);
            SirenStickerUi.SkinButton(cancel);
            SirenStickerUi.SkinButton(confirm, true);
            VisualB1101.AddButtonIcon(cancel, "close");
            VisualB1101.AddButtonIcon(confirm, affordable ? "signpen" : "lock");

            VisualB1101.HelpButton(card, "SignHelp", SignCardW - 152f, 44f, 40f, "签约说明", SignHelpLines);
            VisualB1101.Sfx(affordable ? "orb-chime" : "debuff", .55f);
        }

        private void ShowSignedCard1101(int memberIndex)
        {
            CloseSignModal();
            MemberDefinition member = GameModel.Members[memberIndex];
            ModalPolish038Frame frame = kit.NewModal038(transform, "SignResultOverlay",
                "签约成功", null, CloseSignModal, SignCardW, "SignModalClose", false);
            frame.Panel.gameObject.name = "SignResultCard";
            signModal = frame.Overlay;
            RectTransform card = SignCardFrame1101(frame);
            SignHero1101(card, member, memberIndex, out float columnBottom, "SignResultName");

            // Torn-paper stamp slapped on the portrait.
            RectTransform stamp = VisualB1101.Rect("SignResultStamp", card);
            PanelKit.PlaceTop(stamp, SignCardW - 196f, 440f, 150f, 150f);
            Image torn = VisualB1101.Img("Paper", stamp, VisualB1101.TornBadge(), new Color32(255, 46, 147, 240));
            PanelKit.Stretch(torn.rectTransform);
            VisualB1101.NeonIcon(stamp, "Check", "check", Color.white, 34f, 30f, 82f, false);
            stamp.localEulerAngles = new Vector3(0f, 0f, -12f);
            VisualB1101.Pop(stamp, .25f, 1.6f);

            // Starting level as a rank badge (the number is what matters).
            RectTransform rank = VisualB1101.Rect("SignResultLevel", card);
            PanelKit.PlaceTop(rank, 40f, columnBottom, 220f, 70f);
            VisualB1101.NeonIcon(rank, "Icon", "rank", VisualB1101.Gold, 0f, 8f, 54f);
            VisualB1101.Label(rank, "Lv", "Lv.", 24, VisualB1101.Muted, 64f, 18f, 44f, 40f);
            VisualB1101.Label(rank, "Value", model.LevelOf(memberIndex).ToString(), 52, VisualB1101.Gold, 108f, 0f, 120f, 70f,
                TextAnchor.MiddleLeft, 2f);
            VisualB1101.Pop(rank, .14f, .7f);

            const float bandY = 640f;
            Image band = VisualB1101.Img("SignWalletBand", card, PunkUi.SlantFill, new Color32(22, 12, 42, 235));
            PanelKit.PlaceTop(band.rectTransform, 30f, bandY - 8f, SignCardW - 60f, 60f);
            VisualB1101.Chip(card, "SignResultHint", "team", "已加入", VisualB1101.Teal, 48f, bandY + 4f, 36f, out _);
            RectTransform salary = VisualB1101.Chip(card, "SignSalary", "coin",
                model.EstimatedMonthlySalary(memberIndex).ToString("N0") + "/期", VisualB1101.Gold, 0f, bandY + 4f, 36f, out _);
            salary.anchoredPosition = new Vector2(SignCardW - 48f - salary.sizeDelta.x, salary.anchoredPosition.y);

            const float buttonY = 740f;
            GameObject stay = kit.NewModal038Button(card, "SignResultStay", "继续", 22,
                CloseSignModal, ModalPolish038ButtonStyle.Secondary);
            PanelKit.PlaceTop(stay.GetComponent<RectTransform>(), 40f, buttonY, 220f, 76f);
            GameObject view = kit.NewModal038Button(card, "SignResultProfile", "档案", 26, () =>
            {
                CloseSignModal();
                onSigned?.Invoke(memberIndex);
            }, ModalPolish038ButtonStyle.Primary);
            PanelKit.PlaceTop(view.GetComponent<RectTransform>(), 280f, buttonY, SignCardW - 320f, 76f);
            SirenStickerUi.SkinButton(stay);
            SirenStickerUi.SkinButton(view, true);
            VisualB1101.AddButtonIcon(stay, "mic");
            VisualB1101.AddButtonIcon(view, "doc");
            VisualB1101.HelpButton(card, "SignHelp", SignCardW - 152f, 44f, 40f, "签约说明", SignHelpLines);
            ModalPolish038Chrome.PlaySuccess();
        }

        /// <summary>Resizes the shared modal to the taller character card; content goes on the panel itself.</summary>
        private static RectTransform SignCardFrame1101(ModalPolish038Frame frame)
        {
            frame.Width = SignCardW;
            frame.Height = SignCardH;
            frame.Panel.sizeDelta = new Vector2(SignCardW, SignCardH);
            Transform shadow = frame.Overlay.transform.Find("PanelShadow");
            if (shadow != null) ((RectTransform)shadow).sizeDelta = new Vector2(SignCardW, SignCardH);
            if (frame.CloseButton != null)
                PanelKit.PlaceTop(frame.CloseButton.GetComponent<RectTransform>(), SignCardW - 96f, 40f, 56f, 46f);
            frame.Body.gameObject.SetActive(false);
            return frame.Panel;
        }

        /// <summary>
        /// Portrait stage on the right (behind the title) and the identity column on the left:
        /// name, race badge, role chip, battle-position chip. Returns the column's bottom edge.
        /// </summary>
        private void SignHero1101(RectTransform card, MemberDefinition member, int memberIndex,
            out float columnBottom, string nameNode = "SignConfirmName")
        {
            string race = InterviewShortRace(member, memberIndex);
            string career = InterviewCareer(member, memberIndex);
            Color raceColor = VisualB1101.RaceColor(race);

            // ---- portrait window (first sibling: under the title and close)
            RectTransform stage = VisualB1101.Rect("CandidatePortraitSlot", card);
            PanelKit.PlaceTop(stage, 196f, 14f, SignCardW - 210f, 606f);
            stage.SetSiblingIndex(0);
            stage.gameObject.AddComponent<RectMask2D>();
            Image glow = VisualB1101.Img("Glow", stage, VisualB1101.Glow(), new Color(raceColor.r, raceColor.g, raceColor.b, .55f));
            PanelKit.PlaceTop(glow.rectTransform, 20f, 30f, 420f, 520f);
            Image beam = VisualB1101.Img("Beam", stage, PunkUi.SlantFill, new Color(raceColor.r, raceColor.g, raceColor.b, .18f));
            PanelKit.PlaceTop(beam.rectTransform, 210f, -40f, 90f, 700f);
            beam.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
            Image portrait = VisualB1101.Img("Portrait", stage, CandidateSprite(member), Color.white);
            PortraitFace1101.Apply(portrait, stage, member.Id, PortraitFace1101.Framing.Half);
            Image left = VisualB1101.Img("FadeLeft", stage, VisualB1101.Fade(false), new Color32(12, 6, 24, 255));
            left.rectTransform.anchorMin = new Vector2(0f, 0f);
            left.rectTransform.anchorMax = new Vector2(0f, 1f);
            left.rectTransform.pivot = new Vector2(0f, .5f);
            left.rectTransform.sizeDelta = new Vector2(170f, 0f);
            left.rectTransform.anchoredPosition = Vector2.zero;
            Image bottom = VisualB1101.Img("FadeBottom", stage, VisualB1101.Fade(true), new Color32(12, 6, 24, 255));
            bottom.rectTransform.anchorMin = new Vector2(0f, 0f);
            bottom.rectTransform.anchorMax = new Vector2(1f, 0f);
            bottom.rectTransform.pivot = new Vector2(.5f, 0f);
            bottom.rectTransform.sizeDelta = new Vector2(0f, 170f);
            bottom.rectTransform.anchoredPosition = Vector2.zero;
            VisualB1101.Pop(stage, 0f, .96f);

            // ---- identity column
            const float x = 40f;
            Text name = VisualB1101.Label(card, nameNode, member.Name, 52, PanelKit.White, x, 40f, 300f, 72f,
                TextAnchor.MiddleLeft, 2f);
            SirenStickerUi.SkinTitle(name, true);
            float y = 130f;
            Sprite raceIcon = VisualB1101.RaceIcon(race);
            if (raceIcon != null)
            {
                Image badge = VisualB1101.Img("SignRaceIcon", card, raceIcon, Color.white);
                badge.preserveAspect = true;
                PanelKit.PlaceTop(badge.rectTransform, x - 4f, y, 76f, 76f);
                VisualB1101.Pop(badge.rectTransform, .06f, .6f);
                VisualB1101.Label(card, "SignRaceName", race, 22, raceColor, x + 80f, y + 18f, 140f, 40f);
                y += 88f;
            }
            VisualB1101.Chip(card, "SignCareer", null, career, VisualB1101.Teal, x, y, 42f, out _,
                new Color32(20, 64, 72, 245), 22);
            y += 54f;
            string position = InterviewPosition(career, memberIndex).Replace(" / ", "").Replace("/", "");
            VisualB1101.Chip(card, "SignPosition", VisualB1101.PositionIcon(race), position, raceColor, x, y, 42f, out _,
                null, 20);
            // Five stage stats as a radar, glyphs at the corners (strongest in gold) — no labels.
            CandidateStats(member, memberIndex, interviewPoolIndex, out int vocal, out int rhythm,
                out int presence, out int resonance, out int charm);
            VisualB1101Radar radar = VisualB1101.StatRadar(card, "SignStatRadar", x - 14f, y + 56f, 210f,
                new[] { vocal, rhythm, presence, resonance, charm }, raceColor);
            VisualB1101.Pop(radar.rectTransform.parent as RectTransform, .1f, .5f);
            columnBottom = 548f;
        }
    }
}
