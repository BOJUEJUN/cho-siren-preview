using System.Collections.Generic;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren.Panels
{
    /// <summary>
    /// 2026-11-01 赵金 battle feedback (battle-fx):
    ///  · 「错层了」 the small-skill close-up is a complete foreground beat. It lives in its own layer above the whole
    ///    PSD stage (enemy figures, head plates, bubbles, skill rings) and the status pop-ups, below the pause / rules /
    ///    result overlays; while it shows, the enemy plates and speech bubble fade out and come back afterwards.
    ///  · 「伤害数字要这么大参考天命之子」 our hits on enemies use the big glyph numbers (BigDamageNumbers1101);
    ///    damage on our members keeps the moderate pop-ups.
    /// Both layers mirror the fitted PSD root, so they share its coordinates and scale on every screen shape.
    /// </summary>
    public sealed partial class TacticsBattlePanel
    {
        private RectTransform bigDamageLayer1101, foreground1101;
        private BigDamageNumbers1101 bigDamage1101;
        private BattleFx1101Driver fxDriver1101;

        /// <summary>Called at the end of BuildPsdPresentation1019 (after the close-up and the pop-up layer exist).</summary>
        private void BuildBattleFx1101()
        {
            if (psdRoot == null || bigDamageLayer1101 != null) return;
            bigDamageLayer1101 = MirrorLayer1101("BigDamage1101");
            // No RectMask2D: the close-up crops its art to the stage itself (UVs), so it never depends on a clipper.
            foreground1101 = MirrorLayer1101("BattleForeground1101");
            // Order on the panel: … PSD stage · SkillEffects · Popups · BigDamage1101 · BattleForeground1101 · … overlays.
            int above = popupLayer != null ? popupLayer.GetSiblingIndex() + 1 : psdRoot.GetSiblingIndex() + 1;
            bigDamageLayer1101.SetSiblingIndex(above);
            foreground1101.SetSiblingIndex(bigDamageLayer1101.GetSiblingIndex() + 1);
            if (psdCloseup1019 != null && psdCloseup1019.Root != null) psdCloseup1019.Root.SetParent(foreground1101, false);

            bigDamage1101 = bigDamageLayer1101.gameObject.AddComponent<BigDamageNumbers1101>();
            bigDamage1101.Configure(bigDamageLayer1101, () => paused || closing,
                // The number bursts out during the hit-stop instead of waiting for it; speed shortens it, gently.
                () => Time.unscaledDeltaTime * (1f + (Mathf.Clamp(speed, 1, 3) - 1) * .6f),
                () => psdCloseup1019 != null && psdCloseup1019.HoldsForeground,
                // The enemy stage: under the HUD, above the member cards.
                new Rect(6f, EnemyStageTop1031 * PsdScale - 40f, 708f, (EnemyStageFoot1031 - EnemyStageTop1031) * PsdScale + 40f));

            fxDriver1101 = gameObject.AddComponent<BattleFx1101Driver>();
            fxDriver1101.Panel = this;
            fxDriver1101.MirrorNow();
        }

        private RectTransform MirrorLayer1101(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            CopyRect1101(psdRoot, rect);
            return rect;
        }

        private static void CopyRect1101(RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;
            // Only on change: this runs every frame and an unchanged rect must not dirty the canvas.
            if (to.anchorMin != from.anchorMin) to.anchorMin = from.anchorMin;
            if (to.anchorMax != from.anchorMax) to.anchorMax = from.anchorMax;
            if (to.pivot != from.pivot) to.pivot = from.pivot;
            if (to.sizeDelta != from.sizeDelta) to.sizeDelta = from.sizeDelta;
            if (to.anchoredPosition != from.anchoredPosition) to.anchoredPosition = from.anchoredPosition;
            if (to.localScale != from.localScale) to.localScale = from.localScale;
            if (to.localRotation != from.localRotation) to.localRotation = from.localRotation;
        }

        /// <summary>
        /// Our hit on an enemy as a big number. False when the generic pop-up should show it instead
        /// (our members hurt, poison ticks, shield absorption, no PSD stage).
        /// </summary>
        private bool SpawnBigDamage1101(BattleUnit actor, BattleUnit target, BattleEvent entry)
        {
            if (bigDamage1101 == null || psdRoot == null || target == null || target.Side != BattleSide.Enemy) return false;
            if (entry.SkillId == "rt-poison" || entry.AbsorbedAmount > 0) return false;
            int amount = DamageHpLoss(entry);
            if (amount <= 0) return false;
            bool player = actor != null && actor.Side == BattleSide.Player;
            bool ultimate = player && entry.SkillId == battle.ActiveSkillId(actor, true);
            bool skill = player && (ultimate || entry.SkillId == battle.ActiveSkillId(actor, false));
            DamageNumberStyle1101 style = entry.Critical ? DamageNumberStyle1101.Critical
                : skill ? DamageNumberStyle1101.Skill : DamageNumberStyle1101.Gold;
            if (!BigDamageAnchor1101(target, out Vector2 at)) return false;
            return bigDamage1101.Spawn(at, amount, style, target.Id, ultimate ? 1.12f : 1f);
        }

        /// <summary>Centre of the enemy's visible body, in stage units from the PSD root's top-left.</summary>
        private bool BigDamageAnchor1101(BattleUnit target, out Vector2 at)
        {
            at = default;
            RectTransform visual = UnitVisual(target) ?? FindCell(target)?.Rect;
            if (visual == null) return false;
            Vector3 world = visual.TransformPoint(visual.rect.center);
            Vector2 local = psdRoot.InverseTransformPoint(world);
            Rect r = psdRoot.rect;
            at = new Vector2(local.x - r.xMin, r.yMax - local.y);
            // A little above the middle of the figure (chest height); the boss rig is tall, so lift it more.
            float lift = target.Id == bossUnitId ? .12f : .06f;
            Vector3 top = visual.TransformPoint(new Vector3(visual.rect.center.x, visual.rect.yMax, 0f));
            float h = Mathf.Abs(((Vector2)psdRoot.InverseTransformPoint(top)).y - local.y) * 2f;
            at.y -= h * lift;
            return true;
        }

        // ------------------------------------------------------------------ foreground fade of enemy readouts

        private readonly List<Graphic> fadedReadouts1101 = new List<Graphic>();
        private readonly List<Graphic> readoutScratch1101 = new List<Graphic>();
        private float readoutAlpha1101 = 1f;

        private void CollectEnemyReadouts1101()
        {
            fadedReadouts1101.Clear();
            foreach (CellView cell in cells)
            {
                if (cell.Side != BattleSide.Enemy || cell.Root == null || cell.Root.transform.parent != psdRoot || !cell.Root.activeInHierarchy) continue;
                AddReadout1101(cell.Root.transform);
            }
            for (int i = 0; i < psdRoot.childCount; i++)
            {
                Transform child = psdRoot.GetChild(i);
                if (child.name == "BossPlate1031" || child.name == "EnemyBark1031") AddReadout1101(child);
            }
        }

        private void AddReadout1101(Transform root)
        {
            root.GetComponentsInChildren(true, readoutScratch1101);
            fadedReadouts1101.AddRange(readoutScratch1101);
            readoutScratch1101.Clear();
        }

        private void SetReadoutAlpha1101(float alpha)
        {
            for (int i = 0; i < fadedReadouts1101.Count; i++)
                if (fadedReadouts1101[i] != null) fadedReadouts1101[i].canvasRenderer.SetAlpha(alpha);
        }

        /// <summary>Per frame (LateUpdate): mirrors the PSD root and fades the enemy plates/bubble under the close-up.</summary>
        private void TickBattleFx1101()
        {
            if (psdRoot == null) return;
            CopyRect1101(psdRoot, bigDamageLayer1101);
            CopyRect1101(psdRoot, foreground1101);
            if (psdCloseup1019 != null)
            {
                var fit = psdRoot.GetComponent<BattlePsdViewportFit1019>();
                if (fit != null)
                {
                    fit.RefreshFit();
                    Rect visual = fit.VisualViewport;
                    psdCloseup1019.SetViewportEdges(visual.xMin - psdRoot.rect.xMin, visual.xMax - psdRoot.rect.xMin);
                }
            }
            float presence = psdCloseup1019 != null ? psdCloseup1019.Presence : 0f;
            float target = 1f - presence;
            if (target < .999f && fadedReadouts1101.Count == 0) CollectEnemyReadouts1101();
            if (fadedReadouts1101.Count == 0) { readoutAlpha1101 = 1f; return; }
            readoutAlpha1101 = target;
            SetReadoutAlpha1101(readoutAlpha1101);
            if (readoutAlpha1101 >= .999f) { SetReadoutAlpha1101(1f); fadedReadouts1101.Clear(); }
        }

        /// <summary>Test / capture hook: the alpha the enemy plates and bubble currently carry (1 = fully shown).</summary>
        internal float EnemyReadoutAlpha1101 => readoutAlpha1101;

        private sealed class BattleFx1101Driver : MonoBehaviour
        {
            public TacticsBattlePanel Panel;
            public void MirrorNow() { if (Panel != null) Panel.TickBattleFx1101(); }
            private void LateUpdate() { if (Panel != null) Panel.TickBattleFx1101(); }
            private void OnDisable()
            {
                if (Panel == null) return;
                Panel.SetReadoutAlpha1101(1f);
                Panel.fadedReadouts1101.Clear();
                Panel.readoutAlpha1101 = 1f;
            }
        }
    }
}
