using ChoSiren.Systems.Tactics;
using ChoSiren.UI;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private BattleVoiceDirector battleVoices;
        private BattleChorus1028 battleChorus;

        // Central hook 1: call immediately after panel.gameAudio is assigned in Show().
        private void InitializeBattleVoices()
        {
            if (battleVoices == null) battleVoices = gameObject.AddComponent<BattleVoiceDirector>();
            battleVoices.Configure(gameAudio, () => paused || closing || coveredByStory);
            if (battleChorus == null) battleChorus = gameObject.AddComponent<BattleChorus1028>();
            battleChorus.Configure(gameAudio, () => paused || closing || coveredByStory);
        }

        // Central hook 2: PresentAction(), after the duplicate-action guard. The simulator's actual
        // ActionStarted event is the authority, rather than taps or the visual cut-in coroutine.
        private void QueueBattleVoice(BattleUnit actor, BattleEvent entry)
        {
            if (actor == null || actor.Side != BattleSide.Player || battle == null || entry == null) return;
            bool ultimate = entry.SkillId == battle.ActiveSkillId(actor, true);
            bool active = entry.SkillId == battle.ActiveSkillId(actor, false);
            if (!ultimate && !active) return; // Basics, damage ticks and enemy skills do not speak.
            if (ultimate) battleChorus?.Queue(actor.Definition.Id);
            battleVoices?.Queue(actor.Definition.Id,
                ultimate ? BattleVoiceDirector.Cue.Ultimate : BattleVoiceDirector.Cue.Active,
                actor.Id == battle.CurrentLeaderId);
        }

        // Central hook 3: ShowResult(), after bool victory has been resolved.
        private void CompleteBattleVoices(bool victory)
        {
            BattleUnit captain = battle?.FindUnit(battle.CurrentLeaderId);
            battleVoices?.Complete(victory, captain?.Definition.Id);
            battleChorus?.Release();
        }
    }
}
