using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-11 (玩家反馈, 少爷批准的方案 Builds/qa-logs/chapter20-1111/three-enemy-mock.png): on the few authored
    /// "三敌同屏" stages (<see cref="StageDefinition.MaxOnStage"/> = 3) three minions stand in a row on a stage half
    /// again as wide as the screen, each as large as on a two-minion stage. The "camera" pans that row left / right:
    /// to the enemy we strike, to the enemy that acts, and to a tapped target, holding on it until the next action.
    /// The stage art behind them moves at half the speed (parallax, never showing an edge); an off-screen enemy is
    /// marked by a chevron on that edge, which can be tapped to look (and aim) there.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        /// <summary>Wide-stage lane width = half the PSD stage, as on a two-minion stage.</summary>
        internal const float WideLane1111 = PsdStageWidth1031 / 2f;
        /// <summary>Furthest the camera pans (PSD units): the row is 1.5 stages wide.</summary>
        internal const float WidePanMax1111 = PsdStageWidth1031 * .5f;
        /// <summary>Seconds for most of a pan (critically damped follow).</summary>
        internal const float WidePanSeconds1111 = .32f;

        private RectTransform enemyPan1111, wideBack1111, wideBackArt1111;
        private readonly Dictionary<int, int> wideSlots1111 = new Dictionary<int, int>();
        private readonly RectTransform[] wideChevrons1111 = new RectTransform[2];
        private float wideCamera1111, wideGoal1111, wideVelocity1111;
        private int wideLock1111 = -1;
        /// <summary>Test / capture hook: battle actions and taps stop moving the camera (aims from code still do).</summary>
        internal bool HoldWideCamera1111;

        /// <summary>True while this battle uses the wide three-minion stage (never with a boss).</summary>
        internal bool WideStage1111 => psdRoot != null && battle != null && battle.MinionSlots >= BattleSimulator.MaxWideMinions1111;

        /// <summary>Current pan in PSD units (0 = left edge of the row on screen).</summary>
        internal float WideCamera1111 => wideCamera1111;
        internal float WideCameraGoal1111 => wideGoal1111;

        /// <summary>Test / capture hook: the slot (0 left, 1 middle, 2 right) an enemy stands in, or -1.</summary>
        internal int WideSlotOf1111(int unitId) => wideSlots1111.TryGetValue(unitId, out int slot) ? slot : -1;

        /// <summary>Builds the panning layer, the parallax backdrop and the edge chevrons (PSD battle, wide stages only).</summary>
        private void BuildWideStage1111(Image stageArt)
        {
            if (!WideStage1111 || enemyStageRoot == null || enemyPan1111 != null) return;
            enemyPan1111 = kit.NewRect("WidePan1111", enemyStageRoot);
            PanelKit.Stretch(enemyPan1111);
            foreach (var pair in enemyAnchors) pair.Value.SetParent(enemyPan1111, false);

            // Parallax backdrop over the stage art's upper band: 1.25× the art, clipped above the member cards.
            Sprite art = BattlePsdArt928.Sprite("stage");
            if (art != null && stageArt != null)
            {
                wideBack1111 = kit.NewRect("WideStageBack1111", psdRoot);
                wideBack1111.SetSiblingIndex(stageArt.transform.GetSiblingIndex() + 1);
                PanelKit.PlaceTop(wideBack1111, 0f, 0f, 720f, EnemyStageFoot1031 * PsdScale);
                wideBack1111.gameObject.AddComponent<RectMask2D>();
                Image back = kit.NewImage("WideStageBackArt1111", wideBack1111, art, Color.white);
                back.raycastTarget = false;
                wideBackArt1111 = back.rectTransform;
                // The art's colour treatment matches the stage art beneath it.
                back.color = stageArt.color;
            }

            for (int side = 0; side < 2; side++)
            {
                RectTransform chevron = kit.NewRect(side == 0 ? "WideChevronLeft1111" : "WideChevronRight1111", psdRoot);
                PanelKit.PlaceTop(chevron, side == 0 ? 6f : 650f, 2050f * PsdScale - 50f, 64f, 100f);
                Image hit = chevron.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                for (int arm = 0; arm < 2; arm++)
                {
                    Image bar = kit.NewImage("Arm" + arm, chevron, null, Brand1025.Teal);
                    bar.raycastTarget = false;
                    var r = bar.rectTransform;
                    r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
                    r.pivot = new Vector2(.5f, .5f);
                    r.sizeDelta = new Vector2(46f, 11f);
                    float dir = side == 0 ? 1f : -1f;
                    r.anchoredPosition = new Vector2(0f, arm == 0 ? 15f : -15f);
                    r.localEulerAngles = new Vector3(0f, 0f, (arm == 0 ? 45f : -45f) * dir);
                    var outline = bar.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, .75f); outline.effectDistance = new Vector2(2f, -2f);
                }
                int look = side;
                Button button = chevron.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.onClick.AddListener(() => LookToward1111(look == 0 ? -1 : 1));
                chevron.gameObject.SetActive(false);
                wideChevrons1111[side] = chevron;
            }
            ApplyWideCamera1111();
        }

        /// <summary>Lays out the living minions in their slots. Mirrors <see cref="LayoutEnemyFigures1031"/>.</summary>
        private void LayoutWideFigures1111(List<BattleUnit> active)
        {
            foreach (int gone in wideSlots1111.Keys.Where(id => active.All(u => u.Id != id)).ToList())
            { wideSlots1111.Remove(gone); enemyHeads1031.Remove(gone); }
            foreach (BattleUnit unit in active)
            {
                if (wideSlots1111.ContainsKey(unit.Id)) continue;
                // A newcomer takes the free slot nearest the middle of what the camera shows.
                float centre = wideCamera1111 + PsdStageWidth1031 / 2f;
                int best = -1;
                for (int slot = 0; slot < 3; slot++)
                {
                    if (wideSlots1111.ContainsValue(slot)) continue;
                    if (best < 0 || Mathf.Abs(SlotCentre1111(slot) - centre) < Mathf.Abs(SlotCentre1111(best) - centre)) best = slot;
                }
                if (best >= 0) wideSlots1111[unit.Id] = best;
            }
            float top = EnemyStageTop1031, foot = EnemyStageFoot1031;
            foreach (BattleUnit unit in active)
            {
                if (!wideSlots1111.TryGetValue(unit.Id, out int slot)) continue;
                if (!enemyFigures.TryGetValue(unit.Id, out Image figure) || !enemyMotions.TryGetValue(unit.Id, out EnemyUnitPresentation motion)) continue;
                float x = slot * WideLane1111;
                PsdPlace(enemyAnchors[unit.Id], x, top, WideLane1111, foot - top);
                var rect = motion.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                rect.pivot = Vector2.one * .5f;
                Sprite sprite = figure.sprite;
                float ratio = sprite != null ? sprite.rect.width / sprite.rect.height : .5f;
                float visualHeight = Mathf.Min(foot - top, WideLane1111 * MinionWidthAllowance1107 / Mathf.Max(.1f, ratio));
                rect.sizeDelta = new Vector2(visualHeight * ratio, visualHeight) * PsdScale;
                motion.SetLayoutOrigin(new Vector2(0f, -(foot - top - visualHeight / 2f) * PsdScale));
                // Heads are kept in row space; EnemyHead1031 subtracts the pan.
                enemyHeads1031[unit.Id] = new Rect((x + WideLane1111 / 2f) * PsdScale, (foot - visualHeight * .93f) * PsdScale,
                    visualHeight * ratio * PsdScale, visualHeight * PsdScale);
            }
            if (wideLock1111 >= 0 && !wideSlots1111.ContainsKey(wideLock1111)) wideLock1111 = -1;
            wideGoal1111 = WideGoal1111();
        }

        private static float SlotCentre1111(int slot) => slot * WideLane1111 + WideLane1111 / 2f;

        /// <summary>Pan that centres <paramref name="slot"/> (clamped to the row).</summary>
        internal static float WideFrame1111(int slot) =>
            Mathf.Clamp(SlotCentre1111(slot) - PsdStageWidth1031 / 2f, 0f, WidePanMax1111);

        /// <summary>Where the camera rests: on the locked enemy, or on everyone when the living all fit on screen.</summary>
        private float WideGoal1111()
        {
            if (wideSlots1111.Count == 0) return wideGoal1111;
            int min = wideSlots1111.Values.Min(), max = wideSlots1111.Values.Max();
            if (wideLock1111 >= 0 && wideSlots1111.TryGetValue(wideLock1111, out int locked))
            {
                // Keep a neighbour in view too when both fit.
                if (max - min <= 1) return Mathf.Clamp(min * WideLane1111, 0f, WidePanMax1111);
                return WideFrame1111(locked);
            }
            if (max - min <= 1) return Mathf.Clamp(min * WideLane1111, 0f, WidePanMax1111);
            return Mathf.Clamp(wideGoal1111, 0f, WidePanMax1111);
        }

        /// <summary>Points the camera at <paramref name="unit"/> (an enemy) and holds there.</summary>
        private void AimWideCamera1111(BattleUnit unit)
        {
            if (!WideStage1111 || unit == null || unit.Side != BattleSide.Enemy) return;
            if (!wideSlots1111.ContainsKey(unit.Id)) return;
            wideLock1111 = unit.Id;
            wideGoal1111 = WideGoal1111();
        }

        /// <summary>Called as an action starts: our strikes look at their target, an enemy's turn looks at the enemy.</summary>
        private void AimForAction1111(BattleUnit actor, BattleUnit target)
        {
            if (!WideStage1111 || actor == null || HoldWideCamera1111) return;
            if (actor.Side == BattleSide.Enemy) AimWideCamera1111(actor);
            else if (target != null && target.Side == BattleSide.Enemy) AimWideCamera1111(target);
            else if (wideSlots1111.Count > 0)
            {
                // A sweep over all of them: frame the middle of the row.
                wideLock1111 = -1;
                wideGoal1111 = WidePanMax1111 / 2f;
            }
        }

        /// <summary>The chevron: look at (and aim at) the nearest enemy off that edge.</summary>
        private void LookToward1111(int dir)
        {
            if (paused || battle == null) return;
            BattleUnit best = null; float bestDistance = float.MaxValue;
            float centre = wideCamera1111 + PsdStageWidth1031 / 2f;
            foreach (var pair in wideSlots1111)
            {
                float d = (SlotCentre1111(pair.Value) - centre) * dir;
                if (d <= 0f || d >= bestDistance) continue;
                BattleUnit unit = battle.FindUnit(pair.Key);
                if (unit == null || !unit.Alive) continue;
                best = unit; bestDistance = d;
            }
            if (best == null) return;
            if (battle.FocusEnemy(best.Id)) RefreshRealtimeCommands();
            AimWideCamera1111(best);
        }

        /// <summary>Per-frame follow (Update). Also follows a tapped focus target.</summary>
        private void TickWideCamera1111()
        {
            if (enemyPan1111 == null) return;
            int focus = battle.FocusTargetId;
            if (!HoldWideCamera1111 && focus >= 0 && focus != wideLock1111 && wideSlots1111.ContainsKey(focus) && focusSeen1111 != focus)
                AimWideCamera1111(battle.FindUnit(focus));
            focusSeen1111 = focus;
            float dt = Mathf.Min(.05f, Time.unscaledDeltaTime) * Mathf.Max(1f, speed * .75f);
            if (BattlePresentationOptions.ReduceMotion) wideCamera1111 = wideGoal1111;
            else wideCamera1111 = Mathf.SmoothDamp(wideCamera1111, wideGoal1111, ref wideVelocity1111, WidePanSeconds1111 / 2.2f,
                Mathf.Infinity, dt);
            if (Mathf.Abs(wideCamera1111 - wideGoal1111) < .5f) { wideCamera1111 = wideGoal1111; wideVelocity1111 = 0f; }
            ApplyWideCamera1111();
        }

        private int focusSeen1111 = -1;

        private void ApplyWideCamera1111()
        {
            if (enemyPan1111 == null) return;
            float pan = wideCamera1111 * PsdScale;
            enemyPan1111.anchoredPosition = new Vector2(-pan, 0f);
            if (wideBackArt1111 != null)
            {
                // 1.25× art scaled about the stage centre (PSD y 2200); moves half as far as the row.
                const float zoom = 1.25f;
                float w = 720f * zoom, h = 6144f * PsdScale * zoom;
                float y = 2200f * PsdScale * (1f - zoom);
                float range = w - 720f;
                float x = -range * (wideCamera1111 / WidePanMax1111);
                PanelKit.PlaceTop(wideBackArt1111, x, y, w, h);
            }
            float left = wideCamera1111, right = wideCamera1111 + PsdStageWidth1031;
            bool offLeft = false, offRight = false;
            foreach (var pair in wideSlots1111)
            {
                BattleUnit unit = battle.FindUnit(pair.Key);
                if (unit == null || !unit.Alive) continue;
                float c = SlotCentre1111(pair.Value);
                // Off screen = less than a quarter of her lane shows (a half-visible enemy is still there to tap).
                if (c + WideLane1111 * .25f < left) offLeft = true;
                if (c - WideLane1111 * .25f > right) offRight = true;
            }
            bool show = battle.Outcome == BattleOutcome.Ongoing;
            if (wideChevrons1111[0] != null && wideChevrons1111[0].gameObject.activeSelf != (show && offLeft))
                wideChevrons1111[0].gameObject.SetActive(show && offLeft);
            if (wideChevrons1111[1] != null && wideChevrons1111[1].gameObject.activeSelf != (show && offRight))
                wideChevrons1111[1].gameObject.SetActive(show && offRight);
            foreach (RectTransform chevron in wideChevrons1111)
                if (chevron != null && chevron.gameObject.activeSelf && chevron.GetSiblingIndex() != chevron.parent.childCount - 1)
                    chevron.SetAsLastSibling();
        }

        /// <summary>Hangs a wide-stage plate beside its head, inside the panning row.</summary>
        private bool PositionWidePlate1111(CellView cell)
        {
            BattleUnit unit = cell.Unit;
            if (unit == null || !enemyHeads1031.TryGetValue(unit.Id, out Rect head)) return false;
            if (!wideSlots1111.TryGetValue(unit.Id, out int slot)) return false;
            bool left = slot != 2;
            var tag = BattlePsdArt928.Find(left ? "enemy-tag-l" : "enemy-tag-r");
            if (tag == null) return false;
            cell.Rect.SetParent(enemyPan1111, false);
            float w = tag.width * PsdScale * EnemyPlateScale1031, h = tag.height * PsdScale * EnemyPlateScale1031;
            float gap = Mathf.Clamp(head.width * .16f, 26f, 70f);
            float x = left ? head.x - gap - w : head.x + gap;
            float laneLeft = slot * WideLane1111 * PsdScale;
            x = Mathf.Clamp(x, laneLeft + 4f, laneLeft + WideLane1111 * PsdScale - 4f - w);
            float y = Mathf.Max(head.y - h * .55f, EnemyStageTop1031 * PsdScale - 8f);
            PanelKit.PlaceTop(cell.Rect, x, y, tag.width * PsdScale, tag.height * PsdScale);
            cell.Rect.localScale = Vector3.one * EnemyPlateScale1031;
            if (psdFrames.TryGetValue(cell, out Image frame)) frame.sprite = BattlePsdArt928.Sprite(tag.key);
            cell.Root.transform.SetAsLastSibling();
            if (frame != null) frame.GetComponentInChildren<StatusBadges1026>(true)?.AlignRow(!left, EnemyPlateBadgeOffset1031);
            var floating = cell.Root.GetComponent<BattleHeadEffects1019>();
            if (floating != null && floating.enabled) floating.enabled = false;
            return true;
        }

        /// <summary>Speech bubbles go on the side with more room on screen.</summary>
        private bool BarkOnRight1111(BattleUnit unit, Rect head)
        {
            if (!WideStage1111) return !enemyLanes1031.TryGetValue(unit.Id, out int lane) || lane != 1;
            return head.x < 360f;
        }
    }
}
