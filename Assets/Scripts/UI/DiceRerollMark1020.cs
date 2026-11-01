using UnityEngine;
using UnityEngine.UI;

namespace ChoSiren
{
    /// <summary>
    /// A die marked for reroll lifts, glows pink underneath and sways a little (2026-10-20:
    /// selection shown by motion, not by a 待重投 label). Only the visual moves with it.
    /// </summary>
    public sealed class DiceRerollMark1020 : MonoBehaviour
    {
        private RectTransform rect;
        private Image glow;
        private Vector2 rest;
        private bool hasRest, selected;
        private float lift, clock;

        public static DiceRerollMark1020 Attach(GameObject die, Sprite glowSprite)
        {
            var mark = UnityNull.Real(die.GetComponent<DiceRerollMark1020>()) ?? die.AddComponent<DiceRerollMark1020>();
            if (mark.glow == null)
            {
                mark.rect = (RectTransform)die.transform;
                var go = new GameObject("RerollGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(die.transform, false);
                go.transform.SetAsFirstSibling();
                mark.glow = go.GetComponent<Image>();
                mark.glow.sprite = glowSprite; mark.glow.raycastTarget = false;
                var r = mark.glow.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(-40, -46); r.offsetMax = new Vector2(40, 22);
                mark.glow.color = new Color(1f, .2f, .65f, 0f);
            }
            return mark;
        }

        public void SetSelected(bool value) => selected = value;

        private void LateUpdate()
        {
            if (rect == null) return;
            if (!hasRest || (lift <= .001f && rect.anchoredPosition != rest)) { rest = rect.anchoredPosition; hasRest = true; }
            bool still = BattlePresentationOptions.ReduceMotion;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            lift = Mathf.MoveTowards(lift, selected ? 1f : 0f, dt * 7f);
            clock += dt;
            float bob = selected && !still ? Mathf.Sin(clock * 5f) * 2.5f : 0f;
            rect.anchoredPosition = rest + Vector2.up * (lift * 26f + bob * lift);
            // Selected dice grow a little and pulse, so the choice reads at a glance.
            float pop = still ? 1f : 1f + .03f * Mathf.Sin(clock * 6f);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f * pop, lift);
            rect.localEulerAngles = new Vector3(0, 0, selected && !still ? Mathf.Sin(clock * 3.2f) * 3f * lift : 0f);
            float pulse = .65f + .35f * Mathf.Sin(clock * 6f);
            glow.color = new Color(1f, .25f, .7f, lift * (still ? 1f : .75f + .25f * pulse));
        }
    }
}
