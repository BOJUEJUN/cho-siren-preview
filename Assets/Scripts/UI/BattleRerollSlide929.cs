using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>Availability-driven reveal. Only artwork moves; the hit rectangle stays fixed.</summary>
    public sealed class BattleRerollSlide929 : MonoBehaviour
    {
        RectTransform visual;
        CanvasGroup group;
        Button button;
        Func<bool> paused;
        bool available, allowed;
        Vector2 origin;
        Vector3 originalScale;
        BattleAuthoredFlow1019 smoke;
        public float Reveal { get; private set; }
        public void Configure(RectTransform art, Func<bool> pause)
        {
            visual = art; origin = art.anchoredPosition; originalScale = art.localScale; paused = pause;
            group = art.GetComponent<CanvasGroup>();
            if (group == null) group = art.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            // This reveal owns the visual transform. Disable the older hover lift so two
            // animation owners cannot fight over the same authored group every frame.
            foreach (var motion in GetComponents<BattlePolish038ButtonMotion>()) motion.enabled = false;
            var cyan = art.Find("PsdReroll-reroll-smoke");
            if (cyan != null)
            {
                smoke = cyan.GetComponent<BattleAuthoredFlow1019>();
                if (smoke == null) smoke = cyan.gameObject.AddComponent<BattleAuthoredFlow1019>();
                smoke.Configure(false, pause);
            }
            button = GetComponent<Button>(); Apply();
        }
        public void SetState(bool hasCharge, bool inputAllowed)
        { available = hasCharge; allowed = inputAllowed; if (BattlePresentationOptions.ReduceMotion) Reveal = available || keepShown ? 1 : 0; Apply(); }

        /// <summary>
        /// 2026-11-01c 无限骰子: in a turn battle the bar stays on stage while its next 骰子豆 charges (the ring around the
        /// die shows the progress) — dimmed and not pressable until a pip is ready. <paramref name="shown"/> false hides it.
        /// </summary>
        public void SetEndless1101(bool shown, bool hasCharge, bool inputAllowed)
        { keepShown = shown; SetState(hasCharge, inputAllowed); }
        bool keepShown;
        void Update() => Advance(Time.unscaledDeltaTime);
        public void Advance(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || paused != null && paused()) return;
            Reveal = Mathf.MoveTowards(Reveal, available || keepShown ? 1 : 0, Mathf.Max(0, seconds) / .28f);
            Apply();
        }
        void Apply()
        {
            if (visual == null) return;
            float t = 1 - Mathf.Pow(1 - Reveal, 3);
            // No idle ghost: reveal the heart only when the model grants a usable charge.
            float travel = Mathf.Max(320f, visual.rect.width + 48f);
            visual.anchoredPosition = origin + Vector2.left * (travel * (1 - t));
            visual.localScale = Vector3.Scale(originalScale, new Vector3(.92f + .08f * t, 1f, 1f));
            group.alpha = Mathf.Clamp01(Reveal * 7f) * (available || !keepShown ? 1f : .9f);
            if (smoke != null) smoke.SetShowing(available && Reveal > .01f);
            button.interactable = available && allowed && Reveal >= .9f;
        }
    }
}
