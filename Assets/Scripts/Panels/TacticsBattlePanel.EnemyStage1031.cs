using System;
using System.Collections.Generic;
using System.Linq;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-10-31 (赵金: "对面的怪一定要大、受击做上动画看起来才过瘾"): the enemy stage of the PSD battle.
    /// The boss fills the stage from under the top HUD down to the member cards (refs/07), up to two minions
    /// stand side by side at half the stage each (refs/08), each enemy's name + HP plate hangs beside its head
    /// (smaller than before) and carries the statuses our side put on it. Hit weight per target lives here too.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        // ---- geometry, PSD units (the stage art is 2946 × 6144)
        /// <summary>Just under the top HUD's health bar (battle-52 ends at 678); no enemy art may rise above it (2026-11-08).</summary>
        internal const float EnemyStageTop1031 = 735f;
        /// <summary>The member records start at 3670; feet stand right on their top edge.</summary>
        internal const float EnemyStageFoot1031 = 3690f;
        internal const float PsdStageWidth1031 = 2946f;
        /// <summary>Head plates are 72% of the PSD's corner name frames.</summary>
        internal const float EnemyPlateScale1031 = .72f;
        /// <summary>2026-11-07: the boss is drawn a third larger than the 1031 stage fit.</summary>
        internal const float BossSizeBoost1107 = 4f / 3f;
        /// <summary>Widest the boosted boss may be (canvas units; the stage is 720 wide).</summary>
        internal const float BossMaxWidth1107 = 860f; // about 1/10 of the stage spills past each side, clipped by the screen
        /// <summary>2026-11-07 (赵金: "角色太小"): wide minions may spill this far past their half-stage lane.</summary>
        internal const float MinionWidthAllowance1107 = 1.45f;
        private static readonly Vector2 EnemyPlateBadgeOffset1031 = new Vector2(70f, 22f);
        private const float EnemyPlateBadgeHeight1031 = 30f;

        /// <summary>Lane per enemy: 0 left, 1 right, 2 centre (a lone minion of its wave).</summary>
        private readonly Dictionary<int, int> enemyLanes1031 = new Dictionary<int, int>();
        /// <summary>Head point (x, y from the PSD root's top-left, canvas units) and visible figure size.</summary>
        private readonly Dictionary<int, Rect> enemyHeads1031 = new Dictionary<int, Rect>();
        private Rect bossHead1031;
        private Text bossPlateName1031, bossPlateHp1031;

        // ------------------------------------------------------------------ boss

        /// <summary>Sizes the boss rig so the visible figure spans the stage height (refs/07) and re-centres its effects.</summary>
        private void LayoutBossStage1031(RectTransform rig)
        {
            if (rig == null || bossPresentation == null) return;
            var stage = (RectTransform)bossPresentation.transform;
            float stageTop = -stage.anchoredPosition.y;
            Image portrait = rig.Find("BossPortrait")?.GetComponent<Image>();
            Sprite art = portrait != null ? portrait.sprite : null;
            float ratio = art != null ? art.rect.width / Mathf.Max(1f, art.rect.height) : 1f;
            BossArtFrame1031(art, out float visTop, out float visBottom, out Vector2 headFraction);
            float foot = 3670f * PsdScale, topLimit = EnemyStageTop1031 * PsdScale - 2f;
            // 2026-11-07 (赵金: "BOSS 再大一点，再大 3 分之一"): a third larger than the old 640-wide fit…
            // 2026-11-08 (少爷: "可以大但是 UI 问题太大了"): …but her visible figure never rises into the top HUD
            // and her feet stay on the member cards, so the boss fills the stage exactly and covers no control text.
            float fitHeight = (foot - topLimit) / Mathf.Max(.2f, visBottom - visTop);
            float h = Mathf.Min(fitHeight, 640f / Mathf.Max(.2f, ratio) * BossSizeBoost1107);
            // Never wider than the stage plus a sliver each side, so her silhouette stays on screen and clear of the adds.
            h = Mathf.Min(h, BossMaxWidth1107 / Mathf.Max(.2f, ratio));
            float w = h * ratio;
            float top = foot - visBottom * h;
            PanelKit.PlaceTop(rig, 360f - w / 2f, top - stageTop, w, h);
            PanelKit.CenterPivot(rig);
            bossPresentation.SetLayoutOrigin(rig.anchoredPosition);
            bossHead1031 = new Rect(360f - w / 2f + headFraction.x * w, top + headFraction.y * h, w * .5f, h);

            float cx = 360f, cy = top - stageTop + h / 2f, ground = foot - stageTop;
            PlaceStagePart1031(stage, "BossAuraBack", cx, cy, w * 1.2f, w * 1.2f);
            PlaceStagePart1031(stage, "BossAuraCore", cx, cy, w * .95f, w * .95f);
            PlaceStagePart1031(stage, "BossChargeAuraAI", cx, cy, w * 1.08f, h * .9f);
            PlaceStagePart1031(stage, "BossGroundShadow", cx, ground - 24f, w * .86f, 110f);
            PlaceStagePart1031(stage, "BossStagePulse", cx, ground - 120f, w * 1.15f, 380f);
            PlaceStagePart1031(stage, "BossLowHealthFrameAI", cx, cy, w * 1.12f, h * .8f);
            PlaceStagePart1031(stage, "BossHeartImpactAI", cx, cy, w * .95f, w * .95f);
            PlaceStagePart1031(stage, "BossHitSlashAI", cx, cy, w * 1.05f, h * .85f);
            for (int i = 0; i < 3; i++)
            {
                PlaceStagePart1031(stage, "BossShockwave-" + i, cx, cy, w * 1.1f - i * 64f, h * .78f - i * 40f);
                PlaceStagePart1031(stage, "BossSlashTrail-" + i, cx + (i - 1) * 18f, cy - 110f + i * 80f, w * .92f, 8f + i * 2f);
            }
            // The PSD battle hid the old state caption; its hit reactions re-activate it, and over the larger boss it
            // would sit on her face. Keep it as model state only.
            Text caption = stage.Find("BossAnimationState")?.GetComponent<Text>();
            if (caption != null) caption.enabled = false;
            // The adds stand in front of the boss so they stay readable and tappable.
            if (enemyStageRoot != null && enemyStageRoot.parent == stage.parent &&
                enemyStageRoot.GetSiblingIndex() < stage.GetSiblingIndex())
                enemyStageRoot.SetSiblingIndex(stage.GetSiblingIndex());
        }

        /// <summary>Visible vertical span and head point of the boss illustrations, as fractions of the image.</summary>
        private static void BossArtFrame1031(Sprite art, out float top, out float bottom, out Vector2 head)
        {
            string name = art != null ? art.name : string.Empty;
            if (name.Contains("20260928")) { top = .002f; bottom = .977f; head = new Vector2(.5f, .1f); return; }
            if (name.Contains("boss-throne-user-v1")) { top = .044f; bottom = .938f; head = new Vector2(.5f, .17f); return; }
            // 2026-11-08: measured alpha bounds (crown at the very top, tail fin at the bottom edge).
            if (name.Contains("boss-selene-1103")) { top = .001f; bottom = 1f; head = new Vector2(.5f, .12f); return; }
            top = .03f; bottom = .95f; head = new Vector2(.5f, .15f);
        }

        private static void PlaceStagePart1031(RectTransform stage, string name, float cx, float cy, float w, float h)
        {
            var rect = stage.Find(name) as RectTransform;
            if (rect == null) return;
            PanelKit.PlaceTop(rect, cx - w / 2f, cy - h / 2f, w, h);
            PanelKit.CenterPivot(rect);
        }

        // ------------------------------------------------------------------ minions / adds

        /// <summary>Lays out up to two enemies (boss adds or wave minions). False for a crowd the old layout handles.</summary>
        private bool LayoutEnemyFigures1031()
        {
            var active = battle.Units.Where(u => u.Side == BattleSide.Enemy && u.Alive && u.Id != bossUnitId)
                .OrderBy(u => u.Definition.Id == "noise-wraith" ? 0 : u.Definition.Id == "neon-scout" ? 1 : 2).ToList();
            if (active.Count > BattleSimulator.MaxActiveMinions)
            {
                // Only reachable when a test or old save forces a crowd: the legacy lanes take over.
                enemyLanes1031.Clear(); enemyHeads1031.Clear();
                return false;
            }
            foreach (BattleUnit unit in battle.Units)
            {
                if (!enemyAnchors.TryGetValue(unit.Id, out RectTransform a)) continue;
                bool fading = enemyMotions.TryGetValue(unit.Id, out EnemyUnitPresentation m) && m.IsDefeating;
                a.gameObject.SetActive(unit.Alive || fading);
            }
            AssignEnemyLanes1031(active);
            bool adds = battle.Stage.HasBossPhases;
            foreach (BattleUnit unit in active)
            {
                if (!enemyFigures.TryGetValue(unit.Id, out Image figure) || !enemyMotions.TryGetValue(unit.Id, out EnemyUnitPresentation motion)) continue;
                int lane = enemyLanes1031[unit.Id];
                EnemyLaneBox1031(adds, lane, out float x, out float width, out float top, out float foot);
                RectTransform anchor = enemyAnchors[unit.Id];
                PsdPlace(anchor, x, top, width, foot - top);
                var rect = motion.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
                rect.pivot = Vector2.one * .5f;
                Sprite sprite = figure.sprite;
                float ratio = sprite != null ? sprite.rect.width / sprite.rect.height : .5f;
                // Same footing for every enemy; very wide silhouettes are bounded by their lane.
                float visualHeight = Mathf.Min(foot - top, width * (adds ? 1.2f : MinionWidthAllowance1107) / Mathf.Max(.1f, ratio));
                rect.sizeDelta = new Vector2(visualHeight * ratio, visualHeight) * PsdScale;
                motion.SetLayoutOrigin(new Vector2(0f, -(foot - top - visualHeight / 2f) * PsdScale));
                float centre = x + width / 2f;
                enemyHeads1031[unit.Id] = new Rect(centre * PsdScale, (foot - visualHeight * .93f) * PsdScale,
                    visualHeight * ratio * PsdScale, visualHeight * PsdScale);
            }
            return true;
        }

        /// <summary>A lane is kept while its enemy lives; a newcomer takes the free side.</summary>
        private void AssignEnemyLanes1031(List<BattleUnit> active)
        {
            foreach (int gone in enemyLanes1031.Keys.Where(id => active.All(u => u.Id != id)).ToList())
            { enemyLanes1031.Remove(gone); enemyHeads1031.Remove(gone); }
            bool adds = battle.Stage.HasBossPhases;
            if (active.Count == 1)
            {
                if (!enemyLanes1031.ContainsKey(active[0].Id)) enemyLanes1031[active[0].Id] = adds ? 0 : 2;
                return;
            }
            // Two on stage: nobody stays in the centre.
            foreach (BattleUnit unit in active)
                if (enemyLanes1031.TryGetValue(unit.Id, out int lane) && lane == 2) enemyLanes1031.Remove(unit.Id);
            foreach (BattleUnit unit in active)
            {
                if (enemyLanes1031.ContainsKey(unit.Id)) continue;
                bool leftTaken = enemyLanes1031.Values.Contains(0);
                enemyLanes1031[unit.Id] = leftTaken ? 1 : 0;
            }
        }

        /// <summary>Lane rectangle in PSD units. Minions get half the stage each; boss adds stand at the sides.</summary>
        internal static void EnemyLaneBox1031(bool bossAdds, int lane, out float x, out float width, out float top, out float foot)
        {
            if (bossAdds)
            {
                // Beside a boss the adds stay secondary (the boss is at least twice their size) but still read
                // clearly — about 1.15× the old adds, standing in front of the boss's flanks above the side controls.
                width = 900f; top = 1450f; foot = 2700f;
                x = lane == 1 ? PsdStageWidth1031 - width : 0f;
                return;
            }
            top = EnemyStageTop1031; foot = EnemyStageFoot1031;
            if (lane == 2) { width = 2000f; x = (PsdStageWidth1031 - width) / 2f; return; }
            width = PsdStageWidth1031 / 2f;
            x = lane == 1 ? width : 0f;
        }

        // ------------------------------------------------------------------ head plates

        /// <summary>Hangs a minion's name + HP plate beside its head, on the outer side. False when no head is known.</summary>
        private bool PositionEnemyPlate1031(CellView cell)
        {
            BattleUnit unit = cell.Unit;
            if (unit == null || !enemyHeads1031.TryGetValue(unit.Id, out Rect head)) return false;
            if (!enemyLanes1031.TryGetValue(unit.Id, out int lane)) return false;
            bool left = lane != 1;
            var tag = BattlePsdArt928.Find(left ? "enemy-tag-l" : "enemy-tag-r");
            if (tag == null) return false;
            cell.Rect.SetParent(psdRoot, false);
            Vector2 at = PlateBeside1031(head, left, tag.width * PsdScale * EnemyPlateScale1031, tag.height * PsdScale * EnemyPlateScale1031);
            PanelKit.PlaceTop(cell.Rect, at.x, at.y, tag.width * PsdScale, tag.height * PsdScale);
            cell.Rect.localScale = Vector3.one * EnemyPlateScale1031;
            if (psdFrames.TryGetValue(cell, out Image frame)) frame.sprite = BattlePsdArt928.Sprite(tag.key);
            cell.Root.transform.SetAsLastSibling();
            if (frame != null) frame.GetComponentInChildren<StatusBadges1026>(true)?.AlignRow(!left, EnemyPlateBadgeOffset1031);
            // The plate's own icon + rounds chips replace the separate floating strip over the head.
            var floating = cell.Root.GetComponent<BattleHeadEffects1019>();
            if (floating != null && floating.enabled) floating.enabled = false;
            return true;
        }

        /// <summary>Top-left of a plate of size w × h (canvas units) beside the head, kept on screen and under the HUD.</summary>
        private static Vector2 PlateBeside1031(Rect head, bool left, float w, float h)
        {
            float gap = Mathf.Clamp(head.width * .16f, 26f, 70f);
            float x = left ? head.x - gap - w : head.x + gap;
            float y = head.y - h * .55f;
            x = Mathf.Clamp(x, 4f, 716f - w);
            y = Mathf.Max(y, EnemyStageTop1031 * PsdScale - 8f);
            return new Vector2(x, y);
        }

        /// <summary>The boss's own compact plate (name, HP, statuses) beside its head; replaces the strip under the HUD.</summary>
        private RectTransform BuildBossPlate1031()
        {
            var tag = BattlePsdArt928.Find("enemy-tag-l");
            float tw = tag != null ? tag.width : 667f, th = tag != null ? tag.height : 338f;
            RectTransform root = kit.NewRect("BossPlate1031", psdRoot);
            Vector2 at = PlateBeside1031(bossHead1031, true, tw * PsdScale * EnemyPlateScale1031, th * PsdScale * EnemyPlateScale1031);
            PanelKit.PlaceTop(root, at.x, at.y, tw * PsdScale, th * PsdScale);
            root.localScale = Vector3.one * EnemyPlateScale1031;
            Image frame = kit.NewImage("BossPlateFrame", root, BattlePsdArt928.Sprite("enemy-tag-l"), Color.white);
            PanelKit.Stretch(frame.rectTransform);
            frame.raycastTarget = false;
            bossPlateName1031 = PlateText1031(root, "BossPlateName", 95, 55, 470, 110, 19);
            bossPlateHp1031 = PlateText1031(root, "BossPlateHp", 95, 175, 470, 95, 16);
            int boss = bossUnitId;
            StatusBadges1026.Attach(frame.rectTransform, kit.Font, () => battle.StatusesOf(battle.FindUnit(boss)), true,
                EnemyPlateBadgeOffset1031, EnemyPlateBadgeHeight1031, true);
            root.gameObject.AddComponent<PlateTicker1031>().Tick = RefreshBossPlate1031;
            RefreshBossPlate1031();
            root.SetAsLastSibling();
            return root;
        }

        private Text PlateText1031(Transform parent, string name, float x, float y, float w, float h, int size)
        {
            Text text = kit.NewPlacedText(parent, string.Empty, size, Color.white, 0, 0, 10, 10, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            text.name = name; text.raycastTarget = false;
            PsdPlace(text.rectTransform, x, y, w, h);
            text.rectTransform.localEulerAngles = new Vector3(0, 0, 4f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = UnityNull.Real(text.GetComponent<Outline>()) ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(1.1f, -1.1f);
            return text;
        }

        private void RefreshBossPlate1031()
        {
            if (bossPlateName1031 == null || battle == null) return;
            BattleUnit boss = battle.FindUnit(bossUnitId);
            bool show = boss != null && boss.Alive;
            Transform root = bossPlateName1031.transform.parent;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            bossPlateName1031.text = boss.Definition.Name;
            bossPlateHp1031.text = $"{boss.Hp}/{boss.MaxHp}";
        }

        /// <summary>Refreshes a plate a few times a second (HP and visibility follow the model).</summary>
        private sealed class PlateTicker1031 : MonoBehaviour
        {
            public Action Tick;
            private float next;
            private void LateUpdate()
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .12f;
                Tick?.Invoke();
            }
        }

        /// <summary>Head point (canvas units, from the PSD root's top-left) of an enemy, for plates and speech bubbles.</summary>
        private bool EnemyHead1031(BattleUnit unit, out Rect head)
        {
            head = default;
            if (unit == null) return false;
            if (unit.Id == bossUnitId && bossPresentation != null) { head = bossHead1031; return head.width > 0f; }
            return enemyHeads1031.TryGetValue(unit.Id, out head);
        }

        // ------------------------------------------------------------------ hit weight

        private int HitPower1031(BattleUnit actor, BattleEvent entry)
        {
            bool player = actor != null && actor.Side == BattleSide.Player;
            return player && entry.SkillId == battle.ActiveSkillId(actor, true) ? 2
                : entry.Critical || player && entry.SkillId == battle.ActiveSkillId(actor, false)
                  || entry.SkillId == "rt-enemy-heavy" || entry.SkillId == "rt-enemy-finale" ? 1 : 0;
        }

        /// <summary>Boss hits stop a little longer than minion hits (≈ 90 / 120 ms; ultimates keep 200 ms).</summary>
        private float HitStopSeconds1031(BattleUnit target, int power)
        {
            bool boss = target != null && target.Id == bossUnitId && target.Side == BattleSide.Enemy;
            if (power >= 2) return .2f;
            if (boss) return power == 1 ? .12f : .09f;
            return power == 1 ? .13f : .067f;
        }

        private float ShakeStrength1031(BattleUnit target, int power, bool towardsUs)
        {
            bool boss = target != null && target.Id == bossUnitId && target.Side == BattleSide.Enemy;
            if (boss) return power == 2 ? 13f : power == 1 ? 9f : 4.5f;
            return power == 2 ? 11f : power == 1 ? 7f : towardsUs ? 5f : 3f;
        }

        /// <summary>The boss's own reaction, with the blow's direction and weight.</summary>
        private void PlayBossHit1031(BattleUnit actor, BattleUnit target, BattleEvent entry)
        {
            Vector2 kick = psdRoot != null ? KickDir1021(actor, target) : Vector2.up;
            bossPresentation.PlayHitFrom(DamageHpLoss(entry), entry.Critical, entry.DiceMultiplierPermille, kick,
                entry.SkillId == "rt-poison" ? 0 : HitPower1031(actor, entry));
        }

        /// <summary>Test / capture hook: a hit of the given weight on <paramref name="target"/>, as the battle would play it.</summary>
        private void DebugEnemyHit1031(BattleUnit target, bool critical, int power)
        {
            if (target == null) return;
            Vector2 kick = Vector2.up;
            if (target.Id == bossUnitId && bossPresentation != null)
            {
                bossPresentation.DecoratedHits = psdRoot == null;
                bossPresentation.PlayHitFrom(1234, critical, 1000, kick, power);
            }
            else if (enemyMotions.TryGetValue(target.Id, out EnemyUnitPresentation motion))
                motion.PlayHit(critical, power >= 1, kick, power);
        }
    }
}
