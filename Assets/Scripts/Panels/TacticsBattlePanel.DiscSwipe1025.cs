using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>
        /// 2026-10-25 (少爷): a ready member's record can be swiped up out of its sleeve along the flame's ^^ arrows to cast the
        /// ultimate. The drag lifts the record (spinning faster, fire taller); past the threshold or on a flick it flies off.
        /// </summary>
        private void AttachDiscSwipe1025(CellView cell, BattleMemberAlbum929 album)
        {
            var swipe = cell.Root.AddComponent<DiscSwipe1025>();
            swipe.CanSwipe = () => !paused && !closing && battle != null && battle.IsTurnBased && !pickingCaptain1024
                && tappedUltimateId < 0 && cell.Unit != null && cell.Unit.Alive && battle.IsUltimateReady(cell.Unit) && !album.Launched;
            swipe.Began = () => cell.Root.GetComponent<CellPointer>()?.CancelHold();
            swipe.Lift = lift =>
            {
                album.SetDrag(lift);
                if (psdUltRings.TryGetValue(cell, out UltReadyRing1020 ring) && ring != null) ring.SetCharge(lift / swipe.Threshold);
            };
            swipe.ReachedThreshold = () => kit.PlayClick();
            // A tapping hand over a full card until the player has cast a few times (2026-10-26 「↑ 上滑」 text;
            // 2026-11-01 onboarding: no words, and it points at the card — tapping it is the taught way, the swipe still works).
            var hint = ChoSiren.UI.CoachHand1101.Create(cell.Root.transform, 84f);
            hint.name = "SwipeHint1026";
            hint.Aim(new Vector2(0f, cell.Rect.rect.height * .12f), false);
            hint.gameObject.AddComponent<SwipeHint1026>().Configure(
                () => !paused && battle != null && battle.IsTurnBased && cell.Unit != null && battle.IsUltimateReady(cell.Unit) &&
                    !album.Launched && !OnboardingCoachOpen1101);
            swipe.Launch = () => LaunchUltimateDisc1025(cell);
            swipe.Cancel = () =>
            {
                album.ReleaseDrag();
                if (psdUltRings.TryGetValue(cell, out UltReadyRing1020 ring) && ring != null) ring.SetCharge(0f);
            };
        }

        /// <summary>Tap or swipe: the record flies off and her ultimate is queued for the next gap between turns.</summary>
        private void LaunchUltimateDisc1025(CellView cell)
        {
            BattleUnit unit = cell.Unit;
            if (unit == null || battle == null || !battle.IsUltimateReady(unit)) return;
            SwipeHint1026.RecordLaunch();
            tappedUltimateId = unit.Id;
            Notify($"{unit.Definition.Name} 大招！");
            if (psdAlbums.TryGetValue(cell, out BattleMemberAlbum929 album) && album != null) album.PlayLaunch();
            if (psdUltRings.TryGetValue(cell, out UltReadyRing1020 ring) && ring != null) { ring.SetCharge(0f); ring.SetReady(false); }
            Sfx("cut", .9f);
        }
    }

    /// <summary>Shows the tapping hand over a ready card while the player is still learning to cast.</summary>
    internal sealed class SwipeHint1026 : MonoBehaviour
    {
        const string LearnedKey = "cho-siren.swipe-launches";
        const int LearnedAfter = 3;
        static int launches = -1;
        CanvasGroup group;
        System.Func<bool> show;
        float alpha;

        public static void RecordLaunch()
        {
            if (launches < 0) launches = PlayerPrefs.GetInt(LearnedKey, 0);
            launches++;
            PlayerPrefs.SetInt(LearnedKey, launches);
        }

        static bool Learned
        {
            get { if (launches < 0) launches = PlayerPrefs.GetInt(LearnedKey, 0); return launches >= LearnedAfter; }
        }

        public void Configure(System.Func<bool> visible)
        {
            show = visible;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;
        }

        void LateUpdate()
        {
            if (group == null) return;
            bool on = !Learned && show != null && show();
            alpha = Mathf.MoveTowards(alpha, on ? 1f : 0f, Time.unscaledDeltaTime / .2f);
            group.alpha = alpha;
            if (alpha > 0f) transform.SetAsLastSibling();
        }
    }
}
