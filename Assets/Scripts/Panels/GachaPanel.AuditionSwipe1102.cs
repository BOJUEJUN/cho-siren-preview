using ChoSiren.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    public sealed partial class GachaPanel
    {
        float candidateSwipeEntry1102;

        private void AttachCandidateSwipe1102(RectTransform page, Rect bounds)
        {
            var portrait = page.Find("CandidatePortrait") as RectTransform;
            var card = page.Find("CandidateCard") as RectTransform;
            if (portrait == null || card == null) return;
            Image hit = kit.NewImage("CandidateSwipeSurface", page, null, Color.clear);
            PanelKit.PlaceTop(hit.rectTransform, bounds.x, bounds.y, bounds.width, bounds.height);
            hit.raycastTarget = true;
            var swipe = hit.gameObject.AddComponent<AuditionSwipe1102>();
            swipe.Configure(new[] { portrait, card },
                () => !closing && signModal == null && InterviewCandidates().Count > 1,
                direction =>
                {
                    candidateSwipeEntry1102 = direction * 80f;
                    MoveInterviewCandidate(direction);
                });
            if (candidateSwipeEntry1102 != 0f)
            {
                swipe.Enter(candidateSwipeEntry1102);
                candidateSwipeEntry1102 = 0f;
            }
        }
    }
}
