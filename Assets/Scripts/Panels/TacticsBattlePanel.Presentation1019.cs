using System;
using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren.Panels
{
    public sealed partial class TacticsBattlePanel
    {
        private BattleBasicAttack1019 psdBasic1019;
        private BattleActiveCloseup1019 psdCloseup1019;
        private BattleWaveFracture1019 psdWave1019;
        private readonly Dictionary<int, int> psdBasicSequence1019 = new Dictionary<int, int>();

        // Call once at the END of Build, after PSD layout and the existing overlays exist.
        private void BuildPsdPresentation1019()
        {
            if (psdRoot == null || psdBasic1019 != null) return;
            psdBasic1019 = gameObject.AddComponent<BattleBasicAttack1019>();
            psdBasic1019.Configure(psdRoot, () => paused || closing, () => speed);
            var basicLayer = psdRoot.Find("AuthoredBasicLayer");
            // It rises behind the real cards; HP, energy, skill columns and their hit targets never move.
            int frontCard = psdRoot.childCount - 1;
            foreach (var c in cells)
                if (c.Side == BattleSide.Player) frontCard = Mathf.Min(frontCard, c.Root.transform.GetSiblingIndex());
            // PSD: enlarged attack figures sit immediately in front of the enemy scene,
            // behind ALL sleeves, HP/energy strips, the dice bar and dice pedestals.
            basicLayer.SetSiblingIndex(enemyStageRoot.GetSiblingIndex() + 1);
            psdCloseup1019 = gameObject.AddComponent<BattleActiveCloseup1019>();
            psdCloseup1019.Configure(psdRoot, () => paused || closing, () => speed);
            WarmUltimateGrade1031(); // 2026-10-31: no load hitch on the first judged ultimate beat
            psdWave1019 = gameObject.AddComponent<BattleWaveFracture1019>();
            psdWave1019.Configure(psdRoot, enemyStageRoot, BattlePsdArt928.Sprite("stage"), () => paused || closing, () => speed);
            psdWave1019.OnStamp = () =>
            {
                if (closing) return;
                Sfx("stamp", .9f);
                if (!BattlePresentationOptions.ReduceMotion && stageShake == null)
                    stageShake = StartCoroutine(ShakeStage(.1f, 4f, Vector2.down));
            };
            psdRoot.Find("CrystalFloorWaveChange").SetSiblingIndex(frontCard);
            foreach (var c in cells)
            {
                var captured = c;
                var marker = c.Root.AddComponent<BattleHeadEffects1019>();
                marker.Configure(psdRoot, battle, () => captured.Unit, () => UnitVisual(captured.Unit));
            }
            psdRoot.Find("ActiveSkillCloseup").SetAsLastSibling();
            BuildBattleFx1101(); // 2026-11-01: close-up foreground layer + big damage numbers
            HideCoveredLayers1022();
        }

        // 2026-10-21 perf: the PSD battle is opaque full-screen, yet the lobby's six full-screen
        // layers and the map board kept drawing underneath (pure fill-rate cost on phones).
        // They are hidden while the fight runs and restored the moment the battle settles.
        private readonly List<GameObject> coveredLayers1022 = new List<GameObject>();

        private void HideCoveredLayers1022()
        {
            if (!Application.isPlaying || coveredLayers1022.Count > 0) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Transform root = canvas.rootCanvas.transform;
            foreach (string name in new[] { "PsdHome20260921" })
            {
                Transform layer = root.Find(name);
                if (layer != null && layer.gameObject.activeSelf) { layer.gameObject.SetActive(false); coveredLayers1022.Add(layer.gameObject); }
            }
            Transform parent = transform.parent;
            if (parent == null) return;
            for (int i = 0; i < transform.GetSiblingIndex(); i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling.name == "LevelMapPanel" && sibling.gameObject.activeSelf)
                {
                    sibling.gameObject.SetActive(false);
                    coveredLayers1022.Add(sibling.gameObject);
                }
            }
        }

        private void RestoreCoveredLayers1022()
        {
            foreach (GameObject layer in coveredLayers1022)
                if (layer != null) layer.SetActive(true);
            coveredLayers1022.Clear();
        }

        // Wrap the old attackTrajectories.Play call: only real player rt-basic events use this route.
        private readonly HashSet<string> loggedBasic = new HashSet<string>();
        private static readonly Dictionary<string, bool> basicArtCache = new Dictionary<string, bool>();
        private static bool HasBasicArt(string key)
        {
            if (!basicArtCache.TryGetValue(key, out bool has))
                basicArtCache[key] = has = Resources.Load<Texture2D>("UI/UltFx/" + key + "-basic-proj") != null;
            return has;
        }

        private bool TryPsdBasic1019(BattleUnit actor, BattleUnit target, BattleEvent entry, Action onLand = null)
        {
            if (psdBasic1019 == null || actor == null || target == null || actor.Side != BattleSide.Player) return false;
            // Follow-up hits (魅族追击) use the same per-race strike as the basic attack.
            bool followUp = entry.SkillId == "rt-charm-follow";
            // The small active skill fires the same race strike, drawn larger (owner: no old thin lines).
            bool smallSkill = entry.SkillId == battle.ActiveSkillId(actor, false);
            if (entry.SkillId != "rt-basic" && !followUp && !smallSkill) return false;
            if (psdCloseup1019 != null && psdCloseup1019.IsShowing) return true;
            // 2026-10-20 (owner): each race fires its own basic attack (GPT art), not a shared fireball.
            CombatRace race = battle.RaceOf(actor);
            string basicKey = race == CombatRace.Demon ? "demon" : race == CombatRace.BloodElf ? "bloodelf"
                : race == CombatRace.Mermaid ? "mermaid" : "charm";
            if (psdRoot != null && HasBasicArt(basicKey))
            {
                CellView strikeCell = FindCell(actor);
                if (!PlayHeroLunge1031(strikeCell, actor, entry.SkillId)) strikeCell?.Motion?.PlayAttack(false);
                if (followUp) basicKey = "charm";
                // 2026-10-21 (owner): each race has its own move — fox-fire wisps, claw rakes,
                // blood crescent + cross-cut, water jet + splash — not one projectile for all.
                BasicStrike1021.Play(psdRoot, UnitVisual(actor), UnitVisual(target), basicKey, () => paused || closing,
                    () => { if (smallSkill) Sfx("strike-" + basicKey, .6f); onLand?.Invoke(); }, smallSkill ? .7f : .5f, contact: false);
                hitDeferred1021 = Application.isPlaying && onLand != null;
                if (!loggedBasic.Contains(basicKey)) { loggedBasic.Add(basicKey); Debug.Log("[BasicStrike] " + basicKey + (followUp ? " follow-up" : "")); }
                return true;
            }
            int sequence = psdBasicSequence1019.TryGetValue(actor.Id, out int old) ? old : 0;
            psdBasicSequence1019[actor.Id] = sequence + 1;
            var form = sequence % 2 == 0 ? BattleBasicForm1019.Fireball : BattleBasicForm1019.PortraitBurst;
            Color tint = battle.RaceOf(actor) == CombatRace.Mermaid ? new Color32(87, 221, 255, 255)
                : battle.RaceOf(actor) == CombatRace.Demon ? new Color32(191, 116, 255, 255) : new Color32(255, 145, 65, 255);
            string exactKey = actor.Definition.Id == "feiyin" ? "basic-portrait-feiyin" : null;
            Sprite authored = exactKey != null ? BattlePsdArt928.Sprite(exactKey) : null;
            psdBasic1019.Play(UnitVisual(actor), UnitVisual(target), authored ?? PanelKit.MemberSpriteOrNull(actor.Definition.Id, false), tint, form,
                FindCell(actor)?.Rect, authored != null ? BattlePsdArt928.Find(exactKey) : null);
            // A capacity drop intentionally skips decoration instead of replaying a stale duplicate trajectory.
            return true;
        }

        /// <summary>
        /// Enemy attacks (2026-10-21): each enemy kind has its own move instead of the old flying line —
        /// scouts fire a neon laser, wraiths/golems rake with claws, drones/singers/bosses send sound waves.
        /// </summary>
        private bool TryEnemyStrike1021(BattleUnit actor, BattleUnit target, BattleEvent entry, Action onLand)
        {
            if (psdRoot == null || actor == null || target == null || actor.Side != BattleSide.Enemy) return false;
            RectTransform from = UnitVisual(actor), to = UnitVisual(target);
            if (from == null || to == null) return false;
            string id = actor.Definition.Id ?? string.Empty;
            string style = id.Contains("scout") || id.Contains("neon") ? "enemy-beam"
                : id.Contains("wraith") || id.Contains("golem") || id.Contains("noise") ? "enemy-claw" : "enemy-sonic";
            Color tint = style == "enemy-beam" ? new Color32(80, 240, 255, 255) : style == "enemy-claw" ? new Color32(190, 90, 255, 255)
                : new Color32(255, 70, 160, 255);
            bool heavy = entry.SkillId == "rt-enemy-heavy" || entry.SkillId == "rt-enemy-finale";
            BasicStrike1021.Play(psdRoot, from, to, style, () => paused || closing, onLand, heavy ? .75f : .5f, 0f, null, tint);
            hitDeferred1021 = Application.isPlaying && onLand != null;
            return true;
        }

        // Call in Spawned after RefreshAllCells. Repeated members of the same wave are ignored by Play.
        private void PresentPsdWave1019()
        {
            if (battle.Stage.HasBossPhases) return;
            if (psdWave1019 != null && psdWave1019.Play(battle.CurrentWave, battle.TotalWaves) && psdWave1019.IsShowing) Sfx("cut", .8f);
        }

        // Call before an ultimate cut-in / ShowResult / closing; all are presentation-only owners.
        private void CancelPsdPresentation1019()
        {
            psdBasic1019?.Cancel(); psdCloseup1019?.Cancel(); psdWave1019?.Cancel();
        }
    }
}
