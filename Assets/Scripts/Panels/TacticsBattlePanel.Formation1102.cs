using UnityEngine;
using UnityEngine.EventSystems;
using ChoSiren.UI;
namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        bool chargeClockStarted1102;
        float chargeMilliseconds1102, chargeRefresh1102;
        void TickFormationCharge1102()
        {
            if (!chargeClockStarted1102 || battle == null || !battle.IsTurnBased || paused || closing ||
                EntryPresentationCovered || OnboardingCoachOpen1101 || ManualUltimateTimingActive ||
                (psdUltimate != null && psdUltimate.IsShowing) || (psdWave1019 != null && psdWave1019.IsShowing) ||
                (fx1023 != null && fx1023.UltimatePlaying)) return;
            chargeMilliseconds1102 += Mathf.Min(Time.unscaledDeltaTime, .1f) * Mathf.Clamp(speed, 1, 3) * 1000f;
            int whole = Mathf.FloorToInt(chargeMilliseconds1102);
            chargeMilliseconds1102 -= whole;
            battle.AdvanceFormationCharge1102(whole);
            chargeRefresh1102 += Time.unscaledDeltaTime;
            if (chargeRefresh1102 < .1f) return;
            chargeRefresh1102 = 0;
            foreach (CellView cell in cells)
                if (cell.Unit != null && cell.Side == Systems.Tactics.BattleSide.Player) ApplyCell(cell);
        }
        void AttachFormationDrag1102(CellView cell)
        {
            var drag = cell.Root.AddComponent<FormationDrag1102>();
            drag.Allowed = () => !paused && !closing && !EntryPresentationCovered && !ManualUltimateTimingActive &&
                (psdUltimate == null || !psdUltimate.IsShowing) && battle != null && battle.IsTurnBased;
            drag.Began = () => cell.Root.GetComponent<CellPointer>()?.CancelHold();
            drag.Dropped = (position, camera) =>
            {
                foreach (CellView other in cells)
                {
                    if (other == cell || other.Side != Systems.Tactics.BattleSide.Player || other.Unit == null ||
                        !RectTransformUtility.RectangleContainsScreenPoint(other.Rect, position, camera)) continue;
                    if (!battle.SwapFormationPositions1102(cell.Unit.Id, other.Unit.Id)) return false;
                    pickingCaptain1024 = false; RefreshAllCells(); kit.PlayClick(); return true;
                }
                return false;
            };
        }
    }
    internal sealed class FormationDrag1102 : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public System.Func<bool> Allowed;
        public System.Action Began;
        public System.Func<Vector2, Camera, bool> Dropped;
        bool active;
        RectTransform rect, parent;
        Vector2 home, pointerStart;
        Vector3 restingScale;
        Quaternion restingRotation;
        int sibling;
        CardSlide1026 slide;

        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 d = e.position - e.pressPosition;
            active = (Allowed?.Invoke() ?? false) && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.7f;
            if (!active) return;
            rect = transform as RectTransform;
            parent = rect.parent as RectTransform;
            if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, e.pressPosition, e.pressEventCamera, out pointerStart)) { active = false; return; }
            home = rect.anchoredPosition; restingScale = rect.localScale;
            restingRotation = rect.localRotation; sibling = rect.GetSiblingIndex();
            slide = GetComponent<CardSlide1026>();
            if (slide != null) slide.enabled = false;
            rect.SetAsLastSibling();
            e.eligibleForClick = false; Began?.Invoke();
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (!active) return;
            e.eligibleForClick = false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position,
                e.pressEventCamera, out Vector2 point)) return;
            Vector2 delta = point - pointerStart;
            rect.anchoredPosition = home + delta;
            if (!BattlePresentationOptions.ReduceMotion)
            {
                rect.localScale = restingScale * 1.05f;
                rect.localRotation = restingRotation * Quaternion.Euler(0, 0, Mathf.Clamp(-delta.x * .018f, -4f, 4f));
            }
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (!active) return;
            active = false; e.eligibleForClick = false;
            Vector2 released = rect.anchoredPosition;
            RestoreVisualPose();
            bool swapped = (Allowed?.Invoke() ?? false) && (Dropped?.Invoke(e.position, e.pressEventCamera) ?? false);
            if (!swapped)
            {
                rect.anchoredPosition = home;
                CardSlide1026.Move(rect, released, .2f);
            }
        }
        void RestoreVisualPose()
        {
            if (rect == null) return;
            rect.localScale = restingScale; rect.localRotation = restingRotation;
            rect.SetSiblingIndex(sibling);
            if (slide != null) slide.enabled = true;
        }
        void OnDisable()
        {
            if (!active) return;
            active = false; RestoreVisualPose();
            if (rect != null) rect.anchoredPosition = home;
        }
    }
}
