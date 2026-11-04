using ChoSiren.Panels;
using ChoSiren.Systems.Economy;
using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// 2026-11-04 星钻商城: the monthly card and six top-up packs (first purchase doubled). Until a payment channel
    /// is connected every price button shows <see cref="GameModel.PaymentPendingNotice1104"/> and grants nothing.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        private void OpenTopUp1104()
        {
            const float panelWidth = 640f;
            RectTransform body = ProgressionModal("TopUpModal", "星钻商城", null, panelWidth);
            var frame = new ModalPolish038Frame
            {
                Overlay = modalObject, Body = body, Panel = (RectTransform)body.parent,
                Width = panelWidth, Height = panelWidth / ModalPolish038Chrome.PanelAspect,
            };
            float width = BodyWidth1101(frame);
            float y = 0f;

            // The state comes first: nothing is charged yet.
            GameObject notice = ModalPolish038Chrome.RowPlate(body, "TopUpNotice");
            PanelKit.PlaceTop(notice.GetComponent<RectTransform>(), 0f, y, width, 56f);
            RectTransform noPay = VisualA1101.NeonIcon(notice.transform, "Icon", "nopay", VisualA1101.Gold, 34f);
            VisualA1101.Place(noPay, 14f, 11f, 34f, 34f);
            Text noticeText = VisualA1101.Label(notice.transform, "Text", "支付通道接入中 · 现在不会扣款", 17, VisualA1101.Gold, TextAnchor.MiddleLeft);
            VisualA1101.Place(noticeText.rectTransform, 58f, 0f, width - 70f, 56f);
            y += 68f;

            // Monthly card.
            GameObject card = ModalPolish038Chrome.RowPlate(body, "TopUpMonthlyCard");
            PanelKit.PlaceTop(card.GetComponent<RectTransform>(), 0f, y, width, 96f);
            RectTransform crown = VisualA1101.NeonIcon(card.transform, "Icon", "crown", VisualA1101.Gold, 44f);
            VisualA1101.Place(crown, 14f, 26f, 44f, 44f);
            Text cardTitle = VisualA1101.Label(card.transform, "Title", "星钻月卡", 22, VisualA1101.Paper, TextAnchor.MiddleLeft);
            VisualA1101.Place(cardTitle.rectTransform, 70f, 12f, width - 230f, 36f);
            Text cardBody = VisualA1101.Label(card.transform, "Detail",
                $"立得 {GameModel.MonthlyCardInstantDiamonds1104} · 每天 {GameModel.MonthlyCardDailyDiamonds1104} · {GameModel.MonthlyCardDays1104} 天",
                15, VisualA1101.Muted, TextAnchor.MiddleLeft);
            VisualA1101.Place(cardBody.rectTransform, 70f, 50f, width - 230f, 30f);
            PriceButton1104(card.transform, "TopUpBuy-monthly-card", GameModel.MonthlyCardPriceYuan1104, width - 148f, 20f);
            y += 108f;

            // Packs, two to a row.
            float gap = 12f, cell = (width - gap) * .5f, rowH = 112f;
            for (int i = 0; i < GameModel.TopUpPacks1104.Length; i++)
            {
                GameModel.TopUpPack1104 pack = GameModel.TopUpPacks1104[i];
                float x = (i % 2) * (cell + gap), top = y + (i / 2) * (rowH + gap);
                GameObject plate = ModalPolish038Chrome.RowPlate(body, "TopUpPack-" + pack.Id);
                PanelKit.PlaceTop(plate.GetComponent<RectTransform>(), x, top, cell, rowH);
                RectTransform amount = CurrencyChip1101(plate.transform, "Amount", CurrencyIds.Diamond,
                    pack.Diamonds.ToString("N0"), VisualA1101.Paper, 38f, 24);
                VisualA1101.Place(amount, 14f, 14f, amount.sizeDelta.x, 38f);
                if (model.FirstChargeBonusAvailable1104(pack.Id))
                {
                    Text bonus = VisualA1101.Label(plate.transform, "FirstBonus", $"首充再送 {pack.Diamonds:N0}", 14,
                        VisualA1101.Pink, TextAnchor.MiddleLeft);
                    VisualA1101.Place(bonus.rectTransform, 14f, 56f, cell - 140f, 26f);
                }
                PriceButton1104(plate.transform, "TopUpBuy-" + pack.Id, pack.PriceYuan, cell - 122f, rowH - 60f, 110f);
            }
            y += 3 * (rowH + gap);

            Text footer = VisualA1101.Label(body, "TopUpFooter", "签约券可在每周任务和签到中免费获得", 15, VisualA1101.Muted);
            VisualA1101.Place(footer.rectTransform, 0f, y, width, 30f);
            ModalPolish038Chrome.FitHeight(frame, y + 36f);
        }

        private void PriceButton1104(Transform parent, string name, int priceYuan, float x, float y, float width = 136f)
        {
            IconButton1101(parent, name, "coin", "¥" + priceYuan, 20, () =>
            {
                model.TryTopUp1104(name, out string message);
                VisualA1101.Deny();
                Toast(message);
            }, ModalPolish038ButtonStyle.Primary, x, y, width, 52f, 24f);
        }
    }
}
