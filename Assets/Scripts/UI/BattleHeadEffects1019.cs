using System;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Shows effects on the affected actor, not the captain's prospective dice bonuses.</summary>
    public sealed class BattleHeadEffects1019 : MonoBehaviour
    {
        RectTransform host, strip;
        BattleSimulator battle;
        Func<BattleUnit> unit;
        Func<RectTransform> figure;
        PanelKit kit;
        readonly GameObject[] slots = new GameObject[3];
        readonly SkillIconGraphic[] glyphs = new SkillIconGraphic[3];
        readonly BattlePsdEffectIcon1019[] authoredGlyphs = new BattlePsdEffectIcon1019[3];
        const float SlotSize = 32, SlotStep = 34;
        UnityEngine.UI.Text count;
        float refresh;
        public int DisplayedPoisonStacks { get; private set; }
        public int DisplayedCount { get; private set; }
        public void Configure(RectTransform parent, BattleSimulator model, Func<BattleUnit> actor, Func<RectTransform> visual)
        {
            if (parent == null || model == null) throw new ArgumentNullException();
            host = parent; battle = model; unit = actor; figure = visual;
            if (strip != null) return;
            kit = new PanelKit("HeadEffects1019"); strip = kit.NewRect("AffectedUnitHeadEffects", host);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = kit.NewPanel("Effect-" + i, strip, new Color32(8, 5, 24, 230), 5);
                PanelKit.PlaceTop((RectTransform)slots[i].transform, i * SlotStep, 0, SlotSize, SlotSize);
                glyphs[i] = SkillIconVisuals.Create(slots[i].transform, "EffectSymbol", string.Empty, string.Empty, Color.white);
                PanelKit.PlaceTop(glyphs[i].rectTransform, 3, 3, 26, 26); glyphs[i].raycastTarget = false;
                authoredGlyphs[i] = glyphs[i].gameObject.AddComponent<BattlePsdEffectIcon1019>();
                authoredGlyphs[i].Configure(glyphs[i], SkillIconKind.Poison, "effect-poison-drop");
                slots[i].GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            }
            count = kit.NewPlacedText(slots[0].transform, string.Empty, 12, Color.white, 20, 18, 14, 14, TextAnchor.MiddleCenter, FontStyle.Bold);
            count.raycastTarget = false;
            var edge = count.gameObject.AddComponent<UnityEngine.UI.Outline>(); edge.effectColor = Color.black; edge.effectDistance = Vector2.one;
            Refresh();
        }
        void Update() { refresh -= Time.unscaledDeltaTime; if (refresh <= 0) { refresh = .1f; Refresh(); } else Position(); }
        public void Refresh()
        {
            if (strip == null) return;
            var actor = unit?.Invoke(); DisplayedPoisonStacks = actor != null && actor.Alive ? battle.PoisonStacks(actor) : 0;
            DisplayedCount = 0;
            if (actor == null || !actor.Alive) { strip.gameObject.SetActive(false); return; }
            if (DisplayedPoisonStacks > 0) Add(SkillIconKind.Poison, CombatFeedbackPalette.Debuff);
            if (battle.ConditionRemaining(actor, CombatCondition.Stun) > 0) Add(SkillIconKind.Burst, CombatFeedbackPalette.Control);
            else if (battle.ConditionRemaining(actor, CombatCondition.ArmorBreak) > 0) Add(SkillIconKind.Pierce, PanelKit.Gold);
            if (actor.Shield > 0 || battle.GuardRemaining(actor) > 0) Add(SkillIconKind.Shield, CombatFeedbackPalette.Shield);
            for (int i = 0; i < slots.Length; i++) slots[i].SetActive(i < DisplayedCount);
            count.text = DisplayedPoisonStacks > 0 ? DisplayedPoisonStacks.ToString() : string.Empty;
            strip.gameObject.SetActive(DisplayedCount > 0); Position();
        }
        void Add(SkillIconKind kind, Color color)
        {
            if (DisplayedCount >= glyphs.Length) return;
            authoredGlyphs[DisplayedCount].Apply(kind, color); DisplayedCount++;
        }
        void Position()
        {
            var visual = figure?.Invoke();
            if (strip == null || visual == null || !strip.gameObject.activeSelf) return;
            float width = DisplayedCount * SlotStep - (SlotStep - SlotSize);
            var actor = unit?.Invoke();
            if (actor != null && actor.Side == BattleSide.Player && transform is RectTransform card)
            {
                // 2026-10-23 review: our lasting effects hang on the member's own card (top-right corner
                // of her portrait), not floating mid-stage above the cards.
                if (strip.parent != card) { strip.SetParent(card, false); strip.SetAsLastSibling(); }
                Vector3 corner = card.InverseTransformPoint(visual.TransformPoint(new Vector2(visual.rect.xMax, visual.rect.yMax)));
                PanelKit.PlaceTop(strip, Mathf.Clamp(corner.x - card.rect.xMin - width - 4, 0, card.rect.width - width),
                    Mathf.Max(0, card.rect.yMax - corner.y + 6), width, SlotSize);
                return;
            }
            Vector3 p = host.InverseTransformPoint(visual.TransformPoint(new Vector2(visual.rect.center.x, visual.rect.yMax)));
            PanelKit.PlaceTop(strip, Mathf.Clamp(p.x - host.rect.xMin - width * .5f, 4, host.rect.width - width - 4),
                Mathf.Clamp(host.rect.yMax - p.y - SlotSize - 4, 205, host.rect.height - 390), width, SlotSize);
        }
        void OnDisable() { if (strip != null) strip.gameObject.SetActive(false); }
        void OnDestroy() { if (strip != null) Destroy(strip.gameObject); kit?.Dispose(); }
    }
}
