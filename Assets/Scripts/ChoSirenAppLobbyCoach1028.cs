using System.Collections;
using ChoSiren.Panels;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>
    /// First-visit lobby guide. 2026-10-28 (少爷: "签名完后会简单带过一遍新手教程…但不要太啰嗦") it was four spotlights;
    /// 2026-11-01 (用户: "新手介绍文字太多了，用最少的文字方案") it is one: a hand on 「开始演出」 and one line. Team, practice,
    /// tasks and audition come up later on the guide strip, each when it is the next thing to do. On the map a silent
    /// hand then taps 「开始挑战」 until 1-1 is won.
    /// </summary>
    public sealed partial class ChoSirenApp
    {
        public const string LobbyCoachLine1101 = "点这里，开始第一场演出";

        private IEnumerator LobbyCoach1028()
        {
            // Let the lobby finish its entrance before pointing at anything.
            float settle = Time.unscaledTime + .6f;
            while (Time.unscaledTime < settle || psdOpening) yield return null;
            // The PSD lobby lives beside the safe area (under the canvas); the coach covers the whole canvas.
            RectTransform host = (safeRoot != null ? safeRoot.parent : null) as RectTransform;
            RectTransform stage = (psdHome != null ? FindDeep1028(psdHome.transform, "LiveOnStage") : null) as RectTransform
                ?? FindDeep1028(safeRoot, "LiveOnStage") as RectTransform;
            if (host == null || stage == null) yield break;

            bool over = false, pressed = false;
            // The hole lets the tap through to 「开始演出」 itself (its stage video then covers everything, so the hint
            // leaves at once); tapping the dim just closes the hint.
            UI.CoachMark1101.Show(host, ModalPolish038Chrome.SharedFont != null ? ModalPolish038Chrome.SharedFont : font,
                LobbyCoachLine1101, new[] { stage }, stage,
                () => pressed = psdOpening || FindAnyObjectByType<LevelMapPanel>() != null || !stage.gameObject.activeInHierarchy,
                _ => over = true);
            while (!over) yield return null;
            if (!pressed) yield break;

            // The map opens behind the stage video; then the hand rides on its start button.
            float until = Time.unscaledTime + 10f;
            Transform start = null;
            while (start == null && Time.unscaledTime < until)
            {
                yield return null;
                if (psdOpening) continue;
                LevelMapPanel map = FindAnyObjectByType<LevelMapPanel>();
                if (map != null) start = FindDeep1028(map.transform, "StartChallenge");
            }
            if (start == null || model.IsStageCleared("stage-1-1")) yield break;
            UI.CoachHand1101 hand = UI.CoachHand1101.Create(start, 96f);
            hand.name = "StartChallengeHand1101";
            // From above, the fingertip on the button's top edge: the hand stays clear of 「开始挑战」 itself.
            hand.Aim(new Vector2(0f, ((RectTransform)start).rect.height * .42f), false);
        }

        /// <summary>The first <em>visible</em> object called <paramref name="name"/>: the old 0.3.8 lobby keeps hidden
        /// hotspots with the same names.</summary>
        private static Transform FindDeep1028(Transform root, string name)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep1028(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
