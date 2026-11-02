using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        bool bossEntrancePlaying1102, bossEntranceShown1102;
        IEnumerator PlayBossEntrance1102()
        {
            if (battle == null || !battle.Stage.HasBossPhases) yield break;
            while (!closing && (coveredByStory || ChallengeVideoTransition.IsCoveringGame)) yield return null;
            if (closing) yield break;
            bossEntrancePlaying1102 = true;
            var root = new GameObject("BossEntrance1102", typeof(RectTransform));
            root.transform.SetParent(transform, false); root.transform.SetAsLastSibling();
            var movie = root.AddComponent<BossEntrance1102>();
            try
            {
                yield return movie.Play(battle.IsMajorBossStage, GetComponentInChildren<Text>(true)?.font, () => paused || closing);
                bossEntranceShown1102 = true;
            }
            finally
            {
                bossEntrancePlaying1102 = false;
                if (root != null) Destroy(root);
            }
        }
    }
}
