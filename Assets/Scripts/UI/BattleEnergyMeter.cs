using System;
using ChoSiren.Panels;
using ChoSiren.Systems.Tactics;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// Per-character 0-100 ultimate energy meter. The number always comes from
    /// <see cref="BattleSimulator.EnergyOf"/>; this component only draws it and never advances it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleEnergyMeter : MonoBehaviour
    {
        public static readonly Color TrackColor = new Color32(15, 22, 54, 255);
        public static readonly Color FillColor = new Color32(72, 124, 255, 255);
        public static readonly Color FillReadyColor = new Color32(128, 206, 255, 255);
        public static readonly Color LabelColor = new Color32(186, 214, 255, 255);
        // Color32 -> Color already normalizes alpha to 0-1 (150 -> ~0.588); never divide it by 255.
        public static readonly Color ReadyGlowColor = new Color32(88, 150, 255, 150);
        private static readonly Color ReadyMarkBackground = new Color32(32, 66, 140, 232);

        private BattleSimulator battle;
        private Func<BattleUnit> unitProbe;
        private Func<bool> pausedProbe;
        private Func<bool> reduceMotionProbe;
        private Image fill;
        private Image glow;
        private Text label;
        private Text readyMark;
        private GameObject readyPlate;
        private float pulseClock;

        /// <summary>Last energy drawn; -1 before the first refresh.</summary>
        public int DisplayedEnergy { get; private set; } = -1;
        public bool Ready { get; private set; }
        public Image Fill => fill;
        public Image Glow => glow;
        public Text Label => label;
        public Text ReadyMark => readyMark;

        internal void Configure(RectTransform host, BattleSimulator battle, Func<BattleUnit> unitProbe,
            Func<bool> pausedProbe, Func<bool> reduceMotionProbe, PanelKit kit)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            this.battle = battle;
            this.unitProbe = unitProbe;
            this.pausedProbe = pausedProbe;
            this.reduceMotionProbe = reduceMotionProbe;

            // Soft blue halo behind the portrait; the panel's cell background stays visible.
            glow = kit.NewImage("EnergyGlow", host, kit.RadialSprite(), new Color32(88, 150, 255, 0));
            PanelKit.PlaceTop(glow.rectTransform, 6, 0, 146, 104);
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            // Blue is deliberately deeper than the cyan HP bar and sits on its own labelled row.
            fill = kit.NewBar("EnergyTrack", host, 10, 147, PlayerWidth(host) - 20, 7,
                TrackColor, FillColor, 4);
            fill.gameObject.name = "EnergyFill";

            // The "ready" state is written out, not only coloured: the label changes copy and gets
            // a bright backing plate so colour-blind players still see the usable marker.
            GameObject mark = kit.NewPanel("EnergyReadyMark", host, ReadyMarkBackground, 9);
            PanelKit.PlaceTop(mark.GetComponent<RectTransform>(), 8, 153, PlayerWidth(host) - 16, 18);
            kit.AddOutline(mark, new Color32(140, 214, 255, 190), 1f);
            mark.SetActive(false);
            readyPlate = mark;

            label = kit.NewPlacedText(host, "能量 0/100", 12, LabelColor, 8, 154,
                PlayerWidth(host) - 16, 16, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.gameObject.name = "EnergyLabel";
            PanelKit.EnableBestFit(label, 9);
            readyMark = label;

            Refresh();
        }

        private static float PlayerWidth(RectTransform host) => host.rect.width > 1f ? host.rect.width : 158f;

        /// <summary>Re-reads the authoritative model. Called by the panel after every battle step.</summary>
        public void Refresh()
        {
            BattleUnit unit = unitProbe?.Invoke();
            int energy = battle != null && unit != null ? battle.EnergyOf(unit) : 0;
            // "Ready" means the model would accept a cast now: alive, full, off cooldown, uncontrolled.
            bool ready = battle != null && battle.IsUltimateReady(unit);
            DisplayedEnergy = energy;
            Ready = ready;
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(energy / (float)BattleSimulator.MaxUnitEnergy);
                fill.color = ready ? FillReadyColor : FillColor;
            }
            if (label != null)
            {
                label.text = ready ? "大招就绪 · 满能量" : $"能量 {energy}/{BattleSimulator.MaxUnitEnergy}";
                label.color = ready ? PanelKit.White : LabelColor;
            }
            if (readyPlate != null && readyPlate.activeSelf != ready) readyPlate.SetActive(ready);
            if (glow != null)
            {
                Color color = glow.color;
                color.a = ready ? ReadyGlowColor.a : 0f;
                glow.color = color;
            }
        }

        private void Update()
        {
            if (glow == null || !Ready) return;
            if (pausedProbe != null && pausedProbe()) return;
            bool reduced = reduceMotionProbe != null && reduceMotionProbe();
            pulseClock += Time.unscaledDeltaTime * (reduced ? 0f : 2.6f);
            float pulse = reduced ? 1f : .72f + Mathf.Sin(pulseClock) * .28f;
            Color color = glow.color;
            color.a = ReadyGlowColor.a * pulse;
            glow.color = color;
            if (readyMark != null)
            {
                float scale = reduced ? 1f : 1f + Mathf.Sin(pulseClock) * .04f;
                readyMark.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Short one-shot highlight when the bar gains energy from a real hit.</summary>
        public void FlashGain()
        {
            if (fill == null) return;
            fill.transform.localScale = new Vector3(1.08f, 1.35f, 1f);
            StartCoroutine(ResetGainFlash());
        }

        private System.Collections.IEnumerator ResetGainFlash()
        {
            yield return null;
            yield return null;
            if (fill != null) fill.transform.localScale = Vector3.one;
        }
    }
}
