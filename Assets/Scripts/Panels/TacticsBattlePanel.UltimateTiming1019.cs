using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private BattleUltimateTimingView1019 ultimateTiming;
        private BattleUltimateVideo1019 optionalUltimateVideo;
        private BattleUltimateShow1020 ultimateShow;
        private int ultimateShownFor = -1;
        private bool ManualUltimateTimingActive => (ultimateTiming != null && ultimateTiming.IsActive) ||
            (ultimateShow != null && ultimateShow.IsActive);

        /// <summary>The character's own clip (or portrait) with the tap ring; then the grade.</summary>
        private BattleUltimateShow1020 EnsureUltimateShow()
        {
            if (ultimateShow == null)
            {
                ultimateShow = gameObject.AddComponent<BattleUltimateShow1020>();
                ultimateShow.Configure(transform, kit.Font, () => paused || closing);
                HookUltimateGrade1031(ultimateShow); // 2026-10-31 GOOD/GREAT/PERFECT burst + stinger + announcer
            }
            return ultimateShow;
        }

        private static BattleUltimateVideo1019.ClipEntry UltimateClipFor(string characterId)
        {
            var choices = UltimateClipsFor1112(characterId);
            // 2026-11-12 (少爷 supplied 2-3 clips for 幻纱 / 凛音): a member with several clips plays one of them at random.
            return choices.Count == 0 ? null : choices[Random.Range(0, choices.Count)];
        }

        private static System.Collections.Generic.List<BattleUltimateVideo1019.ClipEntry> UltimateClipsFor1112(string characterId)
        {
            var result = new System.Collections.Generic.List<BattleUltimateVideo1019.ClipEntry>();
            var manifest = BattleUltimateVideo1019.SharedManifest;
            if (manifest?.entries != null)
                foreach (var entry in manifest.entries)
                    if (entry != null && entry.characterId == characterId && entry.approvedOriginalArt &&
                        BattleUltimateVideo1019.IsLocalClipPath(entry.path)) result.Add(entry);
            return result;
        }

        /// <summary>Preload the first team member's ultimate clip so its first cast plays at once.</summary>
        private void WarmUltimateClips()
        {
            foreach (BattleUnit unit in battle.Units)
            {
                if (unit.Side != BattleSide.Player) continue;
                foreach (var entry in UltimateClipsFor1112(unit.Definition.Id)) EnsureUltimateShow().Warm(entry.path);
            }
        }

        private bool BeginUltimateShow(BattleUnit unit, System.Action<UltimateTimingGrade> onGrade)
        {
            EnsureUltimateShow();
            var clipEntry = UltimateClipFor(unit.Definition.Id);
            string clip = clipEntry?.path; float clipLength = clipEntry?.durationSeconds ?? 0;
            CellView cell = FindCell(unit);
            Sprite portrait = PanelKit.MemberSpriteOrNull(unit.Definition.Id, false) ??
                (cell?.Portrait != null ? cell.Portrait.sprite : BattlePortrait(unit.Definition.Id));
            int actorId = unit.Id;
            ultimateShow.ConfigureCutIn(kit.Font, name => Sfx(name, .9f));
            ultimateShow.SetCutIn(unit.Definition.Name, MemberSkillTitle1103(unit, battle.ActiveSkillId(unit, true)) ?? "大招");
            // 2026-10-25 (少爷): the cut-in uses her small-skill art (the authored torn-paper PSD group),
            // then the half-body avatar, then the full portrait.
            string cutinKey = BattleAuthoredCutin1019.KeyForMember(unit.Definition.Id);
            Sprite cutArt = (cutinKey != null ? BattlePsdArt928.Sprite(cutinKey) : null) ??
                Resources.Load<Sprite>("Art/Members/Avatars/avatar-" + unit.Definition.Id);
            ultimateShow.SetCutInArt(cutArt);
            Debug.Log($"[UltShow] {unit.Definition.Id} clip={clip ?? "none"} reduce={BattlePresentationOptions.ReduceMotion}");
            ultimateShow.SetCutInEmblem(psdRoot != null ? BattlePsdArt928.Sprite(BattlePsdSkillLayout1019.For(battle.RaceOf(unit), 0).Key) : null);
            bool began = ultimateShow.Begin(clip, clipLength, portrait, grade =>
            {
                if (closing || battle.Outcome != BattleOutcome.Ongoing) return;
                // Consumed when the cast event is presented (next log drain).
                ultimateShownFor = actorId;
                onGrade(grade);
                RefreshAllCells(); RefreshRealtimeCommands();
            }, autoMode);
            if (began) Sfx("ultimate");
            if (pauseOverlay != null) pauseOverlay.transform.SetAsLastSibling();
            if (rulesOverlay != null) rulesOverlay.transform.SetAsLastSibling();
            if (retreatButton != null) retreatButton.transform.SetAsLastSibling();
            return began;
        }

        private bool TryBeginManualUltimate(BattleUnit unit)
        {
            if (closing || paused || autoMode || battle == null || unit == null) return false;
            if (ManualUltimateTimingActive) return true; // A second tap cannot replace the pending cast.
            if (!battle.IsUltimateReady(unit)) return false;
            SkillDefinition definition = battle.LookupSkill(battle.ActiveSkillId(unit, true));
            string effect = definition?.Effect;
            if (effect != SkillEffect.Damage && effect != SkillEffect.Heal && effect != SkillEffect.Shield)
                return battle.TryCastUltimate(unit.Id);
            BattleSimulator expected = battle;
            int actorId = unit.Id;
            bool began = BeginUltimateShow(unit, grade =>
            {
                if (paused || autoMode || battle != expected) return;
                if (!battle.TryCastUltimateWithTiming(actorId, grade)) Notify("大招状态已变化，请重新选择");
            });
            return began;
        }

        private void PresentUltimateVideoOrPortrait(BattleUnit actor, Sprite portrait, string title, Color color)
        {
            // The manual show already played this character's animation; do not repeat it.
            if (actor != null && actor.Id == ultimateShownFor) { ultimateShownFor = -1; return; }
            if (optionalUltimateVideo == null)
            {
                optionalUltimateVideo = gameObject.AddComponent<BattleUltimateVideo1019>();
                optionalUltimateVideo.Configure(transform, () => paused || closing);
            }
            void PortraitFallback()
            {
                if (closing || paused || !isActiveAndEnabled) return;
                if (psdUltimate != null) psdUltimate.Enqueue(portrait, actor.Definition.Name, title, color);
                else skillCutIn?.Enqueue(portrait, actor.Definition.Name, title, color);
            }
            if (!optionalUltimateVideo.TryPlay(actor.Definition.Id, PortraitFallback)) PortraitFallback();
        }

        private void CancelUltimateTiming()
        {
            ultimateTiming?.Cancel(); ultimateShow?.Cancel(); optionalUltimateVideo?.Cancel(); psdUltimate?.Cancel();
        }

        private void PlayResultEncore(bool victory, int stars)
        {
            var entrance = resultOverlay.GetComponent<ChoSiren.UI.BattleResultEncore1019>()
                ?? resultOverlay.AddComponent<ChoSiren.UI.BattleResultEncore1019>();
            // 2026-10-21: the R2 entrance owns the title stamp; the encore keeps the star count-up.
            entrance.Play(null, resultStars, victory, stars);
            var reward = resultRewardScroll != null ? resultRewardScroll.transform.parent as RectTransform : null;
            ChoSiren.UI.ResultEntrance1022.Play(resultCardRect, resultTitle, reward,
                resultCardRect.Find("ResultContinue") as RectTransform, name => Sfx(name, .8f));
        }
    }
}
