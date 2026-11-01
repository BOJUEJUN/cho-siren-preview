using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using ChoSiren.UI;
using UnityEngine;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-10-31 (赵金: "死了要有一两句台词"): a member who falls says one of her two 阵亡台词 (Japanese,
    /// same MiniMax voice as her skill lines) with a Chinese subtitle beside her card, and the "down" impact
    /// plays. Boss targets get the heavier "boss-hit" layer under each landed blow.
    /// Assets: Resources/Audio/Voice/Battle/&lt;member&gt;-defeat-0N, Sfx/down*, Sfx/boss-hit*;
    /// lines and SHA in Docs/AUDIO-1031-voice-manifest.json.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>Chinese subtitles, index = variant - 1 (must match the recordings' order).</summary>
        public static readonly IReadOnlyDictionary<string, string[]> DefeatSubtitles1031 = new Dictionary<string, string[]>
        {
            { "xingli", new[] { "抱歉……歌声没能传到……", "大家……剩下的，拜托了……" } },
            { "feiyin", new[] { "可恶……明明还能跳的……", "啧……到此为止了吗……" } },
            { "wubai", new[] { "对不起……没能守护你们……", "别哭……我没事的……" } },
            { "yeying", new[] { "节拍……断了……", "该死……下次不会失手……" } },
        };

        private bool voiceSubtitlesHooked1031;

        /// <summary>Called once per fallen member from the Defeated event presentation.</summary>
        private void PresentMemberDefeat1031(BattleUnit unit)
        {
            if (unit == null || unit.Side != BattleSide.Player || closing) return;
            Sfx("down", .85f);
            if (battleVoices == null || battle == null) return;
            if (!voiceSubtitlesHooked1031)
            {
                battleVoices.Spoke += ShowVoiceSubtitle1031;
                voiceSubtitlesHooked1031 = true;
            }
            battleVoices.Queue(unit.Definition.Id, BattleVoiceDirector.Cue.Defeat, unit.Id == battle.CurrentLeaderId);
        }

        private void ShowVoiceSubtitle1031(string member, BattleVoiceDirector.Cue cue, int variant, float seconds)
        {
            if (cue != BattleVoiceDirector.Cue.Defeat || closing || battle == null) return;
            if (!DefeatSubtitles1031.TryGetValue(member, out string[] lines) || variant < 1 || variant > lines.Length) return;
            BattleUnit speaker = null;
            foreach (BattleUnit unit in battle.Units)
                if (unit.Side == BattleSide.Player && unit.Definition.Id == member) { speaker = unit; break; }
            CellView cell = FindCell(speaker);
            RectTransform anchor = cell?.Rect != null ? cell.Rect : cell?.Root != null ? cell.Root.transform as RectTransform : null;
            var layer = (RectTransform)transform;
            VoiceSubtitle1031 caption = VoiceSubtitle1031.Show(layer, anchor, kit.Font, speaker?.Definition.Name,
                lines[variant - 1], seconds, () => paused || closing || coveredByStory);
            // Above the battle stage, below result / pause / rules overlays.
            if (caption != null && psdRoot != null && psdRoot.parent == transform)
                caption.transform.SetSiblingIndex(psdRoot.GetSiblingIndex() + 1);
        }

        /// <summary>The heavy layer under hits that land on the chapter boss.</summary>
        private void BossHitLayer1031(BattleUnit target, BattleEvent entry)
        {
            if (target == null || entry == null || target.Side != BattleSide.Enemy || target.Definition.Id != "siren-queen") return;
            Sfx("boss-hit", entry.Critical ? .95f : .8f);
        }
    }
}
