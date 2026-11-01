using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        // 2026-10-26 (赵金/少爷: "队长的图标要设计成原先在PSD里面给你的那个"): the captain wears the PSD's own 「队长」
        // plate (captain-plate: under the first card, left of 队长演出). The leader always holds the first slot — when the
        // captain changes she trades places with whoever stood there — so the plate never has to cover the dice strip.
        private Image captainPlate1026;
        private Text captainPlateLabel1026, captainPlateSwap1026;
        private readonly List<int> playerOrder1026 = new List<int>();
        private const float CardSlideSeconds1026 = .28f;

        private void BuildCaptainPlate1026()
        {
            if (psdRoot == null || BattlePsdArt928.Find("captain-plate") == null) return;
            captainPlate1026 = PsdArt("captain-plate");
            captainPlate1026.name = "PsdCaptainPlate";
            captainPlate1026.raycastTarget = true;
            var button = captainPlate1026.gameObject.AddComponent<Button>();
            button.targetGraphic = captainPlate1026; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(ToggleCaptainPick1024);
            captainPlateLabel1026 = kit.NewPlacedText(captainPlate1026.transform, "队长", 24, Color.white, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            // The box must be taller than the line, or the text is truncated away entirely.
            PsdPlace(captainPlateLabel1026.rectTransform, 120, 4, 300, 160);
            captainPlateLabel1026.raycastTarget = false;
            captainPlateLabel1026.resizeTextForBestFit = false;
            captainPlateLabel1026.horizontalOverflow = HorizontalWrapMode.Overflow;
            captainPlateLabel1026.verticalOverflow = VerticalWrapMode.Overflow;
            kit.AddOutline(captainPlateLabel1026.gameObject, new Color32(0, 0, 0, 220), 1.2f);
            // A small swap mark so the plate reads as tappable (the plate itself is the 换队长 control).
            captainPlateSwap1026 = kit.NewPlacedText(captainPlate1026.transform, "⇄", 18, Brand1025.Teal, 0, 0, 10, 10,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PsdPlace(captainPlateSwap1026.rectTransform, 390, 14, 110, 140);
            captainPlateSwap1026.raycastTarget = false;
            captainPlateSwap1026.resizeTextForBestFit = false;
            captainPlateSwap1026.horizontalOverflow = HorizontalWrapMode.Overflow;
            captainPlateSwap1026.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary>Called by RefreshAllCells: captain first, everyone else keeps their place where possible.</summary>
        private List<CellView> CaptainFirstOrder1026(List<CellView> players)
        {
            if (playerOrder1026.Count == 0 || players.Exists(c => !playerOrder1026.Contains(c.Unit.Id)))
            {
                playerOrder1026.Clear();
                foreach (CellView cell in players) playerOrder1026.Add(cell.Unit.Id);
            }
            int leader = battle != null && battle.IsTurnBased ? battle.CurrentLeaderId : -1;
            int at = playerOrder1026.IndexOf(leader);
            if (at > 0) { playerOrder1026[at] = playerOrder1026[0]; playerOrder1026[0] = leader; }
            var ordered = new List<CellView>(players);
            ordered.Sort((a, b) => playerOrder1026.IndexOf(a.Unit.Id).CompareTo(playerOrder1026.IndexOf(b.Unit.Id)));
            return ordered;
        }

        private void RefreshCaptainPlate1026()
        {
            if (captainPlate1026 == null) return;
            BattleUnit leader = battle != null && battle.IsTurnBased ? battle.FindUnit(battle.CurrentLeaderId) : null;
            bool show = leader != null && leader.Alive;
            captainPlate1026.gameObject.SetActive(show);
            if (!show) return;
            captainPlate1026.transform.SetAsLastSibling();
            captainPlateLabel1026.text = pickingCaptain1024 ? "取消" : "队长";
            captainPlateLabel1026.color = pickingCaptain1024 ? (Color)Brand1025.Teal : Color.white;
            captainPlateSwap1026.gameObject.SetActive(!pickingCaptain1024);
        }
    }

    /// <summary>Slides a card to its new slot instead of jumping (captain swaps move cards).</summary>
    internal sealed class CardSlide1026 : MonoBehaviour
    {
        Vector2 from, to, applied;
        float t = 1f, seconds = .28f;
        RectTransform rect;

        public static void Move(RectTransform rect, Vector2 previous, float duration)
        {
            if (rect == null) return;
            var slide = UnityNull.Real(rect.GetComponent<CardSlide1026>()) ?? rect.gameObject.AddComponent<CardSlide1026>();
            slide.rect = rect; slide.to = rect.anchoredPosition; slide.from = previous;
            slide.seconds = Mathf.Max(.01f, duration); slide.t = BattlePresentationOptions.ReduceMotion ? 1f : 0f;
            slide.Apply();
        }

        void LateUpdate()
        {
            if (t >= 1f || rect == null) return;
            if (rect.anchoredPosition != applied) to = rect.anchoredPosition;   // a refresh re-placed the card: new target
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / seconds);
            Apply();
        }

        void Apply()
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = applied = Vector2.LerpUnclamped(from, to, e);
        }
    }
}
