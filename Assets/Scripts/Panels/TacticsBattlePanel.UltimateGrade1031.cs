using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-10-31: every judged ultimate tap gets ① the centre burst (UltimateGradeBurst1031),
    /// ② its judgement stinger (Sfx/grade-*, Suno v6 + layered, Docs/AUDIO-1031.md) and
    /// ③ the English announcer (Voice/Announcer/ult-*; MISS stays silent apart from its soft thud).
    /// The grades and bonuses themselves are unchanged (PERFECT +10%, GOOD/GREAT +5%).
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private const string UltimateAnnouncerOwner1031 = "ult-announcer-1031";

        private void HookUltimateGrade1031(BattleUltimateShow1020 show)
        {
            if (show == null) return;
            show.BeatJudged -= OnUltimateBeatJudged1031;
            show.BeatJudged += OnUltimateBeatJudged1031;
        }

        /// <summary>
        /// Called once when the battle is set up: decodes the judgement stingers / announcer and rasterises the
        /// big GREAT!/!! glyphs now, so tapping the first beat never waits on loading (the hitch after GOOD).
        /// </summary>
        private void WarmUltimateGrade1031()
        {
            gameAudio?.Preload(
                new[] { "grade-perfect", "grade-great", "grade-good", "grade-miss", "ultimate", "crit", "hit" },
                new[] { "Announcer/ult-perfect", "Announcer/ult-great", "Announcer/ult-good" });
            Font font = kit?.Font;
            if (font != null && font.dynamic)
            {
                font.RequestCharactersInTexture("GREAT!PERFCODMS", Mathf.RoundToInt(104f * .8f), FontStyle.Bold);
                font.RequestCharactersInTexture("GREAT!PERFCODMS", Mathf.RoundToInt(96f * .8f), FontStyle.Bold);
                font.RequestCharactersInTexture("GREAT!PERFCODMS", Mathf.RoundToInt(84f * .8f), FontStyle.Bold);
            }
        }

        private void OnUltimateBeatJudged1031(UltimateTimingGrade grade, float offsetSeconds)
        {
            if (closing || ultimateShow == null) return;
            UltimateGradeTier1031 tier = UltimateGradeBurst1031.TierFor(grade, offsetSeconds);
            Sfx(UltimateGradeBurst1031.SfxName(tier), tier == UltimateGradeTier1031.Miss ? .8f : 1f);
            string announcer = UltimateGradeBurst1031.AnnouncerClip(tier);
            if (announcer != null && gameAudio != null && !paused) gameAudio.PlayVoice(announcer, UltimateAnnouncerOwner1031);
            RectTransform showRoot = ultimateShow.Root;
            RectTransform layer = showRoot != null ? showRoot.parent as RectTransform : null;
            UltimateGradeBurst1031 burst = UltimateGradeBurst1031.Play(layer, kit.Font, tier, () => paused || closing,
                showRoot, () => gameAudio?.StopVoice(UltimateAnnouncerOwner1031));
            // Right above the show (it outlives the show's last frames), below pause / rules overlays.
            if (burst != null && showRoot != null) burst.transform.SetSiblingIndex(showRoot.GetSiblingIndex() + 1);
        }
    }
}
